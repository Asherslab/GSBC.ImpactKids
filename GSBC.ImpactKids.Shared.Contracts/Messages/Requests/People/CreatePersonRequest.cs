using GSBC.ImpactKids.Shared.Contracts.Entities.People;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreatePersonRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName  { get; set; } = null!;

    public Guid?        SchoolGradeId { get; set; }
    public MediaConsent MediaConsent  { get; set; } = MediaConsent.NotRequested;
    public DateTime?    DateOfBirth   { get; set; }
    public DateTime?    FirstTime     { get; set; }

    public Guid? FamilyId       { get; set; }
    public bool  FamilyGuardian { get; set; }
}