using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdatePersonRequest : ReadRequestBase, IUpdateRequest<Person, UpdatePersonRequest>
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

    public DeltaUpdate<string?> Email { get; set; } = new();
    public DeltaUpdate<string?> PhoneNumber  { get; set; } = new();

    public DeltaUpdate<Guid?>        SchoolGradeId { get; set; } = new();
    public DeltaUpdate<MediaConsent> MediaConsent  { get; set; } = new();
    public DeltaUpdate<DateTime?>    DateOfBirth   { get; set; } = new();
    public DeltaUpdate<DateTime?>    FirstTime     { get; set; } = new();

    /// <inheritdoc cref="Person.Gender"/>
    public DeltaUpdate<Gender?> Gender { get; set; } = new();

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime?> LocalDateOfBirth { get; set; }

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime?> LocalFirstTime { get; set; }

    public DeltaUpdate<Guid?> FamilyId       { get; set; } = new();
    public DeltaUpdate<bool> FamilyGuardian { get; set; } = new();

    /// <inheritdoc cref="Person.PhotoNeedsUpdate"/>
    public DeltaUpdate<bool> PhotoNeedsUpdate { get; set; } = new();

    public static UpdatePersonRequest FromEntity(Person entity)
    {
        UpdatePersonRequest request = new()
        {
            Guid = entity.Id,
        };

        request.FirstName.SetInitialValue(entity.FirstName);
        request.LastName.SetInitialValue(entity.LastName);
        
        request.Email.SetInitialValue(entity.Email);
        request.PhoneNumber.SetInitialValue(entity.PhoneNumber);

        request.SchoolGradeId.SetInitialValue(entity.SchoolGradeId);
        request.MediaConsent.SetInitialValue(entity.MediaConsent);
        request.Gender.SetInitialValue(entity.Gender);
        request.LocalDateOfBirth.SetInitialValue(entity.LocalDateOfBirth);
        request.LocalFirstTime.SetInitialValue(entity.LocalFirstTime);

        request.FamilyId.SetInitialValue(entity.FamilyId);
        request.FamilyGuardian.SetInitialValue(entity.FamilyGuardian);
        request.PhotoNeedsUpdate.SetInitialValue(entity.PhotoNeedsUpdate);

        return request;
    }
}