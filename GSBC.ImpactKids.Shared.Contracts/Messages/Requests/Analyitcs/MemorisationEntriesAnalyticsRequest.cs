namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analyitcs;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemorisationEntriesAnalyticsRequest
{
    public Guid MemoryVerseListId { get; set; }
}