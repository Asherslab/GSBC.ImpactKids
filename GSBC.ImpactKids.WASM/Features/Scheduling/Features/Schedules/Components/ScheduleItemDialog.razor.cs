using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
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
    public Guid LaneId { get; set; }

    /// <summary>Whether a length change now is an adjustment on the night rather than the plan.</summary>
    [Parameter]
    public bool Underway { get; set; }

    private string?   _name;
    private string?   _fillIn;
    private int       _minutes = 5;
    private TimeSpan? _startsAt;
    private bool      _busy;

    protected override void OnInitialized()
    {
        if (Item == null)
            return;

        _name = Item.Name;
        _fillIn = Item.FillIn;
        _minutes = Item.Minutes;
        _startsAt = Item.StartsAt;
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
                LaneId = LaneId,
                Name = _name,
                Minutes = _minutes
            })
            : await ScheduleService.UpdateItem(new UpdateScheduleItemRequest
            {
                ItemId = Item.Id,
                Name = _name ?? "",
                FillIn = _fillIn,
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

    public static Task Open(IDialogService dialogService, ScheduleItem? item, Guid laneId, bool underway) =>
        dialogService.ShowAsync<ScheduleItemDialog>(
            item == null ? "Add a one-off item" : item.DisplayName,
            new DialogParameters<ScheduleItemDialog>
            {
                { x => x.Item, item },
                { x => x.LaneId, laneId },
                { x => x.Underway, underway }
            },
            new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.ExtraSmall });
}
