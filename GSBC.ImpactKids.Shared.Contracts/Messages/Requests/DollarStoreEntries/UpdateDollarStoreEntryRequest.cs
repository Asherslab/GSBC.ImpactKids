using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateDollarStoreEntryRequest : ReadRequestBase
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<int?>    DollarDoosMade { get; set; } = new();
    public DeltaUpdate<string?> Notes          { get; set; } = new();
}