namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SchoolTerm
{
    public required Guid   Id   { get; set; }
    public required string Name { get; set; }

    public required DateTime StartDate { get; set; }
    public required DateTime EndDate   { get; set; }

    [ProtoIgnore]
    public DateTime LocalStartDate
    {
        get => StartDate.ToLocalTime();
        set => StartDate = value.ToUniversalTime();
    }

    [ProtoIgnore]
    public DateTime LocalEndDate
    {
        get => EndDate.ToLocalTime();
        set => EndDate = value.ToUniversalTime();
    }
    
    public static string BuildSubscription(Guid? schoolTermId = null) => $"{nameof(SchoolTerm)}.{schoolTermId?.ToString() ?? "*"}";
}