using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDetails : ComponentBase
{
    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    private          AsyncData<Person>   _person        = AsyncData<Person>.NotAsked();
    private readonly CreatePersonRequest _createRequest = new();
    private          UpdatePersonRequest _updateRequest = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        PeopleStore.Subscribe(_ => RetrievePerson());
        SchoolGradesStore.Subscribe(_ => StateHasChanged());

        RetrievePerson();
        await Task.WhenAll(
            PeopleStore.RefreshAll(),
            SchoolGradesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrievePerson();
    }

    private void RetrievePerson()
    {
        if (State == ModificationState.Creating)
            return;

        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (!people.HasData)
        {
            _person = _person.CopyStatus(people);
            StateHasChanged();
            return;
        }

        Person? person = people.Data!
            .FirstOrDefault(x => x.Id == Id);

        if (person == null)
        {
            _person = _person.ToFailure("Failed to find Person");
            _updateRequest = new UpdatePersonRequest();
            StateHasChanged();
            return;
        }

        _person = _person.ToSuccess(person);

        _updateRequest = new UpdatePersonRequest
        {
            Guid = person.Id,
        };

        _updateRequest.FirstName.SetInitialValue(person.FirstName);
        _updateRequest.LastName.SetInitialValue(person.LastName);

        _updateRequest.SchoolGradeId.SetInitialValue(person.SchoolGrade?.Id);
        _updateRequest.MediaConsent.SetInitialValue(person.MediaConsent);
        _updateRequest.LocalDateOfBirth.SetInitialValue(person.LocalDateOfBirth);
        _updateRequest.LocalFirstTime.SetInitialValue(person.LocalFirstTime);

        StateHasChanged();
    }

    public async Task<bool> CreatePerson()
    {
        _person = _person.ToLoading();
        StateHasChanged();
        BasicResponse resp = await PersonService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            RetrievePerson();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdatePerson()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _person = _person.ToLoading();
        StateHasChanged();
        BasicResponse resp = await PersonService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            RetrievePerson();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task DeletePerson()
    {
        if (_person.Data == null)
            return;
        Guid id = _person.Data.Id;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        _person = _person.ToLoading();
        StateHasChanged();
        BasicReadRequest request = new() { Guid = id };
        BasicResponse    resp    = await PersonService.Delete(request);

        if (!resp.HasErrorOrNull())
            return;

        RetrievePerson();
        Snackbar.AddErrorResponse(resp);
    }
}