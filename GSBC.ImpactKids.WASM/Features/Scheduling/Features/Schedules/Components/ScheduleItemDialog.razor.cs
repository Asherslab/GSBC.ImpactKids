using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Songs;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

/// <summary>Edits one item, or adds a one-off item to a lane when <see cref="Item"/> is null.</summary>
public partial class ScheduleItemDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public ScheduleItem? Item { get; set; }

    [Parameter]
    public Guid ScheduleId { get; set; }

    /// <summary>The split lane to add into; null adds to the end of the main list.</summary>
    [Parameter]
    public Guid? LaneId { get; set; }

    /// <summary>The section to add under. Its items have only a name and a fill-in.</summary>
    [Parameter]
    public Guid? SectionId { get; set; }

    /// <summary>Whether <see cref="Item"/> sits under a section.</summary>
    [Parameter]
    public bool IsChild { get; set; }

    private bool UnderSection => SectionId != null || IsChild;

    /// <summary>Whether a length change now is an adjustment on the night rather than the plan.</summary>
    [Parameter]
    public bool Underway { get; set; }

    private string?   _name;
    private string?   _fillIn;
    private int       _minutes = 5;
    private TimeSpan? _startsAt;
    private bool      _isSection;
    private bool      _busy;

    private Guid?   _songId;
    private string? _arrangementUuid;

    protected override async Task OnInitializedAsync()
    {
        if (Item == null)
            return;

        _name = Item.Name;
        _fillIn = Item.FillIn;
        _songId = Item.SongId;
        _arrangementUuid = Item.ArrangementUuid;
        _minutes = Item.Minutes;
        _startsAt = Item.StartsAt;

        if (Item.IsSong)
            await SongsStore.RefreshAll();
    }

    private ImmutableList<Song> Library =>
        SongsStore.GetState().Entities is { HasData: true } songs ? songs.Data! : [];

    /// <summary>The library song it is filled in with. Null for a new song typed by name, or one since removed from ProPresenter.</summary>
    private Song? LinkedSong => Library.FirstOrDefault(x => x.Id == _songId);

    private string? SongHelp =>
        string.IsNullOrWhiteSpace(_fillIn) ? null
        : _songId == null ? "Not in the song library yet - kept by name"
        : LinkedSong == null ? "No longer in ProPresenter"
        : null;

    private Task<IEnumerable<string>> SearchSongs(string? search, CancellationToken token)
    {
        IEnumerable<Song> songs = Library.OrderBy(x => x.Name);

        if (!string.IsNullOrWhiteSpace(search))
            songs = songs.Where(x => x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(songs.Select(x => x.Name).Distinct());
    }

    /// <summary>
    /// A name the library has links to that song; anything else is a song not in the library yet. The
    /// item's own song keeps its link while its name is unchanged, even if it has left the library.
    /// </summary>
    private void SetSong(string? name)
    {
        _fillIn = name;

        Guid? songId = Library.FirstOrDefault(x => string.Equals(x.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))?.Id
                       ?? (name == Item?.FillIn ? Item?.SongId : null);

        if (songId != _songId)
            _arrangementUuid = songId == Item?.SongId ? Item?.ArrangementUuid : null;

        _songId = songId;
    }

    private string? LengthHelp =>
        Item != null && Underway && Item.AdjustmentMinutes != 0
            ? $"Planned {Item.PlannedMinutes} min, {Item.AdjustmentMinutes:+0;-0} on the night"
            : Underway
                ? "The night is underway: a change here is recorded against the plan"
                : null;

    private async Task Save()
    {
        _busy = true;

        BasicResponse response = Item == null
            ? await ScheduleService.AddItem(new AddScheduleItemRequest
            {
                ScheduleId = ScheduleId,
                LaneId = LaneId,
                SectionId = SectionId,
                IsSection = _isSection,
                Name = _name,
                Minutes = _minutes
            })
            : await ScheduleService.UpdateItem(new UpdateScheduleItemRequest
            {
                ItemId = Item.Id,
                Name = _name ?? "",
                FillIn = _fillIn,
                SongId = _songId,
                ArrangementUuid = _songId == null ? null : _arrangementUuid,
                Minutes = _minutes,
                StartsAt = _startsAt
            });

        _busy = false;

        if (!Snackbar.AddErrorResponse(response))
            MudDialog.Close(DialogResult.Ok(true));
    }

    private async Task Remove()
    {
        if (Item == null)
            return;

        _busy = true;

        BasicResponse response = await ScheduleService.RemoveItem(new BasicReadRequest { Guid = Item.Id });

        _busy = false;

        if (!Snackbar.AddErrorResponse(response))
            MudDialog.Close(DialogResult.Ok(true));
    }

    public static Task Open(
        IDialogService dialogService,
        ScheduleItem?  item,
        Guid           scheduleId,
        Guid?          laneId,
        bool           underway,
        Guid?          sectionId = null,
        bool           isChild   = false
    ) =>
        dialogService.ShowAsync<ScheduleItemDialog>(
            item == null ? "Add a one-off item" : item.DisplayName,
            new DialogParameters<ScheduleItemDialog>
            {
                { x => x.Item, item },
                { x => x.ScheduleId, scheduleId },
                { x => x.LaneId, laneId },
                { x => x.SectionId, sectionId },
                { x => x.IsChild, isChild },
                { x => x.Underway, underway }
            },
            new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.ExtraSmall });
}
