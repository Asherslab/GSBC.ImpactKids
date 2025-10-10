using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BasicResponse : ISuccessResponse, IErrorResponse
{
    public required bool    Success { get; set; }
    public          string? Error   { get; set; }

    public static BasicResponse WithError(string error) => new BasicResponse
    {
        Success = false,
        Error = error
    };
}