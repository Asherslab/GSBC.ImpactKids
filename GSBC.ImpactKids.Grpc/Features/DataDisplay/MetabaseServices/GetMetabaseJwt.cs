using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CallContext = ProtoBuf.Grpc.CallContext;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    public async Task<BasicReadResponse<string?>> GetMetabaseJwt(
        BasicReadRequest request,
        CallContext      context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        string? metabaseSecret = configuration["Metabase:Secret"];

        if (metabaseSecret == null)
            return BasicReadResponse<string?>.WithError(MetabaseSecretNotSet);

        // Signed only for a stored embed: the token opens whatever resource it names, so the
        // client never gets to pick the Metabase id.
        DbMetabaseEmbed? embed = await db.MetabaseEmbeds
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (embed == null)
            return BasicReadResponse<string?>.WithError(MetabaseEmbedNotFound);

        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(metabaseSecret));
        SigningCredentials   credentials = new(securityKey, SecurityAlgorithms.HmacSha256Signature);
        JwtHeader            header      = new(credentials);
        Dictionary<string, int> resource = new()
        {
            { embed.Type.ResourceType(), embed.MetabaseId }
        };

        Dictionary<string, string> pars = new();
        JwtPayload payload = new()
        {
            { "resource", resource },
            { "params", pars }
        };

        var     secToken    = new JwtSecurityToken(header, payload);
        var     handler     = new JwtSecurityTokenHandler();
        string? tokenString = handler.WriteToken(secToken);

        return new BasicReadResponse<string?>
        {
            Success = true,
            Entity = tokenString
        };
    }
}
