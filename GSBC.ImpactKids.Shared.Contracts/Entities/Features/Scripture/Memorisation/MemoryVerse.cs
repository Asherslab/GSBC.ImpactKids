using System.Collections.Immutable;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MemoryVerse : IIdentifiable
{
    public required Guid   Id            { get; init; }
    public required string ReferenceName { get; init; }

    public required string Verse { get; init; }

    public required Guid                MemoryVerseListId { get; init; }
    public required ImmutableList<Guid> ServiceIds        { get; init; } = [];
    public required ImmutableList<Guid> BibleVerseIds     { get; init; } = [];

    public static string BuildSubscription(
        Guid? memoryVerseListId = null,
        Guid? memoryVerseId     = null
    ) =>
        $"{nameof(MemoryVerse)}." +
        $"{memoryVerseListId?.ToString() ?? "*"}." +
        $"{memoryVerseId?.ToString() ?? "*"}";
}