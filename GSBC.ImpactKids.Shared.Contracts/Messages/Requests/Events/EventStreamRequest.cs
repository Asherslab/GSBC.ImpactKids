namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class EventStreamRequest
{
    public required Guid StreamId { get; set; }
}