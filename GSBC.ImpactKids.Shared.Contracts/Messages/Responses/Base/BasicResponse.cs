using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
// NOTE: protobuf-net only carries a base type's members into a derived contract when the
// base declares the subtype with [ProtoInclude]. Tag 100 held PickupDisplayResponse, which
// has been deleted - displays read the ordinary services now. A derived response added here
// later needs its own tag, and 100 is free again only because nothing on the wire used it
// after that deletion.
public class BasicResponse : ISuccessResponse, IErrorResponse
{
    public required bool    Success { get; init; }
    public          string? Error   { get; init; }

    public static BasicResponse WithError(string error) => new BasicResponse
    {
        Success = false,
        Error = error
    };
}