using System.Security.Cryptography;
using System.Text;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GSBC.ImpactKids.Grpc.Data.Interceptors;

public class FieldChangeTrackingInterceptor(ISyncContextAccessor syncContext) : SaveChangesInterceptor
{
    private static readonly HashSet<Type> TrackedTypes = [typeof(DbPerson)];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData      eventData,
        InterceptionResult<int> result,
        CancellationToken       cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        SyncSource source = syncContext.Current;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        List<DbFieldChangeLog> logs = [];
        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (!TrackedTypes.Contains(entry.Entity.GetType()))
                continue;
            if (entry.State != EntityState.Modified && entry.State != EntityState.Added)
                continue;

            Guid entityId = (Guid)entry.Property("Id").CurrentValue!;

            foreach (var prop in entry.Properties)
            {
                if (entry.State == EntityState.Modified && !prop.IsModified)
                    continue;
                if (entry.State == EntityState.Added && prop.CurrentValue is null)
                    continue;

                // Skip navigation/shadow/system props
                if (prop.Metadata.IsShadowProperty())
                    continue;

                string? valueStr = prop.CurrentValue?.ToString();
                logs.Add(new DbFieldChangeLog
                {
                    Id         = Guid.NewGuid(),
                    EntityType = entry.Entity.GetType().Name.Replace("Db", ""),
                    EntityId   = entityId,
                    FieldName  = prop.Metadata.Name,
                    ValueHash  = Hash(valueStr),
                    ChangedAt  = now,
                    Source     = source
                });
            }
        }

        foreach (DbFieldChangeLog log in logs)
            eventData.Context.Set<DbFieldChangeLog>().Add(log);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static string Hash(string? value)
    {
        if (value is null) return "null";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16];
    }
}
