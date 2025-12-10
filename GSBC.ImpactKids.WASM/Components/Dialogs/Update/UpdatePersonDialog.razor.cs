using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Dialogs.Update;

public partial class UpdatePersonDialog
{
    [Parameter]
    public required Person Person { get; set; }

    private readonly UpdatePersonRequest _request = new();
    private          BasicResponse?          _response;
    
    protected override void OnInitialized()
    {
        base.OnInitialized();

        _request.Guid = Person.Id;
        _request.FirstName.SetInitialValue(Person.FirstName);
        _request.LastName.SetInitialValue(Person.LastName);
    }

    private async Task Submit() => _response = await PersonService.Update(_request);
}