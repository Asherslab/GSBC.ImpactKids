using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
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
    private async Task<SyncResult> ApplyPlanAsync(Guid operationId, SyncResult? decided, CancellationToken token)
    {
        DbSyncOperation? operation = await db.SyncOperations
            .FirstOrDefaultAsync(x => x.Id == operationId, token);

        if (operation is null)
            return SyncResult.Failed(operationId, ElvantoSyncMode.Full, "Sync operation not found");

        ElvantoSyncMode mode = UnmapMode(operation.Mode);

        // Expiry is a backstop against a failure the per-item check cannot catch. A stale item is one
        // whose values moved; expiry guards against the SET of items being wrong - people created,
        // deleted or merged in Elvanto since Decide ran, which no per-item check can see because
        // those items are not in the plan. The whole plan is refused, never part of it.
        if (operation.PlanExpiresAt is { } expiry && DateTimeOffset.UtcNow > expiry)
            return SyncResult.Failed(operationId, mode,
                $"This plan expired at {expiry:u}. Run a new sync — the roll may have moved since it was decided.");

        List<DbSyncPlannedChange> plan = await db.PlannedChanges
            .Where(x => x.SyncOperationId == operationId && x.Status == PlannedChangeStatus.Pending)
            .ToListAsync(token);

        if (plan.Count == 0)
            return Merge(decided, operationId, mode, new SyncCounters(), 0, 0, 0);

        SyncWithElvantoRequest request = new()
        {
            Mode     = mode,
            Scope    = UnmapScope(operation.Scope),
            PersonId = operation.PersonId,
            FamilyId = operation.FamilyId
        };

        SyncAuditLogger audit = new(db);

        try
        {
            using IDisposable _ = syncContext.SetSource(SyncSource.Elvanto);

            (SyncWorkingSet? set, string? refusal) = await LoadWorkingSetAsync(operationId, request, token);
            if (set is null)
            {
                logger.LogError("Sync {OperationId}: apply refused — {Reason}", operationId, refusal);
                return SyncResult.Failed(operationId, mode, refusal!);
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

                DbPerson created = CreatePersonFromElvanto(elv, set.SchoolGrades, set.FamilyIdMap);
                await db.People.AddAsync(created, token);
                set.AppByElvantoId[item.ElvantoId] = created;
                appById[created.Id] = created;

                item.PersonId = created.Id;
                MarkApplied(item);

                await audit.Log(operationId, created.Id, SyncEventType.Created, "NewFromElvanto",
                    direction: SyncSource.Elvanto, token: token);
                await UpsertMetadata(created, item.ElvantoId, 100, "DirectElvantoId", set.Metadata, token);
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
                await UpsertMetadata(person, item.ElvantoId, 100, item.Reason, set.Metadata, token);
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

                desc!.SetOnApp(person!, item.ProposedValue);
                MarkApplied(item);

                await audit.Log(operationId, person!.Id, SyncEventType.FieldUpdated, item.Reason,
                    item.FieldName, item.ObservedAppValue, item.ObservedElvantoValue, SyncSource.Elvanto,
                    token: token);

                // The app now holds Elvanto's value, so both legs settle on it. Nothing is
                // outstanding on the app side: the reconciler only chose inbound because the app had
                // not moved, or because Elvanto won a conflict outright.
                await SettleBaseAsync(person.Id, desc, now!.ElvantoValue, now.ElvantoValue, set.Bases, token);
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
                operationId, plan, appById, set, audit, mode, token);

            counters.OutboundPeople += await ApplyCreatesInElvantoAsync(
                operationId, plan, appById, set, audit, mode, token);

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

            return Merge(decided, operationId, mode, counters, autoLinked, archived, stale, plan.Count);
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

            return SyncResult.Failed(operationId, mode, ex.Message);
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
        ElvantoSyncMode            mode,
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

            // AppOnly means local changes only. Named as a skip rather than dropped, so the plan
            // still shows the work and the reason it did not go.
            bool mayPush = mode != ElvantoSyncMode.AppOnly
                           && elvantoService.UpdatesEnabled
                           && elvantoService.MayUpdate(person.Id);

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

                if (!ApplyOutbound(desc!, request, item.ProposedValue, now!.AppValue))
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
                    "Sync {OperationId}: would send outbound update for person {PersonId} ({FirstName} {LastName}) "
                    + "(mode={Mode}). Payload: {Payload}",
                    operationId, person.Id, person.FirstName, person.LastName, mode,
                    ElvantoService.DescribePayload(request));

                // Named by what actually stopped it, not by the mode the plan happened to be decided
                // in. An Execute of a plan a dry run produced is not itself a dry run, and a row
                // saying "WouldPush:DryRun" for it is the same kind of tense-lie the audit trail is
                // being cleaned of.
                string suppression = SuppressionReason(mode, person.Id, elvantoService.UpdatesEnabled);

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

            // Elvanto created the family this person was moved into. Recorded both ways so the rest
            // of this apply treats it as existing.
            if (outcome.NewFamilyId is not null)
            {
                set.ElvantoFamilyIdByLocal[person.FamilyId] = outcome.NewFamilyId;
                set.FamilyIdMap.TryAdd(outcome.NewFamilyId, person.FamilyId);
            }

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
        ElvantoSyncMode            mode,
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
            bool knownFamily = set.ElvantoFamilyIdByLocal.TryGetValue(local.FamilyId, out string? elvantoFamilyId);
            if (!knownFamily) elvantoFamilyId = ElvantoService.NewFamily;

            string payload = elvantoService.DescribeCreatePayload(
                local, ComposeMedicalAllergyText(local), elvantoFamilyId);

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

            if (mode == ElvantoSyncMode.AppOnly || !elvantoService.CreatesEnabled)
            {
                string suppression = mode == ElvantoSyncMode.AppOnly
                    ? "AppOnly run"
                    : "Elvanto:AllowCreates=false";

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

            if (result.FamilyId is not null)
            {
                set.ElvantoFamilyIdByLocal[local.FamilyId] = result.FamilyId;
                set.FamilyIdMap.TryAdd(result.FamilyId, local.FamilyId);
            }

            await UpsertMetadata(local, result.Id, 100, "CreatedOutbound", set.Metadata, token);
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
    private string SuppressionReason(ElvantoSyncMode mode, Guid personId, bool updatesEnabled)
    {
        if (mode == ElvantoSyncMode.AppOnly)          return "AppOnly run";
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
    /// A full run is Decide then Apply, and the person reading the numbers wants one set. The
    /// divergences and reviews belong to Decide; everything else is what Apply actually did.
    /// </summary>
    private static SyncResult Merge(
        SyncResult?  decided,
        Guid         operationId,
        ElvantoSyncMode mode,
        SyncCounters counters,
        int          autoLinked,
        int          archived,
        int          stale,
        int          planned = 0) => new()
    {
        OperationId        = operationId,
        Mode               = mode,
        Success            = true,
        PeopleProcessed    = decided?.PeopleProcessed ?? 0,
        InboundPeople      = counters.InboundPeople,
        InboundFields      = counters.InboundFields,
        OutboundPeople     = counters.OutboundPeople,
        OutboundFields     = counters.OutboundFields,
        Conflicts          = decided?.Conflicts ?? 0,
        AutoLinked         = autoLinked,
        ManualReviewQueued = decided?.ManualReviewQueued ?? 0,
        Archived           = archived,
        Diverged           = decided?.Diverged ?? 0,
        PlannedChanges     = planned,
        StaleItems         = stale,
        ManualReviewItems  = decided?.ManualReviewItems ?? [],
        AuditLog           = decided?.AuditLog ?? []
    };
}
