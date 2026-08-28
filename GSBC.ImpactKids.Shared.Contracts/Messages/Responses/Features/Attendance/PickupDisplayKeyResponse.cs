using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base.Interfaces;

namespace GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;

/// <summary>
/// What an admin may know about the pickup display key. <see cref="Key"/> is populated on
/// exactly one response in the key's life - the rotation that minted it - and is null
/// everywhere else, because only a hash is stored and there is nothing to read back.
/// <para>
/// Restates <c>Success</c>/<c>Error</c> rather than deriving from <c>BasicResponse</c>, the
/// same way <c>BasicReadResponse{T}</c> does: deriving would need another
/// <c>[ProtoInclude]</c> on the base for protobuf-net to carry them.
/// </para>
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class PickupDisplayKeyResponse : ISuccessResponse, IErrorResponse
{
    public required bool    Success { get; init; }
    public          string? Error   { get; init; }

    /// <summary>Null when no key has ever been minted - the wall is unusable until someone rotates one.</summary>
    public DateTime? RotatedAt { get; init; }

    /// <summary>The name of whoever pressed the button, for the admin page. Never an id.</summary>
    public string? RotatedBy { get; init; }

    /// <summary>
    /// The key, in the clear, and only ever on the response to <c>Rotate</c>. Show it once,
    /// let the admin bookmark <c>/bff/display-login?key={Key}</c> on the TV, and never ask
    /// for it again - it cannot be recovered.
    /// </summary>
    public string? Key { get; init; }

    public static PickupDisplayKeyResponse WithError(string error) => new()
    {
        Success = false,
        Error = error
    };
}
