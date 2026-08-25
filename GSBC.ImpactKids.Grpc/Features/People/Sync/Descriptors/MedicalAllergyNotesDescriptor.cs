using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;

/// <summary>
/// Owns Elvanto's single medical/allergy custom field, in both directions.
///
/// This replaces the old AllergiesOutboundDescriptor and MedicalNotesOutboundDescriptor.
/// Two descriptors could not work: both wrote to the same Elvanto field, so each would
/// overwrite the other's section, and neither could read the field back (both returned null
/// from GetFromElvanto), which meant the sync could never tell whether Elvanto already held
/// the note. One Elvanto field is one descriptor.
///
/// Reading back is possible now because <see cref="MedicalAllergyFormat"/> defines a shape
/// that survives the round trip. Text that does not fit that shape is never discarded and
/// never guessed at - it lands in a single "Other" medical note holding the raw text, so a
/// leader who typed freehand into Elvanto keeps exactly what they wrote.
///
/// Inbound is additive: rows are created and updated, never deleted. Deleting a child's
/// allergy because a free-text field did not mention it is not a risk worth taking, so a
/// removal in Elvanto has to be repeated in the app by hand.
/// </summary>
public class MedicalAllergyNotesDescriptor : BaseFieldSyncDescriptor
{
    public override string        EntityType       => "Person";
    public override string        FieldName        => "MedicalAllergyNotes";
    public override SyncDirection DefaultDirection => SyncDirection.Bidirectional;

    /// <summary>
    /// On the very first sync there is no snapshot, so neither side has a trustworthy
    /// "changed at". For this field the app is the system of record in that situation - it
    /// holds structured data a leader entered deliberately, where Elvanto holds whatever text
    /// happened to be in the box.
    /// </summary>
    public override SyncSource FirstSyncPrecedence => SyncSource.App;

    /// <inheritdoc cref="BaseFieldSyncDescriptor.PrecedenceOnTie"/>
    public override PrecedenceOnTie PrecedenceOnTie => PrecedenceOnTie.App;

    /// <summary>
    /// Lookups needed to turn text back into rows. Primed once per sync run by
    /// ElvantoPersonSyncService, because a descriptor cannot reach the database itself.
    /// </summary>
    public MedicalAllergyLookups? Lookups { get; set; }

    public override string? GetFromApp(DbPerson person)
    {
        IEnumerable<MedicalAllergyFormat.Item> allergies = person.Allergies
            .Select(a => ToItem(a.AllergenId, a.Notes, a.Severe, Lookups?.AllergenLabels))
            .Where(CarriesInformation);

        IEnumerable<MedicalAllergyFormat.Item> medical = person.MedicalNotes
            .Select(n => ToItem(n.MedicalTypeId, n.Notes, n.Severe, Lookups?.MedicalTypeLabels))
            .Where(CarriesInformation);

        return MedicalAllergyFormat.Compose(allergies, medical);
    }

    /// <summary>
    /// Labels that carry no information. The app models "no known allergies" as a row pointing
    /// at a "None" allergen, so composing it verbatim rewrote 91 people's Elvanto field with
    /// "Allergies: None / Medical: None" - pure churn against text that already said None.
    /// A person whose only rows are None composes to null, and nothing is pushed at all.
    /// </summary>
    private static readonly HashSet<string> NoInformationLabels =
        new(StringComparer.OrdinalIgnoreCase) { "None", "Nil", "N/A", "NA" };

    private static bool CarriesInformation(MedicalAllergyFormat.Item item) =>
        !string.IsNullOrWhiteSpace(item.Name) &&
        (!NoInformationLabels.Contains(item.Name.Trim()) || !string.IsNullOrWhiteSpace(item.Notes));

    /// <summary>
    /// The label is the thing worth pushing - "Peanuts" matters far more than a blank note.
    /// The old descriptors read only the free-text Notes, so a severe peanut allergy recorded
    /// with no typed note produced null and was never sent at all.
    ///
    /// When a row has no linked allergen or medical type, the note is all we know, so it
    /// becomes the name and the notes section is dropped. Emitting both produced
    /// "Medical: No citrus - rash reaction - No citrus - rash reaction".
    /// </summary>
    private static MedicalAllergyFormat.Item ToItem(
        Guid?                              id,
        string?                            notes,
        bool                               severe,
        IReadOnlyDictionary<Guid, string>? labels)
    {
        if (id is not null && labels is not null && labels.TryGetValue(id.Value, out string? label))
            return new MedicalAllergyFormat.Item(label, severe, notes);

        return new MedicalAllergyFormat.Item(notes?.Trim() ?? string.Empty, severe, null);
    }

    /// <summary>
    /// First sync, app wins - but not by deleting. Elvanto's box is free text a leader may have
    /// typed years ago ("Eggs &amp; Milk &amp; Nuts" where the app only knows about eggs), and the
    /// app has no way to recover it once overwritten. Anything Elvanto says that the app does not
    /// already say is carried across verbatim on its own line.
    ///
    /// That extra line does not fit the grammar on purpose: it parses back as unrecognised, so a
    /// later inbound turns it into an "Other" medical note for a human to reconcile, rather than
    /// being silently absorbed or lost.
    /// </summary>
    public override string? MergeForFirstSync(string? appValue, string? elvValue)
    {
        if (string.IsNullOrWhiteSpace(elvValue)) return appValue;

        // "None", "nil", "none known" and friends say nothing worth preserving.
        if (!string.IsNullOrWhiteSpace(Normalise(elvValue)) && NoInformationText.Contains(Normalise(elvValue)))
            return appValue;

        if (string.IsNullOrWhiteSpace(appValue)) return elvValue.Trim();

        // Already covered by what we are about to write - nothing would be lost.
        if (Normalise(appValue).Contains(Normalise(elvValue), StringComparison.Ordinal))
            return appValue;

        return $"{appValue}\n{elvValue.Trim()}";
    }

