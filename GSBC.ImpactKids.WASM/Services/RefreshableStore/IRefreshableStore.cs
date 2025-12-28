using EasyAppDev.Blazor.Store.Core;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public interface IRefreshableStore<T> : IStore<EntityListState<T>>, IRefreshableStore;

public interface IRefreshableStore
{
    Task RefreshAll(bool setLoading = true);
    Task RefreshEvent();
};