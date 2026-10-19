using GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.ProPresenter;

/// <summary>The settings a leader may change. All are written every time; there are only four.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateProPresenterMachineRequest : IUpdateRequest<ProPresenterMachine, UpdateProPresenterMachineRequest>
{
    public required Guid   Id               { get; set; }
    public required string Name             { get; set; }
    public required string ProPresenterHost { get; set; }
    public required int    ProPresenterPort { get; set; }

    /// <summary>The ProPresenter timer that follows tonight's schedule, from the machine's reported timers. Null for none.</summary>
    public string? ScheduleTimerUuid { get; set; }

    public static UpdateProPresenterMachineRequest FromEntity(ProPresenterMachine entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ProPresenterHost = entity.ProPresenterHost,
        ProPresenterPort = entity.ProPresenterPort,
        ScheduleTimerUuid = entity.ScheduleTimerUuid
    };
}
