using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

/// <summary>
/// Every run is two phases over one code path.
///
/// <b>Decide</b> reads both sides and writes the plan, the divergences, the pending reviews and the
/// bases of fields that already agree. It touches nothing in <c>People</c> and sends nothing to
/// Elvanto. <b>Apply</b> executes a plan — re-reading both sides first and refusing any item whose
/// reading has moved.
///
/// There is no mode. A run decides, and a person then executes the plan they have read - two calls,
/// always in that order, never one that does both behind a dropdown. Full / AppOnly / DryRun were
/// three names for how much of Apply to skip, and the honest version of that question is "has anyone
/// looked at the plan yet?", which a separate Execute answers by existing. AppOnly survives as
/// configuration: an Execute with writes off applies the inbound half and records every outbound as
/// suppressed.
///
/// Deciding and applying were once <i>different</i> create paths, so a preview structurally could not
/// exercise SaveChanges, the change interceptor, the payload builder or the failure branch. "Apply
/// exactly what was shown" is not a meaningful promise until both walk the same code, and now only
/// one path exists to walk.
/// </summary>
public partial class ElvantoPersonSyncService(
    GsbcDbContext                     db,
    ElvantoService                    elvantoService,
    ElvantoConfig                     elvantoConfig,
    IEnumerable<IFieldSyncDescriptor> descriptors,
    IPersonMatcher                    matcher,
    IFieldReconciler                  fieldReconciler,
    ISyncContextAccessor              syncContext,
    ILogger<ElvantoPersonSyncService> logger
) : IElvantoPersonSyncService
{
    private readonly IReadOnlyList<IFieldSyncDescriptor> _descriptors = descriptors.ToList();

    /// <summary>
    /// How much of the linked roll Elvanto must return before a run is willing to archive anything. People genuinely leave, so this is not 100%, but a real week's
    /// departures are a handful out of seventeen hundred - not hundreds.
    /// </summary>
    private const double MinimumElvantoCoverage = 0.9;

    /// <summary>
    /// Decides a plan and stops. Nothing in <c>People</c> is touched and nothing is sent to Elvanto,
    /// so the audit trail cannot claim a write in the past tense that never happened. Making it
    /// happen is <see cref="ApplyPlanAsync(Guid, CancellationToken)"/>.
    /// </summary>
    public Task<SyncResult> SyncAsync(CancellationToken token = default) => DecideAsync(token);

    private sealed class SyncCounters
    {
        public int InboundPeople;
        public int InboundFields;
        public int OutboundPeople;
        public int OutboundFields;
        public int Conflicts;
        public int Diverged;
    }
}
