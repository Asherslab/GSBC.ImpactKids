using System.Collections.Immutable;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Schedules;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Schedules.Pages;

/// <summary>A service's own schedule - planned during the week, and run from a phone on the night.</summary>
public partial class ServiceSchedule
{
    [Parameter]
    public Guid Id { get; set; }

    private Service?                _service;
    private Schedule?               _schedule;
    private ImmutableList<Schedule> _templates = [];
    private bool                    _schedulesLoaded;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(SchedulesStore, Retrieve);
        HandleSubscriptionDisposal(ServicesStore, Retrieve);
        Retrieve();

        await Task.WhenAll(SchedulesStore.RefreshAll(), ServicesStore.RefreshAll());
    }

    protected override void OnParametersSet() => Retrieve();

    private void Retrieve()
    {
        ImmutableList<Schedule>? schedules = SchedulesStore.GetState().Entities.Data;

        _schedulesLoaded = schedules != null;
        _schedule = schedules?.FirstOrDefault(x => x.ServiceId == Id);
        _templates = schedules?.Where(x => x.IsTemplate).OrderBy(x => x.Name).ToImmutableList() ?? [];
        _service = ServicesStore.GetState().Entities.Data?.FirstOrDefault(x => x.Id == Id);
        StateHasChanged();
    }

    private async Task ApplyTemplate(Guid? templateId)
    {
        if (templateId == null)
            return;

        if (_schedule != null)
        {
            string name = _templates.FirstOrDefault(x => x.Id == templateId)?.Name ?? "the template";

            bool? confirmed = await DialogService.ShowMessageBoxAsync(
                "Replace this schedule?",
                $"Everything in this service's schedule - fill-ins and time changes included - is replaced by a fresh copy of {name}.",
                yesText: "Replace", cancelText: "Cancel");

            if (confirmed != true)
                return;
        }

        BasicReadResponse<Guid?> response = await ScheduleService.ApplyTemplate(new ApplyScheduleTemplateRequest
        {
            ServiceId = Id,
            TemplateId = templateId.Value
        });

        Snackbar.AddErrorResponse(response);
        await SchedulesStore.RefreshEvent();
    }
}
