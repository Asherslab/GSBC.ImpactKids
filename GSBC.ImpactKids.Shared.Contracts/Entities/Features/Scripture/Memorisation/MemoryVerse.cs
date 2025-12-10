using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerse
{
    public required Guid   Id            { get; set; }
    public required string ReferenceName { get; set; }

    public required string Verse { get; set; }

    public required Guid       MemoryVerseListId { get; set; }
    public required List<Guid> ServiceIds        { get; set; } = [];
    public required List<Guid> BibleVerseIds     { get; set; } = [];

    public required List<Service>?    Services    { get; set; }
    public required List<BibleVerse>? BibleVerses { get; set; }

    public static string BuildSubscription(
        Guid? memoryVerseListId = null,
        Guid? memoryVerseId     = null
    ) =>
        $"{nameof(MemoryVerse)}." +
        $"{memoryVerseListId?.ToString() ?? "*"}." +
        $"{memoryVerseId?.ToString() ?? "*"}";
}