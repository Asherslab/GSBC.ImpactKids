using GSBC.ImpactKids.Grpc.Data.Models.People;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync;

/// <summary>
/// Allergen and medical-type tables, loaded once per sync run so the descriptor can turn
/// Elvanto's text back into rows without reaching for the database per person.
/// </summary>
public class MedicalAllergyLookups
{
    public required IReadOnlyDictionary<Guid, string> AllergenLabels    { get; init; }
    public required IReadOnlyDictionary<Guid, string> MedicalTypeLabels { get; init; }

    /// <summary>Where text we could not interpret goes. Created on demand by the sync service.</summary>
    public required Guid OtherMedicalTypeId { get; init; }

    private Dictionary<string, Guid>? _allergensByLabel;
    private Dictionary<string, Guid>? _medicalByLabel;

    public Guid? FindAllergen(string name) => Find(name,
        _allergensByLabel ??= Invert(AllergenLabels));

    public Guid? FindMedicalType(string name) => Find(name,
        _medicalByLabel ??= Invert(MedicalTypeLabels));

    private static Guid? Find(string name, Dictionary<string, Guid> index) =>
        index.TryGetValue(name.Trim(), out Guid id) ? id : null;

    // Labels are matched case- and space-insensitively: "peanuts" typed in Elvanto should find
    // the "Peanuts" allergen rather than create a second, unlinked record.
    private static Dictionary<string, Guid> Invert(IReadOnlyDictionary<Guid, string> labels)
    {
        Dictionary<string, Guid> index = new(StringComparer.OrdinalIgnoreCase);
        foreach ((Guid id, string label) in labels)
            index.TryAdd(label.Trim(), id);
        return index;
    }
}
