using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
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

    private string? TranslateElvantoValue(
        string                   fieldName,
        string?                  elvValue,
        List<DbSchoolGrade>      schoolGrades,
        Dictionary<string, Guid> familyIdMap
    )
    {
        if (elvValue is null) return null;

        if (fieldName == "SchoolGradeId")
            return schoolGrades.FirstOrDefault(g => g.ElvantoId == elvValue)?.Id.ToString();

        if (fieldName == "FamilyId")
        {
            if (!familyIdMap.TryGetValue(elvValue, out Guid localId))
            {
                localId = Guid.NewGuid();
                familyIdMap[elvValue] = localId;
            }

            return localId.ToString();
        }

        return elvValue;
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
}
