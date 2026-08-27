using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
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
    /// <b>Three answers, not two.</b> "Elvanto holds nothing", "Elvanto holds nothing <i>and says
    /// so</i>" and "I cannot read this" are different, and collapsing any two of them is a
    /// data-loss bug rather than a tidiness one. A school grade with no <c>DbSchoolGrade</c> row
    /// read as "Elvanto has no grade for this child" and cleared the grade they had, with an audit
    /// row that looked like a legitimate clear.
    /// </summary>
    private Translated TranslateElvantoValue(
        string          fieldName,
        string?         elvValue,
        SyncWorkingSet  set,
        DbPerson        askingPerson
    )
    {
        // A blank family_id is Elvanto stating that this person is in no household - its own UI shows
        // them as "No Family" - and the app records that as Guid.Empty rather than as a household.
        //
        // This is not the read that caused the incident, and the difference is the whole point. That
        // one passed null through to FamilyIdDescriptor.SetOnApp, which fell back to Guid.NewGuid()
        // and gave 411 people a brand-new one-person household each. The fallback is gone, and
        // Guid.Empty is one shared value meaning "none" - not 397 fresh families.
        if (fieldName == "FamilyId")
            return string.IsNullOrWhiteSpace(elvValue)
                ? new Translated(Guid.Empty.ToString(), Known: true)
                : TranslateFamily(elvValue, set, askingPerson);

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
    /// The local family an Elvanto household is — read from the persisted pairing, and recorded
    /// there the first time Elvanto names a household this app has no row for.
    ///
    /// This used to be an inference over the fetched roll, and it could not be made to work. It
    /// answered only from members <i>other</i> than the person being asked about, which was
    /// necessary — including the asker made every answer self-confirming, and fourteen people
    /// ping-ponged between families on a real run — and it was also fatal, because the roll excludes
    /// contacts: household 42 had four members, three of them contacts, so the one member the run
    /// could see had no other evidence and diverged on every run, forever. 97 people in that state.
    ///
    /// A remembered pairing needs no inference. It does not move when the roll does, so the asker
    /// cannot confirm themselves, and a household with no other visible member is answered from the
    /// row rather than from who happened to be fetched.
    ///
    /// <b>This is not the minting that caused the incident.</b> That was per person, per run, with
    /// no memory, so it re-fired forever and scattered 411 people into one-person households. This
    /// mints at most once per <i>Elvanto household</i>, ever, and every later member of that
    /// household resolves to the same local family.
    /// </summary>
    private Translated TranslateFamily(string elvantoFamilyId, SyncWorkingSet set, DbPerson askingPerson)
    {
        if (set.Families.LocalFor(elvantoFamilyId) is Guid known)
            return new Translated(known.ToString(), Known: true);

        // Deliberately unpaired, and named so the row is actionable: another local family claims this
        // household, or this person's local family is spread across several of them. Either way the
        // answer changes who is related to whom, so it is a question rather than a decision.
        if (set.Families.UnmappableReason(elvantoFamilyId) is { } refusal)
            return Translated.CannotRead(refusal);

        Guid local = askingPerson.FamilyId;

        // The asker's own local family is the pairing, unless it is already spoken for. This is the
        // one moment the asker is allowed to be their own evidence, and it is safe precisely
        // because it is remembered: from the next question onwards the row answers, so a later
        // local move is a difference the run can see rather than one that confirms itself.
        //
        // IsMappable is what keeps the bucket out of the table. A bucketed person falls past this to
        // the mint below, which is the point: "never pair the bucket" and "never let anyone leave
        // it" are different rules, and only the first one is wanted. The bucket is not a household,
        // so it may never be one side of a row - but a person Elvanto has put in a real household
        // should be placed in it rather than left in a pile of 412 strangers.
        if (SyncFamilyLinks.IsMappable(local) && set.Families.ElvantoFor(local) is null)
            return new Translated(
                (LinkFamily(set.Families, local, elvantoFamilyId, ElvantoFamilyLinkSource.Observed) ?? local).ToString(),
                Known: true);

        // Either the person has no local family yet (a person Elvanto is about to create here),
        // theirs already is another household - which is what a move in Elvanto looks like - or
        // they are in the bucket, which is not a family at all. A new local family for this
        // household is the honest answer to all three, and because the row is written on the first
        // member, the rest of the household joins them rather than each starting one of their own.
        Guid minted = Guid.NewGuid();
        return new Translated(
            (LinkFamily(set.Families, minted, elvantoFamilyId, ElvantoFamilyLinkSource.Observed) ?? minted).ToString(),
            Known: true);
    }

    /// <summary>
    /// An Elvanto value in the app's terms. <paramref name="Known"/> is false when the value could
    /// not be read at all, which is not the same as Elvanto holding nothing.
    /// </summary>
    /// <summary>
    /// <paramref name="Detail"/> says <b>why</b> Elvanto's side is not a value, and is carried into
    /// the run's reason string. "Unknown" collapsed two different situations - Elvanto naming no
    /// household at all, and naming one this app cannot place - and the row said neither, so 494
    /// identical "ElvantoValueUnknown" rows could not tell anyone which problem they were looking at.
    /// </summary>
    private readonly record struct Translated(string? Value, bool Known, string? Detail = null)
    {
        public static Translated Nothing    => new(null, true);
        public static Translated Unreadable => new(null, false);

        public static Translated CannotRead(string detail) => new(null, false, detail);
    }

}
