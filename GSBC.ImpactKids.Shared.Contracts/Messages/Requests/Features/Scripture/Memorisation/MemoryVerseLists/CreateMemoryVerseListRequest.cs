namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerseLists;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateMemoryVerseListRequest
{
    public string Name         { get; set; } = null!;
    public Guid?  SchoolTermId { get; set; }
}