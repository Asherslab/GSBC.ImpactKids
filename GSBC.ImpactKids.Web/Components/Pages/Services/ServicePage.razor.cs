using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Components.Dialogs.Update;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.Services;

public partial class ServicePage : EventListeningComponent
{
    [SupplyParameterFromQuery]
    public Guid? Id { get; set; }
    
    [SupplyParameterFromQuery]
    public bool Previous { get; set; }
    
    [SupplyParameterFromQuery]
    public bool Upcoming { get; set; }
    
    private Service?                  _service;
    private ICollection<MemoryVerse>? _memoryVerses;

    // use OnParametersSet because changing between pages of different query values does not reload with OnInitialized
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        
        await RefreshService();
        await SubscribeToEvent(Service.BuildSubscription(_service?.SchoolTermId, _service?.Id), RefreshService);
        
        await RefreshMemoryVerses();
        await SubscribeToEvent(MemoryVerse.BuildSubscription(), RefreshMemoryVerses); // We subscribe to all memory verse changes since filtering by service is complex
    }

    private async Task RefreshService()
    {
        BasicReadResponse<Service>? response = await ServicesService.Read(
            new ServiceRequest
            {
                Guid = Id ?? Guid.Empty,
                PreviousService = Previous,
                UpcomingService = Upcoming
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _service = response.Entity;
        StateHasChanged();
    }

    private async Task RefreshMemoryVerses()
    {
        if (_service == null)
            return;
        
        BasicReadMultipleResponse<MemoryVerse>? response = await MemoryVersesService.ReadMultiple(
            new MemoryVersesRequest
            {
                ServiceId = _service.Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _memoryVerses = response.Entities;
        StateHasChanged();
    }
    
    private async Task UpdateService()
    {
        DialogParameters<UpdateServiceDialog> parameters = new()
        {
            { x => x.Service, _service }
        };

        await DialogService.ShowAsync<UpdateServiceDialog>("Update Service", parameters);
    }
    
    private async Task DeleteService()
    {
        bool? result = await DialogService.ShowMessageBox(
            "Warning", 
            "Deleting can not be undone!", 
            yesText:"Delete!", cancelText:"Cancel");
        
        if (result == null || _service == null)
            return;
        
        BasicReadRequest request = new()
        {
            Guid = _service.Id
        };

        await ServicesService.Delete(request);
        Navigation.NavigateTo("/terms");
    }
    
    private string PageName()
    {
        if (_service?.Name != null)
            return _service.Name;
        if (Previous)
            return "Previous Service";
        if (Upcoming)
            return "Upcoming Service";
        return "Service";
    }
}