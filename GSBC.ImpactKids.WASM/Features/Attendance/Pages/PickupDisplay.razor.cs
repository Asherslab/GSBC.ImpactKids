using System.Collections.Immutable;
using System.Globalization;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

/// <summary>
/// Wall display of the children who have been asked for and have not yet gone.
/// <para>
/// <b>There is no pickup display service behind this.</b> The screen reads the ordinary
/// attendance, people and service stores - the same ones every signed in page reads - and
/// works out who is waiting here. That is the whole reason the server side of this feature
/// is three attributes rather than a service, a contract and a stream: a display is a
/// read-only caller on the real data, not a separate API with its own shape to keep in sync.
/// </para>
/// <para>
/// Live updates come from the same SSE stream every other page uses, so a press on the sign
/// out desk reaches the wall as fast as any other screen. Nothing here polls.
/// </para>
/// <para>
/// Takes no input of any kind - signage has nobody standing at it. Everything an operator
/// needs lives on <c>/Attendance/Tool</c>.
/// </para>
/// </summary>
public partial class PickupDisplay : IAsyncDisposable
{
    /// <summary>Which service to show. Absent on the fixed url, which then picks today's.</summary>
    [Parameter]
    public Guid? ServiceId { get; set; }

    [Inject]
    public required IRefreshableStore<AttendanceRecord> AttendanceStore { get; set; }

    [Inject]
    public required IRefreshableStore<Person> PeopleStore { get; set; }

    [Inject]
    public required IRefreshableStore<Service> ServicesStore { get; set; }

    private readonly List<IDisposable> _subscriptions = [];

    /// <summary>
    /// The screen is not enrolled, or was enrolled on a key that has since been rotated.
    /// <para>
    /// Distinguished from every other failure on purpose: this one has a remedy a person
    /// walking past can act on, and the others are waited out. A wall must be able to say "I
    /// need setting up again" rather than sitting on "Connecting..." until somebody notices.
    /// </para>
    /// </summary>
    private bool Unauthorised { get; set; }

    /// <summary>False until every store has answered once, so the screen can say "Connecting".</summary>
    private bool Loaded { get; set; }

    private string? ServiceTitle { get; set; }
    /// <summary>
    /// Renders the display as a layer rather than a screen: no background of its own, and
    /// nothing at all on it while nobody is waiting, so whatever is underneath shows through.
    /// <para>
    /// Built for ProPresenter, whose web element does composite a transparent page over the
    /// slide beneath it - confirmed on a real rig before this was written, because a browser
    /// page's alpha only survives if the host asks for it, and most hosts do not.
    /// </para>
    /// </summary>
    /// <summary>
    /// Taken as a string and interpreted here rather than bound straight to a bool. Blazor's
    /// bool binding <em>throws</em> on anything it cannot parse, so <c>?transparent=1</c> did
    /// not merely fail to switch the mode on - it took the whole page down, and this url gets
    /// typed into a ProPresenter web element by hand and then bookmarked. Anything present and
    /// not explicitly off means on, including a bare <c>?transparent</c>.
    /// </summary>
    [SupplyParameterFromQuery(Name = "transparent")]
    public string? TransparentParam { get; set; }

