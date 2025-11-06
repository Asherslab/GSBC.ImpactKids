using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.People;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePersonRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> FirstName { get; set; } = new();
    public DeltaUpdate<string> LastName  { get; set; } = new();

    public DeltaUpdate<Guid?>        SchoolGradeId { get; set; } = new();
    public DeltaUpdate<MediaConsent> MediaConsent  { get; set; } = new();
    public DeltaUpdate<DateTime?>    DateOfBirth   { get; set; } = new();
    public DeltaUpdate<DateTime?>    FirstTime     { get; set; } = new();

    public DeltaUpdate<Guid> FamilyId       { get; set; } = new();
    public DeltaUpdate<bool> FamilyGuardian { get; set; } = new();
}