using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.Web.Components.Dialogs.Update;

public partial class UpdateSchoolTermDialog
{
    [Parameter]
    public required SchoolTerm Term { get; set; }

    private readonly UpdateSchoolTermRequest _request = new();
    private          BasicResponse?          _response;
    
    protected override void OnInitialized()
    {
        base.OnInitialized();

        _request.Guid = Term.Id;
        _request.Name.SetInitialValue(Term.Name);
        _request.StartDate.SetInitialValue(Term.StartDate);
        _request.EndDate.SetInitialValue(Term.EndDate);
    }

    private async Task Submit() => _response = await SchoolTermsService.Update(_request);
}