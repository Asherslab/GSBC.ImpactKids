using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Dialogs.Update;

public partial class UpdateDollarStoreEntryDialog
{
    [Parameter]
    public required DollarStoreEntry Entry { get; set; }
    
    [Parameter]
    public required Guid ServiceId { get; set; }

    private readonly UpdateDollarStoreEntryRequest _request = new();
    private          BasicResponse?       _response;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        _request.Guid = Entry.Id;
        _request.DollarDoosMade.SetInitialValue(Entry.DollarDoosMade);
        _request.Notes.SetInitialValue(Entry.Notes);
    }

    private async Task Submit()
    {
        _response = await DollarStoreEntryService.Update(_request);
    }
}