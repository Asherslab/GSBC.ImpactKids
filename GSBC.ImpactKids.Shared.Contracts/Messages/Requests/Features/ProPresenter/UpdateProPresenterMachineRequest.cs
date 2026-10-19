using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;

/// <summary>The settings a leader may change. All three are written every time; there are only three.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateProPresenterMachineRequest : IUpdateRequest<ProPresenterMachine, UpdateProPresenterMachineRequest>
{
    public required Guid   Id               { get; set; }
    public required string Name             { get; set; }
    public required string ProPresenterHost { get; set; }
    public required int    ProPresenterPort { get; set; }

    public static UpdateProPresenterMachineRequest FromEntity(ProPresenterMachine entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ProPresenterHost = entity.ProPresenterHost,
        ProPresenterPort = entity.ProPresenterPort
    };
}
