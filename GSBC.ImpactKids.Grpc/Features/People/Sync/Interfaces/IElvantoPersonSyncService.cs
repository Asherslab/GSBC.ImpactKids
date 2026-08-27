using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IElvantoPersonSyncService
{
    /// <summary>
    /// Decides a plan and stops. Takes no arguments, because there is nothing left to ask for: every
    /// run covers the whole roll and writes nothing.
    /// </summary>
    Task<SyncResult> SyncAsync(CancellationToken token = default);

    /// <summary>
    /// Executes a plan a person has read. Refuses an expired plan outright, and refuses any item
    /// whose two sides have moved since it was decided.
    /// </summary>
    Task<SyncResult> ApplyPlanAsync(Guid operationId, CancellationToken token = default);
}
