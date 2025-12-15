using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scheduling.School.SchoolTerms;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Dialogs.Update;

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
        _request.LocalStartDate.SetInitialValue(Term.LocalStartDate);
        _request.LocalEndDate.SetInitialValue(Term.LocalEndDate);
    }

    private async Task Submit() => _response = await SchoolTermsService.Update(_request);
}