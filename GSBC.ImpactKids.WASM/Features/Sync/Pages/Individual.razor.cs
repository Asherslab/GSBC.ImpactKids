using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Features.Sync;
using GSBC.ImpactKids.WASM.Components.Common;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Features.People.Components.Individual;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Sync.Pages;

public partial class Individual
{
    [Parameter]
    public Guid Id { get; set; }

    private readonly BreadcrumbItem[] _breadcrumbs =
    [
        new("Sync", href: "/Sync"),
        new("Individual", href: "/Sync/Individual", disabled: true),
    ];

    [Inject] public required IRefreshableStore<SyncManualReviewEntry> PendingReviewsStore { get; set; }

    private AsyncData<SyncOperation>                        _operation      = AsyncData<SyncOperation>.NotAsked();
    private AsyncData<ImmutableList<SyncAuditLog>>          _auditLogs      = AsyncData<ImmutableList<SyncAuditLog>>.NotAsked();
    private AsyncData<ImmutableList<SyncManualReviewEntry>> _pendingReviews = AsyncData<ImmutableList<SyncManualReviewEntry>>.NotAsked();
    private AsyncData<ImmutableList<SyncPlannedChange>>     _plan           = AsyncData<ImmutableList<SyncPlannedChange>>.NotAsked();
    private bool                                            _reviewLoading  = false;

    // The plan is split the same way the audit trail is - by direction - so a dry run reads like an
    // executed run. Kind, not Direction: a plan row says what it would do, not what it did.
    private ImmutableList<SyncPlannedChange> PlanToElvanto =>
        PlanOf(PlannedChangeKind.OutboundField, PlannedChangeKind.CreateInElvanto);

    private ImmutableList<SyncPlannedChange> PlanFromElvanto =>
        PlanOf(PlannedChangeKind.InboundField, PlannedChangeKind.CreateLocally);

    private ImmutableList<SyncPlannedChange> PlanLinks =>
        PlanOf(PlannedChangeKind.LinkPerson);

    private ImmutableList<SyncPlannedChange> PlanArchive =>
        PlanOf(PlannedChangeKind.Archive);

    private ImmutableList<SyncPlannedChange> PlanOf(params PlannedChangeKind[] kinds) =>
        (_plan.Data ?? [])
        .Where(x => kinds.Contains(x.Kind))
        .Where(MatchesSearch)
        .OrderBy(x => x.FieldName)
        .ThenBy(x => x.PersonId)
        .ToImmutableList();

    /// <summary>
    /// Counts go in the tab label rather than a MudTabPanel badge. The badge is positioned past the
    /// label's right edge and the tab strip clips it, so "50" showed as "5" - and a truncated count
    /// is worse than none on a page whose whole job is telling you how much there is to do.
    /// Only what is still waiting is counted; applied rows are history.
    /// </summary>
    private static string PlanLabel(string text, ImmutableList<SyncPlannedChange> items) =>
        Labelled(text, items.Count(x => x.Status == PlannedChangeStatus.Pending));

    private static string Labelled(string text, int count) =>
        count > 0 ? $"{text} ({count})" : text;

    private int PendingPlanItems =>
        (_plan.Data ?? []).Count(x => x.Status == PlannedChangeStatus.Pending);

    // Direction alone is not enough to call a row executed. "CreateSuppressed:AwaitingReview" is
    // written with Direction=App while the run pushes nothing - the person is held behind an
    // unanswered review - so it was landing under Executed > To Elvanto on a dry run, reading as a
    // push that happened. Every review row's home is the Manual Review tab, which already lists the
    // person it is about.
    private ImmutableList<SyncAuditLog> ToElvantoLogs =>
        ApplyFilters(Executed(SyncSource.App)).ToImmutableList();

    private ImmutableList<SyncAuditLog> FromElvantoLogs =>
        ApplyFilters(Executed(SyncSource.Elvanto)).ToImmutableList();

    private IEnumerable<SyncAuditLog> Executed(SyncSource direction) =>
        (_auditLogs.Data ?? [])
        .Where(x => x.Direction == direction && x.EventType != SyncEventType.ManualReviewQueued)
        .OrderBy(x => x.PersonId)
        .ThenBy(x => x.EventType);

    private ImmutableList<SyncAuditLog> MatchConflictLogs =>
        ApplyFilters((_auditLogs.Data ?? [])
                .Where(x => x.Direction == null
                            && x.EventType != SyncEventType.ManualReviewQueued
                            && x.EventType != SyncEventType.Diverged)
                .OrderBy(x => x.PersonId).ThenBy(x => x.EventType))
            .ToImmutableList();

