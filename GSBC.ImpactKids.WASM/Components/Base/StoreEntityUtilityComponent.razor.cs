using EasyAppDev.Blazor.Store.Core;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Components.Base;

public partial class StoreEntityUtilityComponent<TEntity> : IDisposable where TEntity : notnull
{
    private readonly List<IDisposable> _subscriptions = [];

    protected void HandleSelectorSubscriptionDisposal<T, TSelected>(
        IStore<T>          store,
        Func<T, TSelected> selector,
        Action<TSelected>  callback
    ) where T : notnull
    {
        _subscriptions.Add(store.Subscribe(selector, callback));
    }
    
    protected void HandleSubscriptionDisposal<T>(
        IStore<T> store,
        Action callback
    ) where T : notnull
    {
        _subscriptions.Add(store.Subscribe(_ => callback()));
    }

    protected void HandleSubscriptionDisposal<T>(
        IStore<T> store,
        Action<T> callback
    ) where T : notnull
    {
        _subscriptions.Add(store.Subscribe(callback));
    }

    protected void HandleStateChangeSubscriptionDisposal<T>(
        IStore<T> store
    ) where T : notnull
    {
        _subscriptions.Add(store.Subscribe(_ => StateHasChanged()));
    }

    public async Task<DeleteResult> DeleteWithDialog<T>(
        IBasicDeleteService<T> service,
        Guid?                  id,
        Action?                loadingFunc = null,
        Action?                onError     = null
    )
    {
        if (id == null)
            return DeleteResult.NullId;

        bool? result = await DialogService.ShowMessageBox(
            "Warning",
            "Deleting can not be undone!",
            yesText: "Delete!", cancelText: "Cancel");

        if (result == null)
            return DeleteResult.Cancelled;

        loadingFunc?.Invoke();
        StateHasChanged();

        BasicReadRequest request = new() { Guid = id.Value };
        BasicResponse    resp    = await service.BasicDelete(request);

        if (!resp.HasErrorOrNull())
            return DeleteResult.Success;

        Snackbar.AddErrorResponse(resp);
        onError?.Invoke();
        return DeleteResult.Errored;
    }

    public new void Dispose()
    {
        base.Dispose();
        foreach (IDisposable subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}