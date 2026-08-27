using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

public partial class PickupDisplayKeyService
{
    /// <summary>
    /// Metadata only - when the key was minted and who by. There is no path from here back
    /// to the key itself, by construction: only its hash was ever stored.
    /// </summary>
    public async Task<PickupDisplayKeyResponse> GetKeyInfo(
        PickupDisplayKeyRequest request,
        CallContext             context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        DbPickupDisplayKey? key = await db.PickupDisplayKeys
            .AsNoTracking()
            .Include(x => x.RotatedByUser)
            .FirstOrDefaultAsync(token);

        // No key yet is a fact, not a fault - the admin page reads the null RotatedAt as
        // "nobody has ever set one up" and offers the button.
        if (key == null)
            return new PickupDisplayKeyResponse
            {
                Success = true
            };

        return new PickupDisplayKeyResponse
        {
            Success = true,
            RotatedAt = key.RotatedAt.UtcDateTime,
            RotatedBy = key.RotatedByUser?.Name
        };
    }
}
