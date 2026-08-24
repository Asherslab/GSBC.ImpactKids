using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public class SyncAuditLogger(
    GsbcDbContext db
)
{
    private readonly List<DbSyncAuditLog> _pending = [];

    public Task Log(
        Guid              operationId,
        Guid              personId,
        SyncEventType     eventType,
        string            reason,
        string?           fieldName  = null,
        string?           fromValue  = null,
        string?           toValue    = null,
        SyncSource?       direction  = null,
        CancellationToken token      = default
    )
    {
        _pending.Add(new DbSyncAuditLog
        {
            Id              = Guid.NewGuid(),
            SyncOperationId = operationId,
            PersonId        = personId,
            EventType       = eventType,
            FieldName       = fieldName,
            FromValue       = fromValue,
            ToValue         = toValue,
            Direction       = direction,
            Reason          = reason,
            OccurredAt      = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Persists the operation record and all buffered audit logs in their own transaction,
    /// independent of whether the main sync transaction committed or rolled back.
    /// Clears the change tracker first so rolled-back entities are not re-saved.
    /// </summary>
    public async Task FlushAsync(DbSyncOperation operation, CancellationToken token = default)
    {
        db.ChangeTracker.Clear();
        await db.SyncOperations.AddAsync(operation, token);
        await db.SyncAuditLogs.AddRangeAsync(_pending, token);
        await db.SaveChangesAsync(token);
    }

    public IReadOnlyList<DbSyncAuditLog> GetAll() => _pending.AsReadOnly();
}
