using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Attendance.Features.AttendanceItemRecords.Components.Individual;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

public partial class SignIn
{
    [Parameter]
    public Guid Id { get; set; }

    [SupplyParameterFromQuery]
    public Guid? ServiceId { get; set; }

    private int  _index;
    private bool _errorsInPerson;

    private readonly Dictionary<int, string> _alternativeButtonLabels = new()
    {
        { 1, "Sign In" }
    };

    private AsyncData<Service> _service                = AsyncData<Service>.NotAsked();
    private AsyncData<Person>  _person                 = AsyncData<Person>.NotAsked();
    private AsyncData<Guid?>   _attendanceRecordId = AsyncData<Guid?>.NotAsked();

    private readonly BreadcrumbItem[] _breadcrumbs =
    [
        new("Attendance", href: "/Attendance/Tool"),
        new("Family", href: null, disabled: true),
        new("Person", href: null, disabled: true)
    ];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(ServicesStore, RetrieveService);
        HandleSubscriptionDisposal(PeopleStore, RetrievePerson);
        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveAttendanceRecord);
        HandleStateChangeSubscriptionDisposal(AttendanceItemTypesStore);
        HandleStateChangeSubscriptionDisposal(AttendanceItemRecordsStore);

        RetrieveService();
        RetrievePerson();

        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            AttendanceRecordsStore.RefreshAll(),
            AttendanceItemTypesStore.RefreshAll(),
            AttendanceItemRecordsStore.RefreshAll()
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
        {
            _breadcrumbs[0] = new BreadcrumbItem("Attendance",
                href: $"/Attendance/Tool?{nameof(Family.ServiceId)}={_service.Data?.Id}");
            _breadcrumbs[1] = new BreadcrumbItem(_breadcrumbs[1].Text,
                href: $"/Attendance/Family/{Id}?{nameof(Family.ServiceId)}={_service.Data?.Id}");
        }

        StateHasChanged();
    }

    private void RetrievePerson()
    {
        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (people.Data == null)
        {
            _person = _person.CopyStatus(people);
            StateHasChanged();
            return;
        }

        Person? person = people.Data.FirstOrDefault(x => x.Id == Id);

        if (person == null)
        {
            _person = _person.ToFailure("Failed to retrieve Person!");
            return;
        }

        _person = _person.ToSuccess(person);

        string familyName = people.Data
            .Where(x => x.FamilyId == person.FamilyId)
            .GroupBy(y => y.LastName)
            .MaxBy(y => y.Count())!
            .Key;

        _breadcrumbs[1] = new BreadcrumbItem(familyName,
            href: $"/Attendance/Family/{Id}?{nameof(Family.ServiceId)}={_service.Data?.Id}");
        _breadcrumbs[2] = new BreadcrumbItem($"{person.FirstName} {person.LastName}", href: null, disabled: true);

        RetrieveAttendanceRecord();

        StateHasChanged();
    }

    private void RetrieveAttendanceRecord()
    {
        AsyncData<ImmutableList<AttendanceRecord>> attendanceRecords = AttendanceRecordsStore.GetState().Entities;

        if (attendanceRecords.Data == null)
        {
            _attendanceRecordId = _attendanceRecordId.CopyStatus(attendanceRecords);
            StateHasChanged();
            return;
        }

        if (_person.Data == null)
        {
            _attendanceRecordId = _attendanceRecordId.CopyStatus(_person);
            StateHasChanged();
            return;
        }

        AttendanceRecord? record = attendanceRecords.Data.FirstOrDefault(x =>
            x.ServiceId == ServiceId &&
            x.PersonId == _person.Data.Id &&
            x.SignedOut == null &&
            !x.Deleted
        );
        _attendanceRecordId = _attendanceRecordId.ToSuccess(record?.Id);

        StateHasChanged();
    }

    private async Task PreviousStepAsync(MudStepper stepper)
    {
        string? url = _breadcrumbs[1].Href;
        if (_index == 0 && url != null) // firstPage
        {
            Navigation.NavigateTo(url);
        }
        
        await stepper.PreviousStepAsync();
    }

    private async Task NextStepAsync(MudStepper stepper)
    {
        if (_index == 1 && _attendanceRecordId.Data == null)
        {
            if (_person.Data == null)
            {
                Snackbar.Add("Person data unavailable. Cannot sign in!", Severity.Error);
                return;
            }
            
            if (_service.Data == null)
            {
                Snackbar.Add("Service data unavailable. Cannot sign in!", Severity.Error);
                return;
            }
            
            BasicReadResponse<Guid?> response = await AttendanceRecordsService.Create(new SignInAttendanceRecordRequest
            {  
                PersonId = _person.Data.Id,
                ServiceId = _service.Data.Id
            });

            if (response.HasErrorOrNull())
            {
                Snackbar.AddErrorResponse(response);
                return;
            }
        }

        string? url = _breadcrumbs[1].Href;
        if (_index == 2 && url != null) // lastPage
        {
            Navigation.NavigateTo(url);
        }
        
        await stepper.NextStepAsync();
    }

    private async Task DeleteAttendanceRecord()
    {
        if (_attendanceRecordId.Data == null)
            return;

        bool? result = await DialogService.ShowMessageBoxAsync(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        BasicReadRequest request = new() { Guid = _attendanceRecordId.Data.Value };
        BasicResponse    resp    = await AttendanceRecordsService.BasicDelete(request);

        if (!resp.HasErrorOrNull())
            return;

        Snackbar.AddErrorResponse(resp);
    }
    
    private async Task CreateAttendanceItemRecord(Guid? itemTypeId) =>
        await DetailsComponentDialog.Open<AttendanceItemRecordDetails>(
            DialogService,
            "Create Item Record",
            ModificationState.Creating,
            extraParameters: new Dictionary<string, object?>
            {
                {nameof(AttendanceItemRecordDetails.AttendanceRecordId), _attendanceRecordId.Data},
                {nameof(AttendanceItemRecordDetails.AttendanceItemTypeId), (Guid?) itemTypeId}
            }
        );
    
    private async Task UpdateAttendanceItemRecord(Guid? recordId) =>
        await DetailsComponentDialog.Open<AttendanceItemRecordDetails>(
            DialogService,
            "Update Item Record",
            ModificationState.Updating,
            recordId
        );

    private bool _firstError = true;
    private void PersonDetailsErrorsChanged(bool errors)
    {
        _errorsInPerson = errors;
        if (!_firstError)
            return;
        
        if (!errors)
            _index = 1;
        
        _firstError = false;
    }
}