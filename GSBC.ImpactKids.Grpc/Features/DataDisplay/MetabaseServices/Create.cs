using GSBC.ImpactKids.Grpc.Data.Models.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DataDisplay.MetabaseEmbeds;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.DataDisplay.MetabaseServices;

public partial class MetabaseService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateMetabaseEmbedRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        string? label = request.Label?.Trim();

        if (string.IsNullOrWhiteSpace(label))
            return BasicReadResponse<Guid?>.WithError(MetabaseEmbedLabelNull);

        if (request.MetabaseId <= 0)
            return BasicReadResponse<Guid?>.WithError(MetabaseEmbedIdInvalid);

        if (await db.MetabaseEmbeds.AnyAsync(x => x.Label == label, token))
            return BasicReadResponse<Guid?>.WithError(MetabaseEmbedLabelTaken);

        DbMetabaseEmbed embed = new()
        {
            Id = Guid.Empty,
            Label = label,
            MetabaseId = request.MetabaseId,
            Type = request.Type
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
