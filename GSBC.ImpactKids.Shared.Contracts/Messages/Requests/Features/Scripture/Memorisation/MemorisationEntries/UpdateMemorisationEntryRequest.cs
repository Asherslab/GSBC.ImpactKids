using GSBC.ImpactKids.Shared.Contracts.Entities;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemorisationEntryRequest
    : ReadRequestBase, IUpdateRequest<MemorisationEntry, UpdateMemorisationEntryRequest>
{
    public override string Id { get; set; } = null!;

    public DeltaUpdate<bool> VerseRecited         { get; set; } = new();
    public DeltaUpdate<bool> FiveDollaryDoosGiven { get; set; } = new();
    public DeltaUpdate<bool> OneDollaryDooGiven   { get; set; } = new();

    public static UpdateMemorisationEntryRequest FromEntity(MemorisationEntry entity)
    {
        UpdateMemorisationEntryRequest request = new()
        {
            Guid = entity.Id
        };

        request.VerseRecited.SetInitialValue(entity.VerseRecited);
        request.FiveDollaryDoosGiven.SetInitialValue(entity.FiveDollaryDoosGiven);
        request.OneDollaryDooGiven.SetInitialValue(entity.OneDollaryDooGiven);

        return request;
    }
}