namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record BibleVerse : IIdentifiable
{
    public Guid Id { get; init; }
    
    public          int    BookNumber    { get; init; }
    public required string BookName      { get; init; }
    public          int    ChapterNumber { get; init; }
    public          int    VerseNumber   { get; init; }

    public required string Verse { get; init; }
}