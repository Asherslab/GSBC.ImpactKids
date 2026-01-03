using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Features.People.Components.Individual;

public partial class PersonDetails
{
    private AsyncData<ImmutableList<FamilyDefinition>>
        _families = AsyncData<ImmutableList<FamilyDefinition>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        RetrieveFamilies();
        HandleSubscriptionDisposal(EntityStore, _ => RetrieveFamilies());
        HandleSubscriptionDisposal(SchoolGradesStore, _ => StateHasChanged());
        HandleStateChangeSubscriptionDisposal(SchoolGradesStore);

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            SchoolGradesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        RetrieveFamilies();
    }

    private void RetrieveFamilies()
    {
        AsyncData<ImmutableList<Person>> people = EntityStore.GetState().Entities;

        if (people.Data == null)
        {
            _families = _families.CopyStatus(people);
            StateHasChanged();
            return;
        }

        ImmutableList<FamilyDefinition> familyDefinitions = people.Data
            .GroupBy(x => x.FamilyId)
            .Select(x =>
                new FamilyDefinition(x.Key,
                    x.GroupBy(y => y.LastName)
                        .MaxBy(y => y.Count())!
                        .Key, // gets the most common last name
                    x.Count()
                )
            )
            .OrderBy(x => x.FamilyName)
            .ThenBy(x => x.FamilyCount)
            .ToImmutableList();

        _families = _families.ToSuccess(familyDefinitions);
        StateHasChanged();
    }
}

public record FamilyDefinition(
    Guid   Id,
    string FamilyName,
    int    FamilyCount
);