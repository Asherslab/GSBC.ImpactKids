using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Decides every field for one person, sends whatever the decisions add up to, and settles the
    /// bases the send actually earned.
    ///
    /// This is one loop on purpose. It used to be two - a decision loop that defaulted to <i>skip</i>
    /// and a separate snapshot loop that defaulted to <i>advance</i>, bridged by a hold list with one
    /// entry - and the two disagreed. The snapshot loop ignored the field config entirely, so a run
    /// could discard a change and mark the divergence seen in the same pass, which is how a pending
    /// change was reported as "would push" and then buried by the very run that reported it.
    /// </summary>
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

        // Bases that may be written once this person's work is done, and what each one is waiting on.
        List<PendingBase> pendingBases = [];

        // Both gates, so an excluded person's changes are reported as "would push" rather than
        // pushed, and nothing is sent for them at all.
        bool pushPossible = mode == ElvantoSyncMode.Full
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

            snapshots.TryGetValue((appPerson.Id, desc.FieldName), out DbElvantoFieldSnapshot? baseRow);
            lastAppChange.TryGetValue((appPerson.Id, desc.FieldName), out DateTimeOffset appChangedAt);

            FieldComparison comparison = BuildComparison(
                desc, elv, appPerson, baseRow, appChangedAt, schoolGrades, familyIdMap,
                resolveFamilyInElvanto);

            FieldDecision decision = fieldReconciler.Decide(desc, comparison, config);

            switch (decision.Kind)
            {
                case FieldDecisionKind.Skipped:
                    continue;

                case FieldDecisionKind.Agreed:
                    pendingBases.Add(new PendingBase(desc, comparison.AppValue, comparison.ElvantoValue));
                    continue;

                case FieldDecisionKind.Diverged:
                    await LogDiverged(operationId, appPerson, desc, comparison, decision.Reason,
                        counters, audit, token);
                    continue;

                case FieldDecisionKind.Inbound:
                    desc.SetOnApp(appPerson, decision.Value);
                    logger.LogInformation(
                        "Sync {OperationId}: field {Field} inbound update for person {PersonId} ({FirstName} {LastName}) | {OldValue} -> {NewValue}",
                        operationId, desc.FieldName, appPerson.Id, appPerson.FirstName, appPerson.LastName,
                        comparison.AppValue, comparison.ElvantoValue);

                    await audit.Log(operationId, appPerson.Id,
                        decision.WasConflict ? SyncEventType.Conflict : SyncEventType.FieldUpdated,
                        decision.WasConflict ? decision.Reason : "InboundFromElvanto",
                        desc.FieldName, comparison.AppValue, comparison.ElvantoValue, SyncSource.Elvanto,
                        token: token);

                    // The app now holds Elvanto's value, so both legs settle on it. Nothing is
                    // outstanding on the app side - the reconciler only chose inbound because the
                    // app had not moved, or because Elvanto won a conflict outright.
                    pendingBases.Add(new PendingBase(desc, comparison.ElvantoValue, comparison.ElvantoValue));

                    if (decision.WasConflict) { counters.Conflicts++; hadConflict = true; }
                    else counters.InboundFields++;
                    continue;

                case FieldDecisionKind.Outbound:
                    outboundReq ??= new ElvantoUpdatePersonRequest { Id = elv.Id! };

                    // "Carried" means the built payload genuinely holds this field, not that the
                    // descriptor was asked. Elvanto answers ok to an omitted field and to an explicit
                    // null alike and changes nothing, so a descriptor that declines and is treated as
                    // having pushed buries the change it was given.
                    bool carried = ApplyOutbound(desc, outboundReq, decision.Value, comparison.AppValue);

                    bool willSend = pushPossible;
                    logger.LogInformation(
                        "Sync {OperationId}: field {Field} {Action} for person {PersonId} ({FirstName} {LastName}) | {OldValue} -> {NewValue} (carried={Carried})",
                        operationId, desc.FieldName, willSend ? "outbound update" : "would push",
                        appPerson.Id, appPerson.FirstName, appPerson.LastName,
                        comparison.ElvantoValue, decision.Value, carried);

                    await audit.Log(operationId, appPerson.Id,
                        willSend ? SyncEventType.PushedToElvanto : SyncEventType.WouldPushToElvanto,
                        DescribeOutbound(decision, willSend, carried, mode),
                        desc.FieldName, comparison.ElvantoValue, decision.Value, SyncSource.App,
                        token: token);

                    // Waiting on the send. Nothing settles until the request that carried this field
                    // has actually landed - which with writes off it never does, so the change stays
                    // outstanding and is offered again next run instead of being marked seen.
                    if (carried)
                        pendingBases.Add(new PendingBase(desc, comparison.AppValue, decision.Value, AwaitsSend: true));

                    if (decision.WasConflict) { counters.Conflicts++; hadConflict = true; }
                    else counters.OutboundFields++;
                    continue;
            }
        }

        bool pushLanded = await SendOutboundAsync(
            operationId, elv, appPerson, outboundReq, pushPossible, mode,
            elvantoFamilyIdByLocal, familyIdMap, audit, token);

        await SettleBasesAsync(appPerson, pendingBases, pushLanded, snapshots, token);

        return new FieldProcessResult(hadConflict);
    }

    /// <summary>
    /// Puts one field's two sides and their base into a single comparison space.
    ///
    /// Two fields are not naturally in one. Family is compared in <b>Elvanto's</b> terms: translating
    /// an Elvanto family id back into a local Guid asks a map that learns the pairing from the
    /// members, so a person alone in the wrong Elvanto family translated back to their own local
    /// family and compared equal to it. School grade is compared in the <b>app's</b>, because Elvanto
    /// owns the grade ids and the app owns the rows they point at.
    /// </summary>
    private FieldComparison BuildComparison(
        IFieldSyncDescriptor      desc,
        ElvantoPerson             elv,
        DbPerson                  appPerson,
        DbElvantoFieldSnapshot?   baseRow,
        DateTimeOffset            appChangedAt,
        List<DbSchoolGrade>       schoolGrades,
        Dictionary<string, Guid>  familyIdMap,
        Func<Guid, Guid, string?> resolveFamilyInElvanto)
    {
        string? rawElvValue = desc.GetFromElvanto(elv);
        string? appValue    = desc.GetFromApp(appPerson);
        string? inbound     = TranslateElvantoValue(desc.FieldName, rawElvValue, schoolGrades, familyIdMap);

        // Family: both sides named as Elvanto family ids. The value written onto the app is still
        // the translated local Guid, and the outbound value is resolved by the orchestrator, which
        // is the only thing that can answer "which Elvanto family is this person's local family?".
        bool    isFamily     = desc.FieldName == "FamilyId";
        string? comparedApp  = isFamily ? resolveFamilyInElvanto(appPerson.FamilyId, appPerson.Id) : appValue;
        string? comparedElv  = isFamily ? rawElvValue : inbound;

        return new FieldComparison
        {
            AppValue           = comparedApp,
            ElvantoValue       = comparedElv,
            AppHash            = desc.Hash(comparedApp),
            ElvantoHash        = desc.Hash(comparedElv),
            BaseAppHash        = baseRow?.AppHash,
            BaseElvantoHash    = baseRow?.LastSeenHash,
            ElvantoValueUsable = desc.IsValidInboundValue(comparedElv),
            AppChangedAt       = appChangedAt == default ? null : appChangedAt,
            // Elvanto's own date_modified, not the base's timestamp. The base records when the two
            // sides last agreed; using it as Elvanto's edit time made the app win any conflict where
            // it had been edited since, whatever Elvanto did afterwards.
            ElvantoChangedAt   = elv.LastChangedAtUtc ?? baseRow?.LastSeenAt,
            InboundValue       = inbound,
            OutboundValue      = comparedApp
        };
    }

    private static string DescribeOutbound(FieldDecision decision, bool willSend, bool carried, ElvantoSyncMode mode)
    {
        if (!carried) return $"NotCarried:{decision.Reason}";
        return willSend ? decision.Reason : $"WouldPush:{mode}:{decision.Reason}";
    }

    /// <summary>
    /// Sends the person's accumulated edit, if anything is going out at all. Returns whether it
    /// landed - the one fact the bases are allowed to settle on.
    /// </summary>
    private async Task<bool> SendOutboundAsync(
        Guid                        operationId,
        ElvantoPerson               elv,
        DbPerson                    appPerson,
        ElvantoUpdatePersonRequest? outboundReq,
        bool                        pushPossible,
        ElvantoSyncMode             mode,
        Dictionary<Guid, string>    elvantoFamilyIdByLocal,
        Dictionary<string, Guid>    familyIdMap,
        SyncAuditLogger             audit,
        CancellationToken           token)
    {
        if (outboundReq is null) return false;

        if (!(mode == ElvantoSyncMode.Full && pushPossible))
        {
            // Log the built payload here too. A dry run is the run that gets reviewed, so it should
            // show the same evidence a full run does rather than just a headline.
            logger.LogInformation(
                "Sync {OperationId}: would send outbound update to Elvanto for person {PersonId} ({FirstName} {LastName}) (mode={Mode}). Payload: {Payload}",
                operationId, appPerson.Id, appPerson.FirstName, appPerson.LastName, mode,
                ElvantoService.DescribePayload(outboundReq));
            return false;
        }

        logger.LogInformation(
            "Sync {OperationId}: sending outbound update to Elvanto for person {PersonId} ({FirstName} {LastName})",
            operationId, appPerson.Id, appPerson.FirstName, appPerson.LastName);

        ElvantoService.UpdateOutcome outcome = await elvantoService.UpdatePersonAsync(outboundReq, token);

        // Elvanto created the family this person was moved into. Recorded both ways so the rest of
        // this run treats it as existing: another member moved into the same local family joins this
        // one, and an inbound person carrying the id maps back to it.
        if (outcome.NewFamilyId is not null)
        {
            elvantoFamilyIdByLocal[appPerson.FamilyId] = outcome.NewFamilyId;
            familyIdMap.TryAdd(outcome.NewFamilyId, appPerson.FamilyId);
        }

        // A push that was possible but did not land is a finding, not a shrug. Recorded with
        // Elvanto's own words for the same reason the create path does it: the console is
        // unreachable in some environments, and "the update failed" without a reason costs a cycle.
        if (!outcome.Landed)
            await audit.Log(operationId, appPerson.Id, SyncEventType.WouldPushToElvanto,
                $"PushFailed: {elvantoService.LastUpdateError ?? "no reason given"}",
                direction: SyncSource.App, token: token);

        return outcome.Landed;
    }

    /// <summary>
    /// Writes the bases the run earned, and only those.
    ///
    /// A base may advance when the field has no outstanding app-side change, <b>or</b> when the
    /// request that was actually sent carried it and landed. There is no third case: with writes off
    /// nothing lands, so every outbound change stays outstanding and is offered again next run. That
    /// is the whole guarantee - the documented dry-fire procedure used to consume the very changes
    /// it reported as "would push", an event that reads as a promise they will push next time.
    /// </summary>
    private async Task SettleBasesAsync(
        DbPerson                                           appPerson,
        List<PendingBase>                                  pending,
        bool                                               pushLanded,
        Dictionary<(Guid, string), DbElvantoFieldSnapshot> snapshots,
        CancellationToken                                  token)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (PendingBase item in pending)
        {
            if (item.AwaitsSend && !pushLanded)
            {
                logger.LogInformation(
                    "Sync: holding the base for {Field} on person {PersonId} - the app's value has not "
                    + "reached Elvanto, so the field is not settled",
                    item.Descriptor.FieldName, appPerson.Id);
                continue;
            }

            (Guid, string) key = (appPerson.Id, item.Descriptor.FieldName);

            if (snapshots.TryGetValue(key, out DbElvantoFieldSnapshot? existing))
            {
                existing.AppHash       = item.Descriptor.Hash(item.AppValue);
                existing.AppValue      = item.AppValue;
                existing.LastSeenHash  = item.Descriptor.Hash(item.ElvantoValue);
                existing.LastSeenValue = item.ElvantoValue;
                existing.LastSeenAt    = now;
                continue;
            }

            DbElvantoFieldSnapshot created = new()
            {
                Id            = Guid.NewGuid(),
                EntityType    = "Person",
                EntityId      = appPerson.Id,
                FieldName     = item.Descriptor.FieldName,
                AppHash       = item.Descriptor.Hash(item.AppValue),
                AppValue      = item.AppValue,
                LastSeenHash  = item.Descriptor.Hash(item.ElvantoValue),
                LastSeenValue = item.ElvantoValue,
                LastSeenAt    = now
            };
            await db.ElvantoFieldSnapshots.AddAsync(created, token);
            snapshots[key] = created;
        }
    }

    /// <summary>
    /// A base waiting to be written. <paramref name="AwaitsSend"/> marks the ones that may only be
    /// written if the request carrying them actually reached Elvanto.
    /// </summary>
    private sealed record PendingBase(
        IFieldSyncDescriptor Descriptor,
        string?              AppValue,
        string?              ElvantoValue,
        bool                 AwaitsSend = false);

    /// <summary>
    /// Records that two sides differ and the engine deliberately did nothing. Carries both values so
    /// the row is actionable on its own - a count alone would say a divergence exists without saying
    /// which field, on whom, or what the two sides hold.
    /// </summary>
    private async Task LogDiverged(
        Guid                 operationId,
        DbPerson             appPerson,
        IFieldSyncDescriptor desc,
        FieldComparison      comparison,
        string               reason,
        SyncCounters         counters,
        SyncAuditLogger      audit,
        CancellationToken    token)
    {
        logger.LogInformation(
            "Sync {OperationId}: field {Field} DIVERGED for person {PersonId} ({FirstName} {LastName}) "
            + "| app={AppValue} elvanto={ElvValue} reason={Reason}",
            operationId, desc.FieldName, appPerson.Id, appPerson.FirstName, appPerson.LastName,
            comparison.AppValue, comparison.ElvantoValue, reason);

        await audit.Log(operationId, appPerson.Id, SyncEventType.Diverged, reason,
            desc.FieldName, comparison.AppValue, comparison.ElvantoValue, token: token);
        counters.Diverged++;
    }

    /// <summary>
    /// Puts a field's outbound value on the request and reports whether the payload genuinely
    /// carries it.
    ///
    /// Family cannot go through the descriptor: a descriptor instance is shared across everyone in
    /// the run, so it cannot answer "which Elvanto family is this person's local family?" - the
    /// answer depends on who is asking.
    /// </summary>
    private static bool ApplyOutbound(
        IFieldSyncDescriptor       desc,
        ElvantoUpdatePersonRequest req,
        string?                    value,
        string?                    appFamilyInElvanto)
    {
        if (desc.FieldName != "FamilyId")
            return desc.ApplyToElvantoRequest(req, value);

        req.FamilyId = appFamilyInElvanto ?? ElvantoService.NewFamily;
        return true;
    }

    /// <summary>Outcome of the per-field pass.</summary>
    private record FieldProcessResult(bool HadConflict);
}
