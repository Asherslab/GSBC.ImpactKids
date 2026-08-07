using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Games;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class CreateGamePointRecordRequest
{
    /// <summary>
    /// Client generated. Lets the offline outbox retry a send without risking a duplicate.
    /// </summary>
    public Guid Id { get; set; }

    public GameTeam Team   { get; set; }
    public int      Points { get; set; }

    /// <summary>Null awards a behaviour point, which sits outside any game.</summary>
    public int? GameNumber { get; set; }

    /// <summary>
    /// When the tap happened on the device, not when the server received it.
    /// A record queued offline keeps its original time.
    /// </summary>
    public DateTime Awarded { get; set; }

    public Guid ServiceId { get; set; }
}