    // Its own tab rather than a corner of "Matches & Conflicts". These are the rows where the two
    // sides differ and the run chose not to act, so they are the work-list a person reads to find
    // changes that have been going missing - not a footnote to the conflicts that were resolved.
    private ImmutableList<SyncAuditLog> DivergedLogs =>
        ApplyFilters((_auditLogs.Data ?? [])
                .Where(x => x.EventType == SyncEventType.Diverged)
                .OrderBy(x => x.FieldName).ThenBy(x => x.PersonId))
            .ToImmutableList();

    private ImmutableList<SyncManualReviewEntry> ManualReviewItems
    {
        get
        {
            if (_auditLogs.Data is null || _pendingReviews.Data is null)
                return ImmutableList<SyncManualReviewEntry>.Empty;
            ImmutableHashSet<Guid> personIds = _auditLogs.Data
                .Where(x => x.EventType == SyncEventType.ManualReviewQueued)
                .Select(x => x.PersonId)
                .ToImmutableHashSet();
            return _pendingReviews.Data.Where(r => personIds.Contains(r.PersonId)).ToImmutableList();
        }
    }

    private ImmutableList<string> AvailableFields =>
        (_auditLogs.Data ?? [])
            .Select(x => x.FieldName)
            .Where(x => x != null)
            .Select(x => x!)
            .Distinct()
            .Order()
            .ToImmutableList();

    private bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(State.Search) ||
        State.SelectedEventTypes.Count > 0 ||
        State.SelectedFields.Count > 0;

    private SyncStats? Stats =>
        _auditLogs.HasData ? new SyncStats(_auditLogs.Data!, _plan.Data ?? []) : null;

    protected override async Task OnInitializedAsync()
    {
        Update(_ => IndividualSyncState.Initial);
        HandleSubscriptionDisposal(SyncStore,          RefreshOperation);
        HandleSubscriptionDisposal(PendingReviewsStore, RefreshPendingReviews);

        await Task.WhenAll(
            SyncStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            PendingReviewsStore.RefreshAll()
        );

        RefreshOperation();
        RefreshPendingReviews();
        await LoadAuditLogs();
        await LoadPlan();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        RefreshOperation();
    }

    private void RefreshOperation()
    {
        _operation = SyncStore.GetState().First(x => x.Id == Id);
        InvokeAsync(StateHasChanged);
    }

    private async Task LoadAuditLogs()
    {
        _auditLogs = AsyncData<ImmutableList<SyncAuditLog>>.Loading();
        StateHasChanged();

        try
        {
            List<SyncAuditLog> logs = [];
            await foreach (BasicReadMultipleResponse<SyncAuditLog> response in SyncService.ReadAuditLogs(new BasicReadRequest { Id = Id.ToString() }))
            {
                if (!response.Success)
                {
                    _auditLogs = _auditLogs.ToFailure(response.Error ?? "Failed to load audit logs");
                    StateHasChanged();
                    return;
                }
                logs.AddRange(response.Entities);
            }
            _auditLogs = _auditLogs.ToSuccess(logs.ToImmutableList());
        }
        catch (Exception ex)
        {
            _auditLogs = _auditLogs.ToFailure(ex.Message);
        }

        StateHasChanged();
    }

    private async Task LoadPlan()
    {
        _plan = AsyncData<ImmutableList<SyncPlannedChange>>.Loading();
        StateHasChanged();

        try
        {
            List<SyncPlannedChange> items = [];
            await foreach (BasicReadMultipleResponse<SyncPlannedChange> response in
                           SyncService.ReadPlannedChanges(new BasicReadRequest { Id = Id.ToString() }))
            {
                if (!response.Success)
                {
                    _plan = _plan.ToFailure(response.Error ?? "Failed to load the plan");
                    StateHasChanged();
                    return;
                }
                items.AddRange(response.Entities);
            }
            _plan = _plan.ToSuccess(items.ToImmutableList());
        }
        catch (Exception ex)
        {
            _plan = _plan.ToFailure(ex.Message);
        }

        StateHasChanged();
    }

