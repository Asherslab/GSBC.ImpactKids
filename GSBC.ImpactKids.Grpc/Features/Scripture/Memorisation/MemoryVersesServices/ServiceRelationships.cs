using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVersesServices;

public partial class MemoryVersesService
{
    public async Task<BasicResponse> CreateRelationship(
        BasicMultipleRelationshipRequest<MemoryVerse, Service> request,
        CallContext                                            context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseServiceRelationship? rel = await db.Set<DbMemoryVerseServiceRelationship>().FirstOrDefaultAsync(
            x =>
                x.MemoryVersesId == request.FirstId &&
                x.ServicesId == request.SecondId,
            cancellationToken: token
        );

        if (rel != null)
            return BasicResponse.WithError(MemoryVerseServiceExists);

        rel = new DbMemoryVerseServiceRelationship
        {
            MemoryVersesId = request.FirstId,
            ServicesId = request.SecondId
        };

        await db.AddAsync(rel, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);
        await eventService.SendUpdatedEvent<Service>(token);

        return new BasicResponse
        {
            Success = true
        };
    }

    public async Task<BasicResponse> DeleteRelationship(
        BasicMultipleRelationshipRequest<MemoryVerse, Service> request,
        CallContext                                            context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseServiceRelationship? rel = await db.Set<DbMemoryVerseServiceRelationship>().FirstOrDefaultAsync(
            x =>
                x.MemoryVersesId == request.FirstId &&
                x.ServicesId == request.SecondId,
            cancellationToken: token
        );

        if (rel == null)
            return BasicResponse.WithError(MemoryVerseServiceNotFound);

        db.Remove(rel);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);
        await eventService.SendUpdatedEvent<Service>(token);

        return new BasicResponse
        {
            Success = true
        };
    }
}