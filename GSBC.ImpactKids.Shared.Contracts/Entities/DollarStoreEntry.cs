using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Scheduling;

namespace GSBC.ImpactKids.Shared.Contracts.Entities;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class DollarStoreEntry
{
    public required Guid Id { get; set; }

    public int?    DollarDoosMade { get; set; }
    public string? Notes          { get; set; }
    
    public required Guid     ServiceId { get; set; }
    public          Service? Service   { get; set; }
    
    public static string BuildSubscription(Guid? serviceId = null, Guid? dollarStoreEntryId = null) => 
        $"{nameof(DollarStoreEntry)}.{serviceId?.ToString() ?? "*"}.{dollarStoreEntryId?.ToString() ?? "*"}";
}