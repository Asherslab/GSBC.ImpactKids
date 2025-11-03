using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Dialogs.Create;

public partial class CreateDollarStoreEntryDialog : ComponentBase
{
    [Parameter]
    public Guid? ServiceId { get; set; }
    
    private readonly CreateDollarStoreEntryRequest _request = new();
    private          BasicResponse?                _response;

    private Service? _service;
    
    private async Task Submit()
    {
        _request.ServiceId = ServiceId ?? _service?.Id ?? Guid.Empty; // validated on backend
        _response = await DollarStoreEntryService.Create(_request);
    }
    
    private async Task<IEnumerable<Service>> SearchFunc(
        string            arg,
        CancellationToken token
    )
    {
        BasicReadMultipleResponse<Service>? response;
        try
        {
            response = await ServicesService.ReadMultiple(
                new ServicesRequest
                {
                    Pagination = null,
                    SearchString = arg,
                },
                token
            );
        }
        catch (Exception e)
        {
            if (e is RpcException { StatusCode: StatusCode.Cancelled })
                return [];
            response = null;
        }

        if (response.HasErrorOrNull())
            Snackbar.AddErrorResponse(response);

        return response?.Entities ?? [];
    }
}