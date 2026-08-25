using System.Security.Cryptography;
using System.Text;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync;

/// <summary>
/// The one hash the sync compares by. Trimmed and lower-cased first, so "  Yes " and "yes" are the
/// same answer and a difference in spacing never reads as a change worth pushing.
/// </summary>
public static class SyncHash
{
    public static string Of(string? value)
    {
        if (value is null) return "null";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..16];
    }
}
