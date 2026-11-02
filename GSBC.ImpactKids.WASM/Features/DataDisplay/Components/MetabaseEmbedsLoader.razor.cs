using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.DataDisplay.Components;

/// <summary>
/// The analytics pages for the nav menus, rendered only once there are some. Put it inside an
/// enabled-only <c>AuthorizeView</c>: a signed-out read from the store sends the browser to sign in.
/// </summary>
public partial class MetabaseEmbedsLoader
{
    [Parameter, EditorRequired]
    public required RenderFragment<ImmutableList<MetabaseEmbed>> ChildContent { get; set; }

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
}
