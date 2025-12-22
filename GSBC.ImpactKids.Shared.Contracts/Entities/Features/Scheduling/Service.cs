using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling.School;

namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class Service
{
    public required Guid    Id   { get; set; }
    public          string? Name { get; set; }

    public required DateTime Date { get; set; }

    [ProtoIgnore]
    public DateTime LocalDate
    {
        get => Date.ToLocalTime();
        set => Date = value.ToUniversalTime();
    }

    public required SchoolTerm? SchoolTerm { get; set; }

    public required ServiceType? ServiceType { get; set; }

    public required DollarStoreEntry? DollarStoreEntry { get; set; }

    public string GetDisplayName() => Name ?? LocalDate.ToString("dd/MM/yyyy");

    public static string BuildSubscription(Guid? schoolTermId = null, Guid? serviceId = null) =>
        $"{nameof(Service)}.{schoolTermId?.ToString() ?? "*"}.{serviceId?.ToString() ?? "*"}";
}