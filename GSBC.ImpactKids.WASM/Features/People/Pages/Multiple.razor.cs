using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.People.Components.Individual;

namespace GSBC.ImpactKids.WASM.Features.People.Pages;

public partial class Multiple
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SubscribeToSelector(s => s.Search, _ => UpdateFilteredPeople());
        PeopleStore.Subscribe(_ => UpdateFilteredPeople());

        await Task.WhenAll(
            PeopleStore.RefreshAll(),
            UpdateFilteredPeople()
        );
    }

    private Task UpdateFilteredPeople()
    {
        AsyncData<ImmutableList<Person>> people = PeopleStore.GetState().Entities;

        if (!people.HasData)
            return Update(s => s with { FilteredPeople = people });

        string[]? searchStrings = State.Search?.Split(" ");
        return Update(s => s with
        {
            FilteredPeople = s.FilteredPeople.ToSuccess(
                people.Data!
                    .Where(x =>
                        searchStrings?.All(y =>
                            x.FirstName.Contains(y, StringComparison.InvariantCultureIgnoreCase) ||
                            x.LastName.Contains(y, StringComparison.InvariantCultureIgnoreCase)
                        ) ?? true
                    )
                    .Take(10)
                    .ToImmutableList()
            )
        });
    }

    private async Task OnSearch(string text)
    {
        await UpdateDebounced(s =>
            {
                string? nullableText = text;
                if (string.IsNullOrWhiteSpace(nullableText))
                    nullableText = null;
                return s.SetSearch(nullableText);
            },
            TimeSpan.FromSeconds(0.25).Milliseconds
        );
    }

    private async Task SyncElvantoPeople()
    {
        await PersonService.SyncWithElvanto();
    }

    private async Task CreatePerson() =>
        await DetailsComponentDialog.Open<PersonDetails>(DialogService, "Create Person", ModificationState.Creating);
}