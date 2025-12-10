using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Dialogs.Create;

public partial class CreatePersonDialog
{
    [Parameter]
    public required Person Person { get; set; }

    private readonly CreatePersonRequest _request = new();
    private          BasicResponse?          _response;

    private async Task Submit() => _response = await PersonService.Create(_request);
}