    private bool Transparent =>
        TransparentParam is not null
        && !string.Equals(TransparentParam, "false", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(TransparentParam, "0", StringComparison.Ordinal)
        && !string.Equals(TransparentParam, "no", StringComparison.OrdinalIgnoreCase);

    [Inject]
    public required IJSRuntime Js { get; set; }

    /// <summary>
    /// Whether &lt;html&gt; currently carries the host class. The two surfaces that have to stop
    /// painting - &lt;html&gt;, which MudBlazor paints, and DisplayLayout's .display-root - are both
    /// ancestors of this component, and Blazor's CSS isolation only ever stamps a component's
    /// own markup, so there is no way to reach them from this page's stylesheet.
    /// <para>
    /// Hence a class put onto the document element from here. The obvious alternative is
    /// <c>html:has(.pickup-transparent)</c>, which is in the stylesheet too and does the same
    /// job one frame earlier - but it sets a floor of Safari 15.4, and the host for this is a
    /// ProPresenter web element rather than a browser somebody keeps updated. :has() fails
    /// silently, so on an older WebKit &lt;html&gt; simply stays grey with no error anywhere to
    /// find. This path does not care what the engine supports.
    /// </para>
    /// </summary>
    private bool _hostClassApplied;

    private const string HostClass = "pickup-transparent-host";

    /// <summary>
    /// Past this, a child has been forgotten rather than fetched, and the wall says so with
    /// a visual - the room should chase them.
    /// </summary>
    private static readonly TimeSpan Overdue = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the waiting times are redrawn. The stream only pushes when the list
    /// changes, so without this a row's "3 mins" would sit there until somebody else was
    /// requested.
    /// </summary>
    private static readonly TimeSpan ClockTick = TimeSpan.FromSeconds(5);

    private IReadOnlyList<PickupRow> _rows = [];

    /// <summary>
    /// Every key currently on the wall. A key not in here when a list arrives is a name
    /// the room has not seen yet, which is the entrance animation's whole cue.
    /// </summary>
    private readonly HashSet<string> _onWall = [];

    /// <summary>
    /// Keys that arrived after the first list. Sticky for as long as the row is on the
    /// wall: a store push rebuilds the whole list, and recomputing "new" from the list alone
    /// would be fine, but a class that came and went would re-run the animation. Once a row has been marked as an arrival it stays marked, and
    /// because the row keeps its <c>@key</c> the browser runs the animation exactly once.
    /// </summary>
    private readonly HashSet<string> _arrived = [];

    /// <summary>
    /// False until the first list lands. The names already waiting when the screen boots
    /// are not arrivals - they get the quiet staggered fade the score board's rows get.
    /// </summary>
    private bool _seeded;

    private CancellationTokenSource? _clockTokenSource;

    /// <summary>One line on the wall: a child asked for, and the time they were asked for.</summary>
    private sealed record PickupRow(
        string   Key,
        string   Name,
        DateTime RequestedAt,
        int      Index,
        bool     IsNew
    );

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Every store the wall derives from. A change to any of them can move a name on or
        // off the screen, so all three rebuild the list - a sign out writes an attendance
        // record, but a name correction writes a person.
        _subscriptions.Add(AttendanceStore.Subscribe(_ => Rebuild()));
        _subscriptions.Add(PeopleStore.Subscribe(_ => Rebuild()));
        _subscriptions.Add(ServicesStore.Subscribe(_ => Rebuild()));

        StartClock();

        // The stores push from here on, driven by the SSE stream. These are the first reads,
        // and the only ones this page asks for by hand.
        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            AttendanceStore.RefreshAll()
        );

