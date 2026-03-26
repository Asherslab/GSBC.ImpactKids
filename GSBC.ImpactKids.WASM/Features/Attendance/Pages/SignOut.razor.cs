using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance.AttendanceRecords;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Attendance.Features.AttendanceItemRecords.Components.Individual;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

public partial class SignOut
{
    [Parameter]
    public Guid Id { get; set; }
    //
    // [SupplyParameterFromQuery]
    // public Guid? ServiceId { get; set; }

    [SupplyParameterFromQuery]
    public Guid? AttendanceRecordId { get; set; }

    private int  _index;
    private bool _errorsInPerson;

    private readonly Dictionary<int, string> _alternativeButtonLabels = new()
    {
        { 1, "Sign Out" }
    };

    private AsyncData<Person>            _person           = AsyncData<Person>.NotAsked();
    private AsyncData<AttendanceRecord?> _attendanceRecord = AsyncData<AttendanceRecord?>.NotAsked();

    private readonly BreadcrumbItem[] _breadcrumbs =
    [
        new("Attendance", href: "/Attendance/Tool"),
        new("Family", href: null, disabled: true),
        new("Person", href: null, disabled: true)
    ];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(PeopleStore, RetrievePerson);
        HandleSubscriptionDisposal(AttendanceRecordsStore, RetrieveAttendanceRecord);
        HandleStateChangeSubscriptionDisposal(AttendanceItemTypesStore);
        HandleStateChangeSubscriptionDisposal(AttendanceItemRecordsStore);

        RetrievePerson();

        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            AttendanceRecordsStore.RefreshAll(),
            AttendanceItemTypesStore.RefreshAll(),
            AttendanceItemRecordsStore.RefreshAll()
        );
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
            href: $"/Attendance/Family/{Id}?{nameof(Family.ServiceId)}={_attendanceRecord.Data?.ServiceId}");
        _breadcrumbs[2] = new BreadcrumbItem($"{person.FirstName} {person.LastName}", href: null, disabled: true);

        RetrieveAttendanceRecord();

        StateHasChanged();
    }

    private void RetrieveAttendanceRecord()
    {
        AsyncData<ImmutableList<AttendanceRecord>> attendanceRecords = AttendanceRecordsStore.GetState().Entities;

        if (attendanceRecords.Data == null)
        {
            _attendanceRecord = _attendanceRecord.CopyStatus(attendanceRecords);
            StateHasChanged();
            return;
        }

        if (_person.Data == null)
        {
            _attendanceRecord = _attendanceRecord.CopyStatus(_person);
            StateHasChanged();
            return;
        }

        AttendanceRecord? record = attendanceRecords.Data.FirstOrDefault(x => x.Id == AttendanceRecordId);

        if (record == null)
        {
            _attendanceRecord = _attendanceRecord.ToFailure("Failed to find Attendance Record for Id");
            return;
        }


        _breadcrumbs[0] = new BreadcrumbItem("Attendance",
            href: $"/Attendance/Tool?{nameof(Family.ServiceId)}={record.ServiceId}");
        _breadcrumbs[1] = new BreadcrumbItem(_breadcrumbs[1].Text,
            href: $"/Attendance/Family/{Id}?{nameof(Family.ServiceId)}={record.ServiceId}");

        _attendanceRecord = _attendanceRecord.ToSuccess(record);

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
        if (_index == 1 && _attendanceRecord.Data is { LocalSignedOut: null })
        {
            if (_attendanceRecord.Data == null)
            {
                Snackbar.Add("Attendance data unavailable. Cannot sign out!", Severity.Error);
                return;
            }

            BasicResponse response = await AttendanceRecordsService.Update(new SignOutAttendanceRecordRequest
            {
                Guid = _attendanceRecord.Data.Id
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