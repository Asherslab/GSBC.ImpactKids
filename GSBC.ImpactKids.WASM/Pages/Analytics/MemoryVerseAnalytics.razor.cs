using GSBC.ImpactKids.Shared.Contracts.Messages.Requests;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses;

namespace GSBC.ImpactKids.WASM.Pages.Analytics;

public partial class MemoryVerseAnalytics
{
    private string? _token;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        MetabaseJwtResponse? resp = await MetabaseService.GetMetabaseJwt(new MetabaseJwtRequest
            { DashboardId = MetabaseConfig.DashboardMappings["MemoryVerseAnalytics"] });

        _token = resp?.Jwt;
    }

    private string DashboardUrl()
    {
        return "https://kids-metabase.baptist.com.au/embed/dashboard/" + _token +
               "#theme=night&bordered=true&titled=true";
    }
}