namespace GSBC.ImpactKids.Shared.Contracts.Entities.MemoryVerses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class MemoryVerseList
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public Guid? SchoolTermId { get; set; }
    
    public static string BuildSubscription(Guid? schoolTermId = null, Guid? memoryVerseListId = null) => 
        $"{nameof(MemoryVerseList)}.{schoolTermId?.ToString() ?? "*"}.{memoryVerseListId?.ToString() ?? "*"}";
}