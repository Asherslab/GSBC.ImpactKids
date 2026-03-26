using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DollarStore;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Features.DollarStore.Components.Individual;

public partial class DollarStoreEntryDetails
{
    [Parameter]
    public Guid? ServiceId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await Task.WhenAll(EntityStore.RefreshAll());
    }

    protected override bool AlternativeFilter(DollarStoreEntry entity) => entity.ServiceId == ServiceId;

    protected override CreateDollarStoreEntryRequest ModifyCreateRequest(CreateDollarStoreEntryRequest request)
    {
        if (ServiceId != null)
            request.ServiceId = ServiceId.Value;
        return request;
    }
}