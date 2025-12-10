using GSBC.ImpactKids.Grpc.Data.Models.Scheduling;

namespace GSBC.ImpactKids.Grpc.Data.Models;

public class DbDollarStoreEntry
{
    public required Guid Id { get; set; }

    public int?    DollarDoosMade { get; set; }
    public string? Notes          { get; set; }

    public required Guid       ServiceId { get; set; }
    public          DbService? Service   { get; set; }
}