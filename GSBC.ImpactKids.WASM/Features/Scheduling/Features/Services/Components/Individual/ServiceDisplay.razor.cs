using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using Microsoft.AspNetCore.Components;
using MudBlazor.Utilities;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDisplay
{
    [Parameter]
    public bool Link { get; set; }

    [Parameter]
    public EventCallback<Guid> OnDelete { get; set; }
    
    [Parameter]
    public bool AllowDeleting { get; set; }

    private string? Href => Link
        ? Entity.HasData ? $"/Services/{Id}" : null
        : null;

    private AsyncData<ServiceType?> _serviceType = AsyncData<ServiceType?>.NotAsked();

    private string Class => CssBuilder.Empty()
        .AddClass("clickable mud-ripple", Link)
        .AddClass("d-flex justify-start flex-direction-row flex-grow-1")
        .Build();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServiceTypesStore.Subscribe(_ => RetrieveEntity());

        await Task.WhenAll(
            EntityStore.RefreshAll(),
            ServiceTypesStore.RefreshAll()
        );
    }

    protected override void OnRetrievedEntityNull()
    {
        _serviceType = _serviceType.ToFailure("Service Not Found");
    }

    protected override void OnRetrievedEntity()
    {
        if (Entity.Data!.ServiceTypeId == null)
        {
            _serviceType = _serviceType.ToSuccess(null);
            return;
        }

        // compiler throws a warning even if rider doesn't
        // ReSharper disable once RedundantSuppressNullableWarningExpression
        _serviceType = ServiceTypesStore.GetState().First(x => x.Id == Entity.Data.ServiceTypeId)!;
    }

    private Task DeleteClicked()
    {
        if (OnDelete.HasDelegate && Entity.Data != null)
            return OnDelete.InvokeAsync(Entity.Data.Id);

        return DeleteWithDialog(
            DeleteService,
            Entity.Data?.Id,
            () => Entity = Entity.ToLoading(),
            RetrieveEntity
        );
    }
}