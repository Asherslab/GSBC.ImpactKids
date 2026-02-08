using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.WASM.Features.Attendance.Pages;

public partial class Tool
{
    private AsyncData<Service> _service = AsyncData<Service>.NotAsked();
    
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await Task.WhenAll(
            ServicesStore.RefreshAll()
        );
    }
}