namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Events;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class EventBindRequest
{
    public required Guid   StreamId   { get; set; }
    public required string Topic { get; set; }
}