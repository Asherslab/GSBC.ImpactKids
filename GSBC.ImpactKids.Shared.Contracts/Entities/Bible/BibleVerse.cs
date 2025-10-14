namespace GSBC.ImpactKids.Shared.Contracts.Entities.Bible;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BibleVerse
{
    public          int    BookNumber    { get; set; }
    public required string BookName      { get; set; }
    public          int    ChapterNumber { get; set; }
    public          int    VerseNumber   { get; set; }

    public required string Verse { get; set; }
}