using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Features.Authentication.DisplayAuth;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

/// <summary>
/// Administers the pickup wall's enrolment key - see <see cref="IPickupDisplayKeyService"/>.
/// <para>
/// Leader only, and it says so by carrying no authorization attribute at all: the fallback
/// policy is <see cref="Policies.EnabledOnly"/>, so this is protected without anybody having
/// had to remember. This is the console that hands out a display's key - a display must
/// never reach it, and none can, because no method here is marked
/// <see cref="Policies.EnabledOrDisplay"/>.
/// </para>
/// </summary>
public partial class PickupDisplayKeyService(
    GsbcDbContext             db,
    DisplaySigningKeyProvider signingKeys
) : IPickupDisplayKeyService;
