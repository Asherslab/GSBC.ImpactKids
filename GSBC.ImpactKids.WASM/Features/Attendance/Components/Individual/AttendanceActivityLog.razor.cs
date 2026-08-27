using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Components.Individual;

/// <summary>
/// What has happened at the desk this service, newest first. There is no log table: an
/// <see cref="AttendanceRecord" /> already carries up to three timestamped events, so the log is
/// built by fanning each record out into the events it holds and sorting them descending.
/// </summary>
public partial class AttendanceActivityLog
{
    /// <summary>The service the desk is currently working. Null while the page resolves it.</summary>
    [Parameter]
    public Guid? ServiceId { get; set; }

    /// <summary>Enough to answer "did I already do that one?" without scrolling forever.</summary>
    private const int Limit = 60;

    private AsyncData<ImmutableList<ActivityEntry>> _entries =
        AsyncData<ImmutableList<ActivityEntry>>.NotAsked();

    private Guid? _lastServiceId;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveEntries);
        HandleSubscriptionDisposal(PeopleStore, RetrieveEntries);
        HandleSubscriptionDisposal(UsersStore, RetrieveEntries);

        RetrieveEntries();

        await Task.WhenAll(
            AttendanceRecordsStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            UsersStore.RefreshAll()
        );
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (ServiceId == _lastServiceId)
            return;

        _lastServiceId = ServiceId;
        RetrieveEntries();
    }

    private void RetrieveEntries()
    {
        AsyncData<ImmutableList<AttendanceRecord>> records = AttendanceRecordsStore.GetState().Entities;
        AsyncData<ImmutableList<Person>>           people  = PeopleStore.GetState().Entities;

        // A record with no person to name is a row that reads "signed in by Sam" about nobody, so
        // both stores have to have landed before anything is drawn.
        if (records.Data == null)
        {
            _entries = _entries.CopyStatus(records);
            StateHasChanged();
            return;
        }

        if (people.Data == null)
        {
            _entries = _entries.CopyStatus(people);
            StateHasChanged();
            return;
        }

        if (ServiceId == null)
        {
            _entries = _entries.ToLoading();
            StateHasChanged();
            return;
        }

        Dictionary<Guid, string> personNames = people.Data
            .ToDictionary(x => x.Id, DisplayNameOf);

        // Users is decoration, not a gate: it is a separate authorized call, and a log that
        // withholds "signed out at 7:42" because it cannot name the leader is worse than one
        // that just leaves the actor off.
        Dictionary<Guid, string> userNames = UsersStore.GetState().Entities.Data?
            .ToDictionary(x => x.Id, x => x.Name) ?? [];

        ImmutableList<ActivityEntry> entries = records.Data
            .Where(x => !x.Deleted && x.ServiceId == ServiceId)
            .SelectMany(x => FanOut(x, personNames, userNames))
            .OrderByDescending(x => x.At)
            .Take(Limit)
            .ToImmutableList();

        _entries = _entries.ToSuccess(entries);
        StateHasChanged();
    }

    private static IEnumerable<ActivityEntry> FanOut(
        AttendanceRecord         record,
        Dictionary<Guid, string> personNames,
        Dictionary<Guid, string> userNames
    )
    {
        string name = personNames.GetValueOrDefault(record.PersonId, "Unknown");

        yield return Entry(record.LocalSignedIn, name, "signed in", "in",
            record.SignedInUserId, userNames);

        // Deliberately not gated on SignedOut: a request that was later fulfilled still happened,
        // and "signed out after being requested" is exactly what the log exists to show.
        if (record.LocalPickupRequested is { } requested)
            yield return Entry(requested, name, "requested", "requested",
                record.PickupRequestedUserId, userNames);

        if (record.LocalSignedOut is { } signedOut)
            yield return Entry(signedOut, name, "signed out", "out",
                record.SignedOutUserId, userNames);
    }

    private static ActivityEntry Entry(
        DateTime                 at,
        string                   personName,
        string                   action,
        string                   actionClass,
        Guid?                    actorId,
        Dictionary<Guid, string> userNames
    ) =>
        new()
        {
            At          = at,
            Time        = at.ToString("h:mmtt").ToLowerInvariant(),
            PersonName  = personName,
            Action      = action,
            ActionClass = actionClass,
            ActorName   = actorId != null && userNames.TryGetValue(actorId.Value, out string? actor)
                ? actor
                : null
        };

    /// <summary>First name plus last initial - the desk knows these children by first name.</summary>
    private static string DisplayNameOf(Person person) =>
        string.IsNullOrWhiteSpace(person.LastName)
            ? person.FirstName
            : $"{person.FirstName} {person.LastName[0]}.";

    private sealed record ActivityEntry
    {
        public required DateTime At          { get; init; }
        public required string   Time        { get; init; }
        public required string   PersonName  { get; init; }
        public required string   Action      { get; init; }
        public required string   ActionClass { get; init; }
        public required string?  ActorName   { get; init; }
    }
}
