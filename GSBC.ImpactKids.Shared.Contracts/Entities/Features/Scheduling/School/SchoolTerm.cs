namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record SchoolTerm
{
    public required Guid   Id   { get; init; }
    public required string Name { get; init; }

    public required DateTime StartDate { get; init; }
    public required DateTime EndDate   { get; init; }

    [ProtoIgnore]
    public DateTime LocalStartDate => StartDate.ToLocalTime();

    [ProtoIgnore]
    public DateTime LocalEndDate => EndDate.ToLocalTime();

    public static string BuildSubscription(Guid? schoolTermId = null) =>
        $"{nameof(SchoolTerm)}.{schoolTermId?.ToString() ?? "*"}";
}