using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.DollarStoreEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateDollarStoreEntryRequest
    : ReadRequestBase, IUpdateRequest<DollarStoreEntry, UpdateDollarStoreEntryRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<int?>    DollarDoosMade { get; set; } = new();
    public DeltaUpdate<string?> Notes          { get; set; } = new();

    public static UpdateDollarStoreEntryRequest FromEntity(DollarStoreEntry entity)
    {
        UpdateDollarStoreEntryRequest request = new()
        {
            Guid = entity.Id
        };

        request.DollarDoosMade.SetInitialValue(entity.DollarDoosMade);
        request.Notes.SetInitialValue(entity.Notes);

        return request;
    }
}