using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

/// <summary>
/// One schedule, edited in place: a template, or a service's schedule on the night.
/// <para>
/// <b>Changes show before the server answers.</b> A drag or a +5 is applied to a local copy at once,
/// then sent; the event that follows brings back the server's version, which replaces the copy. A
/// refused change puts the store's version straight back. On a phone mid-programme, waiting a round
/// trip to see an item land where it was dropped reads as the drag not having worked.
/// </para>
/// </summary>
public partial class ScheduleEditor
{
    [Parameter]
    public required Guid ScheduleId { get; set; }

    /// <summary>The service's local date, for clock times and for knowing when the night is underway. Null for a template.</summary>
    [Parameter]
    public DateTime? LocalDate { get; set; }

    private static readonly TimeSpan NowRedrawInterval = TimeSpan.FromSeconds(30);

    private AsyncData<ImmutableList<Schedule>> _status = AsyncData<ImmutableList<Schedule>>.NotAsked();

    private Schedule?                            _schedule;
    private ImmutableDictionary<Guid, TimedItem> _timed         = ImmutableDictionary<Guid, TimedItem>.Empty;
    private ImmutableList<ScheduleItemTemplate>  _itemTemplates = [];
    private bool                                 _underway;
    private int                                  _addRound;

    private ElementReference                           _root;
    private IJSObjectReference?                        _module;
    private DotNetObjectReference<ScheduleEditor>?     _self;
    private Timer?                                     _redraw;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SchedulesStore, RetrieveSchedule);
        HandleSubscriptionDisposal(ItemTemplatesStore, RetrieveItemTemplates);
        RetrieveSchedule();
        RetrieveItemTemplates();

        // Only a service's schedule has a "now" that moves.
        if (LocalDate != null)
            _redraw = new Timer(_ => InvokeAsync(() =>
            {
                Recompute();
                StateHasChanged();
            }), null, NowRedrawInterval, NowRedrawInterval);

        await Task.WhenAll(SchedulesStore.RefreshAll(), ItemTemplatesStore.RefreshAll());
    }

    protected override void OnParametersSet() => RetrieveSchedule();

    private void RetrieveSchedule()
    {
        _status = SchedulesStore.GetState().Entities;
        _schedule = _status.Data?.FirstOrDefault(x => x.Id == ScheduleId);
        Recompute();
        StateHasChanged();
    }

    private void RetrieveItemTemplates()
    {
        _itemTemplates = ItemTemplatesStore.GetState().Entities.Data?.OrderBy(x => x.Name).ToImmutableList() ?? [];
        StateHasChanged();
    }

    private void Recompute()
    {
        if (_schedule == null)
            return;

        _timed = ScheduleTimeline.Compute(_schedule, LocalDate ?? DateTime.Today);
        _underway = LocalDate != null && ScheduleTimeline.IsUnderway(_schedule, LocalDate.Value, DateTime.Now);
    }

    private DateTime? EndsAt => _timed.Values.Where(t => t.End != null).Select(t => t.End).Max();

    private bool IsNow(TimedItem? timed) =>
        LocalDate?.Date == DateTime.Today && timed is { Start: { } start, End: { } end }
                                         && start <= DateTime.Now && DateTime.Now < end;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_schedule == null)
            return;

        _self ??= DotNetObjectReference.Create(this);
        _module ??= await Js.InvokeAsync<IJSObjectReference>("import", "./js/scheduleSortable.js");

        // Every render: blocks and lanes come and go, and attach skips the lists it already has.
        await _module.InvokeVoidAsync("attach", _root, _self);
    }

    /// <summary>Applies a change to the local copy now, sends it, and puts the store's copy back if it is refused.</summary>
    private async Task Send(Func<Schedule, Schedule>? optimistic, Func<Task<BasicResponse>> call)
    {
        if (_schedule != null && optimistic != null)
        {
            _schedule = optimistic(_schedule);
            Recompute();
            StateHasChanged();
        }

        BasicResponse response = await call();

        if (Snackbar.AddErrorResponse(response))
            RetrieveSchedule();

        await SchedulesStore.RefreshEvent();
    }

    // Items \\

    [JSInvokable]
    public Task OnItemDropped(string itemId, string laneId, int position) =>
        Send(
            schedule => ScheduleEdits.MoveItem(schedule, Guid.Parse(itemId), Guid.Parse(laneId), position),
            () => ScheduleService.MoveItem(new MoveScheduleItemRequest
            {
                ItemId = Guid.Parse(itemId),
                LaneId = Guid.Parse(laneId),
                Position = position
            })
        );

    private Task Adjust(ScheduleItem item, int minutes) =>
        Send(
            schedule => ScheduleEdits.Adjust(schedule, item.Id, minutes, _underway),
            () => ScheduleService.AdjustItem(new AdjustScheduleItemRequest { ItemId = item.Id, Minutes = minutes })
        );

    private async Task AddFromTemplate(ScheduleLane lane, Guid? templateId)
    {
        if (templateId == null)
            return;

        _addRound++;

        await Send(null, () => ScheduleService.AddItem(new AddScheduleItemRequest
        {
            LaneId = lane.Id,
            ItemTemplateId = templateId
        }));
    }

    private Task EditItem(ScheduleItem item, ScheduleLane lane) =>
        ScheduleItemDialog.Open(DialogService, item, lane.Id, _underway);

    // Groups \\

    private async Task AddGroup()
    {
        string? name = await TextPromptDialog.Ask(DialogService, "Add a group", "Group name",
            placeholder: "Prep–3, 4–6, Seniors", submitText: "Add");

        if (name != null)
            await Send(null, () => ScheduleService.AddGroup(new AddScheduleGroupRequest { ScheduleId = ScheduleId, Name = name }));
    }

    private async Task RenameGroup(ScheduleGroup group)
    {
        string? name = await TextPromptDialog.Ask(DialogService, "Rename group", "Group name", group.Name);

        if (name != null && name != group.Name)
            await Send(null, () => ScheduleService.RenameGroup(new RenameScheduleGroupRequest { GroupId = group.Id, Name = name }));
    }

    private async Task RemoveGroup(ScheduleGroup group)
    {
        bool? confirmed = await DialogService.ShowMessageBoxAsync(
            $"Remove {group.Name}?",
            "Lanes keep their items; they just stop being marked for this group.",
            yesText: "Remove", cancelText: "Cancel");

        if (confirmed == true)
            await Send(null, () => ScheduleService.RemoveGroup(new BasicReadRequest { Guid = group.Id }));
    }

    private Task ToggleLaneGroup(ScheduleLane lane, ScheduleGroup group)
    {
        List<Guid> groupIds = lane.GroupIds.Contains(group.Id)
            ? lane.GroupIds.Where(id => id != group.Id).ToList()
            : [..lane.GroupIds, group.Id];

        return Send(
            schedule => ScheduleEdits.SetLaneGroups(schedule, lane.Id, groupIds),
            () => ScheduleService.SetLaneGroups(new SetScheduleLaneGroupsRequest { LaneId = lane.Id, GroupIds = groupIds })
        );
    }

    // Blocks and lanes \\

    private Task AddBlock(int lanes) =>
        Send(null, () => ScheduleService.AddBlock(new AddScheduleBlockRequest
        {
            ScheduleId = ScheduleId,
            Position = _schedule?.Blocks.Count ?? 0,
            Lanes = lanes
        }));

    private Task MoveBlock(ScheduleBlock block, int position) =>
        Send(
            schedule => ScheduleEdits.MoveBlock(schedule, block.Id, position),
            () => ScheduleService.MoveBlock(new MoveScheduleBlockRequest { BlockId = block.Id, Position = position })
        );

    private Task AddLane(ScheduleBlock block) =>
        Send(null, () => ScheduleService.AddLane(new BasicReadRequest { Guid = block.Id }));

    private async Task RemoveBlock(ScheduleBlock block)
    {
        int items = block.Lanes.Sum(l => l.Items.Count);

        if (items > 0)
        {
            bool? confirmed = await DialogService.ShowMessageBoxAsync(
                "Remove this block?",
                $"Its {items} item{(items == 1 ? "" : "s")} will be removed with it.",
                yesText: "Remove", cancelText: "Cancel");

            if (confirmed != true)
                return;
        }

        await Send(null, () => ScheduleService.RemoveBlock(new BasicReadRequest { Guid = block.Id }));
    }

    private async Task RemoveLane(ScheduleBlock block, ScheduleLane lane)
    {
        if (lane.Items.Count > 0)
        {
            bool? confirmed = await DialogService.ShowMessageBoxAsync(
                "Remove this lane?",
                $"Its {lane.Items.Count} item{(lane.Items.Count == 1 ? "" : "s")} will be removed with it.",
                yesText: "Remove", cancelText: "Cancel");

            if (confirmed != true)
                return;
        }

        await Send(null, () => ScheduleService.RemoveLane(new BasicReadRequest { Guid = lane.Id }));
    }

    public override void Dispose()
    {
        _redraw?.Dispose();
        _self?.Dispose();

        if (_module != null)
            _ = _module.DisposeAsync().AsTask();

        base.Dispose();
    }
}
