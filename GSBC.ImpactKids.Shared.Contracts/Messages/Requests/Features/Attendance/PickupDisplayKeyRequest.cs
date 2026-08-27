namespace GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;

/// <summary>
/// Deliberately empty. Both operations on <c>IPickupDisplayKeyService</c> act on the one
/// key there is, so neither needs to name it - but protobuf-net wants a message type, and
/// an empty contract is cheaper than pretending there is a parameter.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PickupDisplayKeyRequest;
