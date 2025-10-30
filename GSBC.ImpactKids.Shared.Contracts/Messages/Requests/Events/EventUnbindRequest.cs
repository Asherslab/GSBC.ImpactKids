namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class EventUnbindRequest
{
    public required Guid   StreamId       { get; set; }
    public required Guid   SubscriptionId { get; set; }
}