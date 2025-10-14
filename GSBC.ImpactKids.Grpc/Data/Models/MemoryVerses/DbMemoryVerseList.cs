using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemoryVerseList
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public Guid? SchoolTermId { get; set; }

    [MapperIgnore]
    public DbSchoolTerm? SchoolTerm { get; set; }

    [MapperIgnore]
    public List<DbMemoryVerse> MemoryVerses { get; set; } = [];
}