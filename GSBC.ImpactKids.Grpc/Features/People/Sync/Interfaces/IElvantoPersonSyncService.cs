using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;

public interface IElvantoPersonSyncService
{
    /// <summary>Decides a plan, and applies it too unless the mode is DryRun.</summary>
    Task<SyncResult> SyncAsync(SyncWithElvantoRequest request, CancellationToken token = default);

    /// <summary>
    /// Executes a plan a person has read. Refuses an expired plan outright, and refuses any item
    /// whose two sides have moved since it was decided.
    /// </summary>
    Task<SyncResult> ApplyPlanAsync(Guid operationId, CancellationToken token = default);
}
