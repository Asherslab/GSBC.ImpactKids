using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    private async Task<List<ElvantoPerson>> FetchElvantoAsync(SyncWithElvantoRequest request, CancellationToken ct)
        => request.Scope switch
        {
            ElvantoSyncScope.Person => await elvantoService.GetPersonByIdOrSearchAsync(request.PersonId!.Value, ct),
            ElvantoSyncScope.Family => await elvantoService.GetPeopleForFamilyAsync(request.FamilyId!.Value, ct),
            _                       => await elvantoService.GetAllPeopleAsync(ct)
        };

    private async Task<List<DbPerson>> LoadAppPeopleAsync(SyncWithElvantoRequest request, CancellationToken ct)
    {
        IQueryable<DbPerson> query = db.People
            .IgnoreQueryFilters()
            .Include(x => x.Allergies)
            .Include(x => x.MedicalNotes);

        return request.Scope switch
        {
            ElvantoSyncScope.Person => await query.Where(x => x.Id == request.PersonId!.Value).ToListAsync(ct),
            ElvantoSyncScope.Family => await query.Where(x => x.FamilyId == request.FamilyId!.Value).ToListAsync(ct),
            _                       => await query.ToListAsync(ct)
        };
    }
}
