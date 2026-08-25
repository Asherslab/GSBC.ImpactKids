using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Loads the allergen and medical-type tables into the medical/allergy descriptor so it can
    /// map Elvanto's text back onto rows. Also guarantees an "Other" medical type exists, which
    /// is where text that does not fit the agreed format is parked rather than dropped.
    /// </summary>
    private async Task PrimeMedicalAllergyLookupsAsync(CancellationToken token)
    {
        MedicalAllergyNotesDescriptor? descriptor =
            _descriptors.OfType<MedicalAllergyNotesDescriptor>().FirstOrDefault();
        if (descriptor is null) return;

        List<DbAllergen>    allergens    = await db.Allergens.ToListAsync(token);
        List<DbMedicalType> medicalTypes = await db.MedicalTypes.ToListAsync(token);

        const string otherLabel = "Other";
        DbMedicalType? other = medicalTypes
            .FirstOrDefault(t => string.Equals(t.Label, otherLabel, StringComparison.OrdinalIgnoreCase));

        if (other is null)
        {
            other = new DbMedicalType { Id = Guid.NewGuid(), Label = otherLabel };
            db.MedicalTypes.Add(other);
            medicalTypes.Add(other);
            logger.LogInformation("Sync: created the \"{Label}\" medical type to hold unparsed Elvanto text", otherLabel);
        }

        descriptor.Lookups = new MedicalAllergyLookups
        {
            AllergenLabels     = allergens.ToDictionary(a => a.Id, a => a.Label),
            MedicalTypeLabels  = medicalTypes.ToDictionary(m => m.Id, m => m.Label),
            OtherMedicalTypeId = other.Id
        };
    }

    /// <summary>
    /// Turns an Elvanto value into the app's terms, and says whether it could.
    ///
    /// <b>"Elvanto holds nothing" and "I cannot read this" are different answers</b>, and collapsing
    /// them into a single null is a data-loss bug rather than a tidiness one. A school grade with no
    /// <c>DbSchoolGrade</c> row read as "Elvanto has no grade for this child" and cleared the grade
    /// they had, with an audit row that looked like a legitimate clear.
    /// </summary>
    private Translated TranslateElvantoValue(
        string          fieldName,
        string?         elvValue,
        SyncWorkingSet  set,
        Guid            askingPersonId
    )
    {
        // Family first, because for family "Elvanto said nothing" is not the same answer it is for
        // every other field. A blank family_id means Elvanto has no household for this person, which
        // is not evidence that they have none - and the app's own "no family yet" bucket holds
        // hundreds of people, so reading it as a value proposes to move all of them.
        if (fieldName == "FamilyId")
            return elvValue is null ? Translated.Unreadable : TranslateFamily(elvValue, set, askingPersonId);

        if (elvValue is null) return Translated.Nothing;

        if (fieldName == "SchoolGradeId")
        {
            DbSchoolGrade? grade = set.SchoolGrades.FirstOrDefault(g => g.ElvantoId == elvValue);
            return grade is null
                ? Translated.Unreadable
                : new Translated(grade.Id.ToString(), Known: true);
        }

        return new Translated(elvValue, Known: true);
    }

    /// <summary>
    /// The local family an Elvanto household corresponds to, <b>as evidenced by its members other
    /// than the person being asked about</b>.
    ///
    /// Excluding the asker matters here for the same reason it matters in
    /// <see cref="SyncWorkingSet.ResolveFamilyInElvanto"/>, and leaving it out was a live bug: the
    /// map is seeded from every linked person, so a person alone in their Elvanto household mapped
    /// that household straight back to the local family they are already in. The inbound "move" was
    /// a no-op that settled the base anyway, and the next run read the app's own grouping as a fresh
    /// change and planned to push them back. Fourteen people ping-ponging, on a real run.
    ///
    /// With no other evidence the answer is unknown, not "make a new household" — a person whose
    /// Elvanto family this app knows nothing about, whose relatives say otherwise, is a genuine
    /// conflict for a human rather than something to guess at.
    /// </summary>
    private static Translated TranslateFamily(string elvantoFamilyId, SyncWorkingSet set, Guid askingPersonId)
    {
        // Ranked, not first-found. An Elvanto household can have members in more than one local
        // family, and picking whichever the dictionary happened to yield first moved people into a
        // relative's family at random - 397 of them. Most members wins, ties broken on the id so the
        // answer does not change between runs, which mirrors ResolveFamilyInElvanto exactly.
        Guid? local = set.FamilyMembership
            .Select(kv => (
                Family: kv.Key,
                Members: kv.Value.Count(m => m.PersonId != askingPersonId && m.ElvantoFamilyId == elvantoFamilyId)))
            .Where(x => x.Members > 0)
            .OrderByDescending(x => x.Members)
            .ThenBy(x => x.Family)
            .Select(x => (Guid?)x.Family)
            .FirstOrDefault();

        return local is null ? Translated.Unreadable : new Translated(local.Value.ToString(), Known: true);
    }

    /// <summary>
    /// An Elvanto value in the app's terms. <paramref name="Known"/> is false when the value could
    /// not be read at all, which is not the same as Elvanto holding nothing.
    /// </summary>
    private readonly record struct Translated(string? Value, bool Known)
    {
        public static Translated Nothing    => new(null, true);
        public static Translated Unreadable => new(null, false);
    }

    private static SyncMode MapMode(ElvantoSyncMode mode) => mode switch
    {
        ElvantoSyncMode.DryRun   => SyncMode.DryRun,
        ElvantoSyncMode.AppOnly  => SyncMode.AppOnly,
        _                        => SyncMode.Full
    };

    private static SyncScope MapScope(ElvantoSyncScope scope) => scope switch
    {
        ElvantoSyncScope.Person => SyncScope.Person,
        ElvantoSyncScope.Family => SyncScope.Family,
        _                       => SyncScope.All
    };

    // Apply reconstructs the request from the operation row, because a plan may be executed in a
    // later process than the one that decided it.
    private static ElvantoSyncMode UnmapMode(SyncMode mode) => mode switch
    {
        SyncMode.DryRun  => ElvantoSyncMode.DryRun,
        SyncMode.AppOnly => ElvantoSyncMode.AppOnly,
        _                => ElvantoSyncMode.Full
    };

    private static ElvantoSyncScope UnmapScope(SyncScope scope) => scope switch
    {
        SyncScope.Person => ElvantoSyncScope.Person,
        SyncScope.Family => ElvantoSyncScope.Family,
        _                => ElvantoSyncScope.All
    };
}
