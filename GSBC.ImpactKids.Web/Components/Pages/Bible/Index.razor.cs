using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.Bible;

public partial class Index : ComponentBase
{
    [SupplyParameterFromQuery]
    public string? Search { get; set; }
    
    private ICollection<BibleVerse>? _verses;
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshVerses();
    }
    
    private async Task RefreshVerses()
    {
        BasicReadMultipleResponse<BibleVerse>? response = await
            BibleService.ReadMultiple(new BasicReadMultipleRequest
            {
                SearchString = Search,
                Pagination = new PaginationRequest
                {
                    PerPage = 20
                }
            });

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _verses = response.Entities;
        StateHasChanged();
    }
    
    private async Task OnSearch(string text)
    {
        Search = text;
        if (string.IsNullOrWhiteSpace(Search))
            Search = null;
        SetQueryParameters();
        await RefreshVerses();
    }
    
    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(Search)] = Search
        });
    }
}