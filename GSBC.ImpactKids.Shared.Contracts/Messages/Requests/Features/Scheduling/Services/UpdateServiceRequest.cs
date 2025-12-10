using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateServiceRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<string?>  Name { get; set; } = new();
    public DeltaUpdate<DateTime> Date { get; set; } = new();

    public DeltaUpdate<Guid?> SchoolTermId  { get; set; } = new();
    public DeltaUpdate<Guid?> ServiceTypeId { get; set; } = new();
}