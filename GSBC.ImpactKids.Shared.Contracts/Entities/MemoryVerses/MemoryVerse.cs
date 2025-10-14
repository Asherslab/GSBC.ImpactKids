namespace GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerse
{
    public required Guid   Id            { get; set; }
    public required string ReferenceName { get; set; }

    public required string Verse { get; set; }

    public required Guid MemoryVerseListId { get; set; }
}