namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.DollarStore;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateDollarStoreEntryRequest
{
    public Guid ServiceId { get; set; }

    public int?    DollarDoosMade { get; set; }
    public string? Notes          { get; set; }
}