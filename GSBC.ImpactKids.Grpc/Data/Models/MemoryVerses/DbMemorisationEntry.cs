using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;

namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemorisationEntry : BaseMemorisationEntry;

public class DbVirtualMemorisationEntry : BaseMemorisationEntry
{
    public bool VerseHasBeenRecitedBefore { get; set; }
}

public abstract class BaseMemorisationEntry
{
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