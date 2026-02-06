namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record DollarStoreEntry : IIdentifiable
{
    public required Guid Id { get; init; }

    public int?    DollarDoosMade { get; init; }
    public string? Notes          { get; init; }

    public required Guid ServiceId { get; init; }

    public static string BuildSubscription(Guid? serviceId = null, Guid? dollarStoreEntryId = null) =>
        $"{nameof(DollarStoreEntry)}.{serviceId?.ToString() ?? "*"}.{dollarStoreEntryId?.ToString() ?? "*"}";
}