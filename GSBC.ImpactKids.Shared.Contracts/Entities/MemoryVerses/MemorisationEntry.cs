namespace GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemorisationEntry
{
    public required Guid Id { get; set; }

    public required Guid PersonId      { get; set; }
    public required Guid MemoryVerseId { get; set; }
    public required Guid ServiceId     { get; set; }

    public bool VerseRecited              { get; set; }
    public bool VerseHasBeenRecitedBefore { get; set; }
    public bool FiveDollaryDoosGiven      { get; set; }
    public bool OneDollaryDooGiven        { get; set; }

    public Person?      Person      { get; set; }
    public MemoryVerse? MemoryVerse { get; set; }
    public Service?     Service     { get; set; }

    public static string BuildSubscription(
        Guid? memorisationEntryId = null
    ) =>
        $"{nameof(MemorisationEntry)}." +
        $"{memorisationEntryId?.ToString() ?? "*"}";
}