using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.People;
using GSBC.ImpactKids.Shared.Contracts.Entities.Features.Sync;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Features.People.Sync;
using GSBC.ImpactKids.WASM.Features.People.Components.Individual;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Features.Sync.Pages;

public partial class Multiple : ComponentBase, IDisposable
{
    [Inject] public required IRefreshableStore<SyncManualReviewEntry> PendingReviewsStore { get; set; }

    private ElvantoSyncMode  _mode  = ElvantoSyncMode.DryRun;
    private ElvantoSyncScope _scope = ElvantoSyncScope.All;
    private Guid?            _personId;
    private Guid?            _familyId;
    private bool             _isSubmitting;

    private AsyncData<ImmutableList<SyncOperation>>         _operations     = AsyncData<ImmutableList<SyncOperation>>.NotAsked();
    private AsyncData<ImmutableList<Person>>                _people         = AsyncData<ImmutableList<Person>>.NotAsked();
    private AsyncData<ImmutableList<SyncManualReviewEntry>> _pendingReviews = AsyncData<ImmutableList<SyncManualReviewEntry>>.NotAsked();
    private ImmutableList<FamilyDefinition>                 _families       = ImmutableList<FamilyDefinition>.Empty;

    private IDisposable? _syncSub;
    private IDisposable? _peopleSub;
    private IDisposable? _reviewsSub;

    private int PendingReviewCount =>
        _pendingReviews.Data?.Count(r => r.Status == ManualReviewStatus.Pending) ?? 0;

    private bool CanSubmit => _scope switch
    {
        ElvantoSyncScope.Person => _personId.HasValue,
        ElvantoSyncScope.Family => _familyId.HasValue,
        _                       => true
    };

    protected override async Task OnInitializedAsync()
    {
        _syncSub    = SyncStore.Subscribe(_ => RefreshOperations());
        _peopleSub  = PeopleStore.Subscribe(_ => RefreshPeople());
        _reviewsSub = PendingReviewsStore.Subscribe(_ => RefreshPendingReviews());

        RefreshOperations();
        RefreshPeople();
        RefreshPendingReviews();

        await Task.WhenAll(
            SyncStore.RefreshAll(),
            PeopleStore.RefreshAll(),
            PendingReviewsStore.RefreshAll()
        );
    }

    private void RefreshOperations()
    {
        _operations = SyncStore.GetState().Entities;
        InvokeAsync(StateHasChanged);
    }

    private void RefreshPeople()
    {
        _people = PeopleStore.GetState().Entities;

        if (_people.Data != null)
        {
            _families = _people.Data
                .GroupBy(x => x.FamilyId)
                .Select(x => new FamilyDefinition(
                    x.Key,
                    x.GroupBy(y => y.LastName).MaxBy(y => y.Count())!.Key,
                    x.Count()
                ))
                .OrderBy(x => x.FamilyName)
                .ToImmutableList();
        }

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
            var response = await SyncService.CreateSync(new SyncWithElvantoRequest
            {
                Mode = _mode,
                Scope = _scope,
                PersonId = _personId,
                FamilyId = _familyId
            });

            if (!response.Success)
                Snackbar.Add(response.Error ?? "Sync failed", Severity.Error);
            else
                Snackbar.Add($"Sync complete — {response.PeopleProcessed} people processed", Severity.Success);
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
            await PendingReviewsStore.RefreshEvent();
        }
    }

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

    public void Dispose()
    {
        _syncSub?.Dispose();
        _peopleSub?.Dispose();
        _reviewsSub?.Dispose();
    }
}