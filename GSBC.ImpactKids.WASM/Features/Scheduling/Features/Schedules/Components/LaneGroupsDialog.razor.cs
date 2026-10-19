using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

public partial class LaneGroupsDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public ImmutableList<ScheduleGroup> Groups { get; set; } = [];

    [Parameter]
    public ImmutableList<Guid> Selected { get; set; } = [];

    private HashSet<Guid> _selected = [];

    protected override void OnInitialized() => _selected = Selected.ToHashSet();

    private void Toggle(Guid id, bool on)
    {
        if (on)
            _selected.Add(id);
        else
            _selected.Remove(id);
    }

    /// <summary>The lane's groups as chosen, or null when cancelled.</summary>
    public static async Task<List<Guid>?> Ask(IDialogService dialogService, ImmutableList<ScheduleGroup> groups, ScheduleLane lane)
    {
        IDialogReference dialog = await dialogService.ShowAsync<LaneGroupsDialog>(
            "Groups in this lane",
            new DialogParameters<LaneGroupsDialog>
            {
                { x => x.Groups, groups },
                { x => x.Selected, lane.GroupIds }
            },
            new DialogOptions { FullWidth = true, MaxWidth = MaxWidth.ExtraSmall });

        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: List<Guid> ids } ? ids : null;
    }
}
