using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People.Allergies;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.People.Features.Allergies.Components.Multiple;

public partial class AllergiesList : ComponentBase
{
    [Parameter]
    public Func<Allergy, bool>? Filter { get; set; }

    [Parameter]
    public Guid? PersonId { get; set; }
    
    private AsyncData<ImmutableList<Guid>> _allergyIds = AsyncData<ImmutableList<Guid>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        AllergiesStore.Subscribe(_ => FilterAllergies());

        await Task.WhenAll(
            AllergiesStore.RefreshAll()
        );
        FilterAllergies();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        FilterAllergies();
    }

    private void FilterAllergies()
    {
        AsyncData<ImmutableList<Allergy>> allergies = AllergiesStore.GetState().Entities;

        if (allergies.Data == null)
        {
            _allergyIds = _allergyIds.CopyStatus(allergies);
            return;
        }

        IEnumerable<Allergy> filteredAllergies = allergies.Data;

        if (Filter != null)
        {
            filteredAllergies = filteredAllergies
                .Where(Filter);
        }

        if (PersonId != null)
        {
            filteredAllergies = filteredAllergies
                .Where(x =>
                    x.PersonId == PersonId
                );
        }

        _allergyIds = _allergyIds.ToSuccess(filteredAllergies
            .Select(x => x.Id)
            .ToImmutableList()
        );

        StateHasChanged();
    }
}