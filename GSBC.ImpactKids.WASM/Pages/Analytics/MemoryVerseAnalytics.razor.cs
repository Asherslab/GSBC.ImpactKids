using GSBC.ImpactKids.Shared.Contracts.Messages.Requests;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;
using GSBC.ImpactKids.WASM.Components.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Pages.Analytics;

public partial class MemoryVerseAnalytics : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public int? Refresh { get; set; }
    
    private string? _token;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await RefreshDashboard();
    }

    private async Task RefreshDashboard()
    {
        MetabaseJwtResponse? resp = await MetabaseService.GetMetabaseJwt(new MetabaseJwtRequest
            { DashboardId = MetabaseConfig.DashboardMappings["MemoryVerseAnalytics"] });

        _token = resp?.Jwt;
        
        StateHasChanged();
    }

    private string DashboardUrl()
    {
        string refreshString = Refresh == null ? "" : $"&refresh={Refresh}";
        return "https://kids-metabase.baptist.com.au/embed/dashboard/" + _token +
               $"#theme=night&bordered=true&titled=true{refreshString}";
    }
}