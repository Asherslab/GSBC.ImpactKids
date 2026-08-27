using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    // Both sides, whole. Scope used to narrow these to one person or one family, which meant every
    // run had to be asked "is this the whole roll?" before it could reason about anyone missing from
    // Elvanto - and a subset read as a roll was how a run once archived 726 children. A run is now
    // always everyone, so that question has one answer and nothing has to remember to ask it.
    private Task<List<ElvantoPerson>> FetchElvantoAsync(CancellationToken ct) =>
        elvantoService.GetAllPeopleAsync(ct);

    private Task<List<DbPerson>> LoadAppPeopleAsync(CancellationToken ct) =>
        db.People
            .IgnoreQueryFilters()
            .Include(x => x.Allergies)
            .Include(x => x.MedicalNotes)
            .ToListAsync(ct);
}
