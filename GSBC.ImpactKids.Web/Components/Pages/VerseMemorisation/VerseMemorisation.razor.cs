using System.Text;
using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Pages.VerseMemorisation;

public partial class VerseMemorisation : EventListeningComponent
{
    [Parameter]
    public Guid ServiceId { get; set; }

    [SupplyParameterFromQuery]
    public Guid SelectedMemoryVerse { get; set; }

    private ICollection<MemoryVerse>? _memoryVerses;
    private MemoryVerse?              _selectedVerse;
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        
        await RefreshMemoryVerses();
        await SubscribeToEvent(MemoryVerse.BuildSubscription(), RefreshMemoryVerses); // We subscribe to all memory verse changes since filtering by service is complex
    }

    private async Task RefreshMemoryVerses()
    {
        BasicReadMultipleResponse<MemoryVerse>? response = await MemoryVersesService.ReadMultiple(
            new MemoryVersesRequest
            {
                ServiceId = ServiceId,
                IncludeBibleVerses = true
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _memoryVerses = response.Entities;
        SetSelectedVerse();
        StateHasChanged();
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
            (MarkupString) builder.ToString(),
            yesText: "Close"
        );
    }

    private void SelectedMemoryVerseChanged(Guid value)
    {
        SelectedMemoryVerse = value;
        SetQueryParameters();
        SetSelectedVerse();
        StateHasChanged();
    }

    private void SetSelectedVerse()
    {
        _selectedVerse = _memoryVerses?.FirstOrDefault(x => x.Id == SelectedMemoryVerse);
    }

    private void SetQueryParameters()
    {
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            [nameof(SelectedMemoryVerse)] = SelectedMemoryVerse
        });
    }
}