    /// <summary>
    /// The plan shares the page's search box but not its event-type or field filters: those name
    /// audit vocabulary a plan row does not have.
    /// </summary>
    private bool MatchesSearch(SyncPlannedChange item)
    {
        if (string.IsNullOrWhiteSpace(State.Search)) return true;

        string search = State.Search.Trim();
        return GetPersonName(item.PersonId ?? Guid.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
               || (item.FieldName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || (item.ObservedAppValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || (item.ObservedElvantoValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || (item.ProposedValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || item.Reason.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshPendingReviews()
    {
        _pendingReviews = PendingReviewsStore.GetState().Entities;
        InvokeAsync(StateHasChanged);
    }

    private async Task ApproveReview(SyncManualReviewEntry review)
    {
        _reviewLoading = true;
        StateHasChanged();
        try
        {
            var result = await SyncService.ApproveReview(new ManualReviewActionRequest { Id = review.Id });
            if (result.Success)
            {
                Snackbar.Add($"Approved match for {review.PersonName ?? review.PersonId.ToString()[..8]}", Severity.Success);
                await PendingReviewsStore.RefreshEvent();
            }
            else
            {
                Snackbar.Add(result.Error ?? "Failed to approve", Severity.Error);
            }
        }
        finally
        {
            _reviewLoading = false;
            StateHasChanged();
        }
    }

    private async Task DenyReview(SyncManualReviewEntry review)
    {
        _reviewLoading = true;
        StateHasChanged();
        try
        {
            var result = await SyncService.DenyReview(new ManualReviewActionRequest { Id = review.Id });
            if (result.Success)
            {
                Snackbar.Add($"Denied match for {review.PersonName ?? review.PersonId.ToString()[..8]}", Severity.Warning);
                await PendingReviewsStore.RefreshEvent();
            }
            else
            {
                Snackbar.Add(result.Error ?? "Failed to deny", Severity.Error);
            }
        }
        finally
        {
            _reviewLoading = false;
            StateHasChanged();
        }
    }

    private IEnumerable<SyncAuditLog> ApplyFilters(IEnumerable<SyncAuditLog> logs)
    {
        if (State.SelectedEventTypes.Count > 0)
            logs = logs.Where(x => State.SelectedEventTypes.Contains(x.EventType));

        if (State.SelectedFields.Count > 0)
            logs = logs.Where(x => x.FieldName != null && State.SelectedFields.Contains(x.FieldName));

        if (!string.IsNullOrWhiteSpace(State.Search))
        {
            string search = State.Search.Trim();
            logs = logs.Where(x =>
                GetPersonName(x.PersonId).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (x.FieldName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.FromValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.ToValue?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.Reason.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return logs;
    }

    private async Task OnSearch(string text)
    {
        await UpdateDebounced(
            s => s.SetSearch(string.IsNullOrWhiteSpace(text) ? null : text),
            TimeSpan.FromSeconds(0.25).Milliseconds
        );
    }

    private void OnEventTypesChanged(IEnumerable<SyncEventType> types) =>
        Update(s => s.SetEventTypes(types.ToImmutableHashSet()));

    private void OnFieldsChanged(IEnumerable<string> fields) =>
        Update(s => s.SetFields(fields.ToImmutableHashSet()));

    private void ClearFilters() =>
        Update(_ => IndividualSyncState.Initial);

    internal string GetPersonName(Guid personId)
    {
        AsyncData<Person> person = PeopleStore.GetState().First(x => x.Id == personId);
        if (person.Data is { } p) return p.GetDisplayName();

        // A person named by a plan that has not been executed yet is not in the store; the
        // ManualReviewQueued log entry stores their name in ToValue as a fallback.
        string? auditName = _auditLogs.Data?
            .FirstOrDefault(x => x.PersonId == personId && x.EventType == SyncEventType.ManualReviewQueued)
            ?.ToValue;
        return auditName ?? personId.ToString()[..8];
    }

    internal static string FormatEventType(SyncEventType type) => type switch
    {
        SyncEventType.FieldUpdated         => "Field Updated",
        SyncEventType.PushedToElvanto      => "Pushed",
        SyncEventType.WouldPushToElvanto   => "Would Push",
        SyncEventType.WouldCreateInElvanto => "Would Create",
        SyncEventType.ManualReviewQueued   => "Manual Review",
        SyncEventType.Conflict             => "Conflict",
        SyncEventType.Created              => "Created",
        SyncEventType.Archived             => "Archived",
        SyncEventType.Match                => "Matched",
        SyncEventType.Diverged             => "Diverged",
        _                                  => type.ToString()
    };

    private Task ShowPersonDetails(Guid personId) =>
        DetailsComponentDialog.Open<PersonDetails>(DialogService, "Person", ModificationState.Reading, personId);

    // The two kinds of review ask genuinely different questions, and the buttons used to read
    // "Approve"/"Deny" for both. Approving a low-confidence match links two records together;
    // approving a duplicate says the opposite - that the app person already exists in Elvanto and
    // must NOT be pushed. Same word, opposite outcome, so the card now spells out the question.
    private static bool IsDuplicateReview(SyncManualReviewEntry review) =>
        review.MatchStrategy?.StartsWith("PotentialDuplicate", StringComparison.OrdinalIgnoreCase) == true;

    private static string ReviewQuestion(SyncManualReviewEntry review, string personName) =>
        IsDuplicateReview(review)
            ? $"Is {personName} the same person as this Elvanto record? They share a first and last name with someone already linked."
            : $"Link {personName} to this Elvanto record?";

    private static string ApproveLabel(SyncManualReviewEntry review) =>
        IsDuplicateReview(review) ? "Same person" : "Link";

    private static string DenyLabel(SyncManualReviewEntry review) =>
        IsDuplicateReview(review) ? "Different people" : "Don't link";

    private static string ApproveHint(SyncManualReviewEntry review) =>
        IsDuplicateReview(review)
            ? "Keeps them out of Elvanto — no new record is created."
            : "Links the two records and syncs their fields from now on.";

    private static string DenyHint(SyncManualReviewEntry review) =>
        IsDuplicateReview(review)
            ? "Creates them in Elvanto as a new, separate person."
            : "Never links this pair; both sides are skipped each run.";

    private static Color ConfidenceColor(int confidence) => confidence switch
    {
        >= 70 => Color.Warning,
        >= 50 => Color.Default,
        _     => Color.Error
    };

    private static Color ReviewStatusColor(ManualReviewStatus status) => status switch
    {
        ManualReviewStatus.Approved => Color.Success,
        ManualReviewStatus.Denied   => Color.Error,
        _                           => Color.Warning
    };

    private static Color StatusColor(SyncStatus status) => status switch
    {
        SyncStatus.Success      => Color.Success,
        SyncStatus.Failed       => Color.Error,
        SyncStatus.Conflict     => Color.Warning,
        SyncStatus.ManualReview => Color.Secondary,
        _                       => Color.Default
    };
}

/// <summary>
/// The headline numbers for one operation.
///
/// Each is <b>what happened plus what is still waiting to</b>. Deciding writes a plan and no
/// past-tense audit row, so counting audit rows alone showed a dry run as all zeros while its plan
/// held two hundred items — every tile reading "nothing to do" for the run whose entire job is to
/// say what there is to do. A plan item stops being Pending once Apply has dealt with it, and the
/// audit row appears in the same moment, so nothing is counted twice.
/// </summary>
public record SyncStats(ImmutableList<SyncAuditLog> Logs, ImmutableList<SyncPlannedChange> Plan)
{
    private int Waiting(PlannedChangeKind kind) =>
        Plan.Count(x => x.Kind == kind && x.Status == PlannedChangeStatus.Pending);

    public int Total           => Logs.Count + Plan.Count;
    public int InboundPeople   => Logs.Count(x => x.EventType == SyncEventType.Created && x.Direction == SyncSource.Elvanto)
                                  + Waiting(PlannedChangeKind.CreateLocally);
    public int InboundFields   => Logs.Count(x => x.EventType == SyncEventType.FieldUpdated && x.Direction == SyncSource.Elvanto)
                                  + Waiting(PlannedChangeKind.InboundField);
    public int OutboundPeople  => Logs.Where(x => x.EventType is SyncEventType.WouldCreateInElvanto
                                                   || (x.EventType is SyncEventType.PushedToElvanto && string.IsNullOrEmpty(x.FieldName))).Select(x => x.PersonId).Distinct().Count()
                                  + Waiting(PlannedChangeKind.CreateInElvanto);
    public int OutboundFields  => Logs.Count(x => x.EventType is SyncEventType.PushedToElvanto or SyncEventType.WouldPushToElvanto)
                                  + Waiting(PlannedChangeKind.OutboundField);
    public int Conflicts       => Logs.Count(x => x.EventType == SyncEventType.Conflict);
    public int AutoLinked      => Logs.Count(x => x.EventType == SyncEventType.Match)
                                  + Waiting(PlannedChangeKind.LinkPerson);
    public int ManualReview    => Logs.Count(x => x.EventType == SyncEventType.ManualReviewQueued);
    public int Archived        => Logs.Count(x => x.EventType == SyncEventType.Archived)
                                  + Waiting(PlannedChangeKind.Archive);
    public int Diverged        => Logs.Count(x => x.EventType == SyncEventType.Diverged);
    public int Stale           => Plan.Count(x => x.Status == PlannedChangeStatus.Stale);
}
