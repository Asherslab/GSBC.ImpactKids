using System.Security.Claims;
using Grpc.Core;
using GSBC.ImpactKids.Grpc.Data.Models.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.Attendance;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Features.Attendance;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.Attendance.PickupDisplayKeyServices;

public partial class PickupDisplayKeyService
{
    /// <summary>
    /// Mints a new key, throws the old one away, and returns the new one once.
    /// <para>
    /// Every row is removed before the new one is added, so the table keeps its single-row
    /// shape and the new <see cref="DbPickupDisplayKey.Id"/> becomes the only generation any
    /// enrolment cookie may carry. Screens on the old key stop working the moment this
    /// commits - that is the whole reason to press the button.
    /// </para>
    /// </summary>
    public async Task<PickupDisplayKeyResponse> Rotate(
        PickupDisplayKeyRequest request,
        CallContext             context = default
    )
    {
        CancellationToken token = context.CancellationToken;

        string? userId = context.ServerCallContext?.GetHttpContext().User
            .FindFirstValue("UserId");

        if (userId == null)
            return PickupDisplayKeyResponse.WithError(PermissionDenied);

        string key = PickupDisplayKeys.Generate();

        List<DbPickupDisplayKey> existing = await db.PickupDisplayKeys.ToListAsync(token);

        db.PickupDisplayKeys.RemoveRange(existing);

        DbPickupDisplayKey minted = new()
        {
            Id = Guid.NewGuid(),
            KeyHash = PickupDisplayKeys.Hash(key),
            RotatedAt = DateTimeOffset.UtcNow,
            RotatedByUserId = Guid.Parse(userId)
        };

        db.PickupDisplayKeys.Add(minted);

        await db.SaveChangesAsync(token);

        // No log line here, on purpose, and none on the enrolment path either. A key in a
        // log is a key in every log shipper downstream of it.
        string? rotatedBy = await db.Users
            .AsNoTracking()
            .Where(x => x.Id == minted.RotatedByUserId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(token);

        return new PickupDisplayKeyResponse
        {
            Success = true,
            RotatedAt = minted.RotatedAt.UtcDateTime,
            RotatedBy = rotatedBy,
            Key = key
        };
    }
}
