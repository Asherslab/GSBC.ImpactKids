using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Reads both sides and everything the decision needs, or explains why the run must stop.
    ///
    /// The two abort conditions are load bearing and are checked here rather than by the caller:
    /// absence from the fetched roll is read as deletion, and six of the seven tables referencing a
    /// person cascade. A single dropped page once archived 726 children.
    /// </summary>
    private async Task<(SyncWorkingSet? Set, string? Refusal)> LoadWorkingSetAsync(
        Guid              operationId,
        CancellationToken token)
    {
        List<ElvantoPerson> elvantoPeople = await FetchElvantoAsync(token);
        logger.LogInformation(
            "Sync {OperationId}: fetched {Count} people from Elvanto",
            operationId, elvantoPeople.Count);

        // Both floors below used to be qualified by "if this is a full-scope run". Every run is now
        // the whole roll, so they apply unconditionally - which is what they were always for.
        if (elvantoPeople.Count == 0)
            return (null, "Elvanto returned 0 people — aborting to prevent mass archive");

        List<DbPerson> appPeople = await LoadAppPeopleAsync(token);
        logger.LogInformation("Sync {OperationId}: loaded {Count} app people", operationId, appPeople.Count);

        // Second line of defence behind the fetch itself. A roll that comes back short - for any
        // reason, including one nobody has thought of yet - must stop the run rather than delete
        // people.
        int linkedCount = appPeople.Count(p => p.ElvantoId is not null && p.DeletedAtUtc is null);
        if (linkedCount > 0)
        {
            double coverage = (double)elvantoPeople.Count / linkedCount;
            if (coverage < MinimumElvantoCoverage)
                return (null,
                    $"Elvanto returned {elvantoPeople.Count} people but {linkedCount} app people are linked "
                    + $"({coverage:P0} coverage, minimum {MinimumElvantoCoverage:P0}). Aborting before archive "
                    + "— a short roll would archive everyone missing from it.");
        }

        // The medical/allergy descriptor turns Elvanto's free text back into rows, which needs the
        // allergen and medical-type tables. It cannot reach the database itself, so it is primed
        // here, once, rather than per person.
        await PrimeMedicalAllergyLookupsAsync(token);

        Dictionary<string, DbPerson> appByElvantoId = appPeople
            .Where(p => p.ElvantoId is not null)
            .ToDictionary(p => p.ElvantoId!);

        // The persisted pairing, seeded on first sight from the linked people in this roll and read
        // from the table on every run after. Family stops being re-derived per run - see
        // SyncFamilyLinks for why the derivation was itself the bug.
        SyncFamilyLinks families = await LoadFamilyLinksAsync(elvantoPeople, appByElvantoId, token);

        return (new SyncWorkingSet
        {
            ElvantoPeople = elvantoPeople,
            AppPeople     = appPeople,
            SchoolGrades  = await db.SchoolGrades.ToListAsync(token),

            Bases = await db.ElvantoFieldSnapshots
                .Where(x => x.EntityType == "Person")
                .ToDictionaryAsync(x => (x.EntityId, x.FieldName), token),

            // A high-water mark per field, used only to break a genuine two-sided conflict. It is no
            // longer an admission gate: a missing row means "app timestamp unknown", not "the app
            // did not change".
            LastAppChange = await db.FieldChangeLogs
                .Where(x => x.EntityType == "Person" && x.Source == SyncSource.App)
                .GroupBy(x => new { x.EntityId, x.FieldName })
                .Select(g => new { g.Key.EntityId, g.Key.FieldName, LastAt = g.Max(x => x.ChangedAt) })
                .ToDictionaryAsync(x => (x.EntityId, x.FieldName), x => x.LastAt, token),

            PendingReviews = await db.PendingReviews.ToDictionaryAsync(x => (x.PersonId, x.ElvantoId), token),

            AppByElvantoId = appByElvantoId,
            UnlinkedApp    = appPeople.Where(p => p.ElvantoId is null).ToList(),

            Families = families
        }, null);
    }
}
