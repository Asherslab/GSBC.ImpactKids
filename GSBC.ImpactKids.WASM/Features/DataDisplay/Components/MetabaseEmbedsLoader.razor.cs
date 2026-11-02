using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.DataDisplay.Components;

/// <summary>
/// The Analytics menu for the nav menus, rendered only once it has something in it. Put it inside an
/// enabled-only <c>AuthorizeView</c>: a signed-out read from the store sends the browser to sign in.
/// </summary>
public partial class MetabaseEmbedsLoader
{
    [Parameter, EditorRequired]
    public required RenderFragment<ImmutableList<MetabaseMenuEntry>> ChildContent { get; set; }

    private ImmutableList<MetabaseMenuEntry> _menu = [];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MetabaseEmbedsStore, RetrieveEmbeds);
        RetrieveEmbeds();

        await Task.WhenAll(MetabaseEmbedsStore.RefreshAll());
    }

    private void RetrieveEmbeds()
    {
        AsyncData<ImmutableList<MetabaseEmbed>> embeds = MetabaseEmbedsStore.GetState().Entities;

        _menu = embeds.HasData ? MetabaseMenu.ForNav(embeds.Data!) : [];
        StateHasChanged();
    }
}
