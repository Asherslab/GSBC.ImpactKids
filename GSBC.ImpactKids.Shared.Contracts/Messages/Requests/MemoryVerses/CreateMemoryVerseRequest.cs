namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateMemoryVerseRequest
{
    public required string ReferenceName     { get; set; }
    public required string Verse             { get; set; }
    public required Guid   MemoryVerseListId { get; set; }

    public required List<AttachedMemoryVerse> AttachedMemoryVerses { get; set; } = [];
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class AttachedMemoryVerse
{
    public required int BookNumber    { get; set; }
    public required int ChapterNumber { get; set; }
    public required int VerseNumber   { get; set; }
}