using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMetabaseEmbed? embed = await db.MetabaseEmbeds
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (embed == null)
            return BasicResponse.WithError(MetabaseEmbedNotFound);

        db.MetabaseEmbeds.Remove(embed);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
