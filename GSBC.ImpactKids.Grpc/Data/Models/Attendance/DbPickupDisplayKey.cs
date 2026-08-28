using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.Attendance;

/// <summary>
/// The one credential that lets a screen on a wall ask the pickup display service. A
/// single row: there is one key at a time, and rotating it replaces the row rather than
/// adding to it.
/// <para>
/// <b>Only the hash is stored.</b> The key itself exists twice - once in the response to
/// the rotation that minted it, and once in the bookmark on the TV. It is never written
/// down here, never logged, and cannot be read back.
/// </para>
/// <para>
/// <see cref="Id"/> doubles as the key's <i>generation</i>: it is new on every rotation, it
/// rides in the enrolment cookie, and a cookie carrying any other generation is stale. That
/// is what makes rotation total rather than merely forward-looking - see
/// <c>docs/modules/auth/sign-in.md</c>.
/// </para>
/// </summary>
public class DbPickupDisplayKey
{
    /// <summary>New on every rotation. See the class remarks - this is the generation marker.</summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Base64 SHA-256 of the key. A plain digest rather than a password hash on purpose:
    /// the key is 32 bytes of <see cref="System.Security.Cryptography.RandomNumberGenerator"/>
    /// output, so there is no dictionary to slow down and nothing for a work factor to buy.
    /// </summary>
    public required string KeyHash { get; set; }

    /// <summary>
    /// Signs the tokens the wall displays present to the gRPC service. Minted fresh on every
    /// rotation alongside <see cref="KeyHash"/>, which is what makes a rotation reach the
    /// tokens as well as the cookies: every token issued under the previous key stops
    /// verifying, with no revocation list and no expiry to wait out.
    /// <para>
    /// Unlike <see cref="KeyHash"/> this is the secret itself, not a digest - the service
    /// both signs and verifies with it, so it has to be recoverable. It never leaves the
    /// cluster: the proxy is handed finished tokens and never sees this value.
    /// </para>
    /// </summary>
    public required string TokenSigningKey { get; set; }

    public required DateTimeOffset RotatedAt { get; set; }

    /// <summary>Who pressed the button, so the admin page can say. Null for a key minted before there were users.</summary>
    public Guid? RotatedByUserId { get; set; }

    [MapperIgnore]
    public DbUser? RotatedByUser { get; set; }
}
