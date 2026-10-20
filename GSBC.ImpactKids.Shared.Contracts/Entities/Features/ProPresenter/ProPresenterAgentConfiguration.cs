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

    /// <summary>The ProPresenter timer that follows tonight's schedule; null leaves every timer alone.</summary>
    public string? ScheduleTimerUuid { get; init; }

    /// <summary>The ProPresenter libraries to copy songs from, already resolved by <see cref="ProPresenterLibrary.SongLibraries"/>. Empty copies none.</summary>
    public List<string> SongLibraryUuids { get; init; } = [];

    // By value, list included. The agent logs a configuration that differs from the last one it had,
    // and record equality compares a list by reference - so every pull would look like a change.
    public virtual bool Equals(ProPresenterAgentConfiguration? other) =>
        other is not null
        && MachineId == other.MachineId
        && Name == other.Name
        && ProPresenterHost == other.ProPresenterHost
        && ProPresenterPort == other.ProPresenterPort
        && ScheduleTimerUuid == other.ScheduleTimerUuid
        && SongLibraryUuids.SequenceEqual(other.SongLibraryUuids);

    public override int GetHashCode() =>
        HashCode.Combine(MachineId, Name, ProPresenterHost, ProPresenterPort, ScheduleTimerUuid, SongLibraryUuids.Count);
}
