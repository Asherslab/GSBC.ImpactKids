using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDisplay : ComponentBase
{
    [Parameter]
    public Guid? Id { get; set; }

    private AsyncData<Person> _person = AsyncData<Person>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        PeopleStore.Subscribe(_ => RetrievePerson());

        await Task.WhenAll(
            PeopleStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrievePerson();
    }

    private string? _avatarDisplay;
    private Color   _avatarColor = Color.Default;
    private string  _displayText = "Person";

    private void RetrievePerson()
    {
        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (!people.HasData)
        {
            _person = _person.CopyStatus(people);
            StateHasChanged();
            return;
        }

        Person? person = people.Data!
            .FirstOrDefault(x => x.Id == Id);

        _person = person == null
            ? _person.ToFailure("Failed to find Person")
            : _person.ToSuccess(person);

        _avatarDisplay = _person.Data?.FirstName[0].ToString() ?? "N";

        _displayText = _person.Data == null
            ? "Person"
            : $"{_person.Data.FirstName} {_person.Data.LastName}";

        _avatarColor = _person.Data == null
            ? Color.Default
            : _person.Data.FamilyGuardian
                ? Color.Secondary
                : Color.Primary;

        StateHasChanged();
    }
}