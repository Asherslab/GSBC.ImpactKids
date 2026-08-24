---
name: run-and-inspect-app
description: Run the Aspire app locally and inspect/iterate on the Blazor WASM frontend in a browser. Use when asked to start, stop, restart, or preview the app; to check how a page looks or behaves; to verify a UI change; or when writing scoped CSS against MudBlazor components. ALSO use before planning, proposing or designing ANY change to a page, screen or component — look at the real UI first, never from the code alone. Covers the Rider run config, the correct localhost port, restart and service-worker gotchas, Auth0 sign-in limits, and how to read the DB and the live scoreboard stream.
---

# Running and inspecting this app

## Look at the UI before you plan a change to it

**Non-negotiable, and it comes before any proposal, mockup or opinion about a screen.**
Run the app and look at the page. Reading the `.razor` is not looking - a plan drawn from
the markup invents affordances the page does not have and misses the ones it does. This
has already produced a whole design proposal for a list widget on a page that is a grid of
full-bleed tiles where the tile *is* the button.

- **Changing an existing page**: open that page, at `preset: "mobile"` as well as desktop
  if leaders use it on a phone. Note what the primary tap target already is, what lives in
  the header, what the inline/secondary actions are, and how tight the page is for space.
  A new control has to fit that, not sit beside it.
- **Adding a new page**: open the two or three existing pages that do the nearest thing and
  copy their idiom - the same dialog shape, the same chip presets, the same
  `−  n  +` steppers, the same header layout. New styling is a last resort, not a start.
- **Prefer the existing pattern to a better one.** Where a page already solves a problem
  (presets to avoid typing, an inline action that stops propagation, a collapsed chip for
  secondary detail), reuse it verbatim. Consistency beats a locally nicer control.
- Say what you saw in the proposal. If the app could not be started or the page needs a
  login you cannot do, state that the design is drawn from markup alone rather than
  quietly presenting it as observed.

Aspire AppHost fronts a Blazor WASM client, a YARP BFF, a gRPC service, Postgres,
RabbitMQ and Redis. Everything below was established by doing it — the gotchas are
real failures that cost time, not speculation.

## Tooling you need

Three MCP servers do the work here. Their schemas are **deferred** — the names appear in a
system-reminder but calling one straight off fails with `InputValidationError`. Load them
first:

```
ToolSearch  query: "select:mcp__rider__execute_run_configuration,mcp__rider__get_run_configurations"
```

- **Rider MCP** (`mcp__rider__*`) — runs the app. `get_run_configurations` lists what exists
  (pass `projectPath: /Users/asherp/Documents/Git/GSBC.ImpactKids`) if the name below has
  drifted. There is **no stop tool** — see the restart section, stopping is done with `pkill`.
- **Claude Browser** (`mcp__Claude_Browser__*`) — already loaded, no ToolSearch needed.
  `preview_start`, `navigate`, `computer`, `read_page`, `javascript_tool`,
  `read_console_messages`, `read_network_requests`, `resize_window`.
- **MudBlazor MCP** (`mcp__mudblazor__*`) — component API reference, for checking a
  parameter exists before guessing at it.

**Never `dotnet run` — run configurations only.** Building goes through `mcp__rider__build_solution`
(`{projectPath: /Users/asherp/Documents/Git/GSBC.ImpactKids}` - returns
`{isSuccess, problems}`, and works fine while the app is running; `rebuild: true` for a
clean one). Per-file checks after an edit go through `mcp__rider__get_file_problems`, which
runs Rider's inspections and so catches more than the compiler. Running goes through
`execute_run_configuration`. Both build tools are deferred - ToolSearch them first.

This holds for one-off side experiments too: spinning up a single project on a spare port
to test a config gate is still `dotnet run`, and still not allowed — drive it through a run
configuration, or ask the user.

`dotnet ef` is fine to run directly — it is the CLI exception, since there is no MCP
equivalent for migrations.

If the Rider MCP is unavailable for *running*, say so and ask the user to start it from
Rider rather than falling back to `dotnet run`.

## Start it

Use the **Rider run configuration**, not `dotnet run`:

```
mcp__rider__execute_run_configuration
  configurationName: "GSBC.ImpactKids.AppHost: https"
  projectPath: "/Users/asherp/Documents/Git/GSBC.ImpactKids"
  waitForExit: false
```

`waitForExit: false` is required — the app runs until stopped, so waiting blocks forever.
It returns immediately with a `fullOutputPath` log file you can `Read` if startup fails.

Rider owns the process, so it survives an agent session teardown. A backgrounded
`dotnet run --project GSBC.ImpactKids.AppHost` dies with the session.

Docker must be running. The infra containers (`sql-*`, `rabbitmq-*`, `redis-*`) are
`ContainerLifetime.Persistent`, so they usually stay up between runs and startup is fast.

## The URL is https://localhost:7263

