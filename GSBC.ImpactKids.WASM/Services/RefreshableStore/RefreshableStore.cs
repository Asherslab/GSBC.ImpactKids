using System.Runtime.CompilerServices;
using EasyAppDev.Blazor.Store.AsyncActions;
using EasyAppDev.Blazor.Store.Core;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Extensions;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public partial class RefreshableStore<T>(
    IStore<EntityListState<T>>               store,
    IAsyncActionExecutor<EntityListState<T>> actionExecutor,
    IServiceProvider                         services
)
    : IRefreshableStore<T>
{
    public async Task RefreshAll(bool setLoading = true, [CallerMemberName] string? action = null)
    {
        using IServiceScope          scope   = services.CreateScope();
        IBasicReadMultipleService<T> service = scope.ServiceProvider.GetRequiredService<IBasicReadMultipleService<T>>();

        string name = typeof(T).Name;
        string key  = $"{name}-list";
        await actionExecutor.ExecuteCachedAsync(
            key,
            async () => await service.BasicReadMultiple(BasicReadMultipleRequest.All()),
            loading: s => s with { Entities = s.Entities.ToLoading() },
            success: (s, resp) => resp.HasError()
                ? s with { Entities = s.Entities.ToFailure(resp.Error ?? "An unexpected error occurred") }
                : s with { Entities = s.Entities.ToSuccess(resp.Entities) },
            error: (s, _) => s with { Entities = s.Entities.ToFailure("An unexpected error occurred") },
            cacheFor: TimeSpan.FromMinutes(30)
        );

        // try
        // {
        //     BasicReadMultipleResponse<T> resp = await lazyCache.GetOrLoadAsync(
        //         key,
        //         async () =>
        //         {
        //             await store.UpdateAsync(s => s with { Entities = s.Entities.ToLoading() });
        //             return await service.BasicReadMultiple(BasicReadMultipleRequest.All());
        //         },
        //         TimeSpan.FromMinutes(30)
        //     );
        //
        //     if (store.GetState().Entities.IsLoading) // avoid unnecessary updates
        //     {
        //         await store.UpdateAsync(s => resp.HasError()
        //             ? s with { Entities = s.Entities.ToFailure(resp.Error ?? "An unexpected error occurred") }
        //             : s with { Entities = s.Entities.ToSuccess(resp.Entities) }
        //         );
        //     }
        // }
        // catch (Exception)
        // {
        //     await store.UpdateAsync(s => s with { Entities = s.Entities.ToFailure("An unexpected error occurred") });
        // }
    }

    public async Task RefreshEvent()
    {
        if (store.GetState().Entities.IsNotAsked)
            return;

        string name = typeof(T).Name;
        string key  = $"{name}-list";
        actionExecutor.InvalidateCache(key);
        await RefreshAll();
    }
}