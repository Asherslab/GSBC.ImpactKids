using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

/// <summary>
/// Every run is two phases over one code path.
///
/// <b>Decide</b> reads both sides and writes the plan, the divergences, the pending reviews and the
/// bases of fields that already agree. It touches nothing in <c>People</c> and sends nothing to
/// Elvanto. <b>Apply</b> executes a plan — re-reading both sides first and refusing any item whose
/// reading has moved.
///
/// The three modes stop being three code paths and become two calls: a dry run is Decide and stop, a
/// full run is Decide and then immediately Apply, and Execute applies a plan a person has read. That
/// last one is the point. Full and DryRun used to walk <i>different</i> create paths, so a dry run
/// was a plan preview rather than a rehearsal — it structurally could not exercise SaveChanges, the
/// change interceptor, the payload builder or the failure branch. "Apply exactly what was shown" is
/// not a meaningful promise until both walk the same code.
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
    /// How much of the linked roll Elvanto must return before a full-scope sync is willing to
    /// archive anything. People genuinely leave, so this is not 100%, but a real week's
    /// departures are a handful out of seventeen hundred - not hundreds.
    /// </summary>
    private const double MinimumElvantoCoverage = 0.9;

    public async Task<SyncResult> SyncAsync(
        SyncWithElvantoRequest request,
        CancellationToken      token = default
    )
    {
        SyncResult decided = await DecideAsync(request, token);

        // A dry run stops here, and now genuinely is one: nothing in People was touched, so the
        // audit trail no longer claims local writes in the past tense that never happened.
        if (!decided.Success || request.Mode == ElvantoSyncMode.DryRun)
            return decided;

        return await ApplyPlanAsync(decided.OperationId, decided, token);
    }

    public Task<SyncResult> ApplyPlanAsync(Guid operationId, CancellationToken token = default) =>
        ApplyPlanAsync(operationId, decided: null, token);

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
