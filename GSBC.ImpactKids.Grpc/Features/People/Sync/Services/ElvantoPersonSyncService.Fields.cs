using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Decides every field for one person and records what it decided.
    ///
    /// One loop, on purpose. It used to be two — a decision loop that defaulted to <i>skip</i> and a
    /// separate snapshot loop that defaulted to <i>advance</i>, bridged by a hold list with one entry
    /// — and the two disagreed. The snapshot loop ignored the field config entirely, so a run could
    /// discard a change and mark the divergence seen in the same pass.
    /// </summary>
    private async Task PlanFieldsAsync(
        Guid                      operationId,
        ElvantoPerson             elv,
        DbPerson                  appPerson,
        SyncWorkingSet            set,
        List<DbSyncPlannedChange> plan,
        SyncCounters              counters,
        SyncAuditLogger           audit,
        CancellationToken         token)
    {
        foreach (IFieldSyncDescriptor desc in _descriptors)
        {
            FieldComparison comparison = BuildComparison(desc, elv, appPerson, set);
            FieldDecision   decision   = fieldReconciler.Decide(desc, comparison);

            switch (decision.Kind)
            {
                case FieldDecisionKind.Skipped:
                    continue;

                case FieldDecisionKind.Agreed:
                    // Settled here rather than in Apply: there is nothing to apply. Recording that
                    // the two sides already say the same thing changes neither of them, and it is
                    // what stops the next run re-deriving every field from first-sync rules.
                    await SettleBaseAsync(appPerson.Id, desc, comparison.AppValue, comparison.ElvantoValue,
                        set.Bases, token);
                    continue;

                case FieldDecisionKind.Diverged:
                    await LogDiverged(operationId, appPerson, desc, comparison, decision.Reason,
                        counters, audit, token);
                    continue;

                case FieldDecisionKind.Inbound:
                    plan.Add(PlannedField(operationId, PlannedChangeKind.InboundField,
                        appPerson.Id, elv.Id, desc.FieldName, comparison, decision.Value, decision.Reason));

                    if (decision.WasConflict) counters.Conflicts++;
                    else counters.InboundFields++;
                    continue;

                case FieldDecisionKind.Outbound:
                    // "Carried" means the payload genuinely holds this field, not that the descriptor
                    // was asked. Elvanto answers ok to an omitted field and to an explicit null alike
                    // and changes nothing, so planning a push the payload cannot express would
                    // promise a change that can never happen.
                    if (!WouldCarry(desc, decision.Value))
                    {
                        await LogDiverged(operationId, appPerson, desc, comparison,
                            $"NotCarried:{decision.Reason}", counters, audit, token);
                        continue;
                    }

                    plan.Add(PlannedField(operationId, PlannedChangeKind.OutboundField,
                        appPerson.Id, elv.Id, desc.FieldName, comparison, decision.Value, decision.Reason));

                    if (decision.WasConflict) counters.Conflicts++;
                    else counters.OutboundFields++;
                    continue;
            }
        }
    }

    /// <summary>
    /// Whether the built payload would genuinely carry this field. Asked against a throwaway request
    /// rather than trusting the descriptor to be asked, which is the same question Apply answers
    /// against the real one.
    /// </summary>
    private static bool WouldCarry(IFieldSyncDescriptor desc, string? value) =>
        ApplyOutbound(desc, new ElvantoUpdatePersonRequest { Id = "probe" }, value);

    private static DbSyncPlannedChange PlannedField(
        Guid              operationId,
        PlannedChangeKind kind,
        Guid              personId,
        string?           elvantoId,
        string            fieldName,
        FieldComparison   comparison,
        string?           proposedValue,
        string            reason) => new()
    {
        Id                   = Guid.NewGuid(),
        SyncOperationId      = operationId,
        PersonId             = personId,
        ElvantoId            = elvantoId,
        Kind                 = kind,
        FieldName            = fieldName,
        ObservedAppHash      = comparison.AppHash,
        ObservedAppValue     = comparison.AppValue,
        ObservedElvantoHash  = comparison.ElvantoHash,
        ObservedElvantoValue = comparison.ElvantoValue,
        ProposedValue        = proposedValue,
        Reason               = reason,
        Status               = PlannedChangeStatus.Pending,
        DecidedAt            = DateTimeOffset.UtcNow
    };

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
        IFieldSyncDescriptor desc,
        ElvantoPerson        elv,
        DbPerson             appPerson,
        SyncWorkingSet       set)
    {
        set.Bases.TryGetValue((appPerson.Id, desc.FieldName), out DbElvantoFieldSnapshot? baseRow);
        set.LastAppChange.TryGetValue((appPerson.Id, desc.FieldName), out DateTimeOffset appChangedAt);

        string?    rawElvValue = desc.GetFromElvanto(elv);
        string?    appValue    = desc.GetFromApp(appPerson);
        Translated inbound     = TranslateElvantoValue(desc.FieldName, rawElvValue, set, appPerson);

        bool isFamily = desc.FieldName == "FamilyId";

        // Family is compared in the APP's terms - "is this person in the right local family?" - which
        // is a fact the app owns. Comparing in Elvanto's terms instead asked "which Elvanto household
        // does this person's local family mostly correspond to?", and a family split across two
        // Elvanto households then disagreed with itself forever: the inbound move was a no-op that
        // settled the base anyway, and the next run planned to push the person back.
        //
        // The reason it was ever compared the other way round was a self-confirming map, and that is
        // fixed at the source: TranslateFamily excludes the person being asked about, exactly as
        // ResolveFamilyInElvanto always has.
        string? comparedApp = appValue;
        string? comparedElv = inbound.Value;

        // Unreadable now covers only a school grade with no local row and a bucketed person's
        // household - an Elvanto household this app simply has no row for is no longer unreadable,
        // it is recorded on first sight. Neither may drive a write in either direction, and neither
        // is the same as Elvanto holding nothing, which is its own answer again.
        bool elvValueKnown = inbound.Known;

        // Outbound has to speak Elvanto's language even though the comparison speaks the app's: the
        // Elvanto household this person's local family is, or "new" when it has none - which is a
        // household to create, not a family to clear. Read from the stored pairing rather than from
        // the other members the run happened to fetch, so it is the same answer every run.
        // Guid.Empty is the app saying "no household", and there is nothing to push: "new" would ask
        // Elvanto to create 400 one-person households, which is the inverse of what the value means.
        // Left null it is refused by ApplyOutbound and reported, rather than silently inverted.
        string? outboundValue = isFamily
            ? appPerson.FamilyId == Guid.Empty
                ? null
                : set.Families.ElvantoFor(appPerson.FamilyId) ?? ElvantoService.NewFamily
            : comparedApp;

        return new FieldComparison
        {
            AppValue           = comparedApp,
            ElvantoValue       = comparedElv,
            AppHash            = desc.Hash(comparedApp),
            ElvantoHash        = desc.Hash(comparedElv),
            BaseAppHash        = baseRow?.AppHash,
            BaseElvantoHash    = baseRow?.LastSeenHash,
            ElvantoValueUsable = desc.IsValidInboundValue(comparedElv),
            ElvantoValueKnown = elvValueKnown,
            ElvantoDetail     = inbound.Detail,
            AppChangedAt       = appChangedAt == default ? null : appChangedAt,
            // Elvanto's own date_modified, not the base's timestamp. The base records when the two
            // sides last agreed; using it as Elvanto's edit time made the app win any conflict where
            // it had been edited since, whatever Elvanto did afterwards.
            ElvantoChangedAt   = elv.LastChangedAtUtc ?? baseRow?.LastSeenAt,
            InboundValue       = inbound.Value,
            OutboundValue      = outboundValue
        };
    }

    /// <summary>
    /// Writes what both sides hold now as the field's new base.
    ///
    /// A base may advance when the field has no outstanding app-side change, <b>or</b> when the
    /// request that was actually sent carried it and landed. There is no third case, and the caller
    /// is the one that knows which — Decide settles agreements, Apply settles what it applied.
    /// </summary>
    private async Task SettleBaseAsync(
        Guid                                               personId,
        IFieldSyncDescriptor                               desc,
        string?                                            appValue,
        string?                                            elvantoValue,
        Dictionary<(Guid, string), DbElvantoFieldSnapshot> bases,
        CancellationToken                                  token)
    {
        (Guid, string) key = (personId, desc.FieldName);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (bases.TryGetValue(key, out DbElvantoFieldSnapshot? existing))
        {
            existing.AppHash       = desc.Hash(appValue);
            existing.AppValue      = appValue;
            existing.LastSeenHash  = desc.Hash(elvantoValue);
            existing.LastSeenValue = elvantoValue;
            existing.LastSeenAt    = now;
            return;
        }

        DbElvantoFieldSnapshot created = new()
        {
            Id            = Guid.NewGuid(),
            EntityType    = "Person",
            EntityId      = personId,
            FieldName     = desc.FieldName,
            AppHash       = desc.Hash(appValue),
            AppValue      = appValue,
            LastSeenHash  = desc.Hash(elvantoValue),
            LastSeenValue = elvantoValue,
            LastSeenAt    = now
        };
        await db.ElvantoFieldSnapshots.AddAsync(created, token);
        bases[key] = created;
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
        string?                    value)
    {
        if (desc.FieldName != "FamilyId")
            return desc.ApplyToElvantoRequest(req, value);

        // Refused rather than defaulted. "no value" and "make a new household" are opposite
        // instructions, and reading the first as the second is how a cleared family would become 400
        // creates. A genuine new family arrives here as ElvantoService.NewFamily explicitly.
        if (string.IsNullOrEmpty(value)) return false;

        req.FamilyId = value;
        return true;
    }
}
