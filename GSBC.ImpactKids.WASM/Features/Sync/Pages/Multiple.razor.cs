using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.People;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Sync.Pages;

public partial class Multiple : ComponentBase, IDisposable
{
    [Inject] public required IRefreshableStore<SyncManualReviewEntry> PendingReviewsStore { get; set; }

    private bool _isSubmitting;

    private AsyncData<ImmutableList<SyncOperation>>         _operations     = AsyncData<ImmutableList<SyncOperation>>.NotAsked();
    private AsyncData<ImmutableList<SyncManualReviewEntry>> _pendingReviews = AsyncData<ImmutableList<SyncManualReviewEntry>>.NotAsked();

    private IDisposable? _syncSub;
    private IDisposable? _reviewsSub;

    private int PendingReviewCount =>
        _pendingReviews.Data?.Count(r => r.Status == ManualReviewStatus.Pending) ?? 0;

    protected override async Task OnInitializedAsync()
    {
        _syncSub    = SyncStore.Subscribe(_ => RefreshOperations());
        _reviewsSub = PendingReviewsStore.Subscribe(_ => RefreshPendingReviews());

        RefreshOperations();
        RefreshPendingReviews();

        // People are no longer loaded here. This page used to fetch all ~1700 of them to fill the
        // Person and Family scope dropdowns; with scope gone there is nothing on it that names a
        // person, so the run list no longer waits on the roll to render.
        await Task.WhenAll(
            SyncStore.RefreshAll(),
            PendingReviewsStore.RefreshAll()
        );
    }

    private void RefreshOperations()
    {
        _operations = SyncStore.GetState().Entities;
        InvokeAsync(StateHasChanged);
    }

    private void RefreshPendingReviews()
    {
        _pendingReviews = PendingReviewsStore.GetState().Entities;
        InvokeAsync(StateHasChanged);
    }

    private async Task SubmitSync()
    {
        _isSubmitting = true;
        StateHasChanged();

        try
        {
            SyncResponse response = await SyncService.CreateSync(new SyncWithElvantoRequest());

            if (!response.Success)
                Snackbar.Add(response.Error ?? "Sync failed", Severity.Error);
            else
                Snackbar.Add(
                    $"Plan decided — {response.PlannedChanges} change{(response.PlannedChanges == 1 ? "" : "s")} "
                    + "waiting. Nothing has been written yet.",
                    Severity.Success);
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
            await PendingReviewsStore.RefreshEvent();
        }
    }

    /// <summary>
    /// Runs the work an earlier Decide recorded. Everything that makes this safe is in the engine:
    /// the plan expires, every item's two sides are re-read before it is applied, and nothing
    /// outside the plan is touched - anything that appeared since belongs to the next plan.
    /// </summary>
    private async Task ExecutePlan(SyncOperation operation)
    {
        // The engine's guards are all per-item; nothing in them notices that the person pressing this
        // meant to press View. Naming the count and the fact that Elvanto is written to is the part
        // that has to happen before the call, not after it.
        bool? confirmed = await DialogService.ShowMessageBoxAsync(
            "Execute this plan?",
            $"{operation.PendingPlanItems} change{(operation.PendingPlanItems == 1 ? "" : "s")} will be applied "
            + "to this app and, where writes are enabled, sent to Elvanto. Open View first if you have not "
            + "read the plan.",
            yesText: "Execute", cancelText: "Cancel"
        );

        if (confirmed is null)
            return;

        _isSubmitting = true;
        StateHasChanged();

        try
        {
            SyncResponse response = await SyncService.ExecutePlan(
                new ExecutePlanRequest { OperationId = operation.Id });

            if (!response.Success)
            {
                Snackbar.Add(response.Error ?? "Execute failed", Severity.Error);
            }
            else if (response.StaleItems > 0)
            {
                Snackbar.Add(
                    $"Executed — {response.StaleItems} item{(response.StaleItems == 1 ? "" : "s")} skipped because "
                    + "a side had moved since the plan was decided",
                    Severity.Warning);
            }
            else
            {
                Snackbar.Add("Plan executed", Severity.Success);
            }
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
            await SyncStore.RefreshEvent();
            await PendingReviewsStore.RefreshEvent();
        }
    }

    private static Color StatusColor(SyncStatus status) => status switch
    {
        SyncStatus.Success      => Color.Success,
        SyncStatus.Failed       => Color.Error,
        SyncStatus.Conflict     => Color.Warning,
        SyncStatus.ManualReview => Color.Secondary,
        _                       => Color.Default
    };

    public void Dispose()
    {
        _syncSub?.Dispose();
        _reviewsSub?.Dispose();
    }
}