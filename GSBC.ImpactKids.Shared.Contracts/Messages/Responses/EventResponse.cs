namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class EventResponse
{
    public required string? RoutingKey { get; set; }
}