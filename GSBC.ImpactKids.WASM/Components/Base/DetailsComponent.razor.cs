using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base.Interfaces;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.WASM.Components.Common.Inputs;
using GSBC.ImpactKids.WASM.Extensions;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace GSBC.ImpactKids.WASM.Components.Base;

public partial class DetailsComponent<TEntity, TCreateRequest, TUpdateRequest> : IDetailsComponent
    where TEntity : IIdentifiable
    where TCreateRequest : new()
    where TUpdateRequest : IUpdateRequest<TEntity, TUpdateRequest>, new()
{
    [Parameter]
    public ModificationState State { get; set; } = ModificationState.Reading;

    [Parameter]
    public Action<ModificationState>? OnStateChanged { get; set; }

    [Parameter]
    public bool RequestsReadonly { get; set; }

    [Parameter]
    public TCreateRequest CreateRequest { get; set; } = new();

    [Parameter]
    public TUpdateRequest UpdateRequest { get; set; } = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(EntityStore, _ =>
        {
            if (!Entity.IsLoading && State == ModificationState.Updating)
            {
                Snackbar.Add(
                    "Somebody has made modifications, your edit has been cancelled",
                    Severity.Warning,
                    x =>
                    {
                        x.CloseAfterNavigation = true;
                        x.VisibleStateDuration = int.MaxValue;
                    });
                State = ModificationState.Reading;
                OnStateChanged?.Invoke(State);
            }
        });
    }

    protected virtual void UpdateRequestUpdated()
    {
    }

    protected override bool ShouldRetrieve()        => State != ModificationState.Creating && !_awaitingModification;
    protected override void OnRetrievedEntityNull() => UpdateRequest = new TUpdateRequest();

    protected override void OnRetrievedEntity()
    {
        if (RequestsReadonly)
            return;
        UpdateRequest = TUpdateRequest.FromEntity(Entity.Data!);
        UpdateRequestUpdated();
    }

    private bool _awaitingModification;

    protected virtual TCreateRequest ModifyCreateRequest(TCreateRequest request) => request;

    public async Task<Guid?> CreateEntity()
    {
        _awaitingModification = true;
        try
        {
            Entity = Entity.ToLoading();
            StateHasChanged();
            TCreateRequest           request = ModifyCreateRequest(CreateRequest);
            BasicReadResponse<Guid?> resp    = await CreateService.Create(request);

            if (!resp.HasErrorOrNull())
                return resp.Entity;

            Entity = AsyncData<TEntity>.NotAsked();
            RetrieveEntity();
            Snackbar.AddErrorResponse(resp);
            StateHasChanged();
            return null;
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

    public Task<Guid?> CreateEntity();
    public Task<bool>  UpdateEntity();
    public Task<bool>  DeleteEntity();
}