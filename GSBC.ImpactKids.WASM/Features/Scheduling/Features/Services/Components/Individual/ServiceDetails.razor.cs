using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Entities.Pagination;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.Scheduling.Features.Services.Components.Individual;

public partial class ServiceDetails
{
    [Parameter]
    public Service? Service { get; set; }

    [Parameter]
    public ModificationState State { get; set; }

    [Parameter]
    public ICollection<SchoolTerm>? SchoolTerms { get; set; }

    [Parameter]
    public EventCallback<ICollection<SchoolTerm>?> SchoolTermsChanged { get; set; }

    [Parameter]
    public ICollection<ServiceType>? ServiceTypes { get; set; }

    [Parameter]
    public EventCallback<ICollection<ServiceType>?> ServiceTypesChanged { get; set; }

    private readonly CreateServiceRequest _createRequest = new();
    private          UpdateServiceRequest _updateRequest = new();

    private bool _waitingForRefresh;

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        if (State == ModificationState.Reading && Service != null)
            _waitingForRefresh = false;
        
        if (Service != null && !_waitingForRefresh)
        {
            _updateRequest = new UpdateServiceRequest
            {
                Guid = Service.Id
            };

            _updateRequest.Name.SetInitialValue(Service.Name);
            _updateRequest.LocalDate.SetInitialValue(Service.LocalDate); // Set Date for LocalDate usage

            _updateRequest.SchoolTermId.SetInitialValue(Service.SchoolTerm?.Id);
            _updateRequest.SchoolTerm = Service.SchoolTerm;
            
            _updateRequest.ServiceTypeId.SetInitialValue(Service.ServiceType?.Id);
            _updateRequest.ServiceType = Service.ServiceType;
        }
        else if (State != ModificationState.Creating)
        {
            _waitingForRefresh = true;
        }

        if (State != ModificationState.Reading)
        {
            List<Task> tasks = [];

            if (SchoolTerms == null)
                tasks.Add(RefreshSchoolTerms());

            if (ServiceTypes == null)
                tasks.Add(RefreshServiceTypes());

            await Task.WhenAll(tasks);
        }
    }

    private CancellationTokenSource _refreshSchoolTermsTokenSource = new();

    private async Task RefreshSchoolTerms()
    {
        await _refreshSchoolTermsTokenSource.CancelAsync();
        _refreshSchoolTermsTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<SchoolTerm>? response = await SchoolTermsService.ReadMultiple(
            new SchoolTermsRequest
            {
                Pagination = PaginationRequest.All(),
            },
            _refreshSchoolTermsTokenSource.Token
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        SchoolTerms = response.Entities;
        await SchoolTermsChanged.InvokeAsync(SchoolTerms);
    }

    private CancellationTokenSource _refreshServiceTypesTokenSource = new();

    private async Task RefreshServiceTypes()
    {
        await _refreshServiceTypesTokenSource.CancelAsync();
        _refreshServiceTypesTokenSource = new CancellationTokenSource();

        BasicReadMultipleResponse<ServiceType>? response = await ServiceTypeService.ReadMultiple(
            new BasicReadMultipleRequest
            {
                Pagination = PaginationRequest.All(),
            },
            _refreshServiceTypesTokenSource.Token
        );

        if (response.HasErrorOrNull())
        {
            Snackbar.AddErrorResponse(response);
            return;
        }

        ServiceTypes = response.Entities;
        await ServiceTypesChanged.InvokeAsync(ServiceTypes);
    }

    private ICollection<SchoolTerm> GetSchoolTermsForDropdown()
    {
        if (SchoolTerms == null)
            return [];

        switch (State)
        {
            case ModificationState.Creating:
            {
                int year = _createRequest.LocalDate.Year;

                return SchoolTerms
                    .Where(x => x.LocalStartDate.Year == year)
                    .ToList();
            }
            case ModificationState.Updating:
            {
                int year = _updateRequest.LocalDate.Value.Year;

                return SchoolTerms
                    .Where(x => x.LocalStartDate.Year == year)
                    .ToList();
            }
            case ModificationState.Reading:
            default:
                return SchoolTerms;
        }
    }

    public async Task<bool> CreateService()
    {
        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await ServicesService.Create(_createRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }

    public async Task<bool> UpdateService()
    {
        if (_updateRequest.Guid == Guid.Empty)
            return false;

        _waitingForRefresh = true;
        StateHasChanged();
        BasicResponse? resp = await ServicesService.Update(_updateRequest);

        if (resp.HasErrorOrNull())
        {
            _waitingForRefresh = false;
            Snackbar.AddErrorResponse(resp);
            return false;
        }

        return true;
    }
}