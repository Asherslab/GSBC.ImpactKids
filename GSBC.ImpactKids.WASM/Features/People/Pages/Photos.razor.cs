using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

/// <summary>
/// The Photos tool: a leader, on a phone, during a service, working through the children in front
/// of them.
///
/// <para>
/// A person appears when they are signed in for tonight <b>and</b> either have no photo or have
/// been flagged as needing a new one. Taking the photo clears the flag, which drops them off this
/// list — that disappearance is the tool's only progress indicator, and it is enough.
/// </para>
/// <para>
/// Scoped to tonight the same way <c>Attendance/Tool</c> is — an explicit <c>ServiceId</c>, else
/// today's service by date, else the most recent — and the "signed in" test is
/// <see cref="AttendanceRecord.IsSignedIn"/>, the same predicate the Attendance tool's filter uses
/// rather than a second copy of it.
/// </para>
/// </summary>
public partial class Photos : ComponentBase, IDisposable
{
    /// <summary>
    /// Subscriptions held so they can be dropped when the page goes away.
    ///
    /// Deliberately not <c>StoreEntityUtilityComponent&lt;T&gt;</c>, which the Attendance tool uses:
    /// its type parameter is a page-STATE type with its own registered store, not an entity, and
    /// pointing it at <c>Person</c> makes it resolve a store that does not exist - which fails at
    /// render as "An unhandled error has occurred" rather than at build. This page has no page state
    /// worth storing, so it just keeps its own subscriptions.
    /// </summary>
    private readonly List<IDisposable> _subscriptions = [];

    [SupplyParameterFromQuery]
    public Guid? ServiceId { get; set; }

    private readonly BreadcrumbItem[] _breadcrumbs =
    [
        new("Photos", href: null, disabled: true)
    ];

    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();

    private ImmutableList<AttendanceRecord> _attendanceRecords = [];

    private List<Person> _needingPhotos = [];

    /// <summary>
    /// Whether both stores have actually answered.
    ///
    /// The empty state says "Every child signed in has a current photo", which is a definite claim,
    /// and while the stores are still loading the list is empty for a completely different reason.
    /// Rendering the good news during that window told a leader the job was done before the page had
    /// any idea - and this is the same trap the Attendance tool's filters document: null Data means
    /// both "loading" and "failed", so it must never be read as "none".
    /// </summary>
    private bool _loaded;

    private Person? _capturing;

    protected override async Task OnInitializedAsync()
    {
        _subscriptions.Add(ServicesStore.Subscribe(_ => RetrieveService()));
        _subscriptions.Add(PeopleStore.Subscribe(_ => Recalculate()));
        _subscriptions.Add(AttendanceRecordsStore.Subscribe(_ => Recalculate()));

        RetrieveService();

        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            AttendanceRecordsStore.RefreshAll()
        );
    }

    private void RetrieveService()
    {
        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (!services.HasData)
        {
            _service = _service.CopyStatus(services);
            StateHasChanged();
            return;
        }

        Service? service;

        if (ServiceId != null)
        {
            service = services.Data!.FirstOrDefault(x => x.Id == ServiceId);
        }
        else
        {
            service = services.Data!.FirstOrDefault(x => x.LocalDate.Date == DateTime.Today);
            service ??= services.Data!.OrderByDescending(x => x.LocalDate.Date).FirstOrDefault();
        }

        _service = service != null
            ? _service.ToSuccess(service)
            : ServiceId == null
                ? _service.ToFailure("Failed to find Service for Today")
                : _service.ToFailure("Failed to find Service for Id");

        Recalculate();
    }

    private void Recalculate()
    {
        ImmutableList<AttendanceRecord>? records = AttendanceRecordsStore.GetState().Entities.Data;
        ImmutableList<Person>?           people  = PeopleStore.GetState().Entities.Data;

        _attendanceRecords = records is null || _service.Data is null
            ? []
            : records
                .Where(x => !x.Deleted && x.ServiceId == _service.Data.Id)
                .ToImmutableList();

        _loaded = people is not null && records is not null;

        _needingPhotos = people is null
            ? []
            : people
                .Where(x => AttendanceRecord.IsSignedIn(_attendanceRecords, x.Id))
                .Where(x => x.PhotoVersion == null || x.PhotoNeedsUpdate)
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .ToList();

        StateHasChanged();
    }

    private void OpenCapture(Person person) => _capturing = person;

    private void CloseCapture() => _capturing = null;

    /// <summary>
    /// The upload already raised the data event, so the person store will refresh itself and the
    /// child will drop off the list on its own. This only closes the view — re-fetching here would
    /// race that refresh for no gain.
    /// </summary>
    private void OnPhotoSaved(string photoVersion) => _capturing = null;

    public void Dispose()
    {
        foreach (IDisposable subscription in _subscriptions)
            subscription.Dispose();

        GC.SuppressFinalize(this);
    }
}
