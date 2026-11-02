using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.DataDisplay;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.DataDisplay;

public partial class AnalyticsPage
{
    [Parameter]
    public required string DashboardLabel { get; set; }

    [SupplyParameterFromQuery]
    public int? Refresh { get; set; }

    private Guid           _iframeKey = Guid.NewGuid();
    private string?        _reportUrl;
    private string?        _token;
    private string?        _error;
    private MetabaseEmbed? _embed;
    private bool           _refreshing;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MetabaseEmbedsStore, () => InvokeAsync(EmbedsChanged));
        await MetabaseEmbedsStore.RefreshAll();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        _embed = null;
        _token = null;
        _error = null;
        await EmbedsChanged();
    }

    /// <summary>
    /// Picks this page's embed out of the store, and loads it when it is new or has changed - the
    /// store also changes when somebody edits a different embed, and that must not reload the frame.
    /// </summary>
    private async Task EmbedsChanged()
    {
        AsyncData<ImmutableList<MetabaseEmbed>> embeds = MetabaseEmbedsStore.GetState().Entities;

        if (embeds.HasError)
        {
            _error = embeds.Error ?? "An Unexpected error occured";
            StateHasChanged();
            return;
        }

        if (!embeds.HasData)
            return;

        MetabaseEmbed? embed = embeds.Data!.FirstOrDefault(x => !x.IsFolder && x.Label == DashboardLabel);

        if (embed == null)
        {
            _embed = null;
            _token = null;
            _error = ErrorConstants.MetabaseEmbedNotFound;
            StateHasChanged();
            return;
        }

        if (embed == _embed)
            return;

        _embed = embed;
        _error = null;
        await LoadDashboardAsync();
    }

    private async Task RefreshClicked()
    {
        _refreshing = true;
        StateHasChanged();

        await LoadDashboardAsync();

        _refreshing = false;
        StateHasChanged();
    }

    private async Task LoadDashboardAsync()
    {
        if (_embed == null)
            return;

        _iframeKey = Guid.NewGuid();
        StateHasChanged();

        BasicReadResponse<string?> resp = await MetabaseService.GetMetabaseJwt(new BasicReadRequest
        {
            Guid = _embed.Id
        });

        if (resp.HasErrorOrNull())
        {
            _token = null;
            _error = resp?.Error ?? "An Unexpected error occured";
        }
        else
        {
            _token = resp.Entity;
            _reportUrl = DashboardUrl(_embed);
        }

        StateHasChanged();
    }

    private string DashboardUrl(MetabaseEmbed embed)
    {
        string refreshString = Refresh == null ? "" : $"&refresh={Refresh}";
        return $"https://kids-metabase.baptist.com.au/embed/{embed.ResourceType}/" + _token +
               $"#theme=night&bordered=true&titled=false{refreshString}";
    }
}
