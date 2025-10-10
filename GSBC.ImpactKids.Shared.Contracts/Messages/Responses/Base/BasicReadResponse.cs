using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class BasicReadResponse<T> : IReadResponse<T>, ISuccessResponse, IErrorResponse
{
    public required T? Entity { get; set; }

    public required bool    Success { get; set; }
    public          string? Error   { get; set; }
    
    public static BasicReadResponse<T> WithError(string error) => new()
    {
        Entity = default,
        Success = false,
        Error = error
    };
}