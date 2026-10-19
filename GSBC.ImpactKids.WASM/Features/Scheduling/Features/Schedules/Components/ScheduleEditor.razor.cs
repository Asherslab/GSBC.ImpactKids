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
    private ImmutableList<ScheduleEntry>         _entries       = [];
    private ImmutableDictionary<Guid, TimedItem> _timed         = ImmutableDictionary<Guid, TimedItem>.Empty;
    private ImmutableList<ScheduleItemTemplate>  _itemTemplates = [];
    private bool                                 _underway;

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

        _entries = ScheduleLayout.Entries(_schedule);
        _timed = ScheduleTimeline.Compute(_schedule, LocalDate ?? DateTime.Today);
        _underway = LocalDate != null && ScheduleTimeline.IsUnderway(_schedule, LocalDate.Value, DateTime.Now);
    }

    private DateTime? StartsAt => _timed.Values.Where(t => t.Start != null).Select(t => t.Start).Min();
    private DateTime? EndsAt   => _timed.Values.Where(t => t.End != null).Select(t => t.End).Max();

    /// <summary>
    /// −1 / +1 / +5 belong to the night itself, so they show only on a service's schedule on its day.
    /// Planning changes a length by tapping the item; keeping three buttons on every row all week is
    /// most of what made the page hard to read.
    /// </summary>
    private bool ShowAdjust => LocalDate?.Date == DateTime.Today;

    private string LaneName(ScheduleLane lane)
    {
        List<string> names = _schedule!.Groups.Where(g => lane.GroupIds.Contains(g.Id)).Select(g => g.Name).ToList();
        return names.Count == 0 ? "No group" : string.Join(", ", names);
    }

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

    /// <summary>An item dropped into a split's lane, or into the main list when <paramref name="laneId"/> is "main".</summary>
    [JSInvokable]
    public Task OnItemDropped(string itemId, string laneId, int position)
    {
        Guid  item = Guid.Parse(itemId);
        Guid? lane = Guid.TryParse(laneId, out Guid parsed) ? parsed : null;

        return Send(
            schedule => ScheduleEdits.MoveItem(schedule, item, lane, position),
            () => ScheduleService.MoveItem(new MoveScheduleItemRequest { ItemId = item, LaneId = lane, Position = position })
        );
    }

    /// <summary>A split dropped at a new place in the main list.</summary>
    [JSInvokable]
    public Task OnSplitDropped(string blockId, int position)
    {
        Guid block = Guid.Parse(blockId);

        return Send(
            schedule => ScheduleEdits.MoveBlock(schedule, block, position),
            () => ScheduleService.MoveBlock(new MoveScheduleBlockRequest { BlockId = block, Position = position })
        );
    }

    private Task Adjust(ScheduleItem item, int minutes) =>
        Send(
            schedule => ScheduleEdits.Adjust(schedule, item.Id, minutes, _underway),
            () => ScheduleService.AdjustItem(new AdjustScheduleItemRequest { ItemId = item.Id, Minutes = minutes })
        );

    /// <summary>To the end of a split's lane, or of the main list when <paramref name="laneId"/> is null.</summary>
    private Task AddFromTemplate(Guid? laneId, Guid templateId) =>
        Send(null, () => ScheduleService.AddItem(new AddScheduleItemRequest
        {
            ScheduleId = ScheduleId,
            LaneId = laneId,
            ItemTemplateId = templateId
        }));

    private Task EditItem(ScheduleItem item) =>
        ScheduleItemDialog.Open(DialogService, item, ScheduleId, null, _underway);

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

    private async Task ChooseLaneGroups(ScheduleLane lane)
    {
        if (_schedule == null)
            return;

        if (_schedule.Groups.Count == 0)
        {
            Snackbar.Add("Add groups first, with the + beside Groups", MudBlazor.Severity.Info);
            return;
        }

        List<Guid>? groupIds = await LaneGroupsDialog.Ask(DialogService, _schedule.Groups, lane);

        if (groupIds == null)
            return;

        await Send(
            schedule => ScheduleEdits.SetLaneGroups(schedule, lane.Id, groupIds),
            () => ScheduleService.SetLaneGroups(new SetScheduleLaneGroupsRequest { LaneId = lane.Id, GroupIds = groupIds })
        );
    }

    // Splits and lanes \\

    /// <summary>A split on the end of the main list, a lane per group when there are two or more groups.</summary>
    private Task AddSplit() =>
        Send(null, () => ScheduleService.AddBlock(new AddScheduleBlockRequest
        {
            ScheduleId = ScheduleId,
            Lanes = Math.Clamp(_schedule?.Groups.Count ?? 0, 2, ScheduleRules.MaxLanes)
        }));

    private Task AddLane(ScheduleBlock block) =>
        Send(null, () => ScheduleService.AddLane(new BasicReadRequest { Guid = block.Id }));

    private async Task RemoveBlock(ScheduleBlock block)
    {
        int items = block.Lanes.Sum(l => l.Items.Count);

        if (items > 0)
        {
            bool? confirmed = await DialogService.ShowMessageBoxAsync(
                "Remove this split?",
                $"Its {items} item{(items == 1 ? "" : "s")} will be removed with it.",
                yesText: "Remove", cancelText: "Cancel");

            if (confirmed != true)
                return;
        }

        await Send(null, () => ScheduleService.RemoveBlock(new BasicReadRequest { Guid = block.Id }));
    }

    private async Task RemoveLane(ScheduleLane lane)
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
