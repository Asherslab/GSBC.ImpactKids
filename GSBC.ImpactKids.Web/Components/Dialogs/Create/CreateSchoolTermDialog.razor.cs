using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Create;

public partial class CreateSchoolTermDialog
{
    [Parameter]
    public required SchoolTerm Term { get; set; }

    private readonly CreateSchoolTermRequest _request = new();
    private          BasicResponse?          _response;

    private async Task Submit() => _response = await SchoolTermsService.Create(_request);
}