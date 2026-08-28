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
    /// <summary>
    /// Whether this is one of the screens that cannot sign in. Everything under /Display is,
    /// and nothing else is - the same prefix the proxy will only redirect an enrolment to.
    /// </summary>
    private bool IsDisplaySurface =>
        new Uri(navigation.Uri).AbsolutePath
            .StartsWith("/Display", StringComparison.OrdinalIgnoreCase);

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
            //
            // Never on a wall display. A screen has no Auth0 login to send anybody to, and
            // nobody standing at it to complete one - redirecting a TV to a sign in page
            // replaces the wall with a login form until a human notices. Its remedy is to be
            // re-enrolled from the setup link, so the store is left in its failure state and
            // the page says so.
            if (e.StatusCode is StatusCode.Unauthenticated && !IsDisplaySurface)
            {
                navigation.NavigateTo(
                    $"bff/login?returnUrl={Uri.EscapeDataString(navigation.Uri)}",
                    forceLoad: true
                );

                return;
            }

            if (e.StatusCode is StatusCode.Unauthenticated)
            {
                await store.UpdateAsync(s => s with
                    {
                        Entities = s.Entities.ToFailure(RefreshableStoreErrors.NotEnrolled)
                    }
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
    /// Refresh because something changed. Unlike <see cref="RefreshAll" /> this always goes to
    /// the server - but a burst of callers costs at most two reads, not one each.
    /// <para>
    /// <b>Two things have to hold at once, and they pull against each other.</b>
    /// </para>
    /// <para>
    /// <b>Nothing may be answered by a read that started before it.</b> The action executor
    /// coalesces concurrent calls for one key, so a refresh asked for now could be satisfied by
    /// a request already in flight - one that read the database before the write this refresh
    /// exists to pick up. That stale answer then filled the thirty minute cache, so nothing
    /// fetched again. Measured on a household sign-out: two writes, <b>one</b> read, and that
    /// read straddled the second write; one row correct on screen and one stale, with both
    /// correct in the database.
    /// </para>
    /// <para>
    /// <b>And a burst must not multiply.</b> Every write raises its own event to every
    /// connected client, so twenty quick writes must not become twenty one reads each. Serving
    /// them one at a time fixes the staleness and causes exactly that.
    /// </para>
    /// <para>
    /// The counter satisfies both. Callers bump <see cref="_refreshWanted" /> and return; one
    /// loop fetches, and re-checks afterwards whether anything was asked for <em>while</em> it
    /// was fetching. So there is always a fetch that starts after the last request - never a
    /// stale answer - and a burst of any size collapses into at most two: the one already
    /// running, and one more covering everything that arrived during it.
    /// </para>
    /// <para>
    /// <b>Invalidate inside the loop, never before it</b> - an invalidation that happens while
    /// an earlier request is still running is undone when that request completes and re-fills
    /// the cache.
    /// </para>
    /// </summary>
    /// <summary>Bumped by every caller asking for a refresh.</summary>
    private long _refreshWanted;

    /// <summary>The highest <see cref="_refreshWanted" /> a completed fetch has covered.</summary>
    private long _refreshServed;

    private bool _refreshRunning;

    public async Task RefreshEvent()
    {
        if (store.GetState().Entities.IsNotAsked)
            return;

        string key = $"{typeof(T).Name}-list";

        _refreshWanted++;

        // Somebody is already looping. The bump above is enough - they re-check the counter
        // after every fetch, so they will do another one on our behalf. This is what stops
        // twenty writes costing twenty one reads on every connected client.
        if (_refreshRunning)
            return;

        _refreshRunning = true;

        try
        {
            // Re-check rather than fetch once: a request that arrived while the previous
            // fetch was in flight has not been covered by it, so it needs another.
            while (_refreshServed < _refreshWanted)
            {
                long target = _refreshWanted;

                actionExecutor.InvalidateCache(key);
                await RefreshAll();

                // Only what had been asked for when this fetch STARTED. Anything asked for
                // during it has not been read yet and must go round again.
                _refreshServed = target;
            }
        }
        finally
        {
            _refreshRunning = false;
        }
    }
}