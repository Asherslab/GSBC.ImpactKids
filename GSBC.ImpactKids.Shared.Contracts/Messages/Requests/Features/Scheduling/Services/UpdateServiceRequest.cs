using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateServiceRequest : ReadRequestBase, IUpdateRequest<Service, UpdateServiceRequest>
{
    public UpdateServiceRequest()
    {
        LocalDate = new DelegatingDeltaUpdate<DateTime>(
            Date,
            getter: x => x.ToLocalTime(),
            setter: x => x.ToUniversalTime()
        );
    }

    public override string Id { get; set; } = null!;

    public DeltaUpdate<string?>  Name { get; set; } = new();
    public DeltaUpdate<DateTime> Date { get; set; } = new();

    [ProtoIgnore]
    public DelegatingDeltaUpdate<DateTime> LocalDate { get; set; }

    public DeltaUpdate<Guid?> SchoolTermId  { get; set; } = new();
    public DeltaUpdate<Guid?> ServiceTypeId { get; set; } = new();

    public static UpdateServiceRequest FromEntity(Service entity)
    {
        UpdateServiceRequest request = new()
        {
            Guid = entity.Id
        };

        request.Name.SetInitialValue(entity.Name);
        request.LocalDate.SetInitialValue(entity.LocalDate); // Set Date for LocalDate usage

        request.SchoolTermId.SetInitialValue(entity.SchoolTermId);
        request.ServiceTypeId.SetInitialValue(entity.ServiceTypeId);
        return request;
    }
}