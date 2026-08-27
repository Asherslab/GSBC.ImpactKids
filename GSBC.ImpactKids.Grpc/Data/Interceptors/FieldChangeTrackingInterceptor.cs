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

    // Allergies and medical notes live in their own tables, so they never appear in
    // DbPerson's scalar properties and were invisible to this interceptor. That made
    // appChanged permanently false for them, and the sync never pushed a single one.
    // They are logged against the parent person under the field name the descriptor owns,
    // because that is what the sync looks up.
    private static readonly HashSet<Type> ChildTypesLoggedAgainstPerson =
        [typeof(DbAllergy), typeof(DbMedicalNote)];

    private const string MedicalAllergyFieldName = "MedicalAllergyNotes";

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
            Type entityType = entry.Entity.GetType();

            if (ChildTypesLoggedAgainstPerson.Contains(entityType))
            {
                // Deleted matters here in a way it does not for scalar fields: removing an
                // allergy is an edit that must reach Elvanto, and a delete carries no
                // meaningful CurrentValue to hash.
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                    continue;

                Guid? ownerId = OwningPersonId(entry);
                if (ownerId is null) continue;

                logs.Add(new DbFieldChangeLog
                {
                    Id         = Guid.NewGuid(),
                    EntityType = nameof(DbPerson).Replace("Db", ""),
                    EntityId   = ownerId.Value,
                    FieldName  = MedicalAllergyFieldName,
                    // The composed text is rebuilt from the person at sync time, so the hash
                    // of one child row would be meaningless. Only the timestamp is used.
                    ValueHash  = Hash($"{entityType.Name}:{entry.State}"),
                    ChangedAt  = now,
                    Source     = source
                });
                continue;
            }

            if (!TrackedTypes.Contains(entityType))
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

    // A deleted row's PersonId has to be read from its original values - CurrentValue is
    // gone by the time SaveChanges runs.
    private static Guid? OwningPersonId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry? prop =
            entry.Properties.FirstOrDefault(p => p.Metadata.Name == "PersonId");
        if (prop is null) return null;

        object? value = entry.State == EntityState.Deleted ? prop.OriginalValue : prop.CurrentValue;
        return value as Guid?;
    }

    private static string Hash(string? value)
    {
        if (value is null) return "null";
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16];
    }
}
