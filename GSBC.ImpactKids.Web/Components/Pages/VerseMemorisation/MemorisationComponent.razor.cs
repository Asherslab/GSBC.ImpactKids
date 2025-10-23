using System.Text;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.VerseMemorisation;

public partial class MemorisationComponent : EventListeningComponent
{
    [Parameter]
    public Guid? ServiceId { get; set; }

    [Parameter]
    public bool Previous { get; set; }

    [Parameter]
    public bool Upcoming { get; set; }

    [SupplyParameterFromQuery]
    public Guid? SelectedMemoryVerse { get; set; }

    [SupplyParameterFromQuery]
    public string? Search { get; set; }

    private ICollection<MemoryVerse>? _memoryVerses;
    private MemoryVerse?              _selectedVerse;

    private Service?                        _service;
    private ICollection<MemorisationEntry>? _memorisationEntries;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        await RefreshService();
        await SubscribeToEvent(Service.BuildSubscription(serviceId: _service?.Id), RefreshService);

        await RefreshMemoryVerses();
        // We subscribe to all memory verse changes since filtering by service is complex
        await SubscribeToEvent(MemoryVerse.BuildSubscription(), RefreshMemoryVerses);

        // await RefreshMemorisationEntries(); // called by RefreshMemoryVerses() instead.
        await SubscribeToEvent(MemorisationEntry.BuildSubscription(), RefreshMemorisationEntries);
    }

    private async Task RefreshService()
    {
        BasicReadResponse<Service>? response = await ServicesService.Read(
            new ServiceRequest
            {
                Guid = ServiceId ?? Guid.Empty,
                PreviousService = Previous,
                UpcomingService = Upcoming
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _service = response.Entity;
        StateHasChanged();
    }

    private async Task RefreshMemoryVerses()
    {
        if (_service == null)
            return;

        BasicReadMultipleResponse<MemoryVerse>? response = await MemoryVersesService.ReadMultiple(
            new MemoryVersesRequest
            {
                ServiceId = _service.Id,
                IncludeBibleVerses = true
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _memoryVerses = response.Entities;
        await SetSelectedVerse();
        StateHasChanged();
    }

    private async Task RefreshMemorisationEntries()
    {
        if (_selectedVerse == null || _service == null)
            return;

        BasicReadMultipleResponse<MemorisationEntry>? response = await MemorisationEntriesService.ReadMultiple(
            new MemorisationEntriesRequest
            {
                SearchString = Search,
                Pagination = new PaginationRequest(0, 5),

                ServiceId = _service.Id,
                MemoryVerseId = _selectedVerse.Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _memorisationEntries = response.Entities;
        StateHasChanged();
    }

    private async Task OnSearch(string text)
    {
        Search = text;
        if (string.IsNullOrWhiteSpace(Search))
            Search = null;
        // SetQueryParameters();
        await RefreshMemorisationEntries();
    }

    private async Task ShowOriginalText()
    {
        if (_selectedVerse?.BibleVerses == null)
            return;

        StringBuilder builder = new();

        foreach (BibleVerse verse in _selectedVerse.BibleVerses)
        {
            builder.Append($"{verse.BookName} {verse.ChapterNumber}:{verse.VerseNumber}<br />");
            builder.Append(verse.Verse);
            builder.Append("<br /><br />");
        }

        if (_selectedVerse.BibleVerses.Count != 0)
            builder.Length -= "<br /><br />".Length;

        await DialogService.ShowMessageBox(
            "Original Text",
            (MarkupString)builder.ToString(),
            yesText: "Close"
        );
    }

    private async Task SelectedMemoryVerseChanged(Guid value)
    {
        SelectedMemoryVerse = value;
        SetQueryParameters();
        await SetSelectedVerse();
        StateHasChanged();
    }


    private async Task UpdateMemorisationEntry(
        MemorisationEntry memorisationEntry,
        bool?             recited         = null,
        bool?             fiveDollaryDoos = null,
        bool?             oneDollaryDoo   = null
    )
    {
        UpdateMemorisationEntryRequest request = new()
        {
            Guid = memorisationEntry.Id
        };

        if (recited != null)
            request.VerseRecited.Value = recited.Value;

        if (fiveDollaryDoos != null)
            request.FiveDollaryDoosGiven.Value = fiveDollaryDoos.Value;

        if (oneDollaryDoo != null)
            request.OneDollaryDooGiven.Value = oneDollaryDoo.Value;

        BasicResponse? response = await MemorisationEntriesService.Update(request);

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
        }
    }

    private async Task SetSelectedVerse()
    {
        if (SelectedMemoryVerse == null)
        {
            MemoryVerse? verse = _memoryVerses?.FirstOrDefault();
            SelectedMemoryVerse = verse?.Id;
            SetQueryParameters();
        }
            
        _selectedVerse = _memoryVerses?.FirstOrDefault(x => x.Id == SelectedMemoryVerse);
        await RefreshMemorisationEntries();
    }

    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(SelectedMemoryVerse)] = SelectedMemoryVerse,
            // [nameof(Search)] = Search, // we can't do this until .net 10, because every navigation scrolls to the top
            [nameof(Upcoming)] = Upcoming,
            [nameof(Previous)] = Previous
        });
    }
}