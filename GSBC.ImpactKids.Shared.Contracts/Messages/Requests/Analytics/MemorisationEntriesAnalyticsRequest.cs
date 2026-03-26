namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Analytics;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemorisationEntriesAnalyticsRequest
{
    public Guid MemoryVerseListId { get; set; }
}