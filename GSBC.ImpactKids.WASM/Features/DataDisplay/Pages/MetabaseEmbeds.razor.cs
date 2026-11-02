using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.DataDisplay.Components;

namespace GSBC.ImpactKids.WASM.Features.DataDisplay.Pages;

public partial class MetabaseEmbeds
{
    private AsyncData<ImmutableList<MetabaseEmbed>> _embeds = AsyncData<ImmutableList<MetabaseEmbed>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MetabaseEmbedsStore, RetrieveEmbeds);
        RetrieveEmbeds();

        await Task.WhenAll(MetabaseEmbedsStore.RefreshAll());
    }

    private void RetrieveEmbeds()
    {
        _embeds = MetabaseEmbedsStore.GetState().Entities;
        StateHasChanged();
    }

    public static string AnalyticsHref(MetabaseEmbed embed) => $"/Analytics/{Uri.EscapeDataString(embed.Label)}";

    private async Task CreateEmbed() =>
        await DetailsComponentDialog.Open<MetabaseEmbedDetails>(DialogService, "Add Analytics Page",
            ModificationState.Creating);

    private async Task UpdateEmbed(MetabaseEmbed embed) =>
        await DetailsComponentDialog.Open<MetabaseEmbedDetails>(DialogService, "Update Analytics Page",
            ModificationState.Updating, embed.Id);

    private async Task DeleteEmbed(MetabaseEmbed embed)
    {
        DeleteResult result = await DeleteWithDialog(
            MetabaseService,
            embed.Id,
            () => _embeds = _embeds.ToLoading(),
            RetrieveEmbeds
        );

        if (result == DeleteResult.Success)
            await MetabaseEmbedsStore.RefreshEvent();
    }
}
