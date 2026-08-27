---
title: A screen misses an update when two writes land within about a second
kind: open-question
status: open
verified: 2026-08-28
code:
  - GSBC.ImpactKids.WASM/Services/RefreshableStore/RefreshableStore.cs
  - GSBC.ImpactKids.WASM/Features/Eventing/Services/SseClientService.cs
  - GSBC.ImpactKids.WASM/Extensions/StateStoreExtensions.cs
  - GSBC.ImpactKids.Grpc/Features/Eventing/Services/EventingChannelsService.cs
---

# A screen misses an update when two writes land within about a second

## What was observed

Reproduced repeatedly with "Sign out all" over a household of two
(`FamilyBatchActions`), which writes two records about 0.5–1.1s apart:

- both records are **correct in the database throughout** — nothing is lost server side;
- one row on screen shows the new state, the other keeps the old one;
- it **corrects itself roughly ten seconds later with no reload**.

Contrast, both verified in the running app:

| Shape | Result |
| --- | --- |
| one write, then `RefreshEvent()` | settles immediately, correct |
| two writes about a second apart, by hand | settles immediately, correct |
| two writes ~0.5–1.1s apart from one batch | **last row stale, corrects ~10s later** |

So this is not "the event was dropped". It is an update being *collapsed* on the
client.

## Ruled out

- **Server-side event loss.** `EventingChannelsService` writes to a
  `Channel.CreateBounded<SseItem<string>>(32)`, whose default `FullMode` is `Wait`, and
  every write is preceded by `WaitToWriteAsync`. A slow reader blocks the fanout; it does
  not silently drop. Nothing in the fanout discards an event.
- **`RefreshAll`'s 30 minute cache being wrong.** It is deliberate. The bug reproduces with
  `RefreshEvent()`, which invalidates the key first.

## The leading hypothesis: in-flight coalescing filling the cache with pre-write data

`AddEntityStore<T>` registers one
`IAsyncActionExecutor<EntityListState<T>>` (`AsyncActionExecutor`) as a **singleton** per
entity type, and `RefreshAll` reads through its `ExecuteCachedAsync`. Strings in
`EasyAppDev.Blazor.Store.dll` 2.0.11 show that type carries `_inFlightRequests` and
`_inFlightOperations` — i.e. **it coalesces concurrent calls on the same key**.

Every write also raises its own SSE event, so a batch produces overlapping refreshes:

```
write A commits ─► event A ─► RefreshEvent ─► Invalidate ─► RefreshAll ─► request R1 starts
                                                                            │ (reads DB: A only)
write B commits ────────────► event B ─► RefreshEvent ─► Invalidate ─► RefreshAll
                                                                            │
                                                       coalesced onto R1 ───┘
                                          R1 returns pre-B data ─► cached for 30 minutes
```

Invalidating the cache does not help if the *in-flight request that started before the
write* is what satisfies the next caller. The pre-B result then fills the cache, and
nothing refetches until some later event invalidates it again — which is what the delayed
self-correction looks like.

This fits every observation, including why one write and two slow writes are both fine:
there is no overlapping request to coalesce onto.

**It is not proven.** The decisive measurement — counting
`Attendance.Records/BasicReadMultiple` calls during a batch and comparing against the
number of writes — was not captured. Do that first.

## Also worth a look while in there

- **Two caches, one path.** `SseClientService.Refresh<T>` clears `ILazyCache` by key
  (`lazyCache.RemoveAsync($"{typeof(T).Name}-list")`) and then calls `RefreshEvent()`, which
  invalidates the **executor's** cache. `ExecuteCachedAsync` reads the executor's cache, so
  the `ILazyCache` removal looks vestigial. Confirm which cache is authoritative and delete
  the other call, or the next person will assume both matter.
- **`AddStores()` deliberately skips `AddStoreUtilities()`** ("adds scoped cache. don't want
  that") and hand-registers `IDebounceManager`, `IThrottleManager` and `ILazyCache`. That is
  the most likely place for a setup mistake, and nothing on this path appears to *use* the
  debounce manager.
- **`EventingChannelsService.GetChannel` overwrites `_channels[streamId]` unconditionally**,
  so a reconnect on the same id replaces the channel and orphans whatever was queued in the
  old one. Not the cause here, but it is a real way to lose events.

## Fix directions, once the measurement confirms the cause

1. **Trailing debounce on the SSE refresh** — N events within, say, 250ms cause one refresh
   that starts *after* the last write. Simplest, and it makes bursts cheaper rather than
   more fragile. It must be **trailing**; a leading debounce has exactly the same staleness
   bug.
2. **Epoch the cache** — bump a counter on invalidate; a request that completes carrying a
   stale epoch does not fill the cache and refetches.
3. **Do not coalesce across an invalidation** — a refresh requested after an invalidate must
   start a new request rather than joining one that began before it.

(1) and (2) are complementary: the debounce removes most overlap, the epoch makes the
remaining overlap safe. (3) alone re-fetches more than necessary during a burst.
