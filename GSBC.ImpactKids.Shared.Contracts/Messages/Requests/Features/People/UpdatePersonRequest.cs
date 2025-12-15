using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePersonRequest : ReadRequestBase
{
    public UpdatePersonRequest()
    {
        LocalDateOfBirth = new DelegatingDeltaUpdate<DateTime?>(
            DateOfBirth,
            getter: x => x?.ToLocalTime(),
            setter: x => x?.ToUniversalTime()
        );
        LocalFirstTime = new DelegatingDeltaUpdate<DateTime?>(
            FirstTime,
            getter: x => x?.ToLocalTime(),
            setter: x => x?.ToUniversalTime()
        );
    }
    
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> FirstName { get; set; } = new();
    public DeltaUpdate<string> LastName  { get; set; } = new();

    public DeltaUpdate<Guid?>        SchoolGradeId { get; set; } = new();
    public DeltaUpdate<MediaConsent> MediaConsent  { get; set; } = new();
    public DeltaUpdate<DateTime?>    DateOfBirth   { get; set; } = new();
    public DeltaUpdate<DateTime?>    FirstTime     { get; set; } = new();

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime?> LocalDateOfBirth { get; set; }

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime?> LocalFirstTime { get; set; }

    public DeltaUpdate<Guid> FamilyId       { get; set; } = new();
    public DeltaUpdate<bool> FamilyGuardian { get; set; } = new();
}