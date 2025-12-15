using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreatePersonRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName  { get; set; } = null!;

    public Guid?        SchoolGradeId { get; set; }
    public MediaConsent MediaConsent  { get; set; } = MediaConsent.NotRequested;
    public DateTime?    DateOfBirth   { get; set; }
    public DateTime?    FirstTime     { get; set; }

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

    public Guid? FamilyId       { get; set; }
    public bool  FamilyGuardian { get; set; }
}