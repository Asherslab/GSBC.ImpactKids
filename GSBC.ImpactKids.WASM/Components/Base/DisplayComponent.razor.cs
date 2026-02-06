using EasyAppDev.Blazor.Store.AsyncActions;
using GSBC.ImpactKids.Shared.Contracts.Entities.Interfaces;
using GSBC.ImpactKids.WASM.Services.RefreshableStore;
using Microsoft.AspNetCore.Components;

namespace GSBC.ImpactKids.WASM.Components.Base;

public partial class DisplayComponent<TEntity>
    where TEntity : IIdentifiable
{
    [Parameter]
    public Guid? Id { get; set; }

    [Inject]
    public required IRefreshableStore<TEntity> EntityStore { get; set; }

    protected AsyncData<TEntity> Entity = AsyncData<TEntity>.NotAsked();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        HandleSubscriptionDisposal(EntityStore, _ => RetrieveEntity());
        RetrieveEntity();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        RetrieveEntity();
    }

    protected virtual bool ShouldRetrieve()                  => true;
    protected virtual bool AlternativeFilter(TEntity entity) => false;

    protected virtual void OnRetrievedEntityNull()
    {
    }

    protected virtual void OnRetrievedEntity()
    {
    }

    // ReSharper disable once MemberCanBePrivate.Global
    protected void RetrieveEntity()
    {
        if (!ShouldRetrieve())
            return;

        Entity = EntityStore.GetState().First(x => x.Id == Id);

        if (Entity.HasError && Id == null)
            Entity = EntityStore.GetState().First(AlternativeFilter);

        if (Entity.Data == null)
        {
            OnRetrievedEntityNull();
            StateHasChanged();
            return;
        }

        OnRetrievedEntity();
        StateHasChanged();
    }
}