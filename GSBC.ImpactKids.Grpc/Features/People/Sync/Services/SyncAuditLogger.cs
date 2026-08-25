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
    /// Persists the buffered rows.
    ///
    /// The operation row is <b>not</b> written here. It is inserted by the caller before anything
    /// references it, because the plan rows carry a foreign key to it — and because clearing the
    /// change tracker to re-add it, which is what this used to do, would discard the plan and the
    /// bases the same run had just decided.
    /// </summary>
    public async Task FlushAsync(CancellationToken token = default)
    {
        if (_pending.Count == 0) return;

        await db.SyncAuditLogs.AddRangeAsync(_pending, token);
        await db.SaveChangesAsync(token);
        _flushed.AddRange(_pending);
        _pending.Clear();
    }

    private readonly List<DbSyncAuditLog> _flushed = [];

    public IReadOnlyList<DbSyncAuditLog> GetAll() => [.._flushed, .._pending];
}
