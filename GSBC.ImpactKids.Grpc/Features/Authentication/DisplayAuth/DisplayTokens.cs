using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;

/// <summary>
/// Mints the token a wall display presents to this service.
/// <para>
/// This service is <b>both issuer and validator</b>, which is the whole reason there is no
/// new secret to distribute: the signing key lives on the enrolment key row in this
/// service's own database and never leaves the cluster. The proxy receives a finished token
/// at enrolment and does nothing but carry it.
/// </para>
/// </summary>
public static class DisplayTokens
{
    /// <summary>
    /// A token for a screen that has just proved it holds the current enrolment key.
    /// <para>
    /// Deliberately carries <b>no subject claim of any kind</b>. JwtBearer maps an inbound
    /// <c>sub</c> onto the nameidentifier claim by default, and
    /// <see cref="Services.CustomClaimsTransformation"/> creates a <c>DbUser</c> row for any
    /// nameidentifier it does not recognise - so a token with a subject would silently
    /// manufacture a user for every wall in the building. The scheme also turns that mapping
    /// off; both halves are belt and braces for the same mistake.
    /// </para>
    /// </summary>
    public static string Mint(Guid generation, string signingKey)
    {
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256
        );

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = DisplayAuthDefaults.Issuer,
            Audience = DisplayAuthDefaults.Audience,
            Expires = DateTime.UtcNow.Add(DisplayAuthDefaults.TokenLifetime),
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity([
                new Claim(DisplayAuthDefaults.GenerationClaimType, generation.ToString())
            ])
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
