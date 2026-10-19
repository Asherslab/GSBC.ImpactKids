using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Pages;

public partial class ItemTemplates
{
    private AsyncData<ImmutableList<ScheduleItemTemplate>> _templates =
        AsyncData<ImmutableList<ScheduleItemTemplate>>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(ItemTemplatesStore, RetrieveTemplates);
        RetrieveTemplates();

        await ItemTemplatesStore.RefreshAll();
    }

    private void RetrieveTemplates()
    {
        _templates = ItemTemplatesStore.GetState().Entities;
        StateHasChanged();
    }

    private async Task Create() =>
        await DetailsComponentDialog.Open<ScheduleItemTemplateDetails>(DialogService, "Add Schedule Item",
            ModificationState.Creating);

    private async Task Edit(ScheduleItemTemplate template) =>
        await DetailsComponentDialog.Open<ScheduleItemTemplateDetails>(DialogService, "Edit Schedule Item",
            ModificationState.Updating, template.Id);

    private async Task Delete(ScheduleItemTemplate template) =>
        await DeleteWithDialog(ItemTemplatesService, template.Id);
}
