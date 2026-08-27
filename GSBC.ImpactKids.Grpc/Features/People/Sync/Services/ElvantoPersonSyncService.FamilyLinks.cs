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
    /// The persisted local-family ⟷ Elvanto-household pairing, seeded on first sight from the
    /// linked people in the roll this run just fetched.
    ///
    /// <b>Not from the field snapshots</b>, which is the obvious place to look and is wrong: family
    /// is compared in the app's terms, so a <c>FamilyId</c> snapshot's <c>LastSeenValue</c> holds
    /// the local Guid, not Elvanto's household number. Seeding from it produces rows whose two
    /// columns are the same Guid, every real household then looks unknown, and the run plans to
    /// regroup the entire church — 1213 inbound family moves and 411 freshly minted households on a
    /// real dry run. The roll is the only place the Elvanto side of the pair actually is.
    ///
    /// This is the same evidence the old per-run map read, used once instead of forever. That is
    /// what makes it safe: a pairing derived every run moves when the roll does, so a person alone
    /// in their household confirmed whatever family they were already in and a local move could
    /// never be seen. Written down once, the row stops answering to the roll — and because the
    /// Elvanto side is unique, a later local move cannot rewrite it either.
    ///
    /// Only one kind of ambiguity is refused. Two local families claiming one household is a
    /// <b>merge</b> — it declares people related who are not currently recorded as such — so it
    /// stays a finding. One local family spread across several households is a <b>split</b>, and
    /// Elvanto owns household structure, so the split is simply followed: the household with the
    /// most members keeps the existing local family and the rest get one each, ranked so the answer
    /// does not change between runs.
    ///
    /// Saved immediately rather than with the run's other writes. A seeded pairing is a fact about
    /// the two systems, not a decision this run made, and <c>FailAsync</c> clears the change tracker
    /// — so leaving it pending would throw the bootstrap away every time a run aborted for an
    /// unrelated reason.
    /// </summary>
    private async Task<SyncFamilyLinks> LoadFamilyLinksAsync(
        List<ElvantoPerson>          elvantoPeople,
        Dictionary<string, DbPerson> appByElvantoId,
        CancellationToken            token)
    {
        SyncFamilyLinks links = new(await db.ElvantoFamilyLinks.ToListAsync(token));

        // Blank is not a household - Elvanto returns one for the 397 people it has no household
        // for - and an unlinked Elvanto person has no local family to pair with anything. The
        // bucket is excluded here rather than paired: its 412 people are not a household, so it
        // is never evidence for one. Its members are still placed, one household at a time, by
        // TranslateFamily.
        List<(Guid Local, string Elvanto)> members = elvantoPeople
            .Where(e => e.Id is not null && !string.IsNullOrWhiteSpace(e.FamilyId))
            .Select(e => (App: appByElvantoId.GetValueOrDefault(e.Id!), Elvanto: e.FamilyId!))
            .Where(x => x.App is not null && SyncFamilyLinks.IsMappable(x.App!.FamilyId))
            .Select(x => (x.App!.FamilyId, x.Elvanto))
            .ToList();

        // Contested households are dropped before anything else looks at the data, so a household
        // two families claim cannot also make one of them look split.
        HashSet<string> contested = members.GroupBy(x => x.Elvanto)
            .Where(g => g.Select(x => x.Local).Distinct().Count() > 1)
            .Select(g => g.Key).ToHashSet();

        foreach (string elvanto in contested)
            links.MarkUnmappable(elvanto, $"ElvantoFamilyContested:{elvanto}");

        List<(Guid Local, string Elvanto)> pairs = members.Where(x => !contested.Contains(x.Elvanto)).ToList();

        // Ranked per local family: most members keeps it, ties broken on the household id so the
        // answer is the same on every run. Everything below the first is a household this app has
        // grouped into somebody else's family, and gets one of its own.
        List<(Guid Local, string Elvanto)> seedable = pairs
            .GroupBy(x => x.Local)
            .SelectMany(family => family
                .GroupBy(x => x.Elvanto)
                .OrderByDescending(h => h.Count())
                .ThenBy(h => h.Key, StringComparer.Ordinal)
                .Select((h, rank) => (Local: rank == 0 ? family.Key : Guid.NewGuid(), Elvanto: h.Key)))
            .ToList();

        List<DbElvantoFamilyLink> seeded = [];
        foreach ((Guid local, string elvanto) in seedable)
            if (links.Record(local, elvanto, ElvantoFamilyLinkSource.Seeded) is { } row) seeded.Add(row);

        if (seeded.Count > 0)
        {
            await db.ElvantoFamilyLinks.AddRangeAsync(seeded, token);
            await db.SaveChangesAsync(token);
        }

        int split = pairs.GroupBy(x => x.Local).Count(g => g.Select(x => x.Elvanto).Distinct().Count() > 1);

        logger.LogInformation(
            "Sync: family links — {Existing} already stored, {Seeded} seeded from the roll, "
            + "{Contested} households claimed by more than one local family, {Split} local families "
            + "followed into more than one household",
            links.StoredCount - seeded.Count, seeded.Count, contested.Count, split);

        foreach (string elvanto in contested)
            logger.LogWarning(
                "Sync: Elvanto household {ElvantoFamilyId} is not paired — local families {Families} all "
                + "claim it. Pairing it would merge them; a person has to say which one it is.",
                elvanto, string.Join(", ", members.Where(x => x.Elvanto == elvanto).Select(x => x.Local).Distinct().Order()));

        return links;
    }

    /// <summary>
    /// Records a pairing this run learned, and returns the local family it settles on.
    ///
    /// The row joins the phase's own writes rather than being saved on the spot, so a run that
    /// aborts leaves the table as it found it — a pairing learned by a run that then failed is a
    /// claim nobody has checked, and the next run will learn it again from the same evidence.
    /// </summary>
    private Guid? LinkFamily(
        SyncFamilyLinks         links,
        Guid                    localFamilyId,
        string                  elvantoFamilyId,
        ElvantoFamilyLinkSource source)
    {
        if (links.Record(localFamilyId, elvantoFamilyId, source) is not { } row)
            return links.LocalFor(elvantoFamilyId);

        db.ElvantoFamilyLinks.Add(row);
        logger.LogInformation(
            "Sync: linked local family {LocalFamilyId} to Elvanto household {ElvantoFamilyId} ({Source})",
            localFamilyId, elvantoFamilyId, source);

        return localFamilyId;
    }
}
