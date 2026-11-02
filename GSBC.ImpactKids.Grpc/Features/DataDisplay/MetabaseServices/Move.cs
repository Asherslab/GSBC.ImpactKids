using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    /// <summary>
    /// A drag: into a folder, or to the top of the menu, at a position among the entries already there.
    /// A folder only ever moves within the top of the menu.
    /// </summary>
    public async Task<BasicResponse> Move(MoveMetabaseEmbedRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMetabaseEmbed? embed = await db.MetabaseEmbeds
            .FirstOrDefaultAsync(x => x.Id == request.Id, token);

        if (embed == null)
            return BasicResponse.WithError(MetabaseEmbedNotFound);

        if (request.ParentId != null)
        {
            if (embed.IsFolder)
                return BasicResponse.WithError(MetabaseFolderInFolder);

            bool isFolder = await db.MetabaseEmbeds
                .AnyAsync(x => x.Id == request.ParentId && x.Type == MetabaseEmbedType.Folder, token);

            if (!isFolder)
                return BasicResponse.WithError(MetabaseEmbedNotFound);
        }

        List<DbMetabaseEmbed> from = await Siblings(embed.ParentId, token);
        from.Remove(embed);
        Renumber(from);

        List<DbMetabaseEmbed> to = request.ParentId == embed.ParentId
            ? from
            : await Siblings(request.ParentId, token);

        embed.ParentId = request.ParentId;
        to.Insert(Math.Clamp(request.Position, 0, to.Count), embed);
        Renumber(to);

        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
