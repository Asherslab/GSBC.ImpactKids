namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemoryVerseBibleVerseRelationship
{
    public Guid MemoryVerseId { get; set; }
    
    public Guid BibleVerseId { get; set; }
}