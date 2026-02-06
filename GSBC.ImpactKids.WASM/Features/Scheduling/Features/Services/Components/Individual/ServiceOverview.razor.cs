using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceOverview
{
    [Parameter]
    public required Guid? Id { get; set; }

    private AsyncData<DollarStoreEntry> _dollarStoreEntry = AsyncData<DollarStoreEntry>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        DollarStoreEntriesStore.Subscribe(_ => RetrieveDollarStoreEntry());

        await Task.WhenAll(DollarStoreEntriesStore.RefreshAll());
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveDollarStoreEntry();
    }

    private void RetrieveDollarStoreEntry()
    {
        AsyncData<ImmutableList<DollarStoreEntry>> entries = DollarStoreEntriesStore.GetState().Entities;

        if (!entries.HasData)
        {
            _dollarStoreEntry = _dollarStoreEntry.CopyStatus(entries);
            StateHasChanged();
            return;
        }

        DollarStoreEntry? entry = entries.Data!
            .FirstOrDefault(x => x.ServiceId == Id);

        if (entry == null)
        {
            _dollarStoreEntry = _dollarStoreEntry.ToFailure("No Dollar Store Entry Found");
            StateHasChanged();
            return;
        }

        _dollarStoreEntry = _dollarStoreEntry.ToSuccess(entry);
        StateHasChanged();
    }
}