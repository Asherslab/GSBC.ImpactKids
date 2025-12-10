using GSBC.ImpactKids.Shared.Contracts.Entities;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class UpdateMemorisationEntryRequest
{
    public Guid PersonId      { get; set; }
    public Guid ServiceId     { get; set; }
    public Guid MemoryVerseId { get; set; }

    public DeltaUpdate<bool> VerseRecited         { get; set; } = new();
    public DeltaUpdate<bool> FiveDollaryDoosGiven { get; set; } = new();
    public DeltaUpdate<bool> OneDollaryDooGiven   { get; set; } = new();
}