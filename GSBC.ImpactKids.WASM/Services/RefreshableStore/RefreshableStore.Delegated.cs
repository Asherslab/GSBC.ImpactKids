using EasyAppDev.Blazor.Store.Selectors;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public partial class RefreshableStore<T>
{
    public async Task ExecuteAsync<TResult>(
        Func<Task<TResult>>                                      asyncAction,
        Func<EntityListState<T>, EntityListState<T>>             loading,
        Func<EntityListState<T>, TResult, EntityListState<T>>    success,
        Func<EntityListState<T>, Exception, EntityListState<T>>? error  = null,
        string?                                                  action = null
    )
    {
        await executor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    public async Task ExecuteAsync(
        Func<Task>                                               asyncAction,
        Func<EntityListState<T>, EntityListState<T>>             loading,
        Func<EntityListState<T>, EntityListState<T>>             success,
        Func<EntityListState<T>, Exception, EntityListState<T>>? error  = null,
        string?                                                  action = null
    )
    {
        await executor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    public async Task ExecuteAsync<TResult>(
        Func<Task<TResult>>                                      asyncAction,
        Func<EntityListState<T>, EntityListState<T>>             loading,
        Func<EntityListState<T>, EntityListState<T>>             success,
        Func<EntityListState<T>, Exception, EntityListState<T>>? error  = null,
        string?                                                  action = null
    )
    {
        await executor.ExecuteAsync(asyncAction, loading, success, error, action);
    }

    public EntityListState<T> GetState()
    {
        return store.GetState();
    }

    public async Task UpdateAsync(Func<EntityListState<T>, EntityListState<T>> updater, string? action = null)
    {
        await store.UpdateAsync(updater, action);
    }

    public async Task UpdateAsync(
        Func<EntityListState<T>, Task<EntityListState<T>>> asyncUpdater,
        string?                                            action = null
    )
    {
        await store.UpdateAsync(asyncUpdater, action);
    }

    public IDisposable Subscribe(Action<EntityListState<T>> callback)
    {
        return store.Subscribe(callback);
    }

    public IDisposable Subscribe<TSelected>(Func<EntityListState<T>, TSelected> selector, Action<TSelected> callback)
    {
        return store.Subscribe(selector, callback);
    }

    public IDisposable Subscribe<TSelected>(
        Func<EntityListState<T>, TSelected> selector,
        Action<TSelected>                   callback,
        IEqualityComparer<TSelected>        comparer
    )
    {
        return store.Subscribe(selector, callback, comparer);
    }

    public IDisposable Subscribe<TSelected>(
        ISelector<EntityListState<T>, TSelected> selector,
        Action<TSelected>                        callback
    )
    {
        return store.Subscribe(selector, callback);
    }

    public void Dispose()
    {
        store.Dispose();
    }
}