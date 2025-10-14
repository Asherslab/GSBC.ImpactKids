using Grpc.Core;
using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Update;

public partial class UpdateMemoryVerseListDialog
{
    [Parameter]
    public required MemoryVerseList List { get; set; }

    [Parameter]
    public SchoolTerm? SchoolTerm { get; set; }

    private readonly UpdateMemoryVerseListRequest _request = new();
    private          BasicResponse?       _response;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (SchoolTerm == null && List.SchoolTermId != null)
        {
            BasicReadResponse<SchoolTerm>? resp = await SchoolTermsService.Read(new SchoolTermRequest
            {
                Guid = List.SchoolTermId.Value
            });
        
            if (resp.HasErrorOrNull())
                Snackbar.AddErrorResponse(resp);
        
            SchoolTerm = resp?.Entity;
        }

        _request.Guid = List.Id;
        _request.Name.SetInitialValue(List.Name);
        _request.SchoolTermId.SetInitialValue(List.SchoolTermId);
    }

    private async Task Submit()
    {
        _response = await MemoryVerseListsService.Update(_request);
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