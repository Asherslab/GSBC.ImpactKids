using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;

namespace GSBC.ImpactKids.Grpc.Features.People.Photos;

/// <summary>
/// The object store, as the two operations the app actually performs: put a photo, get it back.
///
/// <b>Objects are keyed by the hash of their own bytes</b>, so nothing is ever overwritten and a
/// re-shoot writes a new object rather than replacing one. That is what lets the serve endpoint mark
/// a response <c>immutable</c> for a year: the version is in the URL, so a new photo is a new URL
/// and busts its own cache with no revalidation and no stale face.
///
/// It also makes the offsite backup trivial — <c>rclone copy --size-only --immutable</c> is correct
/// precisely because a name's content never changes.
/// </summary>
public class PhotoStore(
    IAmazonS3        s3,
    PhotoStoreConfig config,
    ILogger<PhotoStore> logger
)
{
    /// <summary>
    /// A short content hash, used both as the object key and as the <c>PhotoVersion</c> the browser
    /// puts in the URL. Twelve hex characters of SHA-256: 48 bits, which for a few thousand photos
    /// is far past the point where a collision is a realistic concern, and short enough to sit in a
    /// query string without being noise.
    /// </summary>
    public static string VersionOf(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes))[..12];

    private string KeyFor(string version) => $"people/{version}.jpg";

    /// <summary>
    /// Creates the bucket if it is not there. Called once at startup rather than per write — a
    /// bucket that does not exist turns every PUT into a 404 whose message says nothing useful, and
    /// nothing else in the deployment creates it.
    /// </summary>
    public async Task EnsureBucketAsync(CancellationToken token = default)
    {
        ListBucketsResponse buckets = await s3.ListBucketsAsync(token);
        if (buckets.Buckets?.Any(b => b.BucketName == config.BucketName) == true)
            return;

        logger.LogInformation("Creating photo bucket {Bucket}", config.BucketName);
        await s3.PutBucketAsync(new PutBucketRequest { BucketName = config.BucketName }, token);
    }

    /// <summary>
    /// Stores the bytes and answers with their version. Writing the same photo twice is a no-op that
    /// returns the same version, because the key is the content.
    /// </summary>
    public async Task<string> PutAsync(byte[] bytes, string contentType, CancellationToken token = default)
    {
        string version = VersionOf(bytes);

        using MemoryStream stream = new(bytes);
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName  = config.BucketName,
            Key         = KeyFor(version),
            InputStream = stream,
            ContentType = contentType,

            // Both of these are load-bearing, and the second one silently corrupted every photo
            // before it was set.
            //
            // DisablePayloadSigning cannot be used here: the SDK refuses it over plain HTTP - "When
            // DisablePayloadSigning is true, the request must be sent over HTTPS" - and the store is
            // reached over HTTP inside the cluster, so it turned every upload into a 500.
            //
            // With signing on, the SDK defaults to aws-chunked streaming
            // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD), and SeaweedFS 3.98 stores that framing verbatim
            // instead of decoding it. The object is then the right size and the right content type
            // and is NOT a JPEG - it begins "<hex length>;chunk-signature=..." - so nothing fails,
            // the row is written, and the face simply never renders. Verified by reading an object
            // back with xxd. UseChunkEncoding = false sends the body plainly, still signed.
            UseChunkEncoding = false
        }, token);

        return version;
    }

    /// <summary>
    /// The bytes for a version, or null when the store does not have them.
    ///
    /// A missing object is a normal answer, not an exception: the row can name a version the store
    /// has lost, and the caller's job is to 404 so the avatar falls back to the initial. Read whole
    /// rather than streamed — these are 20–50 KB.
    /// </summary>
    public async Task<PhotoBytes?> GetAsync(string version, CancellationToken token = default)
    {
        try
        {
            using GetObjectResponse response = await s3.GetObjectAsync(
                config.BucketName, KeyFor(version), token);

            using MemoryStream buffer = new();
            await response.ResponseStream.CopyToAsync(buffer, token);

            byte[] bytes = buffer.ToArray();

            // Cheap, and it exists because the alternative already happened: an upload wrote
            // aws-chunked framing instead of the image, and every downstream signal - status, size,
            // content type, the database row - said the photo was fine while the bytes were not.
            // Refusing here turns that into a 404, which the avatar already handles by showing the
            // initial, plus one loud log line naming the object.
            if (!LooksLikeAnImage(bytes))
            {
                logger.LogError(
                    "Photo {Version} is stored but is not image data ({Bytes} bytes, starts {Head}). "
                    + "Refusing to serve it. This is a storage bug, not a missing photo.",
                    version, bytes.Length,
                    Convert.ToHexString(bytes.AsSpan(0, Math.Min(8, bytes.Length))));
                return null;
            }

            return new PhotoBytes(bytes, response.Headers.ContentType ?? "image/jpeg");
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("Photo {Version} is named by a person row but not in the store", version);
            return null;
        }
    }

    /// <summary>
    /// Magic bytes for the formats the app can produce or ingest: JPEG, PNG, WebP. Only ever asked
    /// "is this plausibly an image", never "is this valid" - the point is to catch bytes that are
    /// obviously not one, cheaply, on every read.
    /// </summary>
    private static bool LooksLikeAnImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12) return false;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;

        // PNG: 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;

        // WebP: "RIFF" .... "WEBP"
        if (bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8)) return true;

        return false;
    }

    public record PhotoBytes(byte[] Bytes, string ContentType);
}
