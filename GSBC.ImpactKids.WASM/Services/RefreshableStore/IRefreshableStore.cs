using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public interface IRefreshableStore<T> : IStore<T>, IAsyncActionExecutor<T>, IRefreshableStore where T : notnull
{
    Task RefreshAll();
}

public interface IRefreshableStore;