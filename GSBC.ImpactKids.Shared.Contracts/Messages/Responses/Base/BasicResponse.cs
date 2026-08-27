using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
// protobuf-net only carries a base type's members into a derived contract when the base
// declares the subtype. Without this, a PickupDisplayResponse would serialise its own
// fields and silently drop Success and Error - the wall would read every board as failed.
[ProtoInclude(100, typeof(PickupDisplayResponse))]
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