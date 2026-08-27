using System.Data;
using System.Data.Common;
using GSBC.ImpactKids.Grpc.Data;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// One Apply at a time, across the whole system.
    ///
    /// Apply is not re-entrant and the per-item staleness check cannot make it so: two executions in
    /// flight both read <c>Status == Pending</c> before either has written a status back, so both see
    /// the same work and both do it. Every guard in Apply re-reads the two <i>sides</i> - neither side
    /// has moved when the other execution is still mid-flight, so both pass. The visible failure is
    /// two people created in Elvanto for one plan row, which no later run can undo.
    ///
    /// This is a Postgres advisory lock rather than a status column or a <c>SemaphoreSlim</c> for two
    /// reasons. It is held by the <i>connection</i>, so a process that dies mid-apply releases it
    /// instead of leaving a row that says "Applying" forever with no one to clear it. And it is held
    /// by the <i>database</i>, so it still holds if this service is ever run as more than one
    /// replica - which a static semaphore quietly would not.
    /// </summary>
    private const long ApplyLockKey = 6273798564201470001L;

    /// <summary>
    /// Claims the right to apply, or returns null immediately because someone else holds it. It never
    /// waits: a caller who queued would execute a plan the person in front of them has just made
    /// stale, and "try again in a moment" is the honest answer to a second click.
    /// </summary>
    private async Task<ApplyClaim?> TryClaimApplyLockAsync(CancellationToken token)
    {
        // The lock lives on the connection, so the connection has to outlive the statement that takes
        // it. Opening it here pins one for the duration of the apply and hands it back on release.
        await db.Database.OpenConnectionAsync(token);

        try
        {
            DbConnection connection = db.Database.GetDbConnection();

            await using DbCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT pg_try_advisory_lock({ApplyLockKey})";

            object? taken = await command.ExecuteScalarAsync(token);

            if (taken is true) return new ApplyClaim(db, logger);
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }

        await db.Database.CloseConnectionAsync();
        return null;
    }

    /// <summary>
    /// Releases the claim and lets the connection go. Unlocking explicitly rather than relying on the
    /// connection closing keeps the lock's lifetime the same whether or not the pool decides to keep
    /// the connection alive afterwards.
    /// </summary>
    private sealed class ApplyClaim(GsbcDbContext db, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                DbConnection connection = db.Database.GetDbConnection();

                if (connection.State == ConnectionState.Open)
                {
                    await using DbCommand command = connection.CreateCommand();
                    command.CommandText = $"SELECT pg_advisory_unlock({ApplyLockKey})";
                    await command.ExecuteScalarAsync();
                }
            }
            catch (Exception ex)
            {
                // Not fatal: the lock is released when this connection closes either way. Worth a
                // line, because a lock that only ever comes back on close is a lock held longer than
                // the work it guards.
                logger.LogWarning(ex, "Sync: failed to release the apply lock explicitly");
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
