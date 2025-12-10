namespace GSBC.ImpactKids.Grpc.Data.Models.Scheduling;

public class DbServiceType
{
    public required Guid Id { get; set; }

    public required string Label { get; set; }

    public string? Color { get; set; }
}