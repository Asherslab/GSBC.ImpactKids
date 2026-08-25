using GSBC.ImpactKids.Grpc.Data;
using GSBC.ImpactKids.Grpc.Data.Models.People;
using GSBC.ImpactKids.Grpc.Data.Models.Sync;
using GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices;
using GSBC.ImpactKids.Grpc.Features.Elvanto.ElvantoServices.Models;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Descriptors;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Interfaces;
using GSBC.ImpactKids.Grpc.Features.People.Sync.Models;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using GrpcReviewStatus = GSBC.ImpactKids.Grpc.Data.Models.Sync.Enums.ManualReviewStatus;

namespace GSBC.ImpactKids.Grpc.Features.People.Sync.Services;

public class ElvantoPersonSyncService(
    GsbcDbContext                     db,
    ElvantoService                    elvantoService,
    IEnumerable<IFieldSyncDescriptor> descriptors,
    IPersonMatcher                    matcher,
    IConflictResolver                 conflictResolver,
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
        Guid            operationId = Guid.NewGuid();
        SyncAuditLogger audit       = new(db);
        List<string>?   fetchedIds  = null;

        DbSyncOperation operation = new()
        {
            Id = operationId,
            Mode = MapMode(request.Mode),
            Scope = MapScope(request.Scope),
            PersonId = request.Scope == ElvantoSyncScope.Person ? request.PersonId : null,
            FamilyId = request.Scope == ElvantoSyncScope.Family ? request.FamilyId : null,
            StartedAt = DateTimeOffset.UtcNow,
        };

        logger.LogInformation(
            "Sync operation {OperationId} starting | Mode={Mode} Scope={Scope}",
            operationId, request.Mode, request.Scope);

        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(token);

        try
        {
            using IDisposable _ = syncContext.SetSource(SyncSource.Elvanto);

            // 1. Pull Elvanto data (scope-aware)
            List<ElvantoPerson> elvantoPeople = await FetchElvantoAsync(request, token);
            fetchedIds = elvantoPeople.Where(e => e.Id is not null).Select(e => e.Id!).ToList();
            logger.LogInformation(
                "Sync {OperationId}: fetched {Count} people from Elvanto (scope={Scope})",
                operationId, elvantoPeople.Count, request.Scope);

            if (request.Scope == ElvantoSyncScope.All && elvantoPeople.Count == 0)
            {
                const string reason =
                    "Elvanto returned 0 people on a full-scope sync — aborting to prevent mass archive";
                logger.LogError("Sync {OperationId}: {Reason}", operationId, reason);
                await tx.RollbackAsync(token);
                operation.CompletedAt = DateTimeOffset.UtcNow;
                operation.Status = SyncStatus.Failed;
                operation.FailureReason = reason;
                try
                {
                    await audit.FlushAsync(operation, token);
                }
                catch (Exception flushEx)
                {
                    logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs", operationId);
                }

                return new SyncResult
                {
                    OperationId = operationId,
                    Mode = request.Mode,
                    Success = false,
                    Error = reason,
                    PeopleProcessed = 0,
                    InboundPeople = 0,
                    InboundFields = 0,
                    OutboundPeople = 0,
                    OutboundFields = 0,
                    Conflicts = 0,
                    AutoLinked = 0,
                    ManualReviewQueued = 0,
                    Archived = 0
                };
            }

            // 2. Load App-side data (scope-aware, include deleted for matching)
            List<DbPerson> appPeople = await LoadAppPeopleAsync(request, token);
            logger.LogInformation(
                "Sync {OperationId}: loaded {Count} app people",
                operationId, appPeople.Count);

            // Second line of defence behind the fetch itself. Archiving reads "not in the Elvanto
            // list" as "deleted from Elvanto", so a roll that comes back short - for any reason,
            // including one nobody has thought of yet - must stop the run rather than delete
            // people. A dry run once archived 726 children off a single dropped page.
            int linkedCount = appPeople.Count(p => p.ElvantoId is not null && p.DeletedAtUtc is null);
            if (request.Scope == ElvantoSyncScope.All && linkedCount > 0)
            {
                double coverage = (double)elvantoPeople.Count / linkedCount;
                if (coverage < MinimumElvantoCoverage)
                {
                    string reason =
                        $"Elvanto returned {elvantoPeople.Count} people but {linkedCount} app people are linked "
                        + $"({coverage:P0} coverage, minimum {MinimumElvantoCoverage:P0}). Aborting before archive "
                        + "— a short roll would archive everyone missing from it.";
                    logger.LogError("Sync {OperationId}: {Reason}", operationId, reason);
                    await tx.RollbackAsync(token);
                    operation.CompletedAt   = DateTimeOffset.UtcNow;
                    operation.Status        = SyncStatus.Failed;
                    operation.FailureReason = reason;
                    try
                    {
                        await audit.FlushAsync(operation, token);
                    }
                    catch (Exception flushEx)
                    {
                        logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs", operationId);
                    }

                    return new SyncResult
                    {
                        OperationId = operationId,
                        Mode = request.Mode,
                        Success = false,
                        Error = reason,
                        PeopleProcessed = 0,
                        InboundPeople = 0,
                        InboundFields = 0,
                        OutboundPeople = 0,
                        OutboundFields = 0,
                        Conflicts = 0,
                        AutoLinked = 0,
                        ManualReviewQueued = 0,
                        Archived = 0
                    };
                }
            }

            List<DbSchoolGrade> schoolGrades = await db.SchoolGrades.ToListAsync(token);

            // The medical/allergy descriptor turns Elvanto's free text back into rows, which
            // needs the allergen and medical-type tables. It cannot reach the database itself,
            // so it is primed here, once, rather than per person.
            await PrimeMedicalAllergyLookupsAsync(token);

            Dictionary<string, DbSyncMetadata> metaByElvantoId = await db.SyncMetadata
                .ToDictionaryAsync(x => x.ElvantoId, token);

            Dictionary<(Guid, string), DbSyncPendingReview> pendingReviews = await db.PendingReviews
                .ToDictionaryAsync(x => (x.PersonId, x.ElvantoId), token);

            // Last App-side change per (entityId, fieldName)
            Dictionary<(Guid, string), DateTimeOffset> lastAppChange = await db.FieldChangeLogs
                .Where(x => x.EntityType == "Person" && x.Source == SyncSource.App)
                .GroupBy(x => new { x.EntityId, x.FieldName })
                .Select(g => new { g.Key.EntityId, g.Key.FieldName, LastAt = g.Max(x => x.ChangedAt) })
                .ToDictionaryAsync(x => (x.EntityId, x.FieldName), x => x.LastAt, token);

            Dictionary<(Guid, string), DbElvantoFieldSnapshot> snapshots = await db.ElvantoFieldSnapshots
                .Where(x => x.EntityType == "Person")
                .ToDictionaryAsync(x => (x.EntityId, x.FieldName), token);

            Dictionary<string, DbSyncFieldConfig> fieldConfigs = await db.SyncFieldConfigs
                .Where(x => x.EntityType == "Person")
                .ToDictionaryAsync(x => x.FieldName, token);

            // Index app people for fast lookup
            Dictionary<string, DbPerson> appByElvantoId = appPeople
                .Where(p => p.ElvantoId is not null)
                .ToDictionary(p => p.ElvantoId!);

            List<DbPerson> unlinkedApp = appPeople.Where(p => p.ElvantoId is null).ToList();

            // Seed family map from already-linked people: Elvanto family ID string → local Guid
            Dictionary<string, Guid> familyIdMap = [];
            foreach (ElvantoPerson e in elvantoPeople.Where(e => e.FamilyId is not null && e.Id is not null))
            {
                if (!familyIdMap.ContainsKey(e.FamilyId!) && appByElvantoId.TryGetValue(e.Id!, out DbPerson? linked))
                    familyIdMap[e.FamilyId!] = linked.FamilyId;
            }

            // Where each local family's linked members actually sit in Elvanto. Kept as the raw
            // membership rather than a single answer per family, because who is asking matters.
            Dictionary<Guid, List<(Guid PersonId, string ElvantoFamilyId)>> familyMembership = elvantoPeople
                .Where(e => e.FamilyId is not null && e.Id is not null)
                .Select(e => (Elvanto: e.FamilyId!, App: appByElvantoId.GetValueOrDefault(e.Id!)))
                .Where(x => x.App is not null)
                .GroupBy(x => x.App!.FamilyId)
                .ToDictionary(g => g.Key, g => g.Select(x => (x.App!.Id, x.Elvanto)).ToList());

            // The Elvanto family a local family corresponds to, as evidenced by its members other
            // than the one asking. Excluding the asker is the whole point: a person is the only
            // evidence for their own family when they are its sole member, so including them makes
            // any answer self-confirming - a person moved into a brand new local family would be
            // read as that family's Elvanto pairing and compare equal to itself, and the move would
            // never be seen. Excluding them, a lone mover has no evidence, which is exactly right:
            // their new family has no Elvanto counterpart and one has to be created.
            string? ResolveFamilyInElvanto(Guid localFamilyId, Guid askingPersonId) =>
                familyMembership.TryGetValue(localFamilyId, out List<(Guid PersonId, string ElvantoFamilyId)>? members)
                    ? members.Where(m => m.PersonId != askingPersonId)
                        .GroupBy(m => m.ElvantoFamilyId)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Key, StringComparer.Ordinal)
                        .FirstOrDefault()?.Key
                    : null;

            // Still needed by the create loop, where the person has no Elvanto record yet and so
            // cannot be their own evidence. Recorded mid-run when Elvanto makes a family.
            Dictionary<Guid, string> elvantoFamilyIdByLocal = familyMembership
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.GroupBy(m => m.ElvantoFamilyId)
                        .OrderByDescending(g => g.Count())
                        .ThenBy(g => g.Key, StringComparer.Ordinal)
                        .First().Key);

            SyncCounters               counters          = new();
            int                        autoLinked        = 0, manualReview = 0, archived = 0;
            List<ManualReviewItem>     reviewItems       = [];
            List<DbSyncPendingReview>  newPendingReviews = [];
            // Tracks app people that are the candidate in a low-confidence review — they must not
            // be pushed to Elvanto as new, since doing so would create a duplicate alongside the placeholder.
            HashSet<Guid>              reviewCandidateIds = [];

            // 3. Process each Elvanto person
            foreach (ElvantoPerson elv in elvantoPeople)
            {
                if (elv.Id is null) continue;

                logger.LogDebug(
                    "Sync {OperationId}: processing Elvanto person {ElvantoId} ({FirstName} {LastName})",
                    operationId, elv.Id, elv.FirstName, elv.LastName);

                // elv.Id is guaranteed non-null here (checked above)
                if (!appByElvantoId.TryGetValue(elv.Id, out DbPerson? appPerson))
                {
                    // Try to find and link
                    SyncMatchCandidate? match = matcher.FindBestMatch(elv, unlinkedApp);

                    if (match is null)
                    {
                        // New person in Elvanto — create in App
                        appPerson = CreatePersonFromElvanto(elv, schoolGrades, familyIdMap);
                        await db.People.AddAsync(appPerson, token);
                        logger.LogInformation(
                            "Sync {OperationId}: created new app person {PersonId} from Elvanto {ElvantoId} ({FirstName} {LastName})",
                            operationId, appPerson.Id, elv.Id, elv.FirstName, elv.LastName);
                        await audit.Log(operationId, appPerson.Id, SyncEventType.Created, "NewFromElvanto",
                            direction: SyncSource.Elvanto,
                            token: token);
                        appByElvantoId[elv.Id] = appPerson;
                        counters.InboundPeople++;
                        await UpsertMetadata(appPerson, elv.Id, 100, "DirectElvantoId", metaByElvantoId,
                            token);
                    }
                    else if (match.Confidence >= 80)
                    {
                        appPerson = match.Person;
                        appPerson.ElvantoId = elv.Id;
                        logger.LogInformation(
                            "Sync {OperationId}: auto-linked Elvanto {ElvantoId} to app person {PersonId} (confidence={Confidence}, strategy={Strategy})",
                            operationId, elv.Id, appPerson.Id, match.Confidence, match.Strategy);
                        await audit.Log(operationId, appPerson.Id, SyncEventType.Match,
                            $"AutoLinked:Confidence={match.Confidence}:{match.Strategy}", token: token);
                        await UpsertMetadata(appPerson, elv.Id, match.Confidence, match.Strategy, metaByElvantoId,
                            token);
                        unlinkedApp.Remove(appPerson);
                        appByElvantoId[elv.Id] = appPerson;
                        autoLinked++;
                    }
                    else
                    {
                        // Low confidence — check if a human has already reviewed this pair
                        string reviewName = $"{match.Person.FirstName} {match.Person.LastName}".Trim();
                        pendingReviews.TryGetValue((match.Person.Id, elv.Id!), out DbSyncPendingReview? existingReview);

                        if (existingReview?.Status == GrpcReviewStatus.Approved)
                        {
                            // Approved — proceed with linking as if high confidence
                            appPerson             = match.Person;
                            appPerson.ElvantoId   = elv.Id;
                            logger.LogInformation(
                                "Sync {OperationId}: applying approved review — linking Elvanto {ElvantoId} to app person {PersonId} ({Name}) (confidence={Confidence})",
                                operationId, elv.Id, appPerson.Id, reviewName, match.Confidence);
                            await audit.Log(operationId, appPerson.Id, SyncEventType.Match,
                                $"ApprovedReview:Confidence={match.Confidence}:{match.Strategy}", token: token);
                            await UpsertMetadata(appPerson, elv.Id, match.Confidence, match.Strategy,
                                metaByElvantoId, token);
                            unlinkedApp.Remove(appPerson);
                            appByElvantoId[elv.Id] = appPerson;
                            autoLinked++;
                            // Fall through to field sync below
                        }
                        else if (existingReview?.Status == GrpcReviewStatus.Denied)
                        {
                            // Denied — skip both sides of this pair
                            unlinkedApp.Remove(match.Person);
                            reviewCandidateIds.Add(match.Person.Id);
                            logger.LogInformation(
                                "Sync {OperationId}: skipping denied review pair — Elvanto {ElvantoId} / app person {PersonId} ({Name})",
                                operationId, elv.Id, match.Person.Id, reviewName);
                            await audit.Log(operationId, match.Person.Id, SyncEventType.ManualReviewQueued,
                                $"DeniedReview:{match.Confidence}:{match.Strategy}",
                                toValue: reviewName, token: token);
                            manualReview++;
                            continue;
                        }
                        else
                        {
                            // No decision yet — queue for manual review
                            unlinkedApp.Remove(match.Person);
                            reviewCandidateIds.Add(match.Person.Id);

                            DbSyncMetadata reviewMeta = await UpsertMetadata(match.Person, elv.Id, match.Confidence,
                                match.Strategy, metaByElvantoId, token);
                            reviewMeta.LastSyncStatus     = SyncStatus.ManualReview;
                            reviewMeta.ManualReviewReason = $"LowConfidenceMatch:{match.Strategy}:{match.Confidence}";

                            logger.LogInformation(
                                "Sync {OperationId}: queued app person {PersonId} ({Name}) for manual review — Elvanto {ElvantoId} (confidence={Confidence}, strategy={Strategy})",
                                operationId, match.Person.Id, reviewName, elv.Id, match.Confidence, match.Strategy);
                            await audit.Log(operationId, match.Person.Id, SyncEventType.ManualReviewQueued,
                                $"LowConfidence:{match.Confidence}:{match.Strategy}",
                                toValue: reviewName, token: token);

                            reviewItems.Add(new ManualReviewItem(match.Person.Id, elv.Id,
                                reviewMeta.ManualReviewReason, match.Confidence));
                            manualReview++;

                            // Schedule save outside main transaction so DryRun results persist for review
                            if (existingReview is null)
                            {
                                newPendingReviews.Add(new DbSyncPendingReview
                                {
                                    Id              = Guid.NewGuid(),
                                    PersonId        = match.Person.Id,
                                    ElvantoId       = elv.Id!,
                                    MatchConfidence = match.Confidence,
                                    MatchStrategy   = match.Strategy,
                                    Status          = GrpcReviewStatus.Pending,
                                    CreatedAt       = DateTimeOffset.UtcNow,
                                    PersonName      = reviewName
                                });
                            }
                            continue;
                        }
                    }
                }
                else
                {
                    logger.LogDebug(
                        "Sync {OperationId}: Elvanto {ElvantoId} already linked to app person {PersonId}",
                        operationId, elv.Id, appPerson.Id);
                }

                // 4. Per-field decisions
                FieldProcessResult fieldResult = await ProcessFieldsAsync(
                    operationId, elv, appPerson, fieldConfigs, snapshots, lastAppChange,
                    audit, request.Mode, schoolGrades, familyIdMap, elvantoFamilyIdByLocal,
                    ResolveFamilyInElvanto, counters, token
                );
                bool hadConflict = fieldResult.HadConflict;

                // 5. Update snapshots
                await UpdateSnapshotsAsync(elv, appPerson, snapshots, fieldResult.HoldSnapshotFields, token);

                // 6. Sync metadata
                if (metaByElvantoId.TryGetValue(elv.Id, out DbSyncMetadata? meta))
                {
                    meta.LastSyncAt = DateTimeOffset.UtcNow;
                    meta.LastSyncStatus = hadConflict ? SyncStatus.Conflict : SyncStatus.Success;
                }
            }

            // 7. Soft-delete App people whose ElvantoId is no longer in Elvanto (archived/suspended)
            //    Only meaningful for ElvantoSyncScope.All — scoped fetches intentionally return a subset.
            //    Empty full-scope fetch is caught above and aborted before we reach here.
            if (request.Scope == ElvantoSyncScope.All)
            {
                HashSet<string> fetchedElvantoIds = elvantoPeople
                    .Where(e => e.Id is not null)
                    .Select(e => e.Id!)
                    .ToHashSet();

                foreach (DbPerson local in appPeople.Where(p => p.ElvantoId is not null && p.DeletedAtUtc is null))
                {
                    if (!fetchedElvantoIds.Contains(local.ElvantoId!))
                    {
                        local.DeletedAtUtc = DateTimeOffset.UtcNow;
                        logger.LogInformation(
                            "Sync {OperationId}: archiving app person {PersonId} (ElvantoId={ElvantoId}) — no longer in Elvanto",
                            operationId, local.Id, local.ElvantoId);
                        await audit.Log(operationId, local.Id, SyncEventType.Archived, "RemovedFromElvanto",
                            token: token);
                        archived++;
                    }
                }

                logger.LogInformation("Sync {OperationId}: archived {Count} people removed from Elvanto",
                    operationId, archived);
            }

            // 9. Push new App people to Elvanto (people with no ElvantoId, not needing review)
            // Helper: detect app-side duplicates — another person already linked to Elvanto in this sync
            // shares the same first+last name, meaning we'd create a duplicate in Elvanto if we push this one.
            // Returns the already-linked person this one looks like, rather than just "yes".
            // The counterpart's ElvantoId is what makes the skip reviewable: without it there is
            // nothing to approve or deny against.
            DbPerson? FindPotentialDuplicate(DbPerson local)
            {
                DbPerson? candidate = appByElvantoId.Values.FirstOrDefault(linked =>
                    linked.Id != local.Id &&
                    string.Equals(linked.FirstName?.Trim(), local.FirstName?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(linked.LastName?.Trim(), local.LastName?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (candidate?.ElvantoId is null) return candidate;

                // A reviewer answering "no, these are two different people" is what releases the
                // create. Without this the decision changed a row's status and nothing else: these
                // reviews are raised outside the matching loop, which is the only place review
                // status was ever read, so approving or denying one had no effect on any sync.
                if (pendingReviews.TryGetValue((local.Id, candidate.ElvantoId), out DbSyncPendingReview? decided) &&
                    decided.Status == GrpcReviewStatus.Denied)
                {
                    logger.LogInformation(
                        "Sync {OperationId}: duplicate review denied for app person {PersonId} ({FirstName} {LastName}) — treating as a different person and allowing the create",
                        operationId, local.Id, local.FirstName, local.LastName);
                    return null;
                }

                return candidate;
            }

            // Queues a duplicate skip for human review. Previously these bumped the manual-review
            // counter and wrote an audit row but created nothing to act on, so the operation page
            // promised a queue that was always empty and the same people were skipped every run.
            // Note there is deliberately no "approve = link" here. Two app people cannot share one
            // ElvantoId: appByElvantoId is built with ToDictionary on that key and would throw.
            // Approving means "yes, same human", which keeps the create suppressed; merging the
            // two app records is a separate, manual job.
            void QueueDuplicateForReview(DbPerson local, DbPerson duplicate)
            {
                if (duplicate.ElvantoId is null) return;
                if (pendingReviews.ContainsKey((local.Id, duplicate.ElvantoId))) return;
                if (newPendingReviews.Any(r => r.PersonId == local.Id && r.ElvantoId == duplicate.ElvantoId)) return;

                newPendingReviews.Add(new DbSyncPendingReview
                {
                    Id              = Guid.NewGuid(),
                    PersonId        = local.Id,
                    ElvantoId       = duplicate.ElvantoId,
                    MatchConfidence = 50,
                    MatchStrategy   = "PotentialDuplicate:ExactName",
                    Status          = GrpcReviewStatus.Pending,
                    CreatedAt       = DateTimeOffset.UtcNow,
                    PersonName      = $"{local.FirstName} {local.LastName}".Trim()
                });
            }

            if (request.Mode == ElvantoSyncMode.Full)
            {
                foreach (DbPerson local in appPeople.Where(p => p.ElvantoId is null && p.DeletedAtUtc is null))
                {
                    if (reviewCandidateIds.Contains(local.Id)) continue;

                    if (metaByElvantoId.Values.Any(m =>
                            m.PersonId == local.Id && m.LastSyncStatus == SyncStatus.ManualReview))
                        continue;

                    DbPerson? duplicate = FindPotentialDuplicate(local);
                    if (duplicate is not null)
                    {
                        logger.LogWarning(
                            "Sync {OperationId}: skipping outbound create for app person {PersonId} ({FirstName} {LastName}) — potential duplicate of already-linked person {DuplicateId} (Elvanto {ElvantoId})",
                            operationId, local.Id, local.FirstName, local.LastName, duplicate.Id, duplicate.ElvantoId);
                        await audit.Log(operationId, local.Id, SyncEventType.ManualReviewQueued,
                            "PotentialDuplicate:AlreadyLinkedInElvanto",
                            toValue: $"{local.FirstName} {local.LastName}".Trim(),
                            token: token);
                        QueueDuplicateForReview(local, duplicate);
                        manualReview++;
                        continue;
                    }

                    // Narrowed to named people for a controlled first write. Everyone else is
                    // recorded exactly as a suppressed create, so the run still reports the full
                    // work-list rather than pretending the others do not exist.
                    if (!elvantoService.MayCreate(local.Id))
                    {
                        logger.LogInformation(
                            "Sync {OperationId}: create SKIPPED for app person {PersonId} ({FirstName} {LastName}) "
                            + "- not in Elvanto:AllowedCreatePersonIds",
                            operationId, local.Id, local.FirstName, local.LastName);
                        await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                            "WouldCreate:NotInAllowList", direction: SyncSource.App, token: token);
                        counters.OutboundPeople++;
                        continue;
                    }

                    // With writes off there is no Elvanto id to come back, so this cannot be
                    // treated as a real create: writing a placeholder onto local.ElvantoId would
                    // link the person to nothing and corrupt the eventual real sync. Record it the
                    // same way a dry run does, so the two runs report the same numbers.
                    // Known family id when any member is already linked, otherwise ask Elvanto to
                    // make the family. Only the first member of that family asks: the id comes back
                    // on the create and is recorded below, so siblings later in this same loop join
                    // them instead of each starting a household of their own.
                    bool knownFamily =
                        elvantoFamilyIdByLocal.TryGetValue(local.FamilyId, out string? elvantoFamilyId);
                    if (!knownFamily)
                    {
                        elvantoFamilyId = ElvantoService.NewFamily;
                        logger.LogInformation(
                            "Sync {OperationId}: app person {PersonId} ({FirstName} {LastName}) is the first of "
                            + "local family {FamilyId} to reach Elvanto - requesting a new family",
                            operationId, local.Id, local.FirstName, local.LastName, local.FamilyId);
                    }

                    // The body is recorded whether or not it is sent, so what gets reviewed before
                    // approving a write is the same string the transport would post.
                    string createPayload = elvantoService.DescribeCreatePayload(
                        local, ComposeMedicalAllergyText(local), elvantoFamilyId);

                    if (!elvantoService.CreatesEnabled)
                    {
                        logger.LogWarning(
                            "Sync {OperationId}: create SUPPRESSED (writes disabled) for app person {PersonId} ({FirstName} {LastName}) - payload logged, ElvantoId left unset",
                            operationId, local.Id, local.FirstName, local.LastName);
                        await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                            "WouldCreate:WritesDisabled", direction: SyncSource.App,
                            toValue: createPayload, token: token);
                        counters.OutboundPeople++;
                        continue;
                    }

                    // Recorded before the call, so an attempt leaves a trace even if the write is
                    // refused at the transport or the process dies mid-send. Counting these is how
                    // "only one person was written" stops being a claim about the loop above.
                    await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                        "CreateAttempted", direction: SyncSource.App,
                        toValue: createPayload, token: token);

                    logger.LogInformation(
                        "Sync {OperationId}: pushing new app person {PersonId} ({FirstName} {LastName}) to Elvanto",
                        operationId, local.Id, local.FirstName, local.LastName);
                    ElvantoService.CreatedPerson? created = await elvantoService.CreatePersonAsync(
                        local, ComposeMedicalAllergyText(local), elvantoFamilyId, token);
                    if (created is not null)
                    {
                        string newElvantoId = created.Id;
                        local.ElvantoId = newElvantoId;
                        logger.LogInformation(
                            "Sync {OperationId}: created Elvanto person {ElvantoId} for app person {PersonId} "
                            + "in Elvanto family {FamilyId}",
                            operationId, newElvantoId, local.Id, created.FamilyId ?? "(unknown)");

                        // Recorded both ways so the rest of this run treats the family as existing:
                        // the next sibling is given this id, and an inbound person carrying it maps
                        // back to the same local family rather than a new one.
                        if (created.FamilyId is not null)
                        {
                            elvantoFamilyIdByLocal[local.FamilyId] = created.FamilyId;
                            familyIdMap.TryAdd(created.FamilyId, local.FamilyId);
                        }

                        await UpsertMetadata(local, newElvantoId, 100, "CreatedOutbound", metaByElvantoId,
                            token);
                        await audit.Log(operationId, local.Id, SyncEventType.PushedToElvanto, "CreatedNewInElvanto",
                            token: token);
                        counters.OutboundPeople++;
                    }
                    else
                    {
                        logger.LogWarning(
                            "Sync {OperationId}: failed to create Elvanto person for app person {PersonId}",
                            operationId, local.Id);
                        await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                            $"CreateRefusedOrFailed: {elvantoService.LastCreateError ?? "no reason given"}",
                            direction: SyncSource.App, token: token);
                    }
                }
            }
            else
            {
                foreach (DbPerson local in appPeople.Where(p => p.ElvantoId is null && p.DeletedAtUtc is null))
                {
                    if (reviewCandidateIds.Contains(local.Id)) continue;

                    if (metaByElvantoId.Values.Any(m =>
                            m.PersonId == local.Id && m.LastSyncStatus == SyncStatus.ManualReview))
                        continue;

                    DbPerson? duplicate = FindPotentialDuplicate(local);
                    if (duplicate is not null)
                    {
                        logger.LogWarning(
                            "Sync {OperationId}: would-create skipped for app person {PersonId} ({FirstName} {LastName}) — potential duplicate of already-linked person {DuplicateId} (Elvanto {ElvantoId})",
                            operationId, local.Id, local.FirstName, local.LastName, duplicate.Id, duplicate.ElvantoId);
                        await audit.Log(operationId, local.Id, SyncEventType.ManualReviewQueued,
                            "PotentialDuplicate:AlreadyLinkedInElvanto",
                            toValue: $"{local.FirstName} {local.LastName}".Trim(),
                            token: token);
                        QueueDuplicateForReview(local, duplicate);
                        manualReview++;
                        continue;
                    }

                    logger.LogInformation(
                        "Sync {OperationId}: would create Elvanto person for app person {PersonId} ({FirstName} {LastName}) (mode={Mode})",
                        operationId, local.Id, local.FirstName, local.LastName, request.Mode);
                    await audit.Log(operationId, local.Id, SyncEventType.WouldCreateInElvanto,
                        $"WouldCreate:{request.Mode}", direction: SyncSource.App, token: token);
                    counters.OutboundPeople++;
                }
            }

            // 10. Commit or rollback
            if (request.Mode == ElvantoSyncMode.DryRun)
            {
                logger.LogInformation("Sync {OperationId}: dry run — rolling back transaction", operationId);
                await tx.RollbackAsync(token);
            }
            else
            {
                logger.LogInformation("Sync {OperationId}: committing transaction", operationId);
                await db.SaveChangesAsync(token);
                await tx.CommitAsync(token);
            }

            operation.CompletedAt = DateTimeOffset.UtcNow;
            operation.Status = SyncStatus.Success;
            try
            {
                await audit.FlushAsync(operation, token);
                await SaveNewPendingReviewsAsync(newPendingReviews, token);
            }
            catch (Exception flushEx)
            {
                logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs", operationId);
            }

            logger.LogInformation(
                "Sync {OperationId} complete | Processed={Processed} InboundPeople={InboundPeople} InboundFields={InboundFields} OutboundPeople={OutboundPeople} OutboundFields={OutboundFields} Conflicts={Conflicts} AutoLinked={AutoLinked} ManualReview={ManualReview} Archived={Archived}",
                operationId, elvantoPeople.Count,
                counters.InboundPeople, counters.InboundFields,
                counters.OutboundPeople, counters.OutboundFields,
                counters.Conflicts, autoLinked, manualReview, archived);

            return new SyncResult
            {
                OperationId = operationId,
                Mode = request.Mode,
                Success = true,
                PeopleProcessed = elvantoPeople.Count,
                InboundPeople = counters.InboundPeople,
                InboundFields = counters.InboundFields,
                OutboundPeople = counters.OutboundPeople,
                OutboundFields = counters.OutboundFields,
                Conflicts = counters.Conflicts,
                AutoLinked = autoLinked,
                ManualReviewQueued = manualReview,
                Archived = archived,
                ManualReviewItems = reviewItems,
                AuditLog = audit.GetAll().ToList()
            };
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(token);
            logger.LogError(ex, "Sync operation {OperationId} failed", operationId);

            // A concurrency failure says "0 rows affected" and nothing about which row, which is
            // useless from the audit table alone. The entries carry the entity type and its key,
            // so name them here - this is the only place they still exist.
            string? conflictDetail = null;
            if (ex is DbUpdateConcurrencyException concurrency)
            {
                // Type, state and key only. The property values would identify a child and carry
                // their medical text into a column that is read by anyone looking at the run.
                List<string> conflicts = concurrency.Entries.Select(entry =>
                    $"{entry.Metadata.ShortName()}[{entry.State}] "
                    + string.Join(",", entry.Metadata.FindPrimaryKey()?.Properties
                        .Select(k => $"{k.Name}={entry.Property(k.Name).CurrentValue}") ?? [])).ToList();

                conflictDetail = string.Join(" | ", conflicts);
                logger.LogError("Sync {OperationId}: concurrency conflicts: {Conflicts}",
                    operationId, conflictDetail);
            }

            if (fetchedIds is { Count: > 0 })
            {
                try
                {
                    await db.SyncMetadata
                        .Where(m => fetchedIds.Contains(m.ElvantoId))
                        .ExecuteUpdateAsync(s => s
                                .SetProperty(m => m.LastSyncStatus, SyncStatus.Failed)
                                .SetProperty(m => m.LastSyncAt, DateTimeOffset.UtcNow),
                            token);
                }
                catch (Exception updateEx)
                {
                    logger.LogWarning(updateEx, "Could not record Failed sync status for operation {OperationId}",
                        operationId);
                }
            }

            operation.CompletedAt = DateTimeOffset.UtcNow;
            operation.Status = SyncStatus.Failed;
            // Without this the row reads Failed with an empty reason, and the only account of why
            // is a console log that is tail-truncated by the next run.
            operation.FailureReason = conflictDetail is null ? ex.Message : $"{ex.Message} || Conflicting entries: {conflictDetail}";
            try
            {
                await audit.FlushAsync(operation, token);
            }
            catch (Exception flushEx)
            {
                logger.LogWarning(flushEx, "Sync {OperationId}: failed to persist audit logs after failure",
                    operationId);
            }

            return new SyncResult
            {
                OperationId = operationId,
                Mode = request.Mode,
                Success = false,
                Error = ex.Message,
                PeopleProcessed = 0,
                InboundPeople = 0,
                InboundFields = 0,
                OutboundPeople = 0,
                OutboundFields = 0,
                Conflicts = 0,
                AutoLinked = 0,
                ManualReviewQueued = 0,
                Archived = 0
            };
        }
    }

    private sealed class SyncCounters
    {
        public int InboundPeople;
        public int InboundFields;
        public int OutboundPeople;
        public int OutboundFields;
        public int Conflicts;
    }

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

            if (config.Direction == SyncDirection.Disabled) continue;

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

            // If the Elvanto value is semantically empty for this field (e.g. NotRequested for MediaConsent),
            // suppress elvChanged so it can never drive an inbound update or win a conflict.
            if (!desc.IsValidInboundValue(elvValue))
                elvChanged = false;

            // Elvanto has a non-null value we have no snapshot for — treat as first-seen inbound
            bool elvFirstSeen = snapshot is null && elvValue is not null && desc.IsValidInboundValue(elvValue);
            if (!elvChanged && !appChanged && !elvFirstSeen) continue;

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

    private async Task<DbSyncMetadata> UpsertMetadata(
        DbPerson                           person,
        string                             elvantoId,
        int                                confidence,
        string                             strategy,
        Dictionary<string, DbSyncMetadata> metaByElvantoId,
        CancellationToken                  token = default
    )
    {
        if (metaByElvantoId.TryGetValue(elvantoId, out DbSyncMetadata? existing))
        {
            existing.MatchConfidence = confidence;
            existing.MatchStrategy = strategy;
            return existing;
        }

        DbSyncMetadata meta = new()
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            ElvantoId = elvantoId,
            MatchConfidence = confidence,
            MatchedAt = DateTimeOffset.UtcNow,
            MatchStrategy = strategy,
            LastSyncStatus = SyncStatus.Success
        };
        await db.SyncMetadata.AddAsync(meta, token);
        metaByElvantoId[elvantoId] = meta;
        return meta;
    }

    private async Task SaveNewPendingReviewsAsync(
        List<DbSyncPendingReview> reviews,
        CancellationToken         token
    )
    {
        if (reviews.Count == 0) return;

        List<Guid> personIds = reviews.Select(r => r.PersonId).ToList();
        HashSet<(Guid, string)> existing = (await db.PendingReviews
                .Where(r => personIds.Contains(r.PersonId))
                .Select(r => new { r.PersonId, r.ElvantoId })
                .ToListAsync(token))
            .Select(r => (r.PersonId, r.ElvantoId))
            .ToHashSet();

        bool added = false;
        foreach (DbSyncPendingReview review in reviews.Where(r => !existing.Contains((r.PersonId, r.ElvantoId))))
        {
            db.PendingReviews.Add(review);
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(token);
    }

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

    private string? TranslateElvantoValue(
        string                   fieldName,
        string?                  elvValue,
        List<DbSchoolGrade>      schoolGrades,
        Dictionary<string, Guid> familyIdMap
    )
    {
        if (elvValue is null) return null;

        if (fieldName == "SchoolGradeId")
            return schoolGrades.FirstOrDefault(g => g.ElvantoId == elvValue)?.Id.ToString();

        if (fieldName == "FamilyId")
        {
            if (!familyIdMap.TryGetValue(elvValue, out Guid localId))
            {
                localId = Guid.NewGuid();
                familyIdMap[elvValue] = localId;
            }

            return localId.ToString();
        }

        return elvValue;
    }

    private async Task<List<ElvantoPerson>> FetchElvantoAsync(SyncWithElvantoRequest request, CancellationToken ct)
        => request.Scope switch
        {
            ElvantoSyncScope.Person => await elvantoService.GetPersonByIdOrSearchAsync(request.PersonId!.Value, ct),
            ElvantoSyncScope.Family => await elvantoService.GetPeopleForFamilyAsync(request.FamilyId!.Value, ct),
            _                       => await elvantoService.GetAllPeopleAsync(ct)
        };

    private static SyncMode MapMode(ElvantoSyncMode mode) => mode switch
    {
        ElvantoSyncMode.DryRun   => SyncMode.DryRun,
        ElvantoSyncMode.AppOnly  => SyncMode.AppOnly,
        _                        => SyncMode.Full
    };

    private static SyncScope MapScope(ElvantoSyncScope scope) => scope switch
    {
        ElvantoSyncScope.Person => SyncScope.Person,
        ElvantoSyncScope.Family => SyncScope.Family,
        _                       => SyncScope.All
    };

    /// <summary>
    /// The medical/allergy text for a person, in the same format an update would push.
    /// Null when the descriptor is absent, which falls the create back to its own merge.
    /// </summary>
    private string? ComposeMedicalAllergyText(DbPerson person) =>
        _descriptors.OfType<MedicalAllergyNotesDescriptor>().FirstOrDefault()?.GetFromApp(person);

    /// <summary>
    /// Loads the allergen and medical-type tables into the medical/allergy descriptor so it can
    /// map Elvanto's text back onto rows. Also guarantees an "Other" medical type exists, which
    /// is where text that does not fit the agreed format is parked rather than dropped.
    /// </summary>
    private async Task PrimeMedicalAllergyLookupsAsync(CancellationToken token)
    {
        MedicalAllergyNotesDescriptor? descriptor =
            _descriptors.OfType<MedicalAllergyNotesDescriptor>().FirstOrDefault();
        if (descriptor is null) return;

        List<DbAllergen>    allergens    = await db.Allergens.ToListAsync(token);
        List<DbMedicalType> medicalTypes = await db.MedicalTypes.ToListAsync(token);

        const string otherLabel = "Other";
        DbMedicalType? other = medicalTypes
            .FirstOrDefault(t => string.Equals(t.Label, otherLabel, StringComparison.OrdinalIgnoreCase));

        if (other is null)
        {
            other = new DbMedicalType { Id = Guid.NewGuid(), Label = otherLabel };
            db.MedicalTypes.Add(other);
            medicalTypes.Add(other);
            logger.LogInformation("Sync: created the \"{Label}\" medical type to hold unparsed Elvanto text", otherLabel);
        }

        descriptor.Lookups = new MedicalAllergyLookups
        {
            AllergenLabels     = allergens.ToDictionary(a => a.Id, a => a.Label),
            MedicalTypeLabels  = medicalTypes.ToDictionary(m => m.Id, m => m.Label),
            OtherMedicalTypeId = other.Id
        };
    }

    private async Task<List<DbPerson>> LoadAppPeopleAsync(SyncWithElvantoRequest request, CancellationToken ct)
    {
        IQueryable<DbPerson> query = db.People
            .IgnoreQueryFilters()
            .Include(x => x.Allergies)
            .Include(x => x.MedicalNotes);

        return request.Scope switch
        {
            ElvantoSyncScope.Person => await query.Where(x => x.Id == request.PersonId!.Value).ToListAsync(ct),
            ElvantoSyncScope.Family => await query.Where(x => x.FamilyId == request.FamilyId!.Value).ToListAsync(ct),
            _                       => await query.ToListAsync(ct)
        };
    }
}