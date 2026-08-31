using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GrpcReviewStatus = GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums.ManualReviewStatus;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public partial class ElvantoPersonSyncService
{
    /// <summary>
    /// Why an unlinked app person is not being pushed to Elvanto as new, or that nothing is stopping
    /// it.
    ///
    /// Split out from the loop because it is the whole of the "is this person already spoken for?"
    /// question and the only part of the create decision that can be exercised without a database —
    /// see <c>DecideCreatesAsync</c>'s callers for the rest.
    /// </summary>
    public enum CreateGate
    {
        /// <summary>Nothing claims this person; plan the create.</summary>
        Proceed,

        /// <summary>A <c>LinkPerson</c> row was planned for them by this same run.</summary>
        LinkedThisRun,

        /// <summary>They are the app side of a review this run raised.</summary>
        ReviewCandidate,

        /// <summary>A review of theirs is still unanswered, and no denial has released them.</summary>
        AwaitingReview
    }

    /// <summary>
    /// The order matters and is the fix for a real plan: <c>linkedThisRunIds</c> is asked first
    /// because a matched person is settled, and asking any later would report them as held for a
    /// review nobody raised.
    ///
    /// <c>DbPerson.ElvantoId</c> cannot answer any of this. Decide writes nothing to <c>People</c>,
    /// so a person linked at 100% confidence still reads as unlinked here — which is how Lydia and
    /// Julia Agatep drew a <c>LinkPerson</c> row and a <c>CreateInElvanto</c> row apiece from one
    /// run. Apply happened to skip those creates (it links first, then finds <c>ElvantoId</c> set),
    /// but that rescue does not hold if the link goes Stale: the Elvanto record changing after the
    /// plan was decided leaves <c>ElvantoId</c> null and the create fires, duplicating in Elvanto
    /// the very record the person was matched to.
    /// </summary>
    public static CreateGate GateForCreate(
        Guid               personId,
        IReadOnlySet<Guid> linkedThisRunIds,
        IReadOnlySet<Guid> reviewCandidateIds,
        IReadOnlySet<Guid> awaitingReviewIds,
        IReadOnlySet<Guid> deniedPairIds)
    {
        if (linkedThisRunIds.Contains(personId))   return CreateGate.LinkedThisRun;
        if (reviewCandidateIds.Contains(personId)) return CreateGate.ReviewCandidate;

        // A denial is the statement that these are two different people, which is exactly the case
        // where the app person genuinely needs creating - so it releases the hold rather than adding
        // to it.
        if (awaitingReviewIds.Contains(personId) && !deniedPairIds.Contains(personId))
            return CreateGate.AwaitingReview;

        return CreateGate.Proceed;
    }

    /// <summary>
    /// App people who are not in Elvanto. One decision path for every mode — the two used to be
    /// separate loops, so a dry run reported work the full run would do differently.
    /// </summary>
    /// <returns>How many people were queued for, or held behind, a manual review.</returns>
    private async Task<int> DecideCreatesAsync(
        Guid                      operationId,
        SyncWorkingSet            set,
        List<DbSyncPlannedChange> plan,
        SyncCounters              counters,
        SyncAuditLogger           audit,
        HashSet<Guid>             reviewCandidateIds,
        HashSet<Guid>             deniedPairIds,
        HashSet<Guid>             awaitingReviewIds,
        HashSet<Guid>             linkedThisRunIds,
        List<DbSyncPendingReview> newPendingReviews,
        CancellationToken         token)
    {
        int manualReview = 0;

        foreach (DbPerson local in set.AppPeople.Where(p => p.ElvantoId is null && p.DeletedAtUtc is null))
        {
            CreateGate gate = GateForCreate(
                local.Id, linkedThisRunIds, reviewCandidateIds, awaitingReviewIds, deniedPairIds);

            // Neither work to do nor a finding to report. A person the matcher just paired with an
            // Elvanto record has somewhere to be, and one standing as a review candidate is already
            // counted by the loop that raised the review.
            if (gate is CreateGate.LinkedThisRun or CreateGate.ReviewCandidate) continue;

            // Still waiting on a human. Audited rather than skipped in silence: the run reports the
            // full work-list, and a person sitting behind an unanswered review is a finding rather
            // than nothing to do.
            if (gate is CreateGate.AwaitingReview)
            {
                await audit.Log(operationId, local.Id, SyncEventType.ManualReviewQueued,
                    "CreateSuppressed:AwaitingReview", direction: SyncSource.App,
                    toValue: DisplayName(local), token: token);
                manualReview++;
                continue;
            }

            DbPerson? duplicate = FindPotentialDuplicate(operationId, local, set);
            if (duplicate is not null)
            {
                logger.LogWarning(
                    "Sync {OperationId}: create skipped for app person {PersonId} ({FirstName} {LastName}) — "
                    + "potential duplicate of already-linked person {DuplicateId} (Elvanto {ElvantoId})",
                    operationId, local.Id, local.FirstName, local.LastName, duplicate.Id, duplicate.ElvantoId);

                await audit.Log(operationId, local.Id, SyncEventType.ManualReviewQueued,
                    "PotentialDuplicate:AlreadyLinkedInElvanto",
                    toValue: DisplayName(local), token: token);

                QueueDuplicateForReview(operationId, local, duplicate, set, newPendingReviews);
                manualReview++;
                continue;
            }

            // Known family id when any member is already linked, otherwise ask Elvanto to make the
            // family. Only the first member of that family asks: the id comes back on the create and
            // is recorded, so siblings later in the same Apply join them instead of each starting a
            // household of their own.
            string? elvantoFamilyId = set.Families.ElvantoFor(local.FamilyId);
            bool    knownFamily     = elvantoFamilyId is not null;
            if (!knownFamily)
                elvantoFamilyId = ElvantoService.NewFamily;

            // The body is recorded whether or not it is sent, so what gets reviewed before approving
            // a write is the same string the transport would post.
            string payload = elvantoService.DescribeCreatePayload(
                local, ComposeMedicalAllergyText(local), elvantoFamilyId);

            plan.Add(new DbSyncPlannedChange
            {
                Id                   = Guid.NewGuid(),
                SyncOperationId      = operationId,
                PersonId             = local.Id,
                ElvantoId            = null,
                Kind                 = PlannedChangeKind.CreateInElvanto,
                FieldName            = null,
                ObservedAppHash      = SyncHash.Of(payload),
                ObservedAppValue     = payload,
                ProposedValue        = elvantoFamilyId,
                Reason               = knownFamily ? "CreateInElvanto" : "CreateInElvanto:NewFamily",
                Status               = PlannedChangeStatus.Pending,
                DecidedAt            = DateTimeOffset.UtcNow
            });

            counters.OutboundPeople++;
        }

        return manualReview;
    }

    /// <summary>
    /// The already-linked person this one looks like, rather than just "yes". The counterpart's
    /// ElvantoId is what makes the skip reviewable: without it there is nothing to approve or deny
    /// against.
    /// </summary>
    private DbPerson? FindPotentialDuplicate(Guid operationId, DbPerson local, SyncWorkingSet set)
    {
        DbPerson? candidate = set.AppByElvantoId.Values.FirstOrDefault(linked =>
            linked.Id != local.Id &&
            string.Equals(linked.FirstName?.Trim(), local.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(linked.LastName?.Trim(), local.LastName?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (candidate?.ElvantoId is null) return candidate;

        // A reviewer answering "no, these are two different people" is what releases the create.
        // Without this the decision changed a row's status and nothing else: these reviews are
        // raised outside the matching loop, which is the only place review status was ever read.
        if (set.PendingReviews.TryGetValue((local.Id, candidate.ElvantoId), out DbSyncPendingReview? decided) &&
            decided.Status == GrpcReviewStatus.Denied)
        {
            logger.LogInformation(
                "Sync {OperationId}: duplicate review denied for app person {PersonId} ({FirstName} {LastName}) — "
                + "treating as a different person and allowing the create",
                operationId, local.Id, local.FirstName, local.LastName);
            return null;
        }

        return candidate;
    }

    /// <summary>
    /// Queues a duplicate skip for human review. There is deliberately no "approve = link": two app
    /// people cannot share one ElvantoId, so approving means "yes, same human", which keeps the
    /// create suppressed. Merging the two app records is a separate, manual job.
    /// </summary>
    private static void QueueDuplicateForReview(
        Guid                      operationId,
        DbPerson                  local,
        DbPerson                  duplicate,
        SyncWorkingSet            set,
        List<DbSyncPendingReview> newPendingReviews)
    {
        if (duplicate.ElvantoId is null) return;
        if (set.PendingReviews.ContainsKey((local.Id, duplicate.ElvantoId))) return;

        DbSyncPendingReview review = new()
        {
            Id              = Guid.NewGuid(),
            PersonId        = local.Id,
            ElvantoId       = duplicate.ElvantoId,
            MatchConfidence = 50,
            MatchStrategy   = "PotentialDuplicate:ExactName",
            Status          = GrpcReviewStatus.Pending,
            SyncOperationId = operationId,
            CreatedAt       = DateTimeOffset.UtcNow,
            PersonName      = DisplayName(local)
        };

        set.PendingReviews[(local.Id, duplicate.ElvantoId)] = review;
        newPendingReviews.Add(review);
    }

    private static string DisplayName(DbPerson person) => $"{person.FirstName} {person.LastName}".Trim();

    private DbPerson CreatePersonFromElvanto(ElvantoPerson elv, SyncWorkingSet set)
    {
        // Placeholder values for required properties; all overwritten by the descriptor loop below
        DbPerson p = new()
        {
            Id = Guid.NewGuid(),
            ElvantoId = elv.Id,
            FirstName = "",
            LastName = "",
            PhoneNumber = null,
            Email = null,
            SchoolGradeId = null,
            MediaConsent = nameof(Shared.Contracts.Entities.Features.People.MediaConsent.NotRequested),
            DateOfBirth = null,
            FirstTime = null,
            FamilyId = Guid.Empty,
            FamilyGuardian = false,
        };

        foreach (IFieldSyncDescriptor desc in _descriptors)
        {
            Translated value = TranslateElvantoValue(desc.FieldName, desc.GetFromElvanto(elv), set, p);

            // A value we could not read is left off the new person rather than guessed at. They are
            // created without a school grade rather than with someone else's, and in their own new
            // family rather than in a household they may not belong to.
            // A refusal needs nothing done about it here: every field is initialised above, so the
            // person is created without the value rather than with a guess at it.
            if (value.Known) _ = desc.SetOnApp(p, value.Value);
        }

        return p;
    }

    /// <summary>
    /// The medical/allergy text for a person, in the same format an update would push.
    /// Null when the descriptor is absent, which falls the create back to its own merge.
    /// </summary>
    private string? ComposeMedicalAllergyText(DbPerson person) =>
        _descriptors.OfType<MedicalAllergyNotesDescriptor>().FirstOrDefault()?.GetFromApp(person);
}
