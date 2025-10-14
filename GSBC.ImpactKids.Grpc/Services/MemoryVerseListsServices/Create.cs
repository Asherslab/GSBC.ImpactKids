using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async Task<BasicResponse?> Create(CreateMemoryVerseListRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BasicResponse.WithError(MemoryVerseListNotFound);

        DbSchoolTerm? term = await db.Terms
            .FirstOrDefaultAsync(x => x.Id == request.SchoolTermId, token);
        if (term == null)
            return BasicResponse.WithError(SchoolTermNotFound);

        DbMemoryVerseList list = new()
        {
            Id = Guid.Empty,
            Name = request.Name,
            SchoolTermId = request.SchoolTermId
        };

        await db.MemoryVerseLists.AddAsync(list, token);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(list.Id, token: token, list.SchoolTermId ?? Guid.Empty);

        return new BasicResponse
        {
            Success = true
        };
    }
}