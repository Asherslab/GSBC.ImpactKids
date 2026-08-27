using System.Text;
using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using GSBC.ImpactKids.Grpc.Extensions;
using GSBC.ImpactKids.Grpc.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.AttendancePickupDisplayServices;

/// <summary>
/// Unauthenticated on purpose - see <see cref="IAttendancePickupDisplayService"/>. Returns a
/// display name and a time for the children currently requested and not yet signed out, and
/// nothing else. Never add a field here that could identify a child.
/// </summary>
public class AttendancePickupDisplayService(
    GsbcDbContext                    db,
    IDbContextFactory<GsbcDbContext> dbFactory,
    AttendanceDataChangeNotifier     changes
) : IAttendancePickupDisplayService
{
    /// <summary>
    /// How long a watcher sits waiting for a change before it looks anyway. Also the
    /// upper bound on how stale the wall can be if a change event is ever missed.
    /// </summary>
    private static readonly TimeSpan WatchTick = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Resend the list at least this often even when nothing has changed. A wall display
    /// behind a proxy has no other way to notice its stream has quietly died.
    /// </summary>
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(30);

    public async Task<PickupDisplayResponse> GetPickups(PickupDisplayRequest r, CallContext c = default)
        => await BuildPickupsAsync(db, r.ServiceId, c.CancellationToken);

    /// <summary>
    /// Pushes the list on every change instead of making the display ask. The first item is
    /// the current list, so a caller never needs a separate read to paint the screen.
    /// </summary>
    public async IAsyncEnumerable<PickupDisplayResponse> WatchPickups(
        PickupDisplayRequest r,
        CallContext          c = default
    )
    {
        CancellationToken token = c.CancellationToken;

        string?        lastSignature = null;
        DateTimeOffset lastSent      = DateTimeOffset.MinValue;

        while (!token.IsCancellationRequested)
        {
            // Claimed before the read, so a write that lands while we are reading still
            // wakes the wait below instead of sitting until the next tick.
            DataChangeSubscription pending = changes.Subscribe();

            // A fresh context per look: this call outlives any sane scoped lifetime.
            PickupDisplayResponse waiting = await dbFactory.RunWithNewDbContext(
                context => BuildPickupsAsync(context, r.ServiceId, token),
                token
            );

            string signature = Signature(waiting);

            if (signature != lastSignature || DateTimeOffset.UtcNow - lastSent >= KeepAlive)
            {
                lastSignature = signature;
                lastSent = DateTimeOffset.UtcNow;

                yield return waiting;
            }

            await pending.WaitAsync(WatchTick, token);
        }
    }

    /// <summary>
    /// Everything the display would actually render, flattened. Two lists with the same
    /// signature look identical on the wall, so there is no point sending the second. A new
    /// field the wall renders must be added here or the screen will never see it change.
    /// </summary>
    private static string Signature(PickupDisplayResponse response)
    {
        StringBuilder builder = new();

        builder.Append(response.Success).Append('|')
            .Append(response.Error).Append('|')
            .Append(response.ServiceTitle);

        foreach (PickupDisplayEntry entry in response.Waiting)
        {
            builder.Append('|')
                .Append(entry.Name).Append(':')
                .Append(entry.RequestedAt.Ticks);
        }

        return builder.ToString();
    }

    private static async Task<PickupDisplayResponse> BuildPickupsAsync(
        GsbcDbContext     db,
        Guid?             serviceId,
        CancellationToken token
    )
    {
        DbService? service = await ResolveServiceAsync(db, serviceId, token);

        if (service == null)
            return PickupDisplayResponse.WithError(ServiceNotFound);

        // Asked for, not yet gone, and not taken back. PickupRequested is never cleared on
        // sign out, so SignedOut is what takes a child off the wall.
        // Only the three columns the wall needs leave the database - no ids, no dates of
        // birth, no medical or allergy detail, no family.
        var rows = await db.AttendanceRecords
            .AsNoTracking()
            .Where(x => x.ServiceId == service.Id
                && x.PickupRequested != null
                && x.SignedOut == null
                && !x.Deleted
            )
            .OrderBy(x => x.PickupRequested)
            .Select(x => new
                {
                    x.Person!.FirstName,
                    x.Person.LastName,
                    Requested = x.PickupRequested!.Value
                }
            )
            .ToListAsync(token);

        List<PickupDisplayEntry> waiting = rows
            .Select(x => new PickupDisplayEntry
                {
                    Name = DisplayName(x.FirstName, x.LastName),
                    RequestedAt = x.Requested.UtcDateTime
                }
            )
            .ToList();

        return new PickupDisplayResponse
        {
            Success = true,
            ServiceTitle = service.Name,
            Waiting = waiting
        };
    }

    /// <summary>
    /// "Jonah P." - first name plus last initial, the most the wall is ever allowed to say.
    /// A person with no last name on file is just their first name; the wall never shows a
    /// stray dot.
    /// </summary>
    private static string DisplayName(string firstName, string lastName)
    {
        string initial = lastName.Trim();

        return initial.Length == 0
            ? firstName
            : $"{firstName} {initial[0]}.";
    }

    private static async Task<DbService?> ResolveServiceAsync(
        GsbcDbContext     db,
        Guid?             serviceId,
        CancellationToken token
    )
    {
        if (serviceId != null)
            return await db.Services
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == serviceId, token);

        // No id given, so the display is on a fixed URL - fall back to today, then to the
        // most recent service, matching how the attendance tool picks one. Local, not UTC:
        // the tool compares Service.LocalDate against DateTime.Today, and a Friday night
        // service here is already Saturday in UTC.
        DateTimeOffset todayStart = new(DateTime.Today, TimeZoneInfo.Local.GetUtcOffset(DateTime.Today));
        DateTimeOffset todayEnd   = todayStart.AddDays(1);

        DbService? today = await db.Services
            .AsNoTracking()
            .Where(x => x.Date >= todayStart && x.Date < todayEnd)
            .OrderBy(x => x.Date)
            .FirstOrDefaultAsync(token);

        return today ?? await db.Services
            .AsNoTracking()
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync(token);
    }
}
