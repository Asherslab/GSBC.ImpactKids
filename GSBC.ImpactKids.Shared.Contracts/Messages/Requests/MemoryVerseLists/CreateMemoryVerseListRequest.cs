namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerseLists;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateMemoryVerseListRequest
{
    public string Name         { get; set; } = null!;
    public Guid?  SchoolTermId { get; set; }
}