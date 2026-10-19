using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Pages;

public partial class Templates
{
    private AsyncData<ImmutableList<Schedule>> _schedules    = AsyncData<ImmutableList<Schedule>>.NotAsked();
    private ImmutableList<ServiceType>         _serviceTypes = [];

    private IEnumerable<Schedule>? TemplateList =>
        _schedules.Data?.Where(x => x.IsTemplate).OrderBy(x => x.Name);

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SchedulesStore, Retrieve);
        HandleSubscriptionDisposal(ServiceTypesStore, Retrieve);
        Retrieve();

        await Task.WhenAll(SchedulesStore.RefreshAll(), ServiceTypesStore.RefreshAll());
    }

    private void Retrieve()
    {
        _schedules = SchedulesStore.GetState().Entities;
        _serviceTypes = ServiceTypesStore.GetState().Entities.Data ?? [];
        StateHasChanged();
    }

    private string ServiceTypeLabel(Guid? id) =>
        id == null ? "" : _serviceTypes.FirstOrDefault(x => x.Id == id)?.Label ?? "";

    private async Task Create()
    {
        string? name = await TextPromptDialog.Ask(DialogService, "New schedule template", "Name",
            placeholder: "Friday Impact Kids", submitText: "Create");

        if (name == null)
            return;

        BasicReadResponse<Guid?> response = await ScheduleService.Create(new CreateScheduleTemplateRequest { Name = name });

        if (Snackbar.AddErrorResponse(response) || response.Entity == null)
            return;

        await SchedulesStore.RefreshEvent();
        Navigation.NavigateTo($"/Schedules/Templates/{response.Entity}");
    }

    private async Task Delete(Schedule template)
    {
        if (await DeleteWithDialog(ScheduleService, template.Id) == DeleteResult.Success)
            await SchedulesStore.RefreshEvent();
    }
}
