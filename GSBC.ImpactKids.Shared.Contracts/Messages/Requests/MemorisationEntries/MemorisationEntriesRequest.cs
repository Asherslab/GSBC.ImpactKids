namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemorisationEntries;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemorisationEntriesRequest : IReadMultipleRequest
{
    public PaginationRequest? Pagination   { get; set; }
    public string?            SearchString { get; set; }

    public required Guid ServiceId     { get; set; }
    public required Guid MemoryVerseId { get; set; }
}