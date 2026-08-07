namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Games;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class GameScoreboardRequest
{
    /// <summary>Null resolves to today's service, so a wall display can use a fixed URL.</summary>
    public Guid? ServiceId { get; set; }
}