That is the Aspire DCP **proxy** port, from `GSBC.ImpactKids.YARP/Properties/launchSettings.json`.

Do not discover the port with `lsof`/`ps`. YARP's *internal* bind is a random high port
(e.g. 57514). Hitting that directly appears to work — the app loads — but the WASM client's
service discovery still points gRPC at `https://localhost:7263`, so every call is
cross-origin and dies on CORS preflight. The symptom is a page stuck on "Connecting…"
with `No 'Access-Control-Allow-Origin' header` in the console.

The DCP proxy is not a `dotnet` process, which is why process-based port hunting misses it.

## Restarting after a code change

WASM changes need a **full restart**. Blazor's devserver serves a snapshot from
`GSBC.ImpactKids.WASM/bin/Debug/net10.0/wwwroot/_framework/`; a rebuild alone leaves it
serving stale hashed assets and the browser 404s on a `.wasm`/`.pdb`, then shows
"An unhandled error has occurred."

Calling `execute_run_configuration` again while it is running is a **no-op** — it returns
instantly and nothing restarts. Stop everything first:

```bash
pkill -f "GSBC.ImpactKids.AppHost"; sleep 2
pkill -f "blazor-devserver"; pkill -f "GSBC.ImpactKids.YARP"; pkill -f "GSBC.ImpactKids.Grpc"
```

Killing AppHost alone orphans the other three. Then re-run the config and wait on a real
condition (never chain `sleep`s — the harness blocks that):

```bash
until curl -sk -f -o /dev/null -m 3 https://localhost:7263/_framework/dotnet.js \
   && curl -sk -f -o /dev/null -m 3 https://localhost:7263/; do sleep 2; done
```

Assets can 404 briefly while the devserver warms up — that race is transient, not a bug.

## Browser: restart it after an app restart

The app registers a **Blazor PWA service worker**. After the app restarts with new asset
hashes, the SW keeps serving the old manifest and the page fails to boot — even in a brand
new tab, and even though `curl` and a normal browser fetch both return 200 for the asset it
claims is missing. Unregistering the SW from JS is not enough.

Restart the browser pane:

```
mcp__Claude_Browser__preview_list          # get the serverId (browser-preview-…)
mcp__Claude_Browser__preview_stop          # serverId, NOT the previewId
mcp__Claude_Browser__preview_start         # url: https://localhost:7263/…
```

Diagnostic that identifies this: `navigator.serviceWorker.controller` is non-null while a
`_framework` asset 404s in the console but fetches 200.

## Production: stale service worker after a deploy

Symptom: returning visitors get a dead page, fresh browsers are fine. Console shows
`FetchEvent.respondWith received an error: TypeError: Load failed` on `_framework/*.wasm`.

That error means the **old** worker is still in control — the current worker returns a 404
`Response` for a missing asset rather than a rejected promise, so it cannot produce it.
Diagnose from the outside before touching code:

```bash
# Is the fixed worker actually deployed? Compare against wwwroot/service-worker.published.js
curl -s https://kids.baptist.com.au/service-worker.js | grep -c clients.claim

# Do the failing hashes belong to the current build, or a previous one?
curl -s https://kids.baptist.com.au/service-worker-assets.js | grep -c <hash>

# Is Cloudflare involved? DYNAMIC means it is passing through, not caching.
curl -sI https://kids.baptist.com.au/_framework/<asset>.wasm | grep -i cf-cache-status
```

A missing `_framework` asset must return **404**, never 200 `text/html`. If it returns the
HTML shell, nginx's SPA `try_files` fallback is catching it and the runtime's integrity
check turns that into the opaque "Load failed".

**Ask how many tabs are open before theorising.** A new worker enters the *waiting* state
while any client of the old worker still exists, and **reloading a tab does not release
control** - the client persists across the navigation. With two tabs open, refreshing
either one forever never escapes; only closing all of them does. This looks exactly like a
route-specific bug, because whichever tab you happen to keep open is the one that stays
broken. `skipWaiting()` + `clients.claim()` are what remove the trap, and both are now in
`service-worker.published.js`.

Console output survives a failed boot, so errors on screen may be scrollback from an
earlier load. Clear the console, reload, and re-read before drawing conclusions - state
queried after the fact can show a healthy worker while the visible errors are from before
it activated.

Do not conclude the app is broken from one browser. Load it in a clean profile — an origin
with no prior worker boots off the current manifest and will work while a stuck tab does not.

## Auth

Auth0 with **Google SSO only**. Never drive that sign-in — ask the user to complete it in
the browser pane; the BFF cookie then persists for the session and authed pages work.

### Dev bypass — sign yourself in without Auth0

Development only. Navigate to it and you get a real session, cookie and bearer token both:

```
https://localhost:7263/bff/dev-login?returnUrl=/Attendance/Tool
```

`/bff/dev-logout` drops it (the real `/bff/logout` goes out to Auth0 to end a session that
was never started there, and errors). Verify with `curl -sk -b jar .../bff/user` — a
working bypass answers `isAuthenticated: true` with `permissions: user:enabled`.

