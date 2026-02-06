using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Features.MemoryVerses.Components.Individual;

public partial class CreateMemoryVerseDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private readonly CreateMemoryVerseRequest _createMemoryVerseRequest = new();
    private          BibleVerse?              _bibleVerse;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(MemoryVerseListsStore, SetMemoryVerseListIdIfNull);
        HandleStateChangeSubscriptionDisposal(BibleVersesStore);

        await Task.WhenAll(
            Task.Run(async () =>
            {
                await MemoryVerseListsStore.RefreshAll();
                SetMemoryVerseListIdIfNull();
            }),
            BibleVersesStore.RefreshAll()
        );
    }

    private void SetMemoryVerseListIdIfNull()
    {
        if (_createMemoryVerseRequest.MemoryVerseListId == Guid.Empty)
        {
            if (MemoryVerseListsStore.GetState().Entities.HasData)
            {
                _createMemoryVerseRequest.MemoryVerseListId =
                    MemoryVerseListsStore.GetState().Entities.Data!.First().Id;
            }
        }

        StateHasChanged();
    }

    private async Task OnClick()
    {
        BasicResponse resp = await CreateService.Create(_createMemoryVerseRequest);

        if (resp.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(resp);
            return;
        }

        MudDialog.Close();
    }

    private void BibleVerseChanged(BibleVerse? verse)
    {
        _bibleVerse = verse;

        _createMemoryVerseRequest.BibleVerseIds = verse == null ? [] : [verse.Id];
        _createMemoryVerseRequest.Verse = verse?.Verse ?? "";
        _createMemoryVerseRequest.ReferenceName = verse?.Reference() ?? "";
    }

    private Task<IEnumerable<BibleVerse>> BibleVerseSearch(string search, CancellationToken token)
    {
        if (!BibleVersesStore.GetState().Entities.HasData)
            return Task.FromResult<IEnumerable<BibleVerse>>([]);

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (string.IsNullOrEmpty(search))
            return Task.FromResult<IEnumerable<BibleVerse>>(BibleVersesStore.GetState().Entities.Data!);

        return Task.FromResult(BibleVerse.BibleVerseSearch(search, BibleVersesStore.GetState().Entities.Data!));
    }
}