using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    public async Task<BasicResponse> Update(UpdateMetabaseEmbedRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMetabaseEmbed? embed = await db.MetabaseEmbeds
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (embed == null)
            return BasicResponse.WithError(MetabaseEmbedNotFound);

        if (request.Label.IsUpdated)
        {
            string? label = request.Label.Value?.Trim();

            if (string.IsNullOrWhiteSpace(label))
                return BasicResponse.WithError(MetabaseEmbedLabelNull);

            if (await db.MetabaseEmbeds.AnyAsync(x => x.Label == label && x.Id != embed.Id, token))
                return BasicResponse.WithError(MetabaseEmbedLabelTaken);

            embed.Label = label;
        }

        if (request.Type.IsUpdated && request.Type.Value != embed.Type)
        {
            if (embed.IsFolder || request.Type.Value == MetabaseEmbedType.Folder)
                return BasicResponse.WithError(MetabaseFolderTypeFixed);

            embed.Type = request.Type.Value;
        }

        // A folder has no Metabase id; it is only ever renamed.
        if (request.MetabaseId.IsUpdated && !embed.IsFolder)
        {
            if (request.MetabaseId.Value <= 0)
                return BasicResponse.WithError(MetabaseEmbedIdInvalid);

            embed.MetabaseId = request.MetabaseId.Value;
        }

        // Tracked, so only the columns assigned above are written.
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
