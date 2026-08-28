using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

public partial class Family
{
    [Parameter]
    public Guid Id { get; set; }

    [SupplyParameterFromQuery]
    public Guid? ServiceId { get; set; }

    private AsyncData<Service> _service    = AsyncData<Service>.NotAsked();
    private AsyncData<Person>  _person     = AsyncData<Person>.NotAsked();
    private AsyncData<string>  _familyName = AsyncData<string>.NotAsked();

    /// <summary>
    /// The latest record this service for each person, signed out or not. It carries the whole
    /// record rather than just its id because the pickup button reads three states off it, and
    /// "signed out" is one of them - a person with a finished record still has something to show.
    /// </summary>
    private AsyncData<Dictionary<Guid, AttendanceRecord>> _peopleAttendance =
        AsyncData<Dictionary<Guid, AttendanceRecord>>.NotAsked();

    /// <summary>This person plus anyone in their household - what the batch actions act on.</summary>
    private ImmutableList<Person> _members = [];

    private readonly BreadcrumbItem[] _breadcrumbs =
    [
        new("Attendance", href: "/Attendance/Tool"),
        new("Family", href: null, disabled: true)
    ];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(ServicesStore, RetrieveService);
        HandleSubscriptionDisposal(PeopleStore, RetrieveFamily);
        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveAttendanceRecords);

        RetrieveService();
        RetrieveFamily();

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
            service = services.Data!
                .FirstOrDefault(x => x.Id == ServiceId);
        }
        else
        {
            service = services.Data!
                .FirstOrDefault(x => x.LocalDate.Date == DateTime.Today);

            // Same fallback as Tool.razor.cs. Without it, this page reached without a
            // ServiceId - a bookmark, a back button, a night running past midnight - found
            // no service, so _peopleAttendance copied the failure and the family rendered
            // with no sign in and no sign out buttons at all. This is the page where a
            // child gets signed out, so that left no way to do it.
            service ??= services.Data!
                .OrderByDescending(x => x.LocalDate.Date)
                .FirstOrDefault();
        }

        // No ServiceId means we looked today up by date, so say so. These two were the
        // wrong way round.
        _service = service != null
            ? _service.ToSuccess(service)
            : ServiceId == null
                ? _service.ToFailure("Failed to find Service for Today")
                : _service.ToFailure("Failed to find Service for Id");

        if (service != null)
            _breadcrumbs[0] = new BreadcrumbItem("Attendance",
                href: $"/Attendance/Tool?{nameof(Tool.ServiceId)}={service.Id}");

        RetrieveAttendanceRecords();
        StateHasChanged();
    }

    private void RetrieveFamily()
    {
        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (people.Data == null)
        {
            _person = _person.CopyStatus(people);
            _familyName = _familyName.CopyStatus(people);
            StateHasChanged();
            return;
        }

        Person? person = people.Data.FirstOrDefault(x => x.Id == Id);

        if (person == null)
        {
            _person = _person.ToFailure("Failed to retrieve Person!");
            _familyName = _familyName.ToFailure("Failed to retrieve Person!");
            return;
        }

        string familyName = Person.FamilyNameOf(person, people.Data);

        _person = _person.ToSuccess(person);
        _familyName = _familyName.ToSuccess(familyName);

        // The same set the list below renders, computed once so the batch bar and the rows
        // can never disagree about who is in this household.
        _members = people.Data
            .Where(x => x.Id == person.Id || x.SharesFamilyWith(person))
            .ToImmutableList();

        _breadcrumbs[1] = new BreadcrumbItem(familyName, href: null, disabled: true);

        RetrieveAttendanceRecords();
        StateHasChanged();
    }

    private void RetrieveAttendanceRecords()
    {
        AsyncData<ImmutableList<AttendanceRecord>> attendanceRecords = AttendanceRecordsStore.GetState().Entities;

        if (attendanceRecords.Data == null)
        {
            _peopleAttendance = _peopleAttendance.CopyStatus(attendanceRecords);
            StateHasChanged();
            return;
        }

        if (_service.Data == null)
        {
            _peopleAttendance = _peopleAttendance.CopyStatus(_service);
            StateHasChanged();
            return;
        }

        Dictionary<Guid, AttendanceRecord> records = attendanceRecords.Data
            .Where(x => !x.Deleted)
            .Where(x => x.ServiceId == _service.Data.Id)
            .GroupBy(x => x.PersonId)
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.SignedIn)!);

        _peopleAttendance = _peopleAttendance.ToSuccess(records);
        StateHasChanged();
    }

    /// <summary>
    /// The one lookup the list rows do. Callers still test <c>SignedOut</c> themselves - this
    /// deliberately does not hide a finished record, because the row has to say so.
    /// </summary>
    private AttendanceRecord? RecordFor(Guid? personId)
    {
        if (personId == null || _peopleAttendance.Data == null)
            return null;

        return _peopleAttendance.Data.GetValueOrDefault(personId.Value);
    }
}