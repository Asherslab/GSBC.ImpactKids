# Blazor WASM client

Feature-first: `Features/<Feature>/{Pages,Components,Services}`, with `.razor` markup and a
`.razor.cs` partial beside it. Markup in the `.razor`, logic in the partial — a page with fifty lines
of `@code` belongs in its partial.

Every gRPC client needs registering in `Program.cs`. A page injecting an unregistered service fails at
runtime, not at build.

# Stores and `AsyncData`

State comes from `IRefreshableStore<T>`, read as `AsyncData<T>` — a status plus a nullable payload,
never a bare value. The pattern in every page:

```csharp
HandleSubscriptionDisposal(PeopleStore, RetrievePeople);   // re-run on store change, auto-unsubscribe
RetrievePeople();                                          // seed from whatever is cached
await Task.WhenAll(PeopleStore.RefreshAll());              // then fetch
```

- Copy status, do not invent it: `_x = _x.CopyStatus(source)` when the source has no data yet, then
  `ToSuccess` / `ToFailure`. Losing the loading state gives an empty page with no spinner.
- Check `HasData` before `Data!`. `Data == null` with no error means still loading, and rendering that
  as "none" is the most common bug on these pages.
- Derived state is computed in a `Retrieve*` method that ends in `StateHasChanged()`, not in the
  markup. A property that filters a store list runs on every render.

**After a write, call `RefreshEvent()` — never `RefreshAll()`.** `RefreshAll` goes through the
action executor's cache with `cacheFor: TimeSpan.FromMinutes(30)`
(`Services/RefreshableStore/RefreshableStore.cs`), so immediately after a mutation it hands back the
response from *before* the mutation and the screen does not settle. `RefreshEvent` invalidates the
key first, then refreshes.

This one hides: the SSE invalidation from the event bus usually lands a moment later and updates the
page anyway, so a `RefreshAll` after a write looks like it works. `RefreshAll` is for arriving on a
page; `RefreshEvent` is for having just changed something.

## `RefreshEvent` coalesces with a counter, and both halves matter

`RefreshEvent` is not a plain "fetch now". Callers bump `_refreshWanted`; **one** loop fetches and
re-checks the counter afterwards. Do not "simplify" it into a single fetch, a plain lock, or a
fetch-if-not-already-running.

It is holding two requirements apart that pull against each other:

**Nothing may be answered by a read that started before it.** The action executor coalesces
concurrent calls for one key, so a refresh asked for now could be satisfied by a request already in
flight — one that read the database *before* the write this refresh exists to pick up. That stale
answer then filled the 30 minute cache, so nothing fetched again.

**And a burst must not multiply.** Every write raises its own event to *every connected client*, so
twenty quick writes must not become twenty-one reads each. Serving refreshes strictly one at a time
fixes the staleness and causes exactly that.

The counter gives both: there is always a fetch that *starts* after the last request, and a burst of
any size costs at most two reads — the one already running, and one more covering everything that
arrived during it.

Measured on a household batch, two writes about a second apart:

| | writes | `BasicReadMultiple` | result |
|---|---|---|---|
| original | 2 | **1** — it straddled the second write | one row stale, DB correct |
| lock only | 2 | 3, one per request | correct, but scales as N+1 |
| counter | 2 | **2**, both after both writes | correct, and bounded |

Two giveaways when this recurs: **fewer reads than writes** means something was answered by a stale
in-flight read; **one read per write** means the coalescing has been lost and a busy night will
hammer every connected client.

`SseClientService.Refresh<T>` puts a **trailing** debounce in front of this, which collapses events
arriving within 300ms. That is a load optimisation, **not** the correctness fix — on its own it made
things worse, by removing the second read that had been accidentally correcting the first. Keep both,
and keep the debounce trailing: a leading one reads before the later writes land.

**A blank-looking page is usually mid-load.** Only conclude data is missing after the store has
resolved — the same trap applies when inspecting it in a browser (see the `run-and-inspect-app` skill).

# Scoped CSS does not reach MudBlazor components

Blazor CSS isolation stamps its `b-<hash>` attribute only on elements in the component's own markup. A
class on `<MudText>`, `<MudButton>`, `<MudStack>` or `<MudPaper>` gets no scope attribute, so the rule
never matches and fails **silently** — it compiles, and the page looks almost right.

Wrap it in a plain element and reach in with `::deep`, or use a plain element in the first place:

```razor
<span class="game-name"><MudButton>@Name</MudButton></span>
```
```css
.game-name ::deep .mud-button-root { white-space: nowrap; }
```

# Razor traps

- A `@* comment *@` **between component attributes** is parsed as an attribute name. It builds and
  throws at render: `does not have a property matching the name '@* … *@'`. Put comments above the tag.
- MudBlazor's analyzer reports `MUD0002 Illegal Attribute` for a lowercase attribute it does not
  recognise. Existing warnings are pre-existing; do not add more.
- MudMenu popovers do not open under synthetic clicks, so menus and the dialogs behind them cannot be
  driven programmatically. Never claim a dialog works because it compiled.

# These screens are used by leaders on phones, and by children on a wall

- Check `preset: "mobile"` as well as desktop for anything a leader taps mid-programme. The tap target
  is often the whole tile, not a button inside it.
- The display pages (`/Display/Scores`, `/Display/Reveal`) are anonymous, take no input, and carry no
  commentary — visuals only, no narration, no leader-facing text. See
  [docs/modules/games/README.md](../docs/modules/games/README.md).
- Anything a child reads on a wall gets *more* design attention in the rare cases, not less: a tie, a
  team on zero, a heat nobody ran. Those are the moments a child is looking for their own name.

# The service worker will serve you a stale app

This is a PWA. After the app restarts with new asset hashes, the old worker keeps serving the old
manifest and the page fails to boot — in a new tab too, and reloading does not release control. A
missing `_framework` asset must return 404, never a 200 HTML shell, or the runtime's integrity check
turns it into an opaque "Load failed".

`skipWaiting()` and `clients.claim()` are in `wwwroot/service-worker.published.js` for this reason. Do
not remove them. The full diagnosis path — including the production version, where the answer is often
"how many tabs are open?" — is in the `run-and-inspect-app` skill.

# Authorization in the UI is advisory

`Policies.EnabledOnly` here checks the claim `permissions=user:enabled`, delivered from the BFF via
`/bff/user`. It hides controls; it does not protect anything. The server re-checks every call, and that
is the check that counts — never move a rule into the client only.
