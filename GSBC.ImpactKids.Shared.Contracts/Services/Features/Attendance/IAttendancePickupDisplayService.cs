using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

/// <summary>
/// Read only, unauthenticated, for the pickup wall in the room.
/// <para>
/// Routed under "public/" rather than "gRPC/" for the same reason as
/// <see cref="Games.IGameDisplayService"/> - a screen on a wall cannot sign in. This one
/// deliberately puts people on an anonymous screen, so it is a separate service with its
/// own narrower rule: a display name (first name plus last initial) and a time, for
/// children currently requested and not yet signed out, and nothing else. No last names, no
/// dates of birth, no medical or allergy detail, no family, no ids. The only reason a name
/// on a wall is acceptable at all is that the room is full of those children's parents.
/// </para>
/// </summary>
[Service("public/GSBC.ImpactKids.Attendance.Display")]
public interface IAttendancePickupDisplayService
{
    Task<PickupDisplayResponse> GetPickups(PickupDisplayRequest r, CallContext c = default);

    /// <summary>
    /// Server streaming. Yields the list immediately, then again on every change - the
    /// display does not poll. Also re-sends periodically so a stream killed by something in
    /// between is noticed rather than leaving a frozen screen on the wall.
    /// </summary>
    IAsyncEnumerable<PickupDisplayResponse> WatchPickups(PickupDisplayRequest r, CallContext c = default);
}
