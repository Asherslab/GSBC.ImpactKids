using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Update;

public partial class UpdateServiceDialog
{
    [Parameter]
    public required Service Service { get; set; }

    public SchoolTerm? SchoolTerm { get; set; }

    private readonly UpdateServiceRequest _request = new();
    private          BasicResponse?       _response;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (SchoolTerm == null)
        {
            BasicReadResponse<SchoolTerm>? resp = await SchoolTermsService.Read(new BasicReadRequest
            {
                Guid = Service.SchoolTermId
            });

            if (resp.HasErrorOrNull())
                Snackbar.AddErrorResponse(resp);

            SchoolTerm = resp?.Entity;
        }

        _request.Guid = Service.Id;
        _request.Name.SetInitialValue(Service.Name);
        _request.Date.SetInitialValue(Service.Date);
        _request.SchoolTermId.SetInitialValue(Service.SchoolTermId);
    }

    private async Task Submit()
    {
        if (_request.SchoolTermId.Value != SchoolTerm?.Id)
            _request.SchoolTermId.Value = SchoolTerm?.Id ?? Guid.Empty; // backend will validate
        _response = await ServicesService.Update(_request);
    }

    private async Task<IEnumerable<SchoolTerm>> SearchFunc(
        string            arg,
        CancellationToken token
    )
    {
        BasicReadMultipleResponse<SchoolTerm>? response;
        try
        {
            response = await SchoolTermsService.ReadMultiple(
                new SchoolTermsRequest
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