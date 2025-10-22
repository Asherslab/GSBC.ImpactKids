using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Create;

public partial class CreatePersonDialog
{
    [Parameter]
    public required Person Person { get; set; }

    private readonly CreatePersonRequest _request = new();
    private          BasicResponse?          _response;

    private async Task Submit() => _response = await PeopleService.Create(_request);
}