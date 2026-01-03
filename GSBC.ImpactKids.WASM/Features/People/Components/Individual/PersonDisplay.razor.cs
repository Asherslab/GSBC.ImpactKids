using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
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
        _person = PeopleStore.GetState().First(x => x.Id == Id);

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