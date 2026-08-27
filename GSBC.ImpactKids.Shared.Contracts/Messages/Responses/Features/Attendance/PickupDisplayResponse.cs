namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;

/// <summary>
/// Everything the unauthenticated pickup wall needs, and nothing else - a display name and
/// a time. See "The privacy decision" in
/// <c>docs/work/2026-08-pickup-requests-and-activity-log.md</c>: no last names, no dates of
/// birth, no medical or allergy detail, no family, and no ids that can be turned back into a
/// person. Widening this is a decision, not a refactor.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PickupDisplayResponse : BasicResponse
{
    public string? ServiceTitle { get; init; }

    /// <summary>Ordered by <see cref="PickupDisplayEntry.RequestedAt"/> ascending - longest wait first.</summary>
    public List<PickupDisplayEntry> Waiting { get; init; } = [];

    public new static PickupDisplayResponse WithError(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// First name plus last initial, and nothing else. No id - nothing here may be turned back
/// into a person.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PickupDisplayEntry
{
    public required string   Name        { get; init; }
    public required DateTime RequestedAt { get; init; }
}
