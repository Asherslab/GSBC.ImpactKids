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
    private bool                                            _reviewLoading  = false;

    private ImmutableList<SyncAuditLog> ToElvantoLogs =>
        ApplyFilters((_auditLogs.Data ?? []).Where(x => x.Direction == SyncSource.App).OrderBy(x => x.PersonId).ThenBy(x => x.EventType))
            .ToImmutableList();

    private ImmutableList<SyncAuditLog> FromElvantoLogs =>
        ApplyFilters((_auditLogs.Data ?? []).Where(x => x.Direction == SyncSource.Elvanto).OrderBy(x => x.PersonId).ThenBy(x => x.EventType))
            .ToImmutableList();

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

    private SyncStats? Stats => _auditLogs.HasData ? new SyncStats(_auditLogs.Data!) : null;

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

        // Placeholder people from rolled-back DryRun/AppOnly transactions won't be in the store;
        // the ManualReviewQueued log entry stores their name in ToValue as a fallback.
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

    private static Color ModeColor(SyncMode mode) => mode switch
    {
        SyncMode.Full    => Color.Primary,
        SyncMode.AppOnly => Color.Info,
        SyncMode.DryRun  => Color.Default,
        _                => Color.Default
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

public record SyncStats(ImmutableList<SyncAuditLog> Logs)
{
    public int Total           => Logs.Count;
    public int InboundPeople   => Logs.Count(x => x.EventType == SyncEventType.Created && x.Direction == SyncSource.Elvanto);
    public int InboundFields   => Logs.Count(x => x.EventType == SyncEventType.FieldUpdated && x.Direction == SyncSource.Elvanto);
    public int OutboundPeople  => Logs.Where(x => x.EventType is SyncEventType.WouldCreateInElvanto
                                                   || (x.EventType is SyncEventType.PushedToElvanto && string.IsNullOrEmpty(x.FieldName))).Select(x => x.PersonId).Distinct().Count();
    public int OutboundFields  => Logs.Count(x => x.EventType is SyncEventType.PushedToElvanto or SyncEventType.WouldPushToElvanto);
    public int Conflicts       => Logs.Count(x => x.EventType == SyncEventType.Conflict);
    public int AutoLinked      => Logs.Count(x => x.EventType == SyncEventType.Match);
    public int ManualReview    => Logs.Count(x => x.EventType == SyncEventType.ManualReviewQueued);
    public int Archived        => Logs.Count(x => x.EventType == SyncEventType.Archived);
    public int Diverged        => Logs.Count(x => x.EventType == SyncEventType.Diverged);
}
