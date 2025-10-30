using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;
using GSBC.ImpactKids.Shared.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using CallContext = ProtoBuf.Grpc.CallContext;

namespace GSBC.ImpactKids.Grpc.Services;

[Authorize(Policy = Policies.EnabledOnly)]
public class MetabaseService(
    IConfiguration configuration
) : IMetabaseService
{
    public Task<MetabaseJwtResponse?> GetMetabaseJwt(MetabaseJwtRequest request, CallContext context = default)
    {
        string? metabaseSecret = configuration["Metabase:Secret"];

        if (metabaseSecret == null)
            return Task.FromResult<MetabaseJwtResponse?>(new MetabaseJwtResponse { Jwt = null });

        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(metabaseSecret));
        SigningCredentials   credentials = new(securityKey, SecurityAlgorithms.HmacSha256Signature);
        JwtHeader            header      = new(credentials);
        Dictionary<string, int> dash = new()
        {
            { "dashboard", request.DashboardId }
        };

        Dictionary<string, string> pars = new();
        JwtPayload payload = new()
        {
            { "resource", dash },
            { "params", pars }
        };

        var     secToken    = new JwtSecurityToken(header, payload);
        var     handler     = new JwtSecurityTokenHandler();
        string? tokenString = handler.WriteToken(secToken);
        return Task.FromResult<MetabaseJwtResponse?>(new MetabaseJwtResponse { Jwt = tokenString });
    }
}