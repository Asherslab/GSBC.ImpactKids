namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Service
{
    public required Guid    Id   { get; init; }
    public          string? Name { get; init; }

    public required DateTime Date { get; init; }

    [ProtoIgnore]
    public DateTime LocalDate => Date.ToLocalTime();

    public required Guid? SchoolTermId       { get; init; }
    public required Guid? ServiceTypeId      { get; init; }
    public required Guid? DollarStoreEntryId { get; init; }

    public string GetDisplayName() => Name ?? LocalDate.ToString("dd/MM/yyyy");

    public static string BuildSubscription(Guid? schoolTermId = null, Guid? serviceId = null) =>
        $"{nameof(Service)}.{schoolTermId?.ToString() ?? "*"}.{serviceId?.ToString() ?? "*"}";
}