using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;
using Riok.Mapperly.Abstractions;

namespace GSBC.ImpactKids.Grpc.Data.Models.MemoryVerses;

public class DbMemorisationEntry : BaseMemorisationEntry;

public class DbVirtualMemorisationEntry : BaseMemorisationEntry
{
    public bool VerseHasBeenRecitedBefore { get; set; }
}

public abstract class BaseMemorisationEntry
{
    public required Guid Id { get; set; }

    public required Guid      PersonId { get; set; }
    [MapperIgnore]
    public          DbPerson? Person   { get; set; }

    public required Guid           MemoryVerseId { get; set; }
    [MapperIgnore]
    public          DbMemoryVerse? MemoryVerse   { get; set; }

    public required Guid       ServiceId { get; set; }
    [MapperIgnore]
    public          DbService? Service   { get; set; }

    public bool VerseRecited         { get; set; }
    public bool FiveDollaryDoosGiven { get; set; }
    public bool OneDollaryDooGiven   { get; set; }
}