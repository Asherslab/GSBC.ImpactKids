# Front-end Store Architecture (WASM)

Front-end state in `GSBC.ImpactKids.WASM` runs on [`EasyAppDev.Blazor.Store`](https://www.nuget.org/packages/EasyAppDev.Blazor.Store/) (a Zustand-style store library), wrapped by the project's own `RefreshableStore<T>` (`Services/RefreshableStore/`).

## Data flow

```
gRPC service
  → IBasicReadMultipleService<T> adapter   (e.g. Services/SyncManualReviewReadAdapter.cs)
  → RefreshableStore<T>.RefreshAll()
  → EntityListState<T>.Entities            (AsyncData<ImmutableList<T>>)
  → component
```

Every entity store is registered **singleton, app-wide** in `Extensions/StateStoreExtensions.cs` via `AddEntityStore<T>()`.

## Two component patterns — know which one a page uses

- **Library-native `StoreComponent<T>`:** reads `State => Store.GetState()` **live at render**; the subscription only triggers `StateHasChanged`. Immune to load timing.
- **This project's `RefreshableStore` pattern** (the sync pages, people pages, etc.): copies store data into a **local field** inside a subscription callback, e.g. `_pendingReviews = Store.GetState().Entities`. It does **not** read live from the store at render — it depends on the callback firing.

## Two facts that make the local-mirror pattern fragile

1. **`Store.Subscribe` is change-only.** A new subscriber is *not* replayed the current value (see `IStateObservable<TState>` docs; the selector overload only fires "when the selected value changes"). Subscribing gives you future changes, never the present.
2. **`RefreshAll()` calls `ExecuteCachedAsync`, which on a cache hit skips the state write.** With a 30-min `cacheFor` and key `"{TypeName}-list"`, a cache hit `return`s the cached value early **without any state write** — so it produces no change and notifies nobody. Only the first/expired caller does `UpdateAsync(loading)` + `UpdateAsync(success)`.

## Mandatory pattern (both halves required)

1. **Subscribe:** `HandleSubscriptionDisposal(SomeStore, RefreshX)` (from `StoreEntityUtilityComponent`).
2. **Seed:** *after* `await SomeStore.RefreshAll()`, explicitly call `RefreshX()`, where `RefreshX` does `_field = SomeStore.GetState().Entities`. The subscription only covers later changes.

Relying on the subscription alone is a latent bug: it works only when your page is the first to warm that shared store within the cache window.

> **Worked example — the Manual Review tab bug (2026-07):** `Individual.razor.cs` had the subscription but omitted the explicit `RefreshPendingReviews()` seed. When the `SyncManualReviewEntry` store was already warm (e.g. loaded earlier by `Multiple.razor`), `RefreshAll()` hit the cache → no state change → the change-only subscription never fired → the local `_pendingReviews` field stayed at its `NotAsked` default → the tab silently never appeared. `RefreshOperation()` on the same page did the seed correctly; `Multiple.razor.cs` does it correctly.

## `RefreshEvent()` vs `RefreshAll()`

`RefreshEvent` invalidates the cache then calls `RefreshAll`, forcing a real fetch + state write, so subscribers **do** fire. Used after mutations (approve/deny) and by SSE. Caveat: it early-returns if `Entities.IsNotAsked` — it won't fetch a store no component has loaded yet.

## Real-time / cross-tab updates

`Features/Eventing/Services/SseClientService.cs` opens an SSE stream (`/api/stream`). Server push messages carry the **fully-qualified entity type name** as `data`; the client resolves the `Type`, then `Refresh<T>()` clears `ILazyCache` and calls `RefreshEvent()` for that store. That is how one user's change propagates to other clients. `RefreshAll` starts the SSE connection on first successful load.

## Cache layers

There are two: `ILazyCache` and `AsyncActionExecutor._cachedResults` (the 30-min `cacheFor`). `RefreshAll` currently uses the `AsyncActionExecutor` cache; the older `lazyCache.GetOrLoadAsync` path is commented out in `RefreshableStore.cs`.

## Debugging heuristic

When a store-backed local field is stuck at `NotAsked`/empty despite data existing, suspect a **missing explicit `GetState()` seed after `RefreshAll()`** (cache hit + change-only subscription) *before* suspecting the backend or DB.
