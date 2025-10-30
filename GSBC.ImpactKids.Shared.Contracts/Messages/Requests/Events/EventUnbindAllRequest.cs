namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class EventUnbindAllRequest
{
    public required Guid    StreamId      { get; set; }
    public required string? TopicMatcher { get; set; }
}