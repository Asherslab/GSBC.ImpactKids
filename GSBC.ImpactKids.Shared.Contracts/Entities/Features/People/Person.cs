using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class Person
{
    public required Guid Id { get; set; }

    public required string FirstName { get; set; }
    public required string LastName  { get; set; }

    public required SchoolGrade? SchoolGrade  { get; set; }
    public required MediaConsent MediaConsent { get; set; }
    public required DateTime?    DateOfBirth  { get; set; }
    public required DateTime?    FirstTime    { get; set; }
    
    [ProtoIgnore]
    public DateTime? LocalDateOfBirth
    {
        get => DateOfBirth?.ToLocalTime();
        set => DateOfBirth = value?.ToUniversalTime();
    }
    
    [ProtoIgnore]
    public DateTime? LocalFirstTime
    {
        get => FirstTime?.ToLocalTime();
        set => FirstTime = value?.ToUniversalTime();
    }

    public required List<Allergy>     Allergies    { get; set; } = [];
    public required List<MedicalNote> MedicalNotes { get; set; } = [];

    // family stuff
    public required Guid FamilyId       { get; set; }
    public required bool FamilyGuardian { get; set; }

    public int? GetAge() => LocalDateOfBirth == null
        ? null
        : (int.Parse(DateTime.Now.ToString("yyyyMMdd")) - int.Parse(LocalDateOfBirth.Value.ToString("yyyyMMdd")))
          / 10000;

    public string GetDisplayName() => $"{FirstName} {LastName}";

    public static string BuildSubscription(Guid? familyId = null, Guid? personId = null) =>
        $"{nameof(Person)}.{familyId?.ToString() ?? "*"}.{personId?.ToString() ?? "*"}";
}

[ProtoContract]
public enum MediaConsent
{
    NotRequested,
    Yes,
    No,
    StrictlyNo
}

public static class MediaConsentHelper
{
    public static MediaConsent FromElvanto(string? name)
    {
        return name switch
        {
            "Not Requested" => MediaConsent.NotRequested,
            "Yes"           => MediaConsent.Yes,
            "No"            => MediaConsent.No,
            "Strictly No"   => MediaConsent.StrictlyNo,
            _               => MediaConsent.NotRequested
        };
    }

    public static string ToDisplay(this MediaConsent consent)
    {
        return consent switch
        {
            MediaConsent.NotRequested => "Not Requested",
            MediaConsent.Yes          => "Yes",
            MediaConsent.No           => "No",
            MediaConsent.StrictlyNo   => "Strictly No",
            _                         => "Unknown"
        };
    }
}