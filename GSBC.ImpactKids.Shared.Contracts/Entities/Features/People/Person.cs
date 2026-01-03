using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.MedicalNotes;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Person : IIdentifiable
{
    public required Guid Id { get; init; }

    public required string FirstName { get; init; }
    public required string LastName  { get; init; }

    public required SchoolGrade? SchoolGrade  { get; init; }
    public required MediaConsent MediaConsent { get; init; }
    public required DateTime?    DateOfBirth  { get; init; }
    public required DateTime?    FirstTime    { get; init; }

    [ProtoIgnore]
    public DateTime? LocalDateOfBirth => DateOfBirth?.ToLocalTime();

    [ProtoIgnore]
    public DateTime? LocalFirstTime => FirstTime?.ToLocalTime();

    public required ImmutableList<Allergy>     Allergies    { get; init; } = [];
    public required ImmutableList<MedicalNote> MedicalNotes { get; init; } = [];

    // family stuff
    public required Guid FamilyId       { get; init; }
    public required bool FamilyGuardian { get; init; }

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