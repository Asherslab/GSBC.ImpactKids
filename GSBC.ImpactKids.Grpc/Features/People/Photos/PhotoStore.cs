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
            ContentType = contentType
            // No DisablePayloadSigning. The SDK refuses it over plain HTTP - "When
            // DisablePayloadSigning is true, the request must be sent over HTTPS" - and the store is
            // reached over HTTP inside the cluster, so it turned every upload into a 500. SeaweedFS
            // accepts an ordinary signed payload.
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

            return new PhotoBytes(buffer.ToArray(), response.Headers.ContentType ?? "image/jpeg");
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogWarning("Photo {Version} is named by a person row but not in the store", version);
            return null;
        }
    }

    public record PhotoBytes(byte[] Bytes, string ContentType);
}
