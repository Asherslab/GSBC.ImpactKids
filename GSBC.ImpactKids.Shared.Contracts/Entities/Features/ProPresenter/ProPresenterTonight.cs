using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.ProPresenter;

/// <summary>
/// Tonight's schedule, for the agent to drive the stage from: today's service (at the church, see <see cref="ChurchTime"/>) that has
/// one, and that service's date - the day its fixed start times are on. Absent when there is none.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ProPresenterTonight
{
    public required Schedule Schedule    { get; init; }
    public required string   ServiceName { get; init; }

    /// <summary>The service's local date, at midnight.</summary>
    public required DateTime ServiceDate { get; init; }
}
