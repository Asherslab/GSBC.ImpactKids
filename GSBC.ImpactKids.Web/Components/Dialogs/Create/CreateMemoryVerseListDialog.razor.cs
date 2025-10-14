using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Create;

public partial class CreateMemoryVerseListDialog
{
    [Parameter]
    public SchoolTerm? SchoolTerm { get; set; }

    private readonly CreateMemoryVerseListRequest _request = new();
    private          BasicResponse?               _response;

    private async Task Submit()
    {
        _request.SchoolTermId = SchoolTerm?.Id ?? Guid.Empty; // backend will validate
        _response = await MemoryVerseListsService.Create(_request);
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