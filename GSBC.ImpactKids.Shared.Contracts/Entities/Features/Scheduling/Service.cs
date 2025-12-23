using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record Service
{
    public required Guid    Id   { get; init; }
    public          string? Name { get; init; }

    public required DateTime Date { get; init; }

    [ProtoIgnore]
    public DateTime LocalDate => Date.ToLocalTime();

    public required SchoolTerm? SchoolTerm { get; init; }

    public required ServiceType? ServiceType { get; init; }

    public required DollarStoreEntry? DollarStoreEntry { get; init; }

    public string GetDisplayName() => Name ?? LocalDate.ToString("dd/MM/yyyy");

    public static string BuildSubscription(Guid? schoolTermId = null, Guid? serviceId = null) =>
        $"{nameof(Service)}.{schoolTermId?.ToString() ?? "*"}.{serviceId?.ToString() ?? "*"}";
}