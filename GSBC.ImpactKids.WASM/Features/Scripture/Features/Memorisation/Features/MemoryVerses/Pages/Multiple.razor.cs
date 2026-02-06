using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerseLists.Components;
using GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerseLists.Components.Individual;
using GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components;
using MudBlazor;
using CreateMemoryVerseDialog = GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components.Individual.CreateMemoryVerseDialog;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Pages;

public partial class Multiple
{
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MemoryVerseListsStore, UpdateFilteredMemoryVerseLists);

        await Task.WhenAll(
            MemoryVerseListsStore.RefreshAll()
        );
        UpdateFilteredMemoryVerseLists();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        UpdateFilteredMemoryVerseLists();
    }

    private async Task CreateMemoryVerseList() => await DetailsComponentDialog.Open<MemoryVerseListDetails>(
        DialogService,
        "Create Memory Verse List",
        ModificationState.Creating
    );

    private async Task CreateMemoryVerse()
    {
        IDialogReference reference = await DialogService.ShowAsync<CreateMemoryVerseDialog>(
            "Create Memory Verse",
            options:
            new DialogOptions
            {
                FullWidth = true
            }
        );
        await reference.Result;
    }

    private void UpdateFilteredMemoryVerseLists()
    {
        AsyncData<ImmutableList<MemoryVerseList>> memoryVerseLists = MemoryVerseListsStore.GetState().Entities;

        if (memoryVerseLists.Data == null)
        {
            Update(s => s with { FilteredMemoryVerseLists = s.FilteredMemoryVerseLists.CopyStatus(memoryVerseLists) });
            return;
        }

        Update(s => s with
        {
            FilteredMemoryVerseLists = s.FilteredMemoryVerseLists.ToSuccess(
                memoryVerseLists.Data
            )
        });
    }
}