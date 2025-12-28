using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDetails
{
    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    private          AsyncData<Service>   _service       = AsyncData<Service>.NotAsked();
    private readonly CreateServiceRequest _createRequest = new();
    private          UpdateServiceRequest _updateRequest = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        ServicesStore.Subscribe(_ => RetrieveService());
        ServiceTypesStore.Subscribe(_ => StateHasChanged());
        SchoolTermsStore.Subscribe(_ => StateHasChanged());

        await Task.WhenAll(
            ServicesStore.RefreshAll(),
            ServiceTypesStore.RefreshAll(),
            SchoolTermsStore.RefreshAll()
        );
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        RetrieveService();
    }

    private void RetrieveService()
    {
        if (State == ModificationState.Creating)
            return;

        AsyncData<ImmutableList<Service>> services = ServicesStore.GetState().Entities;

        if (!services.HasData)
        {
            _service = _service.CopyStatus(services);
            StateHasChanged();
            return;
        }

        Service? service = services.Data!
            .FirstOrDefault(x => x.Id == Id);

        if (service == null)
        {
            _service = _service.ToFailure("Failed to find Service");
            _updateRequest = new UpdateServiceRequest();
            StateHasChanged();
            return;
        }

        _service = _service.ToSuccess(service);

        _updateRequest = new UpdateServiceRequest
        {
            Guid = service.Id,
        };

        _updateRequest.Name.SetInitialValue(service.Name);
        _updateRequest.LocalDate.SetInitialValue(service.LocalDate); // Set Date for LocalDate usage

        _updateRequest.SchoolTermId.SetInitialValue(service.SchoolTermId);
        _updateRequest.ServiceTypeId.SetInitialValue(service.ServiceTypeId);

        StateHasChanged();
    }

    private ICollection<SchoolTerm> GetSchoolTermsForDropdown()
    {
        ImmutableList<SchoolTerm>? schoolTerms = SchoolTermsStore.GetState().Entities.Data;
        if (schoolTerms == null)
            return [];

        switch (State)
        {
            case ModificationState.Creating:
            {
                int year = _createRequest.LocalDate.Year;

                return schoolTerms
                    .Where(x => x.LocalStartDate.Year == year)
                    .ToList();
            }
            case ModificationState.Updating:
            {
                int? year = _updateRequest.LocalDate.Value.Year;

                return schoolTerms
                    .Where(x => x.LocalStartDate.Year == year)
                    .ToList();
            }
            case ModificationState.Reading:
            default:
                return schoolTerms;
        }
    }

    public async Task<bool> CreateService()
    {
        _service = _service.ToLoading();
        StateHasChanged();
        BasicResponse resp = await ServicesService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            RetrieveService();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateService()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _service = _service.ToLoading();
        StateHasChanged();
        BasicResponse resp = await ServicesService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            RetrieveService();
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task DeleteService()
    {
        if (_service.Data == null)
            return;
        Guid id = _service.Data.Id;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return;

        _service = _service.ToLoading();
        StateHasChanged();
        BasicReadRequest request = new() { Guid = id };
        BasicResponse    resp    = await ServicesService.Delete(request);

        if (!resp.HasErrorOrNull())
            return;

        RetrieveService();
        Snackbar.AddErrorResponse(resp);
    }
}