It signs in as the sub the gRPC claims transformation seeds as enabled. Any other
`DevAuth:Subject` lands as a new *disabled* user, and every call comes back 403.

Three things must line up or the routes 404: `Development`, `DevAuth:Enabled`, and a
signing key ≥32 chars. The AppHost sets the flag and generates the key per run
(`AppHost.cs`, run mode only — never in a published manifest), so tokens die with the
process and no key is ever committed. The gRPC service accepts that key *alongside* Auth0,
never instead of it.

**Two layers, not one.** A cookie alone is not enough — proxied gRPC routes carry a bearer
token that the gRPC service validates against Auth0. Anything that fakes only the cookie
gets an authenticated SPA whose every call 401s.

- `/Display/Scores` — `[AllowAnonymous]`, the wall display. Needs no login, so iterate here freely.
- `/Games/Points`, `/Games/Scores` — `[Authorize]`, need the user to sign in first.

A 401 on `/bff/user` from an anonymous page is expected, not a fault.

## Inspecting the UI

Prefer the DOM over screenshots for anything measurable. The pane's screenshot can capture
mid-layout or render a smaller region than the viewport it reports — twice this looked like
a layout bug that `getBoundingClientRect` disproved. Screenshots are for judging *design*;
JS is for judging *facts*.

Viewport: pass `width` + `height` alone. Passing `preset` together with `width`/`height`
silently resets to native size instead.

```
mcp__Claude_Browser__resize_window  width: 1920  height: 1080   # desktop
mcp__Claude_Browser__resize_window  preset: "mobile"            # 375x812
```

The nav drawer stays open when going desktop → mobile and overlays the page; it is not a
responsive bug.

`read_page` is the fastest way to check accessibility — it lists every `aria-label`.

**A blank-looking page is usually mid-load, not empty data.** The WASM app boots, then
every store fetches over gRPC; read the page in that window and you get real chrome with
empty slots — no service, no rows. This reads exactly like an empty database and has
already produced a confident, wrong "the DB is empty" (it had 1731 people). Re-read after a
beat, or check `read_network_requests` for the `BasicReadMultiple` calls returning 200,
before concluding anything about the data.

**MudMenu popovers do not open under synthetic clicks** (neither `computer left_click` nor
`.click()`), so menu items and the dialogs behind them are hard to reach programmatically.
Ask the user to open those, or verify the markup another way — do not claim a dialog works
because it compiled.

## Scoped CSS does not reach MudBlazor components

Blazor CSS isolation only stamps the `b-<hash>` attribute on elements in the component's
**own markup**. A class on `<MudText>`, `<MudButton>`, `<MudStack>`, `<MudPaper>` gets no
scope attribute, so the rule never matches and fails **silently** — it compiles, and the
page looks almost right.

Confirm with:

```js
[...document.querySelector('.my-class').attributes].map(a => a.name)
// ["class", "b-wasd9xu8ju"]  -> scoped, will style
// ["class"]                  -> NOT scoped, rule is dead
```

Two fixes:

```razor
@* 1. Use a plain element instead of the Mud component *@
<span class="behaviour-hint">counts toward the total</span>

@* 2. Wrap it and reach in with ::deep *@
<span class="game-name"><MudButton>@Name</MudButton></span>
```
```css
.game-name ::deep .mud-button-root { white-space: nowrap; }
```

`::deep` works because the *wrapper* is your own element and carries the attribute.

Also: a `@* razor comment *@` between component attributes is parsed as an attribute name.
It builds fine and throws at render — `does not have a property matching the name '@* … *@'`.
Put comments above the tag.

## Database

Aspire generates the Postgres password into the container env:

```bash
c=$(docker ps --format '{{.Names}}' | grep '^sql-')
docker exec -e PGPASSWORD="$(docker exec $c printenv POSTGRES_PASSWORD)" $c \
  psql -U postgres -d impact-kids -c '\d "GameBoards"'
```

Useful for confirming a migration actually applied and backfilled, rather than trusting
that `dotnet ef` generated the right thing.

## Verifying the live scoreboard

`/Display/Scores` uses a **gRPC server-streaming** call (`WatchScoreboard`), not polling.
A working stream is a single `POST …/Games.Display/WatchScoreboard` held open at 200 —
check `read_network_requests`. Several requests means it is falling back or reconnecting.

End-to-end push test, no auth needed on the display side:

1. Open the tracker in one tab, the display in another.
2. Score in the tracker: `document.querySelectorAll('.team-tile')[0].click()`.
3. Read the display tab — it should change with no reload.
4. Undo to clean up: click the button with `aria-label="Undo last"` once per award.

Bars animate via a 0.5s CSS transition, so `getComputedStyle(bar).width` read immediately
after an update returns a mid-transition value. Wait before measuring or it looks like a bug.
