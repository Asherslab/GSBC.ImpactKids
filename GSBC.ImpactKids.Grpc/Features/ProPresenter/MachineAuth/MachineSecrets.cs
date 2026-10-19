using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;

/// <summary>
/// Minting and checking a machine's secret, its enrolment code and its signing key - one definition
/// of each, shared by the approval and the token endpoint, so there is one comparison to get wrong.
/// The same shapes as <c>PickupDisplayKeys</c>, kept separate because the two credentials must be
/// free to diverge.
/// </summary>
internal static class MachineSecrets
{
    /// <summary>32 bytes of CSPRNG output, URL-safe, for both the secret and the enrolment code.</summary>
    public static string Generate()
    {
        Span<byte> bytes = stackalloc byte[32];

        RandomNumberGenerator.Fill(bytes);

        return Base64Url.EncodeToString(bytes);
    }

    /// <summary>48 bytes - HMAC-SHA256 gains nothing past its block size, and nobody types this.</summary>
    public static string GenerateSigningKey()
    {
        Span<byte> bytes = stackalloc byte[48];

        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes);
    }

    /// <summary>Base64 SHA-256. No work factor: the input is 32 random bytes, so there is nothing to guess.</summary>
    public static string Hash(string value) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Constant time - an early return leaks how much of the stored hash a guess matched.</summary>
    public static bool Matches(string presented, string storedHash)
    {
        byte[] stored;

        try
        {
            stored = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)),
            stored
        );
    }
}
