using GSBC.ImpactKids.Grpc.Data.Models;
using GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Services.MemoryVerseListsServices;

public partial class MemoryVerseListsService
{
    public async Task<BasicResponse?> Update(UpdateMemoryVerseListRequest request, CallContext context = default)
    {
        CancellationToken token = context.CancellationToken;

        DbMemoryVerseList? list = await db.MemoryVerseLists
            .FirstOrDefaultAsync(x => x.Id == request.Guid, token);

        if (list == null)
            return BasicResponse.WithError(MemoryVerseListNotFound);
        
        if (request.Name.IsUpdated)
        {
            if (string.IsNullOrWhiteSpace(request.Name.Value))
                return BasicResponse.WithError(MemoryVerseListNotFound);
            list.Name = request.Name.Value;
        }

        if (request.SchoolTermId.IsUpdated)
        {
            DbSchoolTerm? schoolTerm = await db.Terms
                .FirstOrDefaultAsync(x => x.Id == request.SchoolTermId.Value, token);
            
            if (schoolTerm == null)
                return BasicResponse.WithError(SchoolTermNotFound);
            
            list.SchoolTermId = request.SchoolTermId.Value;
        }

        db.MemoryVerseLists.Update(list);
        await db.SaveChangesAsync(token);
        await eventService.SendUpdatedEvent(list.Id, token: token, list.SchoolTermId ?? Guid.Empty);

        return new BasicResponse
        {
            Success = true
        };
    }
}