using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

/// <summary>
/// Minting and checking the pickup display key. Shared by the operation that rotates it and
/// the internal endpoint the proxy enrols against, so there is exactly one definition of
/// what a key is and one comparison to get wrong.
/// </summary>
internal static class PickupDisplayKeys
{
    /// <summary>
    /// 32 bytes of CSPRNG output. Not tunable: shorter is not worth guessing at, and longer
    /// only makes the bookmark uglier.
    /// </summary>
    private const int KeyBytes = 32;

    /// <summary>
    /// A key that survives being pasted into a query string, a bookmark and a TV's on
    /// screen keyboard - so URL-safe base64, and no padding to be helpfully stripped.
    /// </summary>
    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[KeyBytes];

        RandomNumberGenerator.Fill(bytes);

        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>
    /// Base64 SHA-256. A plain digest rather than a password hash is the right call here and
    /// only here: the input is 32 random bytes, so there is no guessable space for a work
    /// factor to defend.
    /// </summary>
    public static string Hash(string key) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    /// <summary>
    /// Constant time. A comparison that returns early leaks how much of the stored hash a
    /// guess matched, one request at a time.
    /// </summary>
    public static bool Matches(string presentedKey, string storedHash)
    {
        byte[] presented;
        byte[] stored;

        try
        {
            presented = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
            stored = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            // A stored hash that is not base64 is corrupt, not a match.
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(presented, stored);
    }
}
