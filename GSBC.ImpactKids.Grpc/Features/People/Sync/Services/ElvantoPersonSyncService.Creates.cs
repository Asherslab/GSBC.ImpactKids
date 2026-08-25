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
        List<DbSyncPendingReview> newPendingReviews,
        CancellationToken         token)
    {
        int manualReview = 0;

        foreach (DbPerson local in set.AppPeople.Where(p => p.ElvantoId is null && p.DeletedAtUtc is null))
        {
            if (reviewCandidateIds.Contains(local.Id)) continue;

            // Still waiting on a human. Audited rather than skipped in silence: the run reports the
            // full work-list, and a person sitting behind an unanswered review is a finding rather
            // than nothing to do.
            if (awaitingReviewIds.Contains(local.Id) && !deniedPairIds.Contains(local.Id))
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
            bool knownFamily = set.ElvantoFamilyIdByLocal.TryGetValue(local.FamilyId, out string? elvantoFamilyId);
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

    private DbPerson CreatePersonFromElvanto(
        ElvantoPerson            elv,
        List<DbSchoolGrade>      grades,
        Dictionary<string, Guid> familyIdMap
    )
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
            string? elvValue = TranslateElvantoValue(desc.FieldName, desc.GetFromElvanto(elv), grades, familyIdMap);
            desc.SetOnApp(p, elvValue);
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
