using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    /// <summary>A dashboard, question or folder, added at the bottom of the top of the menu.</summary>
    public async Task<BasicReadResponse<Guid?>> Create(CreateMetabaseEmbedRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        string? label  = request.Label?.Trim();
        bool    folder = request.Type == MetabaseEmbedType.Folder;

        if (string.IsNullOrWhiteSpace(label))
            return BasicReadResponse<Guid?>.WithError(MetabaseEmbedLabelNull);

        if (!folder && request.MetabaseId <= 0)
            return BasicReadResponse<Guid?>.WithError(MetabaseEmbedIdInvalid);

        if (await db.MetabaseEmbeds.AnyAsync(x => x.Label == label, token))
            return BasicReadResponse<Guid?>.WithError(MetabaseEmbedLabelTaken);

        int last = await db.MetabaseEmbeds
            .Where(x => x.ParentId == null)
            .MaxAsync(x => (int?)x.SortOrder, token) ?? -1;

        DbMetabaseEmbed embed = new()
        {
            Id = Guid.Empty,
            Label = label,
            MetabaseId = folder ? null : request.MetabaseId,
            Type = request.Type,
            SortOrder = last + 1
        };

        await db.MetabaseEmbeds.AddAsync(embed, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = embed.Id,
            Success = true
        };
    }
}
