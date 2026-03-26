using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Scripture.Memorisation.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async Task<BasicReadResponse<Guid?>> Create(CreateMemoryVerseListRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicReadResponse<Guid?>.WithError(MemoryVerseListNameNull);

        if (request.SchoolTermId != null)
        {
            DbSchoolTerm? term = await db.Terms
                .FirstOrDefaultAsync(x => x.Id == request.SchoolTermId, token);
            if (term == null)
                return BasicReadResponse<Guid?>.WithError(SchoolTermNotFound);
        }

        DbMemoryVerseList list = new()
        {
            Id = Guid.Empty,
            Name = request.Name,
            SchoolTermId = request.SchoolTermId
        };

        await db.MemoryVerseLists.AddAsync(list, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(token);

        return new BasicReadResponse<Guid?>
        {
            Entity = list.Id,
            Success = true
        };
    }
}