    private static readonly HashSet<string> NoInformationText =
        new(StringComparer.Ordinal)
        {
            "none", "nil", "na", "n/a", "noneknown", "nilknown", "noknown",
            "noknownallergies", "noallergies", "nothing", "niknown"
        };

    /// <summary>Lowercase, punctuation- and space-free, so "None known." and "none known" match.</summary>
    private static string Normalise(string? value) =>
        value is null
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    /// <summary>
    /// "None", "Nil Known" and friends say nothing. Without this they parse as unrecognised text
    /// and each becomes an "Other" medical note reading "None" - 47 people picked up a junk
    /// record on the first dry run. Returning false here stops the value driving an inbound
    /// write or winning a conflict, which is exactly what this hook is for.
    /// </summary>
    public override bool IsValidInboundValue(string? elvValue) =>
        !string.IsNullOrWhiteSpace(elvValue) && !NoInformationText.Contains(Normalise(elvValue));

    public override string? GetFromElvanto(ElvantoPerson elvantoPerson) =>
        string.IsNullOrWhiteSpace(elvantoPerson.MedicalAllergyNotes)
            ? null
            : elvantoPerson.MedicalAllergyNotes;

    public override bool ApplyToElvantoRequest(ElvantoUpdatePersonRequest req, string? value) =>
        Set(value, v => req.MedicalAllergyNotes = v);

    public override void SetOnApp(DbPerson person, string? value)
    {
        if (Lookups is null)
            throw new InvalidOperationException(
                $"{nameof(MedicalAllergyNotesDescriptor)}.{nameof(Lookups)} was not primed before an inbound update. "
                + "Without it an inbound write would silently drop every allergen and medical type.");

        MedicalAllergyFormat.ParsedMedicalAllergy parsed = MedicalAllergyFormat.Parse(value);

        foreach (MedicalAllergyFormat.Item item in parsed.Allergies)
            ApplyAllergy(person, item);

        foreach (MedicalAllergyFormat.Item item in parsed.Medical)
            ApplyMedical(person, item);

        // Everything the grammar could not read, kept verbatim in one note rather than
        // dropped or split into guesses.
        if (parsed.Unrecognised.Count > 0)
            ApplyUnrecognised(person, string.Join("\n", parsed.Unrecognised));
    }

    private void ApplyAllergy(DbPerson person, MedicalAllergyFormat.Item item)
    {
        Guid? allergenId = Lookups!.FindAllergen(item.Name);

        DbAllergy? existing = person.Allergies.FirstOrDefault(a =>
            allergenId is not null
                ? a.AllergenId == allergenId
                : string.Equals(a.Notes, item.Notes ?? item.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            person.Allergies.Add(new DbAllergy
            {
                // Empty, not a fresh Guid. These rows are discovered through the navigation
                // rather than added to a DbSet, and EF reads a key that is already set as
                // "this row exists" - so it issues an UPDATE that matches nothing and the whole
                // save dies with a concurrency error. A dry run never reaches SaveChanges, which
                // is why this only surfaced on the first Full run.
                Id         = Guid.Empty,
                PersonId   = person.Id,
                AllergenId = allergenId,
                // An unknown allergen keeps its name in the note, so nothing is lost even
                // though there is no row in Allergens to point at.
                Notes  = allergenId is null ? Describe(item) : item.Notes,
                Severe = item.Severe
            });
            return;
        }

        existing.Severe = item.Severe;
        if (item.Notes is not null) existing.Notes = item.Notes;
    }

    private void ApplyMedical(DbPerson person, MedicalAllergyFormat.Item item)
    {
        Guid? typeId = Lookups!.FindMedicalType(item.Name);

        DbMedicalNote? existing = person.MedicalNotes.FirstOrDefault(n =>
            typeId is not null
                ? n.MedicalTypeId == typeId
                : string.Equals(n.Notes, item.Notes ?? item.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            person.MedicalNotes.Add(new DbMedicalNote
            {
                // Empty so EF treats this as a new row - see ApplyAllergy.
                Id            = Guid.Empty,
                PersonId      = person.Id,
                MedicalTypeId = typeId ?? Lookups.OtherMedicalTypeId,
                Notes         = typeId is null ? Describe(item) : item.Notes,
                Severe        = item.Severe
            });
            return;
        }

        existing.Severe = item.Severe;
        if (item.Notes is not null) existing.Notes = item.Notes;
    }

    private void ApplyUnrecognised(DbPerson person, string rawText)
    {
        DbMedicalNote? existing = person.MedicalNotes.FirstOrDefault(n =>
            n.MedicalTypeId == Lookups!.OtherMedicalTypeId &&
            string.Equals(n.Notes, rawText, StringComparison.OrdinalIgnoreCase));

        if (existing is not null) return;

        person.MedicalNotes.Add(new DbMedicalNote
        {
            // Empty so EF treats this as a new row - see ApplyAllergy.
            Id            = Guid.Empty,
            PersonId      = person.Id,
            MedicalTypeId = Lookups!.OtherMedicalTypeId,
            Notes         = rawText,
            Severe        = false
        });
    }

    private static string Describe(MedicalAllergyFormat.Item item) =>
        item.Notes is null ? item.Name : $"{item.Name} - {item.Notes}";
}
