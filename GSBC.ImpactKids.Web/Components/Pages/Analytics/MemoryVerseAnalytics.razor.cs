using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analyitcs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Analytics;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.Analytics;

public partial class MemoryVerseAnalytics : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public Guid? SelectedMemoryVerseList { get; set; }

    [SupplyParameterFromQuery]
    public Guid? SelectedSchoolTerm { get; set; }

    private ICollection<SchoolTerm>?      _terms;
    private ICollection<MemoryVerseList>? _lists;

    private SchoolTerm?      _selectedTerm;
    private MemoryVerseList? _selectedList;

    private MemoryVerseAnalyticsResponse? _recitationsPerMemoryVerseResp;
    private MemoryVerseAnalyticsResponse? _recitationsPerChildResp;

    private readonly ChartOptions _chartOptions = new()
    {
        YAxisTicks = 1
    };

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshSchoolTerms();
        await SubscribeToEvent(SchoolTerm.BuildSubscription(), RefreshSchoolTerms);
        await SubscribeToEvent(MemoryVerseList.BuildSubscription(), RefreshMemoryVerseLists);
        await SubscribeToEvent(MemorisationEntry.BuildSubscription(), RefreshAnalytics);

        // await RefreshMemorisationEntries(); // called by RefreshMemoryVerses() instead.
        // await SubscribeToEvent(MemorisationEntry.BuildSubscription(), RefreshMemorisationEntries);
    }

    private async Task RefreshSchoolTerms()
    {
        BasicReadMultipleResponse<SchoolTerm>? response = await SchoolTermsService.ReadMultiple(
            new SchoolTermsRequest
            {
                Year = DateTime.Now.Year
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _terms = response.Entities;
        await SetSelectedSchoolTerm();
        StateHasChanged();
    }

    private async Task RefreshMemoryVerseLists()
    {
        if (_selectedTerm == null)
            return;

        BasicReadMultipleResponse<MemoryVerseList>? response = await MemoryVerseListsService.ReadMultiple(
            new MemoryVerseListsRequest
            {
                SchoolTermId = _selectedTerm.Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _lists = response.Entities;
        await SetSelectedVerseList();
        StateHasChanged();
    }

    private async Task RefreshAnalytics()
    {
        if (_selectedList == null)
            return;

        _recitationsPerMemoryVerseResp = await MemorisationEntriesService.RecitationsPerVerseAnalytics(
            new MemorisationEntriesAnalyticsRequest
            {
                MemoryVerseListId = _selectedList.Id
            }
        );
        if (_recitationsPerMemoryVerseResp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(_recitationsPerMemoryVerseResp);
            return;
        }
        
        _recitationsPerChildResp = await MemorisationEntriesService.RecitationsPerChildAnalytics(
            new MemorisationEntriesAnalyticsRequest
            {
                MemoryVerseListId = _selectedList.Id
            }
        );
        if (_recitationsPerChildResp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(_recitationsPerChildResp);
            return;
        }

        StateHasChanged();
    }

    private List<ChartSeries> GetChartSeries(MemoryVerseAnalyticsResponse response)
    {
        return response.VerticalAxis.Select(x =>
            new ChartSeries
            {
                Name = x.Label,
                Data = x.DataPoints
            }
        ).ToList();
    }

    private async Task SelectedSchoolTermChanged(Guid value)
    {
        SelectedSchoolTerm = value;
        SelectedMemoryVerseList = null;
        _recitationsPerMemoryVerseResp = null;
        SetQueryParameters();
        await SetSelectedSchoolTerm();
        StateHasChanged();
    }

    private async Task SelectedMemoryVerseListChanged(Guid value)
    {
        SelectedMemoryVerseList = value;
        _recitationsPerMemoryVerseResp = null;
        SetQueryParameters();
        await SetSelectedVerseList();
        StateHasChanged();
    }

    private async Task SetSelectedSchoolTerm()
    {
        if (SelectedSchoolTerm == null)
        {
            DateTime? now = DateTime.Now;

            SchoolTerm? term = _terms?.FirstOrDefault(x => x.StartDate <= now && now <= x.EndDate);
            term ??= _terms?.FirstOrDefault();
            SelectedSchoolTerm = term?.Id;
            if (SelectedSchoolTerm != null)
                SetQueryParameters();
        }

        _selectedTerm = _terms?.FirstOrDefault(x => x.Id == SelectedSchoolTerm);

        await RefreshMemoryVerseLists();
    }

    private async Task SetSelectedVerseList()
    {
        if (SelectedMemoryVerseList == null)
        {
            MemoryVerseList? list = _lists?.FirstOrDefault();
            SelectedMemoryVerseList = list?.Id;
            if (SelectedMemoryVerseList != null)
                SetQueryParameters();
        }

        _selectedList = _lists?.FirstOrDefault(x => x.Id == SelectedMemoryVerseList);
        await RefreshAnalytics();
    }

    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(SelectedSchoolTerm)] = SelectedSchoolTerm,
            [nameof(SelectedMemoryVerseList)] = SelectedMemoryVerseList
        });
    }
}