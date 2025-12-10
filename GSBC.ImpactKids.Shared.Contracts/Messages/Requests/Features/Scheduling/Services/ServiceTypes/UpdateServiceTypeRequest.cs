using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services.ServiceTypes;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateServiceTypeRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string> Label { get; set; } = new();

    public DeltaUpdate<string?> Color { get; set; } = new();
}