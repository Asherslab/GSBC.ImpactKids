namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemoryVerseBibleVerseRelationship
{
    public Guid MemoryVersesId { get; set; }
    
    public Guid BibleVersesId { get; set; }
}