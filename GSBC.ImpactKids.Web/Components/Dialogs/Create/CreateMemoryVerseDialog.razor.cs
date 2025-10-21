using System.Text;
using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Bible;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Create;

public partial class CreateMemoryVerseDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public MemoryVerseList? List { get; set; }

    private          MudStep?                 _submitStep;
    private readonly CreateMemoryVerseRequest _request   = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await RefreshVerses();
        await RefreshServices();
    }

    private async Task Submit()
    {
        _request.MemoryVerseListId = List?.Id ?? Guid.Empty; // backend will validate
        BasicResponse? response = await MemoryVersesService.Create(_request);

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            if (_submitStep != null)
                await _submitStep.SetCompletedAsync(false);
            StateHasChanged();
            return;
        }

        MudDialog.Close(DialogResult.Ok(true));
    }

    private string?                  _bibleSearch;
    private List<BibleVerse>         _selectedVerses = [];
    private ICollection<BibleVerse>? _verses;

    private void AddOrRemoveBibleVerse(BibleVerse verse)
    {
        if (_selectedVerses.Any(x => x.Id == verse.Id))
        {
            RemoveBibleVerse(verse);
            return;
        }

        AddBibleVerse(verse);
    }

    private void AddBibleVerse(BibleVerse verse)
    {
        _selectedVerses = _selectedVerses
            .Append(verse)
            .OrderBy(x => x.BookNumber)
            .ThenBy(x => x.ChapterNumber)
            .ThenBy(x => x.VerseNumber)
            .ToList();
        _request.BibleVerseIds = _selectedVerses.Select(x => x.Id).ToList();
        UpdateReferenceName();
        UpdateVerseText();
    }

    private void RemoveBibleVerse(BibleVerse verse)
    {
        _selectedVerses = _selectedVerses
            .Where(x => x.Id != verse.Id)
            .OrderBy(x => x.BookNumber)
            .ThenBy(x => x.ChapterNumber)
            .ThenBy(x => x.VerseNumber)
            .ToList();
        _request.BibleVerseIds = _selectedVerses.Select(x => x.Id).ToList();
        UpdateReferenceName();
        UpdateVerseText();
    }

    private void UpdateReferenceName()
    {
        StringBuilder referenceBuilder = new();

        foreach (
            IGrouping<int, BibleVerse> versesByBook in
            _selectedVerses
                .GroupBy(x => x.BookNumber)
                .OrderBy(x => x.Key)
        )
        {
            referenceBuilder.Append(versesByBook.First().BookName);
            referenceBuilder.Append(' ');
            foreach (
                IGrouping<int, BibleVerse> versesByChapter in
                versesByBook
                    .GroupBy(x => x.ChapterNumber)
                    .OrderBy(x => x.Key)
            )
            {
                referenceBuilder.Append(versesByChapter.First().ChapterNumber);
                referenceBuilder.Append(':');
                int firstVerseNumber = 0;
                int lastVerseNumber  = 0;
                foreach (
                    BibleVerse orderedVerse in versesByChapter
                        .OrderBy(x => x.VerseNumber)
                )
                {
                    if (firstVerseNumber == 0)
                    {
                        firstVerseNumber = orderedVerse.VerseNumber;
                        lastVerseNumber = orderedVerse.VerseNumber;
                    }
                    else if (lastVerseNumber + 1 == orderedVerse.VerseNumber)
                    {
                        lastVerseNumber = orderedVerse.VerseNumber;
                    }
                    else
                    {
                        if (firstVerseNumber == lastVerseNumber)
                        {
                            referenceBuilder.Append($"{firstVerseNumber}, ");
                        }
                        else
                        {
                            referenceBuilder.Append($"{firstVerseNumber}-{lastVerseNumber}, ");
                        }

                        firstVerseNumber = orderedVerse.VerseNumber;
                        lastVerseNumber = orderedVerse.VerseNumber;
                    }
                }

                if (firstVerseNumber == lastVerseNumber)
                {
                    referenceBuilder.Append($"{firstVerseNumber}; ");
                }
                else
                {
                    referenceBuilder.Append($"{firstVerseNumber}-{lastVerseNumber}; ");
                }
            }
        }

        if (referenceBuilder.Length >= 2)
        {
            referenceBuilder.Length -= 2;
        }

        _request.ReferenceName = referenceBuilder.ToString();
        StateHasChanged();
    }

    private void UpdateVerseText()
    {
        StringBuilder verseBuilder = new();

        if (_selectedVerses.Count == 1)
        {
            verseBuilder.Append(_selectedVerses[0].Verse);
        }
        else
        {
            bool firstVerse = true;
            foreach (BibleVerse verse in _selectedVerses)
            {
                if (firstVerse)
                {
                    firstVerse = false;
                }
                else
                {
                    verseBuilder.Append(' ');
                }

                verseBuilder.Append(verse.BookName);
                verseBuilder.Append(' ');
                verseBuilder.Append(verse.ChapterNumber);
                verseBuilder.Append(':');
                verseBuilder.Append(verse.VerseNumber);
                verseBuilder.Append(". ");
                verseBuilder.Append(verse.Verse);
            }
        }

        _request.Verse = verseBuilder.ToString();
        StateHasChanged();
    }

    private async Task RefreshVerses()
    {
        BasicReadMultipleResponse<BibleVerse>? response = await
            BibleService.ReadMultiple(new BasicReadMultipleRequest
            {
                SearchString = _bibleSearch,
                Pagination = new PaginationRequest
                {
                    PerPage = 5
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

    private async Task OnBibleSearch(string text)
    {
        _bibleSearch = text;
        if (string.IsNullOrWhiteSpace(_bibleSearch))
            _bibleSearch = null;
        await RefreshVerses();
    }

    private          string?               _serviceSearch;
    private readonly List<Service>         _selectedServices = [];
    private          ICollection<Service>? _services;

    private void AddOrRemoveService(Service service)
    {
        if (_selectedServices.Any(x => x.Id == service.Id))
        {
            RemoveService(service);
            return;
        }

        AddService(service);
    }

    private void AddService(Service service)
    {
        _selectedServices.Add(service);
        _request.ServiceIds = _selectedServices.Select(x => x.Id).ToList();
        StateHasChanged();
    }

    private void RemoveService(Service service)
    {
        _selectedServices.Remove(_selectedServices.First(x => x.Id == service.Id));
        _request.BibleVerseIds = _selectedServices.Select(x => x.Id).ToList();
        StateHasChanged();
    }

    private async Task RefreshServices()
    {
        BasicReadMultipleResponse<Service>? response = await
            ServicesService.ReadMultiple(new ServicesRequest
            {
                SearchString = _serviceSearch,
                Pagination = new PaginationRequest
                {
                    PerPage = 5
                }
            });

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _services = response.Entities;
        StateHasChanged();
    }

    private async Task OnServiceSearch(string text)
    {
        _serviceSearch = text;
        if (string.IsNullOrWhiteSpace(_serviceSearch))
            _serviceSearch = null;
        await RefreshServices();
    }

    private async Task<IEnumerable<MemoryVerseList>> ListSearchFunc(
        string            arg,
        CancellationToken token
    )
    {
        BasicReadMultipleResponse<MemoryVerseList>? response;
        try
        {
            response = await MemoryVerseListsService.ReadMultiple(
                new MemoryVerseListsRequest
                {
                    Pagination = null,
                    SearchString = arg,
                },
                token
            );
        }
        catch (Exception e)
        {
            if (e is RpcException { StatusCode: StatusCode.Cancelled })
                return [];
            response = null;
        }

        if (response.HasErrorOrNull())
            Snackbar.AddErrorResponse(response);

        return response?.Entities ?? [];
    }
}