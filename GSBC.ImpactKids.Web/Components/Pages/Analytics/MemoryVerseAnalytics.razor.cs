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

    private List<ChartSeries>? _series;
    private List<string>?      _xAxisLabels;

    private ChartOptions _chartOptions = new()
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

        MemoryVerseAnalyticsResponse? response = await MemorisationEntriesService.RetrieveAnalyticsData(
            new MemorisationEntriesAnalyticsRequest
            {
                MemoryVerseListId = _selectedList.Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _xAxisLabels = response.Services.Select(x => x.Date.ToString("dd/MM")).ToList();

        _series = response.VerticalAxis.Select(x =>
            new ChartSeries
            {
                Name = x.Verse.ReferenceName,
                Data = x.DataPoints
            }
        ).ToList();

        StateHasChanged();
    }

    private async Task SelectedSchoolTermChanged(Guid value)
    {
        SelectedSchoolTerm = value;
        SetQueryParameters();
        await SetSelectedVerseList();
        StateHasChanged();
    }

    private async Task SelectedMemoryVerseListChanged(Guid value)
    {
        SelectedMemoryVerseList = value;
        SetQueryParameters();
        await SetSelectedVerseList();
        StateHasChanged();
    }

    private async Task SetSelectedSchoolTerm()
    {
        if (SelectedSchoolTerm == null)
        {
            SchoolTerm? term = _terms?.FirstOrDefault();
            SelectedSchoolTerm = term?.Id;
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