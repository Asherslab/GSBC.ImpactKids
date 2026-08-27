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

    private AsyncData<Service>                _service          = AsyncData<Service>.NotAsked();
    private AsyncData<Person>                 _person           = AsyncData<Person>.NotAsked();
    private AsyncData<string>                 _familyName       = AsyncData<string>.NotAsked();
    private AsyncData<Dictionary<Guid, Guid>> _peopleAttendance = AsyncData<Dictionary<Guid, Guid>>.NotAsked();

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
        }

        _service = service != null
            ? _service.ToSuccess(service)
            : ServiceId == null
                ? _service.ToFailure("Failed to find Service for Id")
                : _service.ToFailure("Failed to find Service for Today");

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

        Dictionary<Guid, Guid> records = attendanceRecords.Data
            .Where(x => x is { Deleted: false, LocalSignedOut: null })
            .Where(x => x.ServiceId == _service.Data.Id)
            .GroupBy(x => x.PersonId)
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.SignedIn)!.Id);

        _peopleAttendance = _peopleAttendance.ToSuccess(records);
        StateHasChanged();
    }
}