using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Attendance;
using Microsoft.AspNetCore.Authorization;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

/// <summary>
/// Administers the pickup wall's enrolment key - see <see cref="IPickupDisplayKeyService"/>.
/// <para>
/// Authorized like every other "gRPC/" service. The <i>display</i> service next door is the
/// anonymous one; this is the console that hands out its key, and nothing anonymous may
/// ever reach it.
/// </para>
/// </summary>
[Authorize(Policy = Policies.EnabledOnly)]
public partial class PickupDisplayKeyService(
    GsbcDbContext db
) : IPickupDisplayKeyService;
