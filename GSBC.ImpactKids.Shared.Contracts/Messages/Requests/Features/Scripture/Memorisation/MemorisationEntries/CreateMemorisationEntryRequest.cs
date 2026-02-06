namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemorisationEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record CreateMemorisationEntryRequest
{
    public required Guid PersonId      { get; init; }
    public required Guid MemoryVerseId { get; init; }
    public required Guid ServiceId     { get; init; }

    public bool VerseRecited         { get; init; }
    public bool FiveDollaryDoosGiven { get; init; }
    public bool OneDollaryDooGiven   { get; init; }
}