        Rebuild();
    }

    /// <summary>
    /// Works out the wall from the three stores. This is the logic that used to be a server
    /// side query in a display-only service: asked for, not yet gone, not deleted, for the
    /// service this screen is showing.
    /// </summary>
    private void Rebuild()
    {
        AsyncData<ImmutableList<AttendanceRecord>> records = AttendanceStore.GetState().Entities;
        AsyncData<ImmutableList<Person>>           people  = PeopleStore.GetState().Entities;
        AsyncData<ImmutableList<Service>>          services = ServicesStore.GetState().Entities;

        // One store saying "you are not enrolled" is the whole answer - the others will be
        // saying it too, or are about to.
        Unauthorised = IsNotEnrolled(records) || IsNotEnrolled(people) || IsNotEnrolled(services);

        if (Unauthorised)
        {
            StateHasChanged();
            return;
        }

        if (!records.HasData || !people.HasData || !services.HasData)
        {
            StateHasChanged();
            return;
        }

        Service? service = ResolveService(services.Data!);

        ServiceTitle = service?.GetDisplayName();
        Loaded = true;

        if (service == null)
        {
            _rows = [];
            _onWall.Clear();
            _arrived.Clear();

            StateHasChanged();
            return;
        }

        Dictionary<Guid, Person> byId = people.Data!.ToDictionary(x => x.Id);

        // Ordered longest wait first, which is the order the room should send children to
        // the door in.
        List<AttendanceRecord> waiting = records.Data!
            .Where(x => x.ServiceId == service.Id && x.AwaitingPickup && !x.Deleted)
            .OrderBy(x => x.PickupRequested)
            .ToList();

        List<PickupRow> rows = [];
        HashSet<string> keys = [];

        int index = 0;

        foreach (AttendanceRecord record in waiting)
        {
            if (!byId.TryGetValue(record.PersonId, out Person? person))
                continue;

            string name = DisplayName(person);

            // Name plus the instant they were asked for. A child requested, sent home and
            // requested again gets a different key, so the wall animates them in a second
            // time - which is exactly what happened.
            string key = $"{name}@{record.PickupRequested!.Value.Ticks}";

            keys.Add(key);

            if (_seeded && !_onWall.Contains(key))
                _arrived.Add(key);

            rows.Add(new PickupRow(key, name, record.PickupRequested!.Value, index++, _arrived.Contains(key)));
        }

        // Rows that have gone are forgotten, so a name coming back later is an arrival
        // again rather than a row that quietly reappears.
        _onWall.Clear();
        _onWall.UnionWith(keys);
        _arrived.IntersectWith(keys);

        _seeded = true;
        _rows = rows;

        StateHasChanged();
    }

    private static bool IsNotEnrolled<T>(AsyncData<T> data) =>
        data.HasError && data.Error == RefreshableStoreErrors.NotEnrolled;

    /// <summary>
    /// The service this screen is showing. An id in the url wins; otherwise today's, and
    /// failing that the most recent - the same fallback the attendance tool uses, and the
    /// same one the deleted display service used to do server side.
    /// <para>
    /// The day is the LOCAL one. A Friday night service here is already Saturday in UTC, so
    /// comparing anything but <see cref="Service.LocalDate"/> puts the wrong service on the
    /// wall for exactly the services this screen exists for.
    /// </para>
    /// </summary>
    private Service? ResolveService(IReadOnlyList<Service> services)
    {
        if (ServiceId != null)
            return services.FirstOrDefault(x => x.Id == ServiceId);

        DateTime today = DateTime.Today;

        return services
                   .Where(x => x.LocalDate.Date == today)
                   .OrderBy(x => x.LocalDate)
                   .FirstOrDefault()
               ?? services
                   .OrderByDescending(x => x.LocalDate)
                   .FirstOrDefault();
    }

    /// <summary>
    /// "Jonah Parry" - the full name. A person with no last name on file is just their first
    /// name; the wall never shows a trailing space.
    /// <para>
    /// This was first name plus last initial. Widening it was a deliberate call on 28 Aug
    /// 2026 - see the display contract in
    /// <c>docs/work/2026-08-pickup-requests-and-activity-log.md</c>.
    /// </para>
    /// </summary>
    private static string DisplayName(Person person)
    {
        string first = person.FirstName.Trim();
        string last  = person.LastName.Trim();

        return last.Length == 0
            ? first
            : $"{first} {last}";
    }

    /// <summary>
    /// Re-checked on every render rather than only the first: this is a routable page and
    /// Blazor reuses the component across a navigation between /Display/Pickup and the same
    /// url with ?transparent=true, so a first-render-only hook would leave the class stuck
    /// however it started.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (Transparent != _hostClassApplied)
            await SetHostClass(Transparent);
    }

    private async Task SetHostClass(bool on)
    {
        try
        {
            await Js.InvokeVoidAsync(
                on ? "document.documentElement.classList.add"
                   : "document.documentElement.classList.remove",
                HostClass);

            _hostClassApplied = on;
        }
        catch (JSException)
        {
            // Nothing to recover: without the class the page renders on its normal opaque
            // background, which is wrong for an overlay but is not a broken screen.
        }
    }

    /// <summary>
    /// Redraws the waiting times between pushes. Nothing else on this page changes on a
    /// tick, and the rows keep their keys, so no animation restarts.
    /// </summary>
    private void StartClock()
    {
        _clockTokenSource?.Cancel();
        _clockTokenSource = new CancellationTokenSource();
        CancellationToken token = _clockTokenSource.Token;

        _ = Task.Run(async () =>
            {
                try
                {
                    using PeriodicTimer timer = new(ClockTick);

                    while (await timer.WaitForNextTickAsync(token))
                        await InvokeAsync(StateHasChanged);
                }
                catch (OperationCanceledException)
                {
                    // The screen went away.
                }
            }, token
        );
    }

    /// <summary>
    /// Type size relative to the five name wall everything was designed around, so a busy
    /// end of night still fits on one screen instead of scrolling off it.
    /// </summary>
    private string RowScale =>
        Math.Clamp(5d / Math.Max(_rows.Count, 1), .32, 1).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary><c>RequestedAt</c> crosses the wire in UTC, so the clock it is measured against is too.</summary>
    private static TimeSpan Waited(PickupRow row)
    {
        TimeSpan waited = DateTime.UtcNow - DateTime.SpecifyKind(row.RequestedAt, DateTimeKind.Utc);

        return waited < TimeSpan.Zero ? TimeSpan.Zero : waited;
    }

    /// <summary>Whole minutes only - a wall is read at a glance, and seconds ticking on it are noise.</summary>
    private static string WaitText(PickupRow row)
    {
        int minutes = (int)Waited(row).TotalMinutes;

        return minutes switch
        {
            < 1 => "just now",
            1   => "1 min",
            _   => $"{minutes} mins"
        };
    }

    private static bool IsOverdue(PickupRow row) => Waited(row) >= Overdue;

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();

        // The class lives on <html>, which outlives this component - left behind it would
        // strip the background off every page navigated to next.
        if (_hostClassApplied)
            await SetHostClass(false);

        if (_clockTokenSource is not null)
        {
            await _clockTokenSource.CancelAsync();
            _clockTokenSource.Dispose();
            _clockTokenSource = null;
        }
    }
}
