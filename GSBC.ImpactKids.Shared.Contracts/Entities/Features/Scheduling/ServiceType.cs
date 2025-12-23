namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public record ServiceType
{
    public required Guid Id { get; init; }

    public required string Label { get; init; }

    public string? Color { get; init; }

    public static string BuildSubscription(Guid? serviceTypeId = null) =>
        $"{nameof(ServiceType)}.{serviceTypeId?.ToString() ?? "*"}";
    
    public const string ColorBackup = "#008b8b";
}