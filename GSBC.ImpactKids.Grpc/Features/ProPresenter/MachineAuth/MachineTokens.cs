using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GSBC.ImpactKids.Grpc.Features.ProPresenter.MachineAuth;

/// <summary>
/// Mints the access token a ProPresenter machine presents to this service. This service is both
/// issuer and validator, with the key on the machine's own row, so there is no secret to distribute.
/// </summary>
public static class MachineTokens
{
    /// <summary>
    /// <b>No subject claim</b>, for the reason <c>DisplayTokens.Mint</c> gives: an inbound <c>sub</c>
    /// becomes a nameidentifier, and <c>CustomClaimsTransformation</c> creates a user row for any
    /// nameidentifier it does not know. The scheme also turns the inbound mapping off.
    /// <para>
    /// The key id is the machine id, which is how the validator picks this machine's key out of all
    /// of them - see <see cref="MachineSigningKeyProvider.Resolve"/>.
    /// </para>
    /// </summary>
    public static string Mint(Guid machineId, string signingKey)
    {
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)) { KeyId = machineId.ToString() },
            SecurityAlgorithms.HmacSha256
        );

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = MachineAuthDefaults.Issuer,
            Audience = MachineAuthDefaults.Audience,
            Expires = DateTime.UtcNow.Add(MachineAuthDefaults.TokenLifetime),
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity([
                new Claim(MachineAuthDefaults.MachineIdClaimType, machineId.ToString())
            ])
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>The calling machine's id, from its token. Null for any other kind of caller.</summary>
    public static Guid? MachineId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(MachineAuthDefaults.MachineIdClaimType), out Guid id) ? id : null;
}
