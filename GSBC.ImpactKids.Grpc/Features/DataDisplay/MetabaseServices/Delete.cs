using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    /// <summary>
    /// Deleting a folder keeps what was in it: its entries move up to the top of the menu, in the
    /// folder's place and in their order.
    /// </summary>
    public async Task<BasicResponse> BasicDelete(BasicReadRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMetabaseEmbed? embed = await db.MetabaseEmbeds
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (embed == null)
            return BasicResponse.WithError(MetabaseEmbedNotFound);

        List<DbMetabaseEmbed> siblings = await Siblings(embed.ParentId, token);
        int                   at       = siblings.IndexOf(embed);
        siblings.Remove(embed);

        if (embed.IsFolder)
        {
            List<DbMetabaseEmbed> children = await Siblings(embed.Id, token);

            foreach (DbMetabaseEmbed child in children)
                child.ParentId = null;

            siblings.InsertRange(Math.Max(at, 0), children);
        }

        Renumber(siblings);

        // One save: EF writes the children out of the folder before it deletes the folder row, which
        // the restricting foreign key needs.
        db.MetabaseEmbeds.Remove(embed);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}
