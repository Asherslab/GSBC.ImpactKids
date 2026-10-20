namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;

/// <summary>
/// A reusable schedule item - "Welcome &amp; Games, 10 minutes", or a placeholder like "Song 1" whose
/// real name is filled in on each service. Adding one to a schedule copies these values; the item
/// remembers which template it came from, but editing the template later changes no schedule.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ScheduleItemTemplate : IIdentifiable
{
    public required Guid   Id      { get; init; }
    public required string Name    { get; init; }
    public required int    Minutes { get; init; }

    /// <summary>The local time of day the item starts, for an item pinned to the clock. Null runs on from the item before it.</summary>
    public TimeSpan? StartsAt { get; init; }

    /// <summary>A placeholder whose name is filled in per service - "Song 1" becomes "Song 1 — Way Maker".</summary>
    public required bool NeedsFillIn { get; init; }

    /// <summary>A heading, like "Songs" or "Games", that items go under. Added to a schedule, it starts empty.</summary>
    public bool IsSection { get; init; }

    /// <summary>A placeholder filled in with a song, chosen from the song library. Always <see cref="NeedsFillIn"/>.</summary>
    public bool IsSong { get; init; }
}
