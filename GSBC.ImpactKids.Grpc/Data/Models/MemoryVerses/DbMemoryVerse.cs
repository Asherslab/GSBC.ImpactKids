using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemoryVerse
{
    public required Guid   Id            { get; set; }
    public required string ReferenceName { get; set; }

    public required string Verse { get; set; }

    public required Guid MemoryVerseListId { get; set; }

    [MapperIgnore]
    public DbMemoryVerseList? MemoryVerseList { get; set; }

    [MapperIgnore]
    public List<DbBibleVerse> BibleVerses { get; set; } = [];

    [MapperIgnore]
    public List<DbService> Services { get; set; } = [];
}