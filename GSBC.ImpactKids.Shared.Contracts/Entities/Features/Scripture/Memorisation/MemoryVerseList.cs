namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scripture.Memorisation;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record MemoryVerseList
{
    public required Guid   Id   { get; init; }
    public required string Name { get; init; }

    public Guid? SchoolTermId { get; init; }
    
    public static string BuildSubscription(Guid? schoolTermId = null, Guid? memoryVerseListId = null) => 
        $"{nameof(MemoryVerseList)}.{schoolTermId?.ToString() ?? "*"}.{memoryVerseListId?.ToString() ?? "*"}";
}