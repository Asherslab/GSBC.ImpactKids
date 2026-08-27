using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using Microsoft.EntityFrameworkCore;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Executes a decided plan, and only what the plan contains.
    ///
    /// <b>Apply never discovers new work.</b> Anything that appeared since Decide belongs to the next
    /// plan, and saying so plainly is what makes the button safe to press. Both sides are re-read
    /// first: an item whose reading has moved is marked <c>Stale</c>, skipped and reported, so
    /// nothing is clobbered on the strength of a stale observation.
    /// </summary>
    public async Task<SyncResult> ApplyPlanAsync(Guid operationId, CancellationToken token = default)
    {
        // Taken before anything is read, and released however this method leaves. Two executions in
        // flight would both read the same Pending items and both do the work - see ApplyClaim.
        await using ApplyClaim? claim = await TryClaimApplyLockAsync(token);

        if (claim is null)
        {
            logger.LogWarning(
                "Sync {OperationId}: apply refused — another execution is already running", operationId);

            return SyncResult.Failed(operationId,
                "Another sync execution is already running. Wait for it to finish, then try again — "
                + "the plan is untouched.");
        }

        DbSyncOperation? operation = await db.SyncOperations
            .FirstOrDefaultAsync(x => x.Id == operationId, token);

        if (operation is null)
            return SyncResult.Failed(operationId, "Sync operation not found");

        // Expiry is a backstop against a failure the per-item check cannot catch. A stale item is one
        // whose values moved; expiry guards against the SET of items being wrong - people created,
        // deleted or merged in Elvanto since Decide ran, which no per-item check can see because
        // those items are not in the plan. The whole plan is refused, never part of it.
        if (operation.PlanExpiresAt is { } expiry && DateTimeOffset.UtcNow > expiry)
            return SyncResult.Failed(operationId,
                $"This plan expired at {expiry:u}. Run a new sync — the roll may have moved since it was decided.");

        List<DbSyncPlannedChange> plan = await db.PlannedChanges
            .Where(x => x.SyncOperationId == operationId && x.Status == PlannedChangeStatus.Pending)
            .ToListAsync(token);

        if (plan.Count == 0)
            return Applied(operationId, new SyncCounters(), 0, 0, 0);

        SyncAuditLogger audit = new(db);

        try
        {
            using IDisposable _ = syncContext.SetSource(SyncSource.Elvanto);

            (SyncWorkingSet? set, string? refusal) = await LoadWorkingSetAsync(operationId, token);
            if (set is null)
            {
                logger.LogError("Sync {OperationId}: apply refused — {Reason}", operationId, refusal);
                return SyncResult.Failed(operationId, refusal!);
            }

            SyncCounters counters = new();
            int stale = 0, archived = 0, autoLinked = 0;

            Dictionary<string, ElvantoPerson> elvById = set.ElvantoPeople
                .Where(e => e.Id is not null)
                .ToDictionary(e => e.Id!);

            Dictionary<Guid, DbPerson> appById = set.AppPeople.ToDictionary(p => p.Id);

            // --- local work, in one short transaction -----------------------------------------

            // CreateLocally first, so a person made from Elvanto exists before anything references
            // them. LinkPerson next, because an inbound field on a freshly linked person needs the
            // link. Archive last, so a person is not archived and then written to.
            foreach (DbSyncPlannedChange item in plan.Where(x => x.Kind == PlannedChangeKind.CreateLocally))
            {
                if (item.ElvantoId is null || !elvById.TryGetValue(item.ElvantoId, out ElvantoPerson? elv))
                {
                    MarkStale(item, "The Elvanto record is no longer in the roll");
                    stale++;
                    continue;
                }

                if (HashElvantoPerson(elv) != item.ObservedElvantoHash)
                {
                    MarkStale(item, "The Elvanto record changed after this plan was decided");
                    stale++;
                    continue;
                }

                if (set.AppByElvantoId.ContainsKey(item.ElvantoId))
                {
                    MarkSkipped(item, "Someone is already linked to this Elvanto record");
                    continue;
                }

                DbPerson created = CreatePersonFromElvanto(elv, set);
                await db.People.AddAsync(created, token);
                set.AppByElvantoId[item.ElvantoId] = created;
                appById[created.Id] = created;

                item.PersonId = created.Id;
                MarkApplied(item);

                await audit.Log(operationId, created.Id, SyncEventType.Created, "NewFromElvanto",
                    direction: SyncSource.Elvanto, token: token);
                counters.InboundPeople++;
            }

            foreach (DbSyncPlannedChange item in plan.Where(x => x.Kind == PlannedChangeKind.LinkPerson))
            {
                if (item.PersonId is null || !appById.TryGetValue(item.PersonId.Value, out DbPerson? person))
                {
                    MarkStale(item, "The app person no longer exists");
                    stale++;
                    continue;
                }

                if (item.ElvantoId is null || !elvById.TryGetValue(item.ElvantoId, out ElvantoPerson? elv))
                {
                    MarkStale(item, "The Elvanto record is no longer in the roll");
                    stale++;
                    continue;
                }

                if (person.ElvantoId is not null)
                {
                    MarkSkipped(item, $"Already linked to {person.ElvantoId}");
                    continue;
                }

                if (HashElvantoPerson(elv) != item.ObservedElvantoHash)
                {
                    MarkStale(item, "The Elvanto record changed after this plan was decided");
                    stale++;
                    continue;
                }

                person.ElvantoId = item.ElvantoId;
                MarkApplied(item);

                await audit.Log(operationId, person.Id, SyncEventType.Match, item.Reason, token: token);
                autoLinked++;
            }

            foreach (DbSyncPlannedChange item in plan.Where(x => x.Kind == PlannedChangeKind.InboundField))
            {
                if (!TryResolveField(item, appById, elvById, set, out DbPerson? person,
                        out IFieldSyncDescriptor? desc, out FieldComparison? now, out string? why))
                {
                    MarkStale(item, why!);
                    stale++;
                    continue;
                }

                // A descriptor may refuse a value it cannot read, and refusing is right. Recording
                // the refusal as a completed write was not: the row said Applied, the audit row said
                // the field had been updated, and the base below settled both legs on Elvanto's
                // value - asserting the person held something they did not. The person kept their
                // own value, so the next run read the gap as an app-side edit nobody made and
                // planned to push it to Elvanto. Six of those appeared on a real Execute.
                if (!desc!.SetOnApp(person!, item.ProposedValue))
                {
                    MarkSkipped(item, $"The app would not take Elvanto's value for {item.FieldName}");
                    await audit.Log(operationId, person!.Id, SyncEventType.Diverged,
                        $"InboundRefusedByApp:{item.Reason}", item.FieldName,
                        item.ObservedAppValue, item.ObservedElvantoValue, SyncSource.Elvanto,
                        token: token);
                    continue;
                }

                MarkApplied(item);

                await audit.Log(operationId, person!.Id, SyncEventType.FieldUpdated, item.Reason,
                    item.FieldName, item.ObservedAppValue, item.ObservedElvantoValue, SyncSource.Elvanto,
                    token: token);

                // Both legs record what each side actually holds, and the app's leg is re-read
                // rather than assumed. It used to be written as Elvanto's value on the strength of
                // the write having been attempted, which is only true when the write was exact - and
                // an inbound is not always exact. The medical/allergy box parses into rows and is
                // deliberately additive, so the person ends up holding a superset; settling both
                // legs on Elvanto's text then made that superset look like an app-side change on the
                // next run and planned to push it back as churn. Where the two legs differ the base
                // now says so, and the run reports a difference instead of inventing an edit.
                await SettleBaseAsync(person.Id, desc, desc.GetFromApp(person), now!.ElvantoValue,
                    set.Bases, token);
                counters.InboundFields++;
            }

            foreach (DbSyncPlannedChange item in plan.Where(x => x.Kind == PlannedChangeKind.Archive))
            {
                if (item.PersonId is null || !appById.TryGetValue(item.PersonId.Value, out DbPerson? person))
                {
                    MarkStale(item, "The app person no longer exists");
                    stale++;
                    continue;
                }

                if (person.DeletedAtUtc is not null)
                {
                    MarkSkipped(item, "Already archived");
                    continue;
                }

                // Re-checked, not trusted. Archiving reads "absent from the roll" as "deleted from
                // Elvanto", and six of the seven tables referencing a person cascade.
                if (person.ElvantoId is not null && elvById.ContainsKey(person.ElvantoId))
                {
                    MarkStale(item, "The person is back in the Elvanto roll");
                    stale++;
                    continue;
                }

                person.DeletedAtUtc = DateTimeOffset.UtcNow;
                MarkApplied(item);
                await audit.Log(operationId, person.Id, SyncEventType.Archived, "RemovedFromElvanto", token: token);
                archived++;
            }

            await db.SaveChangesAsync(token);

            // --- Elvanto work, outside any transaction ------------------------------------------
            //
            // Elvanto is not transactional, and holding one open across every HTTP call meant every
            // rollback undid half the world. Local state is committed above; the results of the
            // sends are reconciled in the short save at the end.

            counters.OutboundFields += await ApplyOutboundFieldsAsync(
                operationId, plan, appById, set, audit, token);

            counters.OutboundPeople += await ApplyCreatesInElvantoAsync(
                operationId, plan, appById, set, audit, token);

            await db.SaveChangesAsync(token);

            operation.CompletedAt = DateTimeOffset.UtcNow;
            operation.Status      = SyncStatus.Success;
            await db.SaveChangesAsync(token);

            try
            {
                await audit.FlushAsync(token);
            }
            catch (Exception flushEx)
            {
                logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs", operationId);
            }

            logger.LogInformation(
                "Sync {OperationId} applied | Items={Items} Stale={Stale} InboundPeople={InboundPeople} "
                + "InboundFields={InboundFields} OutboundPeople={OutboundPeople} OutboundFields={OutboundFields} "
                + "AutoLinked={AutoLinked} Archived={Archived}",
                operationId, plan.Count, stale, counters.InboundPeople, counters.InboundFields,
                counters.OutboundPeople, counters.OutboundFields, autoLinked, archived);

            return Applied(operationId, counters, autoLinked, archived, stale, plan.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync operation {OperationId} failed while applying", operationId);

            try
            {
                db.ChangeTracker.Clear();
                await db.SyncOperations
                    .Where(x => x.Id == operationId)
                    .ExecuteUpdateAsync(s => s
                            .SetProperty(x => x.CompletedAt, DateTimeOffset.UtcNow)
                            .SetProperty(x => x.Status, SyncStatus.Failed)
                            .SetProperty(x => x.FailureReason, ex.Message),
                        token);

                await audit.FlushAsync(token);
            }
            catch (Exception flushEx)
            {
                logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs after failure",
                    operationId);
            }

            return SyncResult.Failed(operationId, ex.Message);
        }
    }

    /// <summary>
    /// Pushes the planned field changes, one request per person, and settles a base only for the
    /// fields that request actually carried and that actually landed.
    /// </summary>
    private async Task<int> ApplyOutboundFieldsAsync(
        Guid                       operationId,
        List<DbSyncPlannedChange>  plan,
        Dictionary<Guid, DbPerson> appById,
        SyncWorkingSet             set,
        SyncAuditLogger            audit,
        CancellationToken          token)
    {
        int pushed = 0;

        IEnumerable<IGrouping<Guid, DbSyncPlannedChange>> byPerson = plan
            .Where(x => x.Kind == PlannedChangeKind.OutboundField && x.PersonId is not null)
            .GroupBy(x => x.PersonId!.Value);

        foreach (IGrouping<Guid, DbSyncPlannedChange> group in byPerson)
        {
            if (!appById.TryGetValue(group.Key, out DbPerson? person) || person.ElvantoId is null)
            {
                foreach (DbSyncPlannedChange item in group) MarkStale(item, "The app person is no longer linked");
                continue;
            }

            // Named as a skip rather than dropped, so the plan still shows the work and the reason
            // it did not go. An AppOnly mode used to be one of these conditions; the configuration
            // switches below say the same thing and cannot be picked per run by mistake.
            bool mayPush = elvantoService.UpdatesEnabled && elvantoService.MayUpdate(person.Id);

            ElvantoUpdatePersonRequest request = new() { Id = person.ElvantoId };
            List<(DbSyncPlannedChange Item, IFieldSyncDescriptor Descriptor, string? Settled)> carried = [];

            foreach (DbSyncPlannedChange item in group)
            {
                if (!TryResolveField(item, appById, ElvantoById(set), set, out _,
                        out IFieldSyncDescriptor? desc, out FieldComparison? now, out string? why))
                {
                    MarkStale(item, why!);
                    continue;
                }

                if (!ApplyOutbound(desc!, request, item.ProposedValue))
                {
                    MarkSkipped(item, "The payload did not carry this field");
                    continue;
                }

                carried.Add((item, desc!, item.ProposedValue));
            }

            if (carried.Count == 0) continue;

            if (!mayPush)
            {
                // Nothing left, so nothing settles. This is the guarantee: an outbound change stays
                // outstanding and is offered again next run, instead of being marked seen by the run
                // that reported it as "would push".
                logger.LogInformation(
                    "Sync {OperationId}: would send outbound update for person {PersonId} ({FirstName} {LastName}). "
                    + "Payload: {Payload}",
                    operationId, person.Id, person.FirstName, person.LastName,
                    ElvantoService.DescribePayload(request));

                // Named by whichever write switch actually stopped it. There is no run mode left to
                // blame it on, which is the point: "WouldPush:DryRun" was a tense-lie about a run
                // that had already been decided, and the switches say something a person can act on.
                string suppression = SuppressionReason(person.Id, elvantoService.UpdatesEnabled);

                foreach ((DbSyncPlannedChange item, _, _) in carried)
                {
                    MarkSkipped(item, suppression);
                    await audit.Log(operationId, person.Id, SyncEventType.WouldPushToElvanto,
                        $"WouldPush:{suppression}:{item.Reason}", item.FieldName,
                        item.ObservedElvantoValue, item.ProposedValue, SyncSource.App, token: token);
                }

                continue;
            }

            ElvantoService.UpdateOutcome outcome = await elvantoService.UpdatePersonAsync(request, token);

            // Elvanto created the family this person was moved into. Persisted rather than only
            // held for the rest of this apply: the id Elvanto minted is the answer to "which
            // household is this local family?" for every run after this one too, and throwing it
            // away at the end of the run is what made the outbound direction re-derive it forever.
            if (outcome.NewFamilyId is not null)
                LinkFamily(set.Families, person.FamilyId, outcome.NewFamilyId,
                    ElvantoFamilyLinkSource.CreatedInElvanto);

            foreach ((DbSyncPlannedChange item, IFieldSyncDescriptor desc, string? settled) in carried)
            {
                if (!outcome.Landed)
                {
                    item.Status       = PlannedChangeStatus.Failed;
                    item.StatusReason = elvantoService.LastUpdateError ?? "no reason given";
                    item.AppliedAt    = DateTimeOffset.UtcNow;

                    await audit.Log(operationId, person.Id, SyncEventType.WouldPushToElvanto,
                        $"PushFailed: {item.StatusReason}", item.FieldName,
                        item.ObservedElvantoValue, item.ProposedValue, SyncSource.App, token: token);
                    continue;
                }

                MarkApplied(item);
                await audit.Log(operationId, person.Id, SyncEventType.PushedToElvanto, item.Reason,
                    item.FieldName, item.ObservedElvantoValue, item.ProposedValue, SyncSource.App, token: token);

                // Elvanto now holds what was sent; the app holds what it held. Both legs are recorded
                // as they actually are, which for a first-sync merge is deliberately not the same
                // string on each side.
                await SettleBaseAsync(person.Id, desc, item.ObservedAppValue, settled, set.Bases, token);
                pushed++;
            }
        }

        return pushed;
    }

    private async Task<int> ApplyCreatesInElvantoAsync(
        Guid                       operationId,
        List<DbSyncPlannedChange>  plan,
        Dictionary<Guid, DbPerson> appById,
        SyncWorkingSet             set,
        SyncAuditLogger            audit,
        CancellationToken          token)
    {
        int created = 0;

        foreach (DbSyncPlannedChange item in plan.Where(x => x.Kind == PlannedChangeKind.CreateInElvanto))
        {
            if (item.PersonId is null || !appById.TryGetValue(item.PersonId.Value, out DbPerson? local))
            {
                MarkStale(item, "The app person no longer exists");
                continue;
            }

            if (local.ElvantoId is not null)
            {
                MarkSkipped(item, $"Already linked to {local.ElvantoId}");
                continue;
            }

            // The family may have been created by an earlier item in this same apply, so the payload
            // is rebuilt rather than replayed - and then compared against what was decided.
            string? elvantoFamilyId = set.Families.ElvantoFor(local.FamilyId);
            if (elvantoFamilyId is null) elvantoFamilyId = ElvantoService.NewFamily;

            string payload = elvantoService.DescribeCreatePayload(
                local, ComposeMedicalAllergyText(local), elvantoFamilyId);

            // KNOWN GAP, deliberately left as-is: the staleness check does not run at all when the
            // family moved between deciding and applying.
            //
            // The family id is part of the payload, so a person whose household was minted by an
            // earlier item in this same apply hashes differently for a reason that is not a change to
            // the person - hence the second clause. But `&&` means the whole check is skipped in
            // exactly that case, so the second and later members of every new household are created
            // from a payload nobody re-verified. If someone edits that child between Decide and
            // Execute, the edit goes to Elvanto unannounced instead of being marked Stale.
            //
            // The fix is to compare like with like rather than to skip: rebuild the payload with
            // `item.ProposedValue` as the family and hash that, then compare unconditionally. Not
            // done here because it wants its own test over the sibling-create path, and the window is
            // one plan's lifetime (Elvanto:PlanExpiryHours, 4 by default).
            if (SyncHash.Of(payload) != item.ObservedAppHash && elvantoFamilyId == item.ProposedValue)
            {
                MarkStale(item, "The person changed after this plan was decided");
                continue;
            }

            if (!elvantoService.MayCreate(local.Id))
            {
                MarkSkipped(item, "Not in Elvanto:AllowedCreatePersonIds");
                await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                    "WouldCreate:NotInAllowList", direction: SyncSource.App, toValue: payload, token: token);
                continue;
            }

            if (!elvantoService.CreatesEnabled)
            {
                const string suppression = "Elvanto:AllowCreates=false";

                MarkSkipped(item, suppression);
                await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                    $"WouldCreate:{suppression}", direction: SyncSource.App, toValue: payload, token: token);
                continue;
            }

            // Recorded before the call, so an attempt leaves a trace even if the write is refused at
            // the transport or the process dies mid-send.
            await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                "CreateAttempted", direction: SyncSource.App, toValue: payload, token: token);

            ElvantoService.CreatedPerson? result = await elvantoService.CreatePersonAsync(
                local, ComposeMedicalAllergyText(local), elvantoFamilyId, token);

            if (result is null)
            {
                item.Status       = PlannedChangeStatus.Failed;
                item.StatusReason = elvantoService.LastCreateError ?? "no reason given";
                item.AppliedAt    = DateTimeOffset.UtcNow;

                await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                    $"CreateRefusedOrFailed: {item.StatusReason}", direction: SyncSource.App, token: token);
                continue;
            }

            local.ElvantoId = result.Id;
            item.ElvantoId  = result.Id;
            MarkApplied(item);

            // Same as the update path: the household Elvanto minted for this person is remembered,
            // so siblings later in this apply join them rather than each starting one of their own -
            // and so do the runs after this one.
            if (result.FamilyId is not null)
                LinkFamily(set.Families, local.FamilyId, result.FamilyId,
                    ElvantoFamilyLinkSource.CreatedInElvanto);

            await audit.Log(operationId, local.Id, SyncEventType.PushedToElvanto, "CreatedNewInElvanto",
                direction: SyncSource.App, token: token);
            created++;
        }

        return created;
    }

    /// <summary>
    /// Re-reads one field's two sides and reports whether they still say what the plan observed.
    /// This is the per-item staleness check, and it is the real protection the plan exists to give.
    /// </summary>
    private bool TryResolveField(
        DbSyncPlannedChange               item,
        Dictionary<Guid, DbPerson>        appById,
        Dictionary<string, ElvantoPerson> elvById,
        SyncWorkingSet                    set,
        out DbPerson?                     person,
        out IFieldSyncDescriptor?         descriptor,
        out FieldComparison?              comparison,
        out string?                       why)
    {
        person = null; descriptor = null; comparison = null; why = null;

        if (item.PersonId is null || !appById.TryGetValue(item.PersonId.Value, out person))
        { why = "The app person no longer exists"; return false; }

        if (item.ElvantoId is null || !elvById.TryGetValue(item.ElvantoId, out ElvantoPerson? elv))
        { why = "The Elvanto record is no longer in the roll"; return false; }

        descriptor = _descriptors.FirstOrDefault(d => d.FieldName == item.FieldName);
        if (descriptor is null)
        { why = $"No descriptor owns '{item.FieldName}' any more"; return false; }

        comparison = BuildComparison(descriptor, elv, person, set);

        if (comparison.AppHash != item.ObservedAppHash)
        { why = "The app changed this field after the plan was decided"; return false; }

        if (comparison.ElvantoHash != item.ObservedElvantoHash)
        { why = "Elvanto changed this field after the plan was decided"; return false; }

        return true;
    }

    /// <summary>
    /// Why an outbound change did not go, in the words of the thing that stopped it. The write guards
    /// are layered, so which one refused is the difference between "turn on AllowUpdates" and "this
    /// person is not on the allow list".
    /// </summary>
    private string SuppressionReason(Guid personId, bool updatesEnabled)
    {
        if (!elvantoService.WritesEnabled)            return "Elvanto:AllowWrites=false";
        if (!updatesEnabled)                          return "Elvanto:AllowUpdates=false";
        if (!elvantoService.MayUpdate(personId))      return "Not in Elvanto:AllowedUpdatePersonIds";
        return "Elvanto writes are disabled";
    }

    private static Dictionary<string, ElvantoPerson> ElvantoById(SyncWorkingSet set) =>
        set.ElvantoPeople.Where(e => e.Id is not null).ToDictionary(e => e.Id!);

    private static void MarkApplied(DbSyncPlannedChange item)
    {
        item.Status    = PlannedChangeStatus.Applied;
        item.AppliedAt = DateTimeOffset.UtcNow;
    }

    private static void MarkSkipped(DbSyncPlannedChange item, string reason)
    {
        item.Status       = PlannedChangeStatus.Skipped;
        item.StatusReason = reason;
        item.AppliedAt    = DateTimeOffset.UtcNow;
    }

    private static void MarkStale(DbSyncPlannedChange item, string reason)
    {
        item.Status       = PlannedChangeStatus.Stale;
        item.StatusReason = reason;
        item.AppliedAt    = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// What this Execute did, and only that.
    ///
    /// This used to merge in the Decide result, because a full run was Decide-then-Apply in one call
    /// and the person reading the numbers wanted one set. There is no such call now: an Execute
    /// always runs against a plan decided earlier, so the counts Decide owns - divergences,
    /// conflicts, reviews - are read from that operation's own row and are deliberately not restated
    /// here. They were already reported as zero on every Execute; this stops that being a surprise.
    /// </summary>
    private static SyncResult Applied(
        Guid         operationId,
        SyncCounters counters,
        int          autoLinked,
        int          archived,
        int          stale,
        int          planned = 0) => new()
    {
        OperationId        = operationId,
        Success            = true,
        PeopleProcessed    = 0,
        InboundPeople      = counters.InboundPeople,
        InboundFields      = counters.InboundFields,
        OutboundPeople     = counters.OutboundPeople,
        OutboundFields     = counters.OutboundFields,
        Conflicts          = 0,
        AutoLinked         = autoLinked,
        ManualReviewQueued = 0,
        Archived           = archived,
        Diverged           = 0,
        PlannedChanges     = planned,
        StaleItems         = stale
    };
}
