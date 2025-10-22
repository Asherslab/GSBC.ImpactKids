namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemorisationEntry
{
    public required Guid Id { get; set; }

    public required Guid      PersonId { get; set; }
    public          DbPerson? Person   { get; set; }

    public required Guid           MemoryVerseId { get; set; }
    public          DbMemoryVerse? MemoryVerse   { get; set; }

    public required Guid       ServiceId { get; set; }
    public          DbService? Service   { get; set; }

    public bool VerseRecited         { get; set; }
    public bool FiveDollaryDoosGiven { get; set; }
    public bool OneDollaryDooGiven   { get; set; }
}