using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.Services;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Dialogs.Update;

public partial class UpdateServiceDialog
{
    [Parameter]
    public required Service Service { get; set; }

    [Parameter]
    public SchoolTerm? SchoolTerm { get; set; }

    private readonly UpdateServiceRequest _request = new();
    private          BasicResponse?       _response;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (SchoolTerm == null && Service.SchoolTerm?.Id != null)
        {
            BasicReadResponse<SchoolTerm>? resp = await SchoolTermsService.Read(new SchoolTermRequest
            {
                Guid = Service.SchoolTerm.Id
            });

            if (resp.HasErrorOrNull())
                Snackbar.AddErrorResponse(resp);

            SchoolTerm = resp?.Entity;
        }

        _request.Guid = Service.Id;
        _request.Name.SetInitialValue(Service.Name);
        _request.LocalDate.SetInitialValue(Service.LocalDate);
        _request.SchoolTermId.SetInitialValue(Service.SchoolTerm?.Id);
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