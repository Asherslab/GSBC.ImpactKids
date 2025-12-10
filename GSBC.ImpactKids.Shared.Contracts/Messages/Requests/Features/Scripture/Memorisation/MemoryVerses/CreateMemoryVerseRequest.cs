namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Scripture.Memorisation.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateMemoryVerseRequest
{
    public string ReferenceName     { get; set; }
    public string Verse             { get; set; }
    public Guid   MemoryVerseListId { get; set; }

    public List<Guid> ServiceIds    { get; set; } = [];
    public List<Guid> BibleVerseIds { get; set; } = [];
}