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
page anyway, so a `RefreshAll` after a write looks like it works. It fails where the timing is
tightest — a batch of two writes left one child's row showing the new state and the other showing the
old, with **both already written in the database**. `RefreshAll` is for arriving on a page;
`RefreshEvent` is for having just changed something.

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
