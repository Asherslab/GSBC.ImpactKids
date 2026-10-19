namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// Everything an agent is told about how to do its job. Pulled from Impact Kids on a timer, so a
/// change on the admin page reaches the Mac without anybody touching it - the only thing configured
/// on the Mac itself is the credential.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ProPresenterAgentConfiguration
{
    public required Guid   MachineId        { get; init; }
    public required string Name             { get; init; }
    public required string ProPresenterHost { get; init; }
    public required int    ProPresenterPort { get; init; }
}
