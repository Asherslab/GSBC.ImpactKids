using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class CreatePersonWorkflow
{
    [Parameter]
    public EventCallback<Guid?> OnExit { get; set; }

    [Parameter]
    public Guid? FamilyId { get; set; }

    private PersonDetails?    _personDetails;
    private ModificationState _state = ModificationState.Creating;
    private Guid?             _createdPersonId;
    private int               _index;

    private AsyncData<Person> _createdPerson = AsyncData<Person>.NotAsked();

    private readonly Dictionary<int, string> _alternativeButtonLabels = new()
    {
        { 0, "Create Person" }
    };

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(PeopleStore, RetrieveCreatedPerson);

        RetrieveCreatedPerson();

        await Task.WhenAll(
            PeopleStore.RefreshAll()
        );
    }

    private void RetrieveCreatedPerson()
    {
        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (people.Data == null)
        {
            _createdPerson = _createdPerson.CopyStatus(people);
            StateHasChanged();
            return;
        }

        if (_createdPersonId == null)
        {
            _createdPerson = _createdPerson.ToLoading();
            StateHasChanged();
            return;
        }

        Person? person = people.Data.FirstOrDefault(x => x.Id == _createdPersonId);

        if (person == null)
        {
            _createdPerson = _createdPerson.ToFailure("Failed to retrieve created Person!");
            return;
        }

        if (_index != 0 && person.FamilyGuardian && OnExit.HasDelegate)
            OnExit.InvokeAsync(_createdPersonId);

        _createdPerson = _createdPerson.ToSuccess(person);
        StateHasChanged();
    }

    private async Task PreviousStepAsync(MudStepper stepper)
    {
        if (_index == 0 && OnExit.HasDelegate)
        {
            await OnExit.InvokeAsync(_createdPersonId);
        }

        await stepper.PreviousStepAsync();
    }

    private async Task NextStepAsync(MudStepper stepper)
    {
        if (_index == 0 && _createdPersonId == null)
        {
            if (_personDetails == null)
                return;

            _createdPersonId = await _personDetails.CreateEntity();

            if (_createdPersonId == null)
                return;

            _state = ModificationState.Reading;
        }

        if (_index == 1 && OnExit.HasDelegate)
        {
            await OnExit.InvokeAsync(_createdPersonId);
        }

        await stepper.NextStepAsync();
    }
}