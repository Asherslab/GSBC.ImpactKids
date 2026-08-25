using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using Microsoft.EntityFrameworkCore;
using GrpcReviewStatus = GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums.ManualReviewStatus;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Reads both sides and records what should happen. Writes the plan, the divergences, the
    /// pending reviews, and the bases of fields that already agree — and nothing else.
    ///
    /// An agreement settles here rather than in Apply because there is nothing to apply: recording
    /// that two sides already say the same thing changes neither of them. Everything that needs an
    /// action becomes a plan row and stays outstanding until that action lands.
    /// </summary>
    private async Task<SyncResult> DecideAsync(SyncWithElvantoRequest request, CancellationToken token)
    {
        Guid            operationId = Guid.NewGuid();
        SyncAuditLogger audit       = new(db);

        DbSyncOperation operation = new()
        {
            Id            = operationId,
            Mode          = MapMode(request.Mode),
            Scope         = MapScope(request.Scope),
            PersonId      = request.Scope == ElvantoSyncScope.Person ? request.PersonId : null,
            FamilyId      = request.Scope == ElvantoSyncScope.Family ? request.FamilyId : null,
            StartedAt     = DateTimeOffset.UtcNow,
            PlanExpiresAt = DateTimeOffset.UtcNow.AddHours(elvantoConfig.PlanExpiryHours)
        };

        logger.LogInformation(
            "Sync operation {OperationId} deciding | Mode={Mode} Scope={Scope}",
            operationId, request.Mode, request.Scope);

        // Written before anything references it: the plan rows carry a foreign key to it, and a run
        // that dies mid-decide should still leave a row saying it started.
        await db.SyncOperations.AddAsync(operation, token);
        await db.SaveChangesAsync(token);

        try
        {
            (SyncWorkingSet? set, string? refusal) = await LoadWorkingSetAsync(operationId, request, token);
            if (set is null)
            {
                logger.LogError("Sync {OperationId}: {Reason}", operationId, refusal);
                return await FailAsync(operation, audit, refusal!, request.Mode, token);
            }

            SyncCounters              counters          = new();
            List<DbSyncPlannedChange> plan              = [];
            List<ManualReviewItem>    reviewItems       = [];
            List<DbSyncPendingReview> newPendingReviews = [];
            int                       autoLinked = 0, manualReview = 0;

            // App people that are the candidate in a review raised this run - they must not be
            // pushed to Elvanto as new, since doing so would create a duplicate alongside the
            // person they may turn out to be.
            HashSet<Guid> reviewCandidateIds = [];

            // App people whose only review was denied. Kept apart from reviewCandidateIds because
            // the two mean opposite things for the create decision below.
            HashSet<Guid> deniedPairIds = [];

            // People with a review still awaiting a human. This is the live signal, and it is the one
            // the create decision asks. It used to ask DbSyncMetadata.LastSyncStatus, which is set to
            // ManualReview once and reset by nothing - so a person queued for review a single time
            // was skipped by every later run, for all time, with no audit row.
            HashSet<Guid> awaitingReviewIds = set.PendingReviews.Values
                .Where(r => r.Status == GrpcReviewStatus.Pending)
                .Select(r => r.PersonId)
                .ToHashSet();

            foreach (ElvantoPerson elv in set.ElvantoPeople)
            {
                if (elv.Id is null) continue;

                if (!set.AppByElvantoId.TryGetValue(elv.Id, out DbPerson? appPerson))
                {
                    SyncMatchCandidate? match = matcher.FindBestMatch(elv, set.UnlinkedApp);

                    if (match is null)
                    {
                        // Out of scope, and silently so on purpose: a scoped run pulls the whole
                        // Elvanto roll so the matcher can work, and every unmatched row in it is
                        // somebody this run was never asked about. Auditing them would write ~1718
                        // rows for a sync of one person.
                        if (!set.MayCreateLocalPeople) continue;

                        // New in Elvanto. Nothing is created now - the plan names the Elvanto record
                        // and Apply makes the person, so a dry run and a full run walk one path.
                        plan.Add(Planned(operationId, PlannedChangeKind.CreateLocally,
                            personId: null, elvantoId: elv.Id, fieldName: null,
                            observedElvantoHash: HashElvantoPerson(elv),
                            observedElvantoValue: $"{elv.FirstName} {elv.LastName}".Trim(),
                            proposedValue: null, reason: "NewFromElvanto"));
                        counters.InboundPeople++;
                        continue;
                    }

                    if (match.Confidence >= 80)
                    {
                        appPerson = match.Person;
                        plan.Add(Planned(operationId, PlannedChangeKind.LinkPerson,
                            personId: appPerson.Id, elvantoId: elv.Id, fieldName: null,
                            observedElvantoHash: HashElvantoPerson(elv),
                            observedElvantoValue: $"{elv.FirstName} {elv.LastName}".Trim(),
                            proposedValue: elv.Id,
                            reason: $"AutoLinked:Confidence={match.Confidence}:{match.Strategy}"));

                        set.UnlinkedApp.Remove(appPerson);
                        set.AppByElvantoId[elv.Id] = appPerson;
                        autoLinked++;
                        // Fall through: this person's fields are decided in the same run.
                    }
                    else
                    {
                        string reviewName = $"{match.Person.FirstName} {match.Person.LastName}".Trim();
                        set.PendingReviews.TryGetValue((match.Person.Id, elv.Id), out DbSyncPendingReview? existingReview);

                        if (existingReview?.Status == GrpcReviewStatus.Approved)
                        {
                            appPerson = match.Person;
                            plan.Add(Planned(operationId, PlannedChangeKind.LinkPerson,
                                personId: appPerson.Id, elvantoId: elv.Id, fieldName: null,
                                observedElvantoHash: HashElvantoPerson(elv),
                                observedElvantoValue: reviewName,
                                proposedValue: elv.Id,
                                reason: $"ApprovedReview:Confidence={match.Confidence}:{match.Strategy}"));

                            set.UnlinkedApp.Remove(appPerson);
                            set.AppByElvantoId[elv.Id] = appPerson;
                            autoLinked++;
                        }
                        else if (existingReview?.Status == GrpcReviewStatus.Denied)
                        {
                            // Never link this pair, so neither side is matched again. Deliberately
                            // NOT a review candidate: denying a low-confidence match says these are
                            // two different people, which is the case where the app person genuinely
                            // needs creating. Suppressing the create made a denial permanent.
                            set.UnlinkedApp.Remove(match.Person);
                            deniedPairIds.Add(match.Person.Id);
                            await audit.Log(operationId, match.Person.Id, SyncEventType.ManualReviewQueued,
                                $"DeniedReview:{match.Confidence}:{match.Strategy}",
                                toValue: reviewName, token: token);
                            manualReview++;
                            continue;
                        }
                        else
                        {
                            set.UnlinkedApp.Remove(match.Person);
                            reviewCandidateIds.Add(match.Person.Id);

                            string reason = $"LowConfidenceMatch:{match.Strategy}:{match.Confidence}";
                            await audit.Log(operationId, match.Person.Id, SyncEventType.ManualReviewQueued,
                                $"LowConfidence:{match.Confidence}:{match.Strategy}",
                                toValue: reviewName, token: token);

                            reviewItems.Add(new ManualReviewItem(match.Person.Id, elv.Id, reason, match.Confidence));
                            manualReview++;

                            if (existingReview is null)
                                newPendingReviews.Add(new DbSyncPendingReview
                                {
                                    Id              = Guid.NewGuid(),
                                    PersonId        = match.Person.Id,
                                    ElvantoId       = elv.Id,
                                    MatchConfidence = match.Confidence,
                                    MatchStrategy   = match.Strategy,
                                    Status          = GrpcReviewStatus.Pending,
                                    SyncOperationId = operationId,
                                    CreatedAt       = DateTimeOffset.UtcNow,
                                    PersonName      = reviewName
                                });

                            continue;
                        }
                    }
                }

                await PlanFieldsAsync(operationId, elv, appPerson, set, plan, counters, audit, token);
            }

            int archived = DecideArchives(operationId, request, set, plan);
            manualReview += await DecideCreatesAsync(
                operationId, set, plan, counters, audit,
                reviewCandidateIds, deniedPairIds, awaitingReviewIds, newPendingReviews, token);

            // Decide's own writes: the plan, the divergences, the reviews, and the settled
            // agreements. Nothing in People, so there is nothing to roll back and no transaction
            // spanning an HTTP call.
            await db.PlannedChanges.AddRangeAsync(plan, token);

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

            // Its own try, because it used to share the audit flush's. One `catch` covered both and
            // was logged as "audit logs", so a flush failure discarded every review from the run
            // while the method still returned Success with a non-zero ManualReviewQueued.
            try
            {
                await SaveNewPendingReviewsAsync(newPendingReviews, token);
            }
            catch (Exception reviewEx)
            {
                logger.LogError(reviewEx,
                    "Sync {OperationId}: failed to persist {Count} pending reviews - they are lost and the "
                    + "run's ManualReviewQueued count overstates what can be actioned",
                    operationId, newPendingReviews.Count);
            }

            logger.LogInformation(
                "Sync {OperationId} decided | Processed={Processed} Planned={Planned} InboundFields={InboundFields} "
                + "OutboundFields={OutboundFields} Conflicts={Conflicts} AutoLinked={AutoLinked} "
                + "ManualReview={ManualReview} Archived={Archived} Diverged={Diverged}",
                operationId, set.ElvantoPeople.Count, plan.Count,
                counters.InboundFields, counters.OutboundFields, counters.Conflicts,
                autoLinked, manualReview, archived, counters.Diverged);

            return new SyncResult
            {
                OperationId        = operationId,
                Mode               = request.Mode,
                Success            = true,
                PeopleProcessed    = set.ElvantoPeople.Count,
                InboundPeople      = counters.InboundPeople,
                InboundFields      = counters.InboundFields,
                OutboundPeople     = counters.OutboundPeople,
                OutboundFields     = counters.OutboundFields,
                Conflicts          = counters.Conflicts,
                AutoLinked         = autoLinked,
                ManualReviewQueued = manualReview,
                Archived           = archived,
                Diverged           = counters.Diverged,
                PlannedChanges     = plan.Count,
                ManualReviewItems  = reviewItems,
                AuditLog           = audit.GetAll().ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Sync operation {OperationId} failed while deciding", operationId);
            return await FailAsync(operation, audit, ex.Message, request.Mode, token);
        }
    }

    /// <summary>
    /// People whose Elvanto record is gone. Only meaningful at full scope — a scoped fetch returns a
    /// subset by design, and reading that as deletion is how a dry run once archived 726 children.
    /// </summary>
    private int DecideArchives(
        Guid                      operationId,
        SyncWithElvantoRequest    request,
        SyncWorkingSet            set,
        List<DbSyncPlannedChange> plan)
    {
        if (request.Scope != ElvantoSyncScope.All) return 0;

        HashSet<string> fetched = set.ElvantoPeople
            .Where(e => e.Id is not null)
            .Select(e => e.Id!)
            .ToHashSet();

        int archived = 0;
        foreach (DbPerson local in set.AppPeople.Where(p => p.ElvantoId is not null && p.DeletedAtUtc is null))
        {
            if (fetched.Contains(local.ElvantoId!)) continue;

            plan.Add(Planned(operationId, PlannedChangeKind.Archive,
                personId: local.Id, elvantoId: local.ElvantoId, fieldName: null,
                observedAppHash: local.ElvantoId, observedAppValue: $"{local.FirstName} {local.LastName}".Trim(),
                reason: "RemovedFromElvanto"));
            archived++;
        }

        return archived;
    }

    /// <summary>
    /// A fingerprint of the Elvanto record as this run read it, so Apply can tell whether the record
    /// moved between the two phases without storing the whole thing.
    /// </summary>
    private string HashElvantoPerson(ElvantoPerson elv) =>
        SyncHash.Of(string.Join("|", _descriptors.Select(d => d.GetFromElvanto(elv) ?? "")));

    private static DbSyncPlannedChange Planned(
        Guid              operationId,
        PlannedChangeKind kind,
        Guid?             personId,
        string?           elvantoId,
        string?           fieldName,
        string            reason,
        string?           observedAppHash      = null,
        string?           observedAppValue     = null,
        string?           observedElvantoHash  = null,
        string?           observedElvantoValue = null,
        string?           proposedValue        = null) => new()
    {
        Id                   = Guid.NewGuid(),
        SyncOperationId      = operationId,
        PersonId             = personId,
        ElvantoId            = elvantoId,
        Kind                 = kind,
        FieldName            = fieldName,
        ObservedAppHash      = observedAppHash,
        ObservedAppValue     = observedAppValue,
        ObservedElvantoHash  = observedElvantoHash,
        ObservedElvantoValue = observedElvantoValue,
        ProposedValue        = proposedValue,
        Reason               = reason,
        Status               = PlannedChangeStatus.Pending,
        DecidedAt            = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// One failure path for every abort. The change tracker is cleared first, so half-decided plan
    /// rows and bases from the run that just failed are not saved alongside the failure.
    /// </summary>
    private async Task<SyncResult> FailAsync(
        DbSyncOperation   operation,
        SyncAuditLogger   audit,
        string            reason,
        ElvantoSyncMode   mode,
        CancellationToken token)
    {
        db.ChangeTracker.Clear();

        try
        {
            await db.SyncOperations
                .Where(x => x.Id == operation.Id)
                .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.CompletedAt, DateTimeOffset.UtcNow)
                        .SetProperty(x => x.Status, SyncStatus.Failed)
                        .SetProperty(x => x.FailureReason, reason),
                    token);

            await audit.FlushAsync(token);
        }
        catch (Exception flushEx)
        {
            logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs after failure", operation.Id);
        }

        return SyncResult.Failed(operation.Id, mode, reason);
    }
}
