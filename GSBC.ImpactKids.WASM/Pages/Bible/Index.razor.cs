using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Pages.Bible;

public partial class Index : ComponentBase
{
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
        await RefreshVerses();
    }
}