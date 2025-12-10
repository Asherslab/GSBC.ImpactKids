namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolTerm
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public required DateTime StartDate { get; set; }
    public required DateTime EndDate   { get; set; }
    
    public static string BuildSubscription(Guid? schoolTermId = null) => $"{nameof(SchoolTerm)}.{schoolTermId?.ToString() ?? "*"}";
}