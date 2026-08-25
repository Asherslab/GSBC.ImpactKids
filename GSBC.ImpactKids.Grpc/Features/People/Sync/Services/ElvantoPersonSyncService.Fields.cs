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
    private async Task<FieldProcessResult> ProcessFieldsAsync(
        Guid                                               operationId,
        ElvantoPerson                                      elv,
        DbPerson                                           appPerson,
        Dictionary<string, DbSyncFieldConfig>              fieldConfigs,
        Dictionary<(Guid, string), DbElvantoFieldSnapshot> snapshots,
        Dictionary<(Guid, string), DateTimeOffset>         lastAppChange,
        SyncAuditLogger                                    audit,
        ElvantoSyncMode                                    mode,
        List<DbSchoolGrade>                                schoolGrades,
        Dictionary<string, Guid>                           familyIdMap,
        Dictionary<Guid, string>                           elvantoFamilyIdByLocal,
        Func<Guid, Guid, string?>                          resolveFamilyInElvanto,
        SyncCounters                                       counters,
        CancellationToken                                  token
    )
    {
        ElvantoUpdatePersonRequest? outboundReq = null;
        bool                        hadConflict = false;

        // Fields where the app won a conflict, so the app's value only survives if it actually
        // reaches Elvanto. Until it does, their snapshots must not move - see the hold below.
        List<string> appWonConflictFields = [];
        // Both gates, so an excluded person's changes are reported as "would push" rather than
        // pushed, and nothing is sent for them at all.
        bool         pushPossible         = mode == ElvantoSyncMode.Full
                                            && elvantoService.UpdatesEnabled
                                            && elvantoService.MayUpdate(appPerson.Id);

        foreach (IFieldSyncDescriptor desc in _descriptors)
        {
            if (!fieldConfigs.TryGetValue(desc.FieldName, out DbSyncFieldConfig? config))
                config = new DbSyncFieldConfig
                {
                    Id = Guid.Empty, EntityType = desc.EntityType, FieldName = desc.FieldName,
                    Direction = desc.DefaultDirection, PrecedenceOnTie = PrecedenceOnTie.Elvanto
                };

            string? appValue = desc.GetFromApp(appPerson);
            string? elvValue = desc.GetFromElvanto(elv);
            string  appHash  = desc.Hash(appValue);
            string  elvHash  = desc.Hash(elvValue);

            snapshots.TryGetValue((appPerson.Id, desc.FieldName), out DbElvantoFieldSnapshot? snapshot);
            string lastSeenHash = snapshot?.LastSeenHash ?? "";

            // No snapshot means no Elvanto history — can't claim it changed, only that we haven't seen it
            bool elvChanged = snapshot is not null && elvHash != lastSeenHash;
            lastAppChange.TryGetValue((appPerson.Id, desc.FieldName), out DateTimeOffset appChangedAt);
            bool appChanged = appChangedAt != default &&
                              (snapshot is null || appChangedAt > snapshot.LastSeenAt);

            elvValue = TranslateElvantoValue(desc.FieldName, elvValue, schoolGrades, familyIdMap);
            elvHash = desc.Hash(elvValue);

            // Family is compared in Elvanto's terms, not the app's. Translating Elvanto's family id
            // into a local Guid asks familyIdMap, which learns that pairing from the members - so a
            // person alone in the wrong Elvanto family translated back to their own local family and
            // compared equal to it. Naming the app's family as the Elvanto family its members agree
            // on, and comparing that against where this person actually sits, makes the difference
            // visible. Left alone when the app's family has no Elvanto id: nothing to compare with.
            string? appFamilyInElvanto = null;
            if (desc.FieldName == "FamilyId")
            {
                appFamilyInElvanto = resolveFamilyInElvanto(appPerson.FamilyId, appPerson.Id);
                appHash = desc.Hash(appFamilyInElvanto);
                elvHash = desc.Hash(desc.GetFromElvanto(elv));
            }

            if (appHash == elvHash) continue; // values already identical — stale snapshot/log, nothing to do

            // Past this line the two sides genuinely differ. Everything below either acts on that
            // or records a Diverged row saying why it did not; there is no third way out.
            if (config.Direction == SyncDirection.Disabled)
            {
                await LogDiverged(operationId, appPerson, desc, appValue, elvValue,
                    "Direction:Disabled", counters, audit, token);
                continue;
            }

            // If the Elvanto value is semantically empty for this field (e.g. NotRequested for MediaConsent),
            // suppress elvChanged so it can never drive an inbound update or win a conflict.
            bool elvValueUsable = desc.IsValidInboundValue(elvValue);
            if (!elvValueUsable)
                elvChanged = false;

            // Elvanto has a non-null value we have no snapshot for — treat as first-seen inbound
            bool elvFirstSeen = snapshot is null && elvValue is not null && elvValueUsable;
            if (!elvChanged && !appChanged && !elvFirstSeen)
            {
                // The single most common silent outcome, and the one that hides the whole restored-dump
                // backlog: the values differ, the change log has no row postdating the snapshot, and
                // Elvanto's hash matches what was last polled. Reported rather than skipped.
                await LogDiverged(operationId, appPerson, desc, appValue, elvValue,
                    elvValue is not null && !elvValueUsable
                        ? "NoChangeDetected:ElvantoValueNotUsable"
                        : "NoChangeDetected",
                    counters, audit, token);
                continue;
            }

            // First sight of a field means no snapshot, so neither "changed at" is trustworthy.
            // Most fields let Elvanto win; medical/allergy notes are the app's to state, since it
            // holds structured records rather than whatever text was in the box.
            bool firstSeenAppWins = elvFirstSeen && !elvChanged &&
                                    desc.FirstSyncPrecedence == SyncSource.App &&
                                    !string.IsNullOrWhiteSpace(appValue);

            if ((elvChanged || elvFirstSeen) && !appChanged && !firstSeenAppWins &&
                config.Direction is SyncDirection.Bidirectional or SyncDirection.InboundOnly)
            {
                desc.SetOnApp(appPerson, elvValue);
                logger.LogInformation(
                    "Sync {OperationId}: field {Field} inbound update for person {PersonId} ({FirstName} {LastName}) | {OldValue} -> {NewValue}",
                    operationId, desc.FieldName, appPerson.Id, appPerson.FirstName, appPerson.LastName, appValue,
                    elvValue);
                await audit.Log(operationId, appPerson.Id, SyncEventType.FieldUpdated,
                    "InboundFromElvanto", desc.FieldName, appValue, elvValue, SyncSource.Elvanto, token: token);
                counters.InboundFields++;
            }
            else if ((appChanged || firstSeenAppWins) && !elvChanged &&
                     config.Direction is SyncDirection.Bidirectional or SyncDirection.OutboundOnly)
            {
                // On a first sync the app wins, but a plain overwrite would delete free text
                // Elvanto holds and the app has no record of. The descriptor decides what
                // survives; every other field keeps the app value untouched.
                string? outboundValue = firstSeenAppWins
                    ? desc.MergeForFirstSync(appValue, elvValue)
                    : appValue;

                outboundReq ??= new ElvantoUpdatePersonRequest { Id = elv.Id! };
                ApplyOutbound(desc, outboundReq, outboundValue, appFamilyInElvanto);

                // Writes off means nothing leaves, so the audit trail must say "would push".
                // Otherwise a review of these entries reads as though Elvanto was changed.
                bool willSend = pushPossible;
                logger.LogInformation(
                    "Sync {OperationId}: field {Field} {Action} for person {PersonId} ({FirstName} {LastName}) | {OldValue} -> {NewValue}",
                    operationId, desc.FieldName, willSend ? "outbound update" : "would push",
                    appPerson.Id, appPerson.FirstName, appPerson.LastName, elvValue, outboundValue);
                await audit.Log(operationId, appPerson.Id,
                    willSend ? SyncEventType.PushedToElvanto : SyncEventType.WouldPushToElvanto,
                    willSend ? "OutboundToElvanto" : $"WouldPush:{mode}",
                    desc.FieldName, elvValue, outboundValue, SyncSource.App, token: token);

                counters.OutboundFields++;
            }
            else if (elvChanged && appChanged && config.Direction == SyncDirection.Bidirectional)
            {
                // Elvanto's own date_modified, not snapshot.LastSeenAt. LastSeenAt is when this
                // app last polled, so using it made the app win any conflict where it had been
                // edited since the last sync, whatever Elvanto did afterwards. Falls back to
                // LastSeenAt only when Elvanto gives no usable timestamp.
                DateTimeOffset? elvantoChangedAt = elv.LastChangedAtUtc ?? snapshot?.LastSeenAt;

                ConflictResolution resolution = conflictResolver.Resolve(
                    desc.FieldName, appValue, appChangedAt == default ? null : appChangedAt,
                    elvValue, elvantoChangedAt, config);

                logger.LogInformation(
                    "Sync {OperationId}: field {Field} conflict for person {PersonId} ({FirstName} {LastName}) | appValue={AppValue} appChangedAt={AppChangedAt} elvValue={ElvValue} elvChangedAt={ElvChangedAt} winner={Winner} reason={Reason}",
                    operationId, desc.FieldName, appPerson.Id, appPerson.FirstName, appPerson.LastName, appValue,
                    appChangedAt, elvValue, elvantoChangedAt, resolution.WinningSide, resolution.Reason);

                if (resolution.WinningSide == SyncSource.Elvanto)
                    desc.SetOnApp(appPerson, resolution.WinningValue);
                else
                {
                    outboundReq ??= new ElvantoUpdatePersonRequest { Id = elv.Id! };
                    ApplyOutbound(desc, outboundReq, resolution.WinningValue, appFamilyInElvanto);
                    appWonConflictFields.Add(desc.FieldName);
                }

                await audit.Log(operationId, appPerson.Id, SyncEventType.Conflict,
                    resolution.WinningSide == SyncSource.App && !pushPossible
                        ? $"{resolution.Reason}:PushSuppressed"
                        : resolution.Reason,
                    desc.FieldName, appValue, elvValue,
                    resolution.WinningSide, token: token);
                counters.Conflicts++;
                hadConflict = true;
            }
            else
            {
                // A side moved and the configured direction refuses to carry it. On InboundOnly with
                // an app-side edit nothing is logged today; on InboundOnly or OutboundOnly with both
                // sides moved, both changes are discarded AND the snapshot advances anyway. Naming
                // the refusal is what makes those rows findable.
                await LogDiverged(operationId, appPerson, desc, appValue, elvValue,
                    $"DirectionRefused:{config.Direction}:"
                    + $"app{(appChanged || firstSeenAppWins ? "Changed" : "Same")}:"
                    + $"elv{(elvChanged || elvFirstSeen ? "Changed" : "Same")}",
                    counters, audit, token);
            }
        }

        bool pushLanded = false;

        if (outboundReq is not null)
        {
            if (mode == ElvantoSyncMode.Full && pushPossible)
            {
                logger.LogInformation(
                    "Sync {OperationId}: {Action} outbound update to Elvanto for person {PersonId} ({FirstName} {LastName})",
                    operationId, pushPossible ? "sending" : "SUPPRESSING",
                    appPerson.Id, appPerson.FirstName, appPerson.LastName);
                // Called either way: with writes off this only builds and logs the payload.
                // The return distinguishes a write that actually landed from one that was
                // suppressed or refused, which is what decides the snapshot hold below.
                ElvantoService.UpdateOutcome outcome = await elvantoService.UpdatePersonAsync(outboundReq, token);
                pushLanded = outcome.Landed;

                // Elvanto created the family this person was moved into. Recorded both ways so the
                // rest of this run treats it as existing: another member moved into the same local
                // family joins this one, and an inbound person carrying the id maps back to it.
                if (outcome.NewFamilyId is not null)
                {
                    elvantoFamilyIdByLocal[appPerson.FamilyId] = outcome.NewFamilyId;
                    familyIdMap.TryAdd(outcome.NewFamilyId, appPerson.FamilyId);
                }
            }
            else
            {
                // Log the built payload here too. A dry run is the run that gets reviewed, so it
                // should show the same evidence a full run does rather than just a headline.
                logger.LogInformation(
                    "Sync {OperationId}: would send outbound update to Elvanto for person {PersonId} ({FirstName} {LastName}) (mode={Mode}). Payload: {Payload}",
                    operationId, appPerson.Id, appPerson.FirstName, appPerson.LastName, mode,
                    ElvantoService.DescribePayload(outboundReq));
            }
        }

        // A conflict the app won is only settled once Elvanto has the app's value. If the push was
        // suppressed or failed, advancing the snapshot would push LastSeenAt past the app's change
        // log entry, appChanged would read false on every later run, and the two sides would sit
        // silently divergent forever. Holding the snapshot keeps the conflict visible until the
        // write lands. UpdateSnapshotsAsync's own guard cannot cover this: a conflict is by
        // definition a field where Elvanto changed, so its snapshot would otherwise have to move.
        // A push that was possible but did not land is a finding, not a shrug. Recorded with
        // Elvanto's own words for the same reason the create path does it: the console is
        // unreachable in some environments, and "the update failed" without a reason costs a cycle.
        if (outboundReq is not null && pushPossible && !pushLanded)
            await audit.Log(operationId, appPerson.Id, SyncEventType.WouldPushToElvanto,
                $"PushFailed: {elvantoService.LastUpdateError ?? "no reason given"}",
                direction: SyncSource.App, token: token);

        IReadOnlyCollection<string> holdSnapshots = pushLanded ? [] : appWonConflictFields;

        if (holdSnapshots.Count > 0)
            logger.LogWarning(
                "Sync {OperationId}: holding snapshots for person {PersonId} ({FirstName} {LastName}) on {Fields} "
                + "- the app won the conflict but its value did not reach Elvanto, so the field stays unsettled",
                operationId, appPerson.Id, appPerson.FirstName, appPerson.LastName,
                string.Join(",", holdSnapshots));

        return new FieldProcessResult(hadConflict, holdSnapshots);
    }

    /// <summary>
    /// Records that two sides differ and the engine deliberately did nothing. Carries both values so
    /// the row is actionable on its own - a count alone would say a divergence exists without saying
    /// which field, on whom, or what the two sides hold.
    /// </summary>
    private async Task LogDiverged(
        Guid                 operationId,
        DbPerson             appPerson,
        IFieldSyncDescriptor desc,
        string?              appValue,
        string?              elvValue,
        string               reason,
        SyncCounters         counters,
        SyncAuditLogger      audit,
        CancellationToken    token)
    {
        logger.LogInformation(
            "Sync {OperationId}: field {Field} DIVERGED for person {PersonId} ({FirstName} {LastName}) "
            + "| app={AppValue} elvanto={ElvValue} reason={Reason}",
            operationId, desc.FieldName, appPerson.Id, appPerson.FirstName, appPerson.LastName,
            appValue, elvValue, reason);

        await audit.Log(operationId, appPerson.Id, SyncEventType.Diverged, reason,
            desc.FieldName, appValue, elvValue, token: token);
        counters.Diverged++;
    }

    /// <summary>
    /// Puts a field's outbound value on the request. Family cannot go through the descriptor: a
    /// descriptor instance is shared across everyone in the run, so it cannot answer "which Elvanto
    /// family is this person's local family?" - the answer depends on who is asking. Both the plain
    /// outbound branch and the conflict branch go through here, because they did not before: family
    /// was special-cased in one of them only, so an app-won family conflict resolved correctly and
    /// then sent an edit with no family on it, changing nothing and reporting success.
    /// </summary>
    private static void ApplyOutbound(
        IFieldSyncDescriptor       desc,
        ElvantoUpdatePersonRequest req,
        string?                    value,
        string?                    appFamilyInElvanto)
    {
        if (desc.FieldName == "FamilyId")
            req.FamilyId = appFamilyInElvanto ?? ElvantoService.NewFamily;
        else
            desc.ApplyToElvantoRequest(req, value);
    }

    /// <summary>
    /// Outcome of the per-field pass. <paramref name="HoldSnapshotFields"/> are fields whose
    /// snapshot must not advance this run, because the app's winning value has not reached Elvanto.
    /// </summary>
    private record FieldProcessResult(bool HadConflict, IReadOnlyCollection<string> HoldSnapshotFields);

    private async Task UpdateSnapshotsAsync(
        ElvantoPerson                                      elv,
        DbPerson                                           appPerson,
        Dictionary<(Guid, string), DbElvantoFieldSnapshot> snapshots,
        IReadOnlyCollection<string>                        holdFields,
        CancellationToken                                  token = default
    )
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (IFieldSyncDescriptor desc in _descriptors)
        {
            // Held because the app won a conflict on this field and its value has not reached
            // Elvanto yet. Recording what Elvanto currently says would settle a field that is
            // not settled, and the pending outbound change would never be offered again.
            if (holdFields.Contains(desc.FieldName)) continue;

            string? elvValue = desc.GetFromElvanto(elv);
            if (elvValue is null) continue;

            string                      hash = desc.Hash(elvValue);
            (Guid Id, string FieldName) key  = (appPerson.Id, desc.FieldName);

            if (snapshots.TryGetValue(key, out DbElvantoFieldSnapshot? existing))
            {
                // Only advance LastSeenAt when Elvanto's value actually changed.
                // Bumping the timestamp unconditionally would push it past any pending app changes,
                // causing appChanged = false on the next run and silently dropping outbound updates.
                if (existing.LastSeenHash != hash)
                {
                    existing.LastSeenHash = hash;
                    existing.LastSeenValue = elvValue;
                    existing.LastSeenAt = now;
                }
            }
            else
            {
                DbElvantoFieldSnapshot snap = new()
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Person",
                    EntityId = appPerson.Id,
                    FieldName = desc.FieldName,
                    LastSeenHash = hash,
                    LastSeenValue = elvValue,
                    LastSeenAt = now
                };
                await db.ElvantoFieldSnapshots.AddAsync(snap, token);
                snapshots[key] = snap;
            }
        }
    }
}
