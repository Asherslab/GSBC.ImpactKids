using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Components.Base;
using GSBC.ImpactKids.Web.Components.Dialogs.Create;
using GSBC.ImpactKids.Web.Components.Dialogs.Update;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.Web.Components.Pages.Terms;

public partial class ServicesList : EventListeningComponent
{
    [Parameter]
    public required SchoolTerm SchoolTerm { get; set; }

    private ICollection<Service>? _services;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await RefreshServices();
        await SubscribeToEvent(Service.BuildSubscription(SchoolTerm.Id), RefreshServices);
    }

    private async Task RefreshServices()
    {
        BasicReadMultipleResponse<Service>? response = await ServicesService.ReadMultiple(
            new ServicesRequest
            {
                SchoolTermId = SchoolTerm.Id
            }
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        _services = response.Entities;
        StateHasChanged();
    }

    private async Task CreateService()
    {
        DialogParameters<CreateServiceDialog> parameters = new()
        {
            { x => x.SchoolTerm, SchoolTerm }
        };

        DialogOptions opts = new()
        {
            FullWidth = true
        };
        
        await DialogService.ShowAsync<CreateServiceDialog>("Create Service", parameters, opts);
    }
}