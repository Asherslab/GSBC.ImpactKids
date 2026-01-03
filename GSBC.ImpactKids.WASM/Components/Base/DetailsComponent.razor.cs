using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Base;

public partial class DetailsComponent<TEntity, TCreateRequest, TUpdateRequest> : IDetailsComponent, IDisposable
    where TEntity : IIdentifiable
    where TCreateRequest : new()
    where TUpdateRequest : IUpdateRequest<TEntity, TUpdateRequest>, new()
{
    [Parameter]
    public Guid? Id { get; set; }

    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;

    [Parameter]
    public Action<ModificationState>? OnStateChanged { get; set; }

    [Inject]
    public required IRefreshableStore<TEntity> EntityStore { get; set; }

    protected          AsyncData<TEntity> Entity        = AsyncData<TEntity>.NotAsked();
    protected readonly TCreateRequest     CreateRequest = new();
    protected          TUpdateRequest     UpdateRequest = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(EntityStore, _ =>
        {
            if (!Entity.IsLoading && State == ModificationState.Updating)
            {
                Snackbar.Add(
                    "Somebody else has made modifications, your edit has been cancelled",
                    Severity.Warning,
                    x =>
                    {
                        x.CloseAfterNavigation = true;
                        x.VisibleStateDuration = int.MaxValue;
                    });
                State = ModificationState.Reading;
                OnStateChanged?.Invoke(State);
            }

            RetrieveEntity();
        });
        RetrieveEntity();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        RetrieveEntity();
    }

    protected virtual void UpdateRequestUpdated()
    {
    }

    protected virtual bool AlternativeFilter(TEntity entity) => false;

    // ReSharper disable once MemberCanBePrivate.Global
    protected void RetrieveEntity()
    {
        if (State == ModificationState.Creating || _awaitingModification)
            return;

        Entity = EntityStore.GetState().First(x => x.Id == Id);

        if (Entity.HasError && Id == null)
            Entity = EntityStore.GetState().First(AlternativeFilter);

        if (Entity.Data == null)
        {
            UpdateRequest = new TUpdateRequest();
            StateHasChanged();
            return;
        }

        UpdateRequest = TUpdateRequest.FromEntity(Entity.Data);
        UpdateRequestUpdated();
        StateHasChanged();
    }

    private bool _awaitingModification;

    protected virtual TCreateRequest ModifyCreateRequest(TCreateRequest request) => request;

    public async Task<bool> CreateEntity()
    {
        _awaitingModification = true;
        try
        {
            Entity = Entity.ToLoading();
            StateHasChanged();
            TCreateRequest request = ModifyCreateRequest(CreateRequest);
            BasicResponse  resp    = await CreateService.Create(request);

            if (!resp.HasErrorOrNull())
                return true;

            RetrieveEntity();
            Snackbar.AddErrorResponse(resp);
            return false;
        }
        finally
        {
            _awaitingModification = false;
        }
    }

    protected virtual TUpdateRequest ModifyUpdateRequest(TUpdateRequest request) => request;

    public async Task<bool> UpdateEntity()
    {
        _awaitingModification = true;
        try
        {
            Entity = Entity.ToLoading();
            StateHasChanged();
            TUpdateRequest request = ModifyUpdateRequest(UpdateRequest);
            BasicResponse  resp    = await UpdateService.Update(request);

            if (!resp.HasErrorOrNull())
                return true;

            RetrieveEntity();
            Snackbar.AddErrorResponse(resp);
            return false;
        }
        finally
        {
            _awaitingModification = false;
        }
    }

    public async Task<bool> DeleteEntity()
    {
        _awaitingModification = true;
        try
        {
            DeleteResult result = await DeleteWithDialog(
                DeleteService,
                Entity.Data?.Id,
                () => Entity = Entity.ToLoading(),
                RetrieveEntity
            );
            return result == DeleteResult.Success;
        }
        finally
        {
            _awaitingModification = false;
        }
    }
}

public interface IDetailsComponent
{
    public Guid?                      Id             { get; set; }
    public ModificationState          State          { get; set; }
    public Action<ModificationState>? OnStateChanged { get; set; }

    public Task<bool> CreateEntity();
    public Task<bool> UpdateEntity();
    public Task<bool> DeleteEntity();
}