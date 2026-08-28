using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;

namespace GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

/// <summary>
/// Administers the one key every wall display enrols with - the pickup list and the game
/// boards share it. Leader only, like everything the gRPC service does not explicitly open:
/// this is the console that hands a display its credential, and no display may ever reach it.
/// <para>
/// There is deliberately no read-the-key operation. Only a hash is stored, so
/// <see cref="PickupDisplayKeyResponse.Key"/> comes back once, from
/// <see cref="Rotate"/>, and never again.
/// </para>
/// </summary>
[Service("gRPC/GSBC.ImpactKids.Attendance.PickupDisplayKey")]
public interface IPickupDisplayKeyService
{
    /// <summary>
    /// When the current key was minted and by whom, and nothing else. Safe to call on every
    /// page load.
    /// </summary>
    Task<PickupDisplayKeyResponse> GetKeyInfo(PickupDisplayKeyRequest request, CallContext context = default);

    /// <summary>
    /// Mints a new key and returns it <b>once</b>. Immediately and totally invalidating:
    /// every screen already enrolled on the old key falls back to the unauthorised state
    /// and has to be re-opened from the new setup link. That is the point of rotating.
    /// </summary>
    Task<PickupDisplayKeyResponse> Rotate(PickupDisplayKeyRequest request, CallContext context = default);
}
