using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    private DbPerson CreatePersonFromElvanto(
        ElvantoPerson            elv,
        List<DbSchoolGrade>      grades,
        Dictionary<string, Guid> familyIdMap
    )
    {
        // Placeholder values for required properties; all overwritten by the descriptor loop below
        DbPerson p = new()
        {
            Id = Guid.NewGuid(),
            ElvantoId = elv.Id,
            FirstName = "",
            LastName = "",
            PhoneNumber = null,
            Email = null,
            SchoolGradeId = null,
            MediaConsent = nameof(Shared.Contracts.Entities.Features.People.MediaConsent.NotRequested),
            DateOfBirth = null,
            FirstTime = null,
            FamilyId = Guid.Empty,
            FamilyGuardian = false,
        };

        foreach (IFieldSyncDescriptor desc in _descriptors)
        {
            string? elvValue = TranslateElvantoValue(desc.FieldName, desc.GetFromElvanto(elv), grades, familyIdMap);
            desc.SetOnApp(p, elvValue);
        }

        return p;
    }

    /// <summary>
    /// The medical/allergy text for a person, in the same format an update would push.
    /// Null when the descriptor is absent, which falls the create back to its own merge.
    /// </summary>
    private string? ComposeMedicalAllergyText(DbPerson person) =>
        _descriptors.OfType<MedicalAllergyNotesDescriptor>().FirstOrDefault()?.GetFromApp(person);
}
