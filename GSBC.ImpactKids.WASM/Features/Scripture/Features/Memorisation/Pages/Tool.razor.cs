using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scripture.Features.Memorisation.Pages;

public partial class Tool : IDisposable
{
    [Parameter]
    public Guid ServiceId { get; set; }

    [SupplyParameterFromQuery]
    public bool Previous { get; set; }

    [SupplyParameterFromQuery]
    public bool Upcoming { get; set; }

    [SupplyParameterFromQuery]
    public Guid? MemoryVerseId { get; set; }

    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();
    private IDisposable?       _subscription;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _subscription = Store.Subscribe(_ => { SetQueryParametersIfNecessary(); });

        await Task.WhenAll(
            ServicesStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (ParametersDifferFromState())
            await Update(s =>
                s.SetServiceId(ServiceId)
                    .SetPrevious(Previous)
                    .SetUpcoming(Upcoming)
                    .SetMemoryVerseId(MemoryVerseId)
            );

        RetrieveService();
    }

    private void RetrieveService()
    {
        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (!services.HasData)
        {
            _service = _service.CopyStatus(services);
            StateHasChanged();
            return;
        }

        Service? service = null;
        if (State.ServiceId != Guid.Empty)
            service = services.Data!
                .FirstOrDefault(x => x.Id == State.ServiceId);
        else if (State.Previous)
            service = services.Data!
                .OrderByDescending(x => x.LocalDate)
                .FirstOrDefault(x => x.LocalDate.Date <= DateTime.Now.Date);
        else if (State.Upcoming)
            service = services.Data!
                .OrderBy(x => x.LocalDate)
                .FirstOrDefault(x => x.LocalDate.Date >= DateTime.Now.Date);

        _service = service == null
            ? _service.ToFailure("Failed to find Service")
            : _service.ToSuccess(service);
        StateHasChanged();
    }

    private async Task OnMemoryVerseIdChanged(Guid? memoryVerseId)
    {
        await Update(s => s.SetMemoryVerseId(memoryVerseId));
    }

    private bool ParametersDifferFromState()
    {
        return ServiceId != State.ServiceId ||
               Previous != State.Previous ||
               Upcoming != State.Previous ||
               MemoryVerseId != State.MemoryVerseId;
    }

    private void SetQueryParametersIfNecessary()
    {
        if (!ParametersDifferFromState())
            return;

        Console.WriteLine($"GET QUERY PARAMS: {ServiceId} | {State.ServiceId}");
        Navigation.NavigateTo(GetQueryParameters());
    }

    private string GetQueryParameters()
    {
        return Navigation.GetUriWithQueryParameters($"/Scripture/Memorisation/Tool/{ServiceId}",
            new Dictionary<string, object?>
            {
                [nameof(Previous)] = State.Previous,
                [nameof(Upcoming)] = State.Upcoming,
                [nameof(MemoryVerseId)] = State.MemoryVerseId
            });
    }

    public new void Dispose()
    {
        base.Dispose();
        _subscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}