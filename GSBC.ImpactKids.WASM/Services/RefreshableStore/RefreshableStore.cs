using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using EasyAppDev.Blazor.Store.Utilities;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public partial class RefreshableStore<T>(
    ILazyCache                               cache,
    IAsyncActionExecutor<EntityListState<T>> executor,
    IStore<EntityListState<T>>               store,
    IReadMultipleServiceBase<T>              service,
    EventSubscriptionService                 eventSubscriptions
)
    : IRefreshableStore<EntityListState<T>>
{
    public async Task RefreshAll()
    {
        // await store.UpdateAsync(s => s with { Entities = s.Entities.ToLoading() });
        await executor.ExecuteAsync(
            () => cache.GetOrLoadAsync(
                $"{typeof(T).Name}-list",
                () => service.ReadMultiple(BasicReadMultipleRequest.All()),
                TimeSpan.FromMinutes(30)
            ),
            loading: s => s with { Entities = s.Entities.ToLoading() },
            success: (s, resp) => resp.HasError()
                ? s with { Entities = s.Entities.ToFailure(resp.Error!) }
                : s with { Entities = s.Entities.ToSuccess(resp.Entities) },
            error: (s, _) => s with { Entities = s.Entities.ToFailure("An unexpected error occurred") }
        );
    }
}