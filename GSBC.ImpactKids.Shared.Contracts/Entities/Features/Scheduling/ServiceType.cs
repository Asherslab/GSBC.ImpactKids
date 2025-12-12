namespace GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ServiceType
{
    public required Guid Id { get; set; }

    public required string Label { get; set; }

    public string? Color { get; set; }

    public static string BuildSubscription(Guid? serviceTypeId = null) =>
        $"{nameof(ServiceType)}.{serviceTypeId?.ToString() ?? "*"}";
    
    public const string ColorBackup = "#008b8b";
}