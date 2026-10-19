using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Components;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Pages;

public partial class TemplateEditor
{
    [Parameter]
    public Guid Id { get; set; }

    private Schedule?                  _template;
    private ImmutableList<ServiceType> _serviceTypes = [];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SchedulesStore, Retrieve);
        HandleSubscriptionDisposal(ServiceTypesStore, Retrieve);
        Retrieve();

        await Task.WhenAll(SchedulesStore.RefreshAll(), ServiceTypesStore.RefreshAll());
    }

    protected override void OnParametersSet() => Retrieve();

    private void Retrieve()
    {
        _template = SchedulesStore.GetState().Entities.Data?.FirstOrDefault(x => x.Id == Id);
        _serviceTypes = ServiceTypesStore.GetState().Entities.Data?.OrderBy(x => x.Label).ToImmutableList() ?? [];
        StateHasChanged();
    }

    private async Task Rename()
    {
        if (_template == null)
            return;

        string? name = await TextPromptDialog.Ask(DialogService, "Rename template", "Name", _template.Name);

        if (name == null)
            return;

        UpdateScheduleTemplateRequest request = UpdateScheduleTemplateRequest.FromEntity(_template);
        request.Name = name;
        await Save(request);
    }

    private Task SetDefault(Guid? serviceTypeId)
    {
        if (_template == null)
            return Task.CompletedTask;

        UpdateScheduleTemplateRequest request = UpdateScheduleTemplateRequest.FromEntity(_template);
        request.DefaultForServiceTypeId = serviceTypeId;
        return Save(request);
    }

    private async Task Save(UpdateScheduleTemplateRequest request)
    {
        BasicResponse response = await ScheduleService.Update(request);

        Snackbar.AddErrorResponse(response);
        await SchedulesStore.RefreshEvent();
    }
}
