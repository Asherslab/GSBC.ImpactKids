using System.Collections.Immutable;
using EasyAppDev.Blazor.Store.AsyncActions;
using Grpc.Core;
using Microsoft.AspNetCore.Components;
using EasyAppDev.Blazor.Store.Core;
using GSBC.ImpactKids.Shared.Contracts.Messages.Requests.Base;
using GSBC.ImpactKids.Shared.Contracts.Messages.Responses.Base;
using GSBC.ImpactKids.Shared.Contracts.Services.Base;
using GSBC.ImpactKids.WASM.Extensions;
using GSBC.ImpactKids.WASM.Features.Eventing.Services;

namespace GSBC.ImpactKids.WASM.Services.RefreshableStore;

public partial class RefreshableStore<T>(
    IStore<EntityListState<T>>               store,
    IAsyncActionExecutor<EntityListState<T>> actionExecutor,
    IServiceProvider                         services,
    ISseClientService                        sseClientService,
    NavigationManager                        navigation
)
    : IRefreshableStore<T>
{
    public async Task RefreshAll()
    {
        string name = typeof(T).Name;
        string key  = $"{name}-list";

        BasicReadMultipleResponse<T> resp;

        // BasicReadMultiple is server streaming, and ExceptionInterceptor only wraps
        // unary calls, so a failed read reaches the calling component and takes the
        // render tree down with it. The executor has already put the store into its
        // failure state by this point, so pages can render from that instead.
        try
        {
            resp = await actionExecutor.ExecuteCachedAsync(
                key,
                RetrieveEntities,
                loading: s => s with { Entities = s.Entities.ToLoadingPreserved() },
                success: (s, resp) => resp.HasError()
                    ? s with { Entities = s.Entities.ToFailure(resp.Error ?? "An unexpected error occurred") }
                    : s with { Entities = s.Entities.ToSuccess(resp.Entities) },
                error: (s, _) => s with { Entities = s.Entities.ToFailure("An unexpected error occurred") },
                cacheFor: TimeSpan.FromMinutes(30)
            );
        }
        catch (RpcException e)
        {
            // The session died out from under us - the cached client side principal can
            // outlive the proxy's cookie. Get a fresh one rather than sitting on a page
            // where nothing will ever load.
            if (e.StatusCode is StatusCode.Unauthenticated)
            {
                navigation.NavigateTo(
                    $"bff/login?returnUrl={Uri.EscapeDataString(navigation.Uri)}",
                    forceLoad: true
                );
            }

            return;
        }

        if (!resp.HasErrorOrNull())
        {
            if (sseClientService is { Connected: false, Started: false })
                await sseClientService.StartAsync();
        }

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

    private async Task<BasicReadMultipleResponse<T>> RetrieveEntities()
    {
        using IServiceScope scope = services.CreateScope();
        IBasicReadMultipleService<T> service =
            scope.ServiceProvider.GetRequiredService<IBasicReadMultipleService<T>>();

        List<T> entities = [];
        await foreach (
            BasicReadMultipleResponse<T> resp in
            service.BasicReadMultiple(
                BasicReadMultipleRequest.All()
            )
        )
        {
            if (resp.HasErrorOrNull())
                return resp;

            entities.AddRange(resp.Entities);
        }

        return new BasicReadMultipleResponse<T>
        {
            Entities = entities.ToImmutableList(),
            Success = true
        };
    }

    /// <summary>
    /// One refresh-after-a-write at a time, per entity type. This store is registered as a
    /// singleton per <typeparamref name="T" />, so this gate covers every caller.
    /// </summary>
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    /// <summary>
    /// Refresh because something changed. Unlike <see cref="RefreshAll" /> this always goes
    /// to the server.
    /// <para>
    /// <b>The gate and the ordering inside it are the whole point.</b> The action executor
    /// coalesces concurrent calls for one key, so a refresh asked for now can be answered by
    /// a request that started <em>before</em> the write it is meant to pick up - and that
    /// stale answer then fills the thirty minute cache, so nothing fetches again. Measured on
    /// a household sign-out: two writes, <b>one</b> read, and that read straddled the second
    /// write. One row correct on screen, one stale, both correct in the database.
    /// </para>
    /// <para>
    /// Waiting for any in-flight refresh to finish before invalidating means this one starts
    /// afterwards and therefore sees the write. <b>Invalidate inside the gate, never before
    /// it</b> - an invalidation that happens while the earlier request is still running is
    /// undone when that request completes and re-fills the cache.
    /// </para>
    /// </summary>
    public async Task RefreshEvent()
    {
        if (store.GetState().Entities.IsNotAsked)
            return;

        string name = typeof(T).Name;
        string key  = $"{name}-list";

        await _refreshGate.WaitAsync();
        try
        {
            actionExecutor.InvalidateCache(key);
            await RefreshAll();
        }
        finally
        {
            _refreshGate.Release();
        }
    }
}