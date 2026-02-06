using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateServiceTypeRequest : ReadRequestBase, IUpdateRequest<ServiceType, UpdateServiceTypeRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> Label { get; set; } = new();

    public DeltaUpdate<string?> Color { get; set; } = new();

    public static UpdateServiceTypeRequest FromEntity(ServiceType entity)
    {
        UpdateServiceTypeRequest request = new()
        {
            Guid = entity.Id
        };

        request.Label.SetInitialValue(entity.Label);
        request.Color.SetInitialValue(entity.Color);
        return request;
    }
}