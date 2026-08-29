---
name: run-and-inspect-app
description: Run the Aspire app locally and inspect/iterate on the Blazor WASM frontend in a browser. Use when asked to start, stop, restart, or preview the app; to check how a page looks or behaves; to verify a UI change; or when writing scoped CSS against MudBlazor components. ALSO use before planning, proposing or designing ANY change to a page, screen or component — look at the real UI first, never from the code alone. Covers the Rider run config, the correct localhost port, driving the page from a nodeterm browser node (and the narrow cases that still need Chrome), restart and service-worker gotchas, Auth0 sign-in limits, and how to read the DB and the live scoreboard stream.
---

# Running and inspecting this app

## Look at the UI before you plan a change to it

**Non-negotiable, and it comes before any proposal, mockup or opinion about a screen.**
Run the app and look at the page. Reading the `.razor` is not looking - a plan drawn from
the markup invents affordances the page does not have and misses the ones it does. This
has already produced a whole design proposal for a list widget on a page that is a grid of
full-bleed tiles where the tile *is* the button.

- **Changing an existing page**: open that page in a nodeterm browser node, and if leaders use
  it on a phone, at mobile width too — see the mobile node procedure under Inspecting the UI.
  Note what the primary tap target already is, what lives in
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

**The browser rule, in one line: drive the app from a nodeterm browser node — desktop and
mobile both — and reach for Chrome only for console, network or JavaScript, asking first with
`AskUserQuestion`, because Chrome is usually closed.**

Four MCP servers plus the nodeterm canvas CLI do the work here. The MCP schemas are **deferred**
— the names appear in a system-reminder but calling one straight off fails with
`InputValidationError`. Load them first:

```
ToolSearch  query: "select:mcp__rider__execute_run_configuration,mcp__rider__get_run_configurations"
```

- **Rider MCP** (`mcp__rider__*`) — runs the app. `get_run_configurations` lists what exists
  (pass `projectPath: /Users/asherp/Documents/Git/GSBC.ImpactKids`) if the name below has
  drifted. There is **no stop tool** — see the restart section, stopping is done with `pkill`.
- **Aspire dashboard MCP** (`mcp__gsbc-impactkids-aspire__*`) — **the first thing to reach for when the
  app does not come up.** `list_resources` gives every resource's state and its health report,
  `list_console_logs` its output, plus `list_structured_logs`, `list_traces`,
  `list_trace_structured_logs` and `execute_resource_command`. Configured in `.mcp.json` (gitignored:
  its key is `AppHost:McpApiKey` from your own user secrets), served at `http://localhost:16036/mcp`
  while the AppHost runs.

  This exists because the alternative wasted an hour: `ps`, `lsof` and an almost-empty Rider log led to
  "nothing logged about why", when `list_resources` had the answer all along — a resource `Running but
  not in a healthy state` with the exception text in its health report. **Do not diagnose a
  non-starting stack by poking at processes.** Ask the dashboard.

  **It only connects if the AppHost was already running when the Claude Code session started.**
  This is the usual reason the tools are missing, and it is not a config fault — an `http` MCP server
  is dialled once at session startup, so a stack you start *during* the session cannot be attached to
  and no amount of fixing `.mcp.json` helps. Start the app first, then start the session. If you are
  already mid-session without it, say so and work from `psql` and the browser instead — do not
  conclude the config is stale.

  Confirm reachability before blaming anything, and note the scheme — the endpoint is plain
  **`http`**, so probing `https://localhost:16036` returns `000` (connection refused) and looks
  exactly like a dead dashboard:

  ```bash
  curl -s -o /dev/null -w '%{http_code}\n' -m 3 http://localhost:16036/mcp                 # 404 = up, needs key
  curl -s -o /dev/null -w '%{http_code}\n' -m 3 -X POST http://localhost:16036/mcp \
    -H "x-mcp-api-key: $(dotnet user-secrets list --project GSBC.ImpactKids.AppHost \
        | sed -n 's/^AppHost:McpApiKey = //p')" \
    -H 'Accept: application/json, text/event-stream' -H 'Content-Type: application/json' \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}'
  # 200 = endpoint and key are both good
  ```

  **Both values have an authoritative source — read them, never guess or ask for them:**

  - **Key** — `dotnet user-secrets list --project GSBC.ImpactKids.AppHost`, entry `AppHost:McpApiKey`.
    (`secrets.json` is written with a UTF-8 BOM, so `json.load` on it throws
    `Unexpected UTF-8 BOM`; use `encoding='utf-8-sig'` or just use the CLI.)
  - **Port** — `ASPIRE_DASHBOARD_MCP_ENDPOINT_URL` in
    `GSBC.ImpactKids.AppHost/Properties/launchSettings.json`, set on *both* the `http` and `https`
    profiles. It is pinned there rather than allocated per run, so it does not normally move. Read it
    from the running process instead if you want ground truth: `ps eww <apphost-pid> | tr ' ' '\n' |
    grep ASPIRE_DASHBOARD_MCP`. Confirm something is actually bound with
    `lsof -nP -iTCP:16036 -sTCP:LISTEN` — that shows `dcpctrl`, not a `dotnet` process.

  Either value *can* change — a Production-profile run regenerates the key, and editing
  `launchSettings.json` moves the port — so if `/mcp` answers 404 to a keyed request, or the port is
  not listening, re-read both from the sources above and write them into `.mcp.json`:

  ```json
  { "mcpServers": { "gsbc-impactkids-aspire": {
      "type": "http", "url": "http://localhost:<port>/mcp",
      "headers": { "x-mcp-api-key": "<AppHost:McpApiKey>" } } } }
  ```

  A **404 to a keyed request** means a stale key; **401** means the key was rejected; **000** means
  nothing is listening (or you used `https`). `.mcp.json` is gitignored — the key stays out of git,
  so never paste it into a doc, a commit or a summary. The edit only takes effect on the **next**
  session, per the startup rule above.
- **The nodeterm browser node** (`nodeterm.sh open-browser` + `nodeterm.sh browser`) — **the
  default way to look at and drive this app.** It is a browser that lives on the canvas, owned by
  this session, so it does not depend on the user's Chrome being open — which removes the single
  most expensive failure mode this skill used to have. Everything below was verified against this
  app on nodeterm v0.3.3.

  **Reuse the nodes that are already open; only open a new one when you have to.** Nodes cost
  the user canvas space and a fresh one starts logged out, so start every browser task with
  `list` and a one-call drivability probe, and open a node only if nothing usable comes back:

  ```sh
  S="/Users/asherp/Library/Application Support/node-terminal/canvas-control/nodeterm.sh"
  sh "$S" list | grep '\[browser\]'
  for n in <ids from that list>; do
    printf '%s -> ' "$n"; sh "$S" browser --node "$n" --read title 2>&1 | head -1
  done
  ```

  A node that answers with a URL and title is yours to reuse — `--nav` it to the page you need
  rather than opening another. A node that answers `no drivable browser node "<id>"` is unusable
  *to you* and cannot be recovered from the CLI. **Why a live node goes dead is not known**: it
  has happened mid-session to nodes this session opened, with no user-facing control for handing
  a node back and nothing in the reply to explain it. So do not theorise and do not try to
  revive it — reuse another live node or open a fresh one, and mention the dead node to the user
  rather than silently piling up replacements.

  Reuse also means **leaving the nodes open at the end of a task**. The desktop node and the
  user-resized mobile node are session infrastructure; closing them throws away a login and a
  manual resize.

  ```sh
  sh "$S" open-browser --url "https://localhost:7263/bff/dev-login?returnUrl=/Games/Points"
  # -> opened browser browser-xxxxxxxx-xxxxxxxx   (keep that id; every action needs --node)
  sh "$S" browser --node <id> --read map                 # interactive elements + aria-labels
  sh "$S" browser --node <id> --read text [--full true] [--selector <css>] [--max <n>]
  sh "$S" browser --node <id> --click @12                # or a css selector
  sh "$S" browser --node <id> --type "text" --into @7 [--clear true]
  sh "$S" browser --node <id> --press Enter [--times n]
  sh "$S" browser --node <id> --scroll up|down|top|bottom|<px>
  sh "$S" browser --node <id> --wait @12 [--timeout 15000]
  sh "$S" browser --node <id> --screenshot .claude/shot.png    # path jailed to the project dir
  ```

  It handles the two things that used to cost the most setup: **the self-signed localhost
  certificate just loads**, and **`/bff/dev-login` establishes a real session in the node** that
  persists across every later action.

  **`--click @ref` genuinely drives Blazor here** — input goes through CDP
  (`Input.dispatchMouseEvent`), not a synthetic `.click()`. This is the opposite of the
  Chrome extension, where a ref click reports success and does nothing on a MudButton. Clicking
  a `@ref` from `--read map` is the normal way to interact; there is no coordinate click and you
  do not need one. Still verify the *effect* (re-read the page, or check Postgres) rather than
  the tool's acknowledgement — one click produces exactly one effect, confirmed, but a click can
  be refused.

  Known quirks, all reproduced on an untouched node — do not spend time re-diagnosing them:

  - **The scroll readout is stale by one action.** The delta always prints `0px` even when the
    page moved, and the position printed is the position *before* the action: `--scroll bottom`
    from the top says `(at 0/574)`, and the next call says `(at 84/574)` without moving. Do not
    trust the number; re-read the page to see where you are.
  - **"is off-screen — scroll it into view first" can be spurious**, off the same stale state.
    Scroll, then retry the click once — it usually lands. It is a refusal, not a silent failure:
    a refused click has no effect on the page.
  - **`--scroll +200` is rejected** even though the error message offers a signed count. Use
    `200` or `-200`.
  - **`--screenshot --full true` stitches badly** on this app's sticky header — duplicated header,
    black bands, overlapping content. Use the viewport screenshot (omit `--full`) and scroll.
  - **You cannot set the viewport, but the user can.** There is no resize verb and
    `--width`/`--height`/`--preset` on `open-browser` are silently accepted and ignored — a node
    always opens at ~787×490. The viewport does, however, follow the node's frame on the canvas,
    so a node the user drags narrow renders at that width. That is the mobile procedure under
    Inspecting the UI.
  - `browser` is gated by the project's browser-control switch (Settings → Agents). If it answers
    that control is off, that is terminal — ask the user to turn it on, do not retry.
  - You can only drive a node **this run** opened, and only while the user has not taken it back.
    `no drivable browser node "<id>"` is not a fault to debug — probe, then reuse a live node or
    open one. Seen for real: of four nodes open on the canvas, two answered normally and two were
    dead, with nothing in the reply to say why.

- **Claude in Chrome** (`mcp__claude-in-chrome__*`) — **the fallback, for the three things the
  nodeterm node cannot do.** Nothing else. Do not reach for it to look at a page, click something,
  read text, or check a mobile layout — the nodeterm node does all of that without needing the
  user to open a browser.

  The three: **console messages** (`read_console_messages`), **network requests**
  (`read_network_requests`), and **reading page state with JavaScript** (`javascript_tool`).

  **Chrome is usually closed. Ask before you reach for it, do not discover it.** Use
  `AskUserQuestion` — one question, saying which of the three you need and why — and wait for the
  answer instead of firing a tool call that will come back "Browser extension is not connected".
  If you do hit that error anyway, it means Chrome is closed: ask, then wait. It is not a broken
  extension, not a login problem, and not a cue to go and do the job another way. Do not silently
  fall back to curl, and do not start editing code to print diagnostics the page would have shown
  you.

  This has already gone wrong repeatedly in one session: the fallback turns a thirty second look
  at a page into a chain of `pkill`, rebuild, restart and hand-encoded protobuf frames, several
  minutes each. The user's words, after the second time: *"if you can't connect it's because I
  closed chrome. just ask me to reopen it."*

  Once it is open: `tabs_context_mcp` first, then `tabs_create_mcp`/`navigate`, then the read you
  came for. Drive the app from the nodeterm node even then — Chrome is for reading.

  **The narrow exception to using a browser at all** is a fact no browser can show: gRPC trailers
  (`grpc-status`/`grpc-message`), or a cookie-only session that has to stay separate from the
  browser's. Reach for `curl` for those and say why — never as a substitute for looking.
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

**Check the DLL is actually newer than your edit before generating a migration.**
`build_solution` can return `{isSuccess: true, problems: []}` without recompiling the project
you just changed, and `dotnet ef migrations add` reads the built assembly, not your source. The
result is a migration file with an empty `Up()` — which looks like "EF found no model change"
and invites you to go hunting for a modelling mistake that isn't there. This has already
happened twice, once on a `HasData` seed change and once on an index change.

```bash
stat -f '%Sm  %N' -t '%H:%M:%S' GSBC.ImpactKids.Grpc/bin/Debug/net10.0/GSBC.ImpactKids.Grpc.dll
```

If that timestamp predates your edit, rebuild with `mcp__rider__build_solution {rebuild: true}`
and check again. An empty migration is the symptom; a stale assembly is the cause.

Two related traps in the same area:

- `dotnet ef migrations add --no-build` is safe *only* after you have verified the timestamp.
  Without `--no-build` it builds itself, which is slower but cannot go stale.
- `dotnet ef migrations remove` needs a working database connection, and
  `GsbcDbContextFactory` hardcodes port 60536 while a persistent container keeps whatever port
  it was first created with. When it fails with `28P01: password authentication failed` or a
  refused connection, delete the migration's two `.cs` files and
  `git checkout -- .../GsbcDbContextModelSnapshot.cs` instead.

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

### When a `_framework` asset 404s and stays 404 — the clean rebuild

**This is the recovery. Reach for it early; do not grind on the browser.**

The symptom is a boot stuck at `100%` with

```
download '…/GSBC.ImpactKids.<Project>.<hash>.pdb' … failed 404
```

and the giveaway is that **`curl` and an in-page `fetch()` both return 200 for that exact
URL** while the runtime still cannot load it. At that point it is not the service worker and
not the browser cache — both can be ruled out in one call (`navigator.serviceWorker.controller`
false, `fetch(url, {cache:'reload'})` 200) — and no amount of unregistering, hard-reloading or
opening fresh tabs will fix it. One known cause: the devserver snapshots
`bin/Debug/net10.0/wwwroot/_framework/` on startup, and if the AppHost's own build is still
writing assets at that moment it serves a snapshot that never existed. Compare
`ps -o lstart=` on `blazor-devserver` against the asset's `stat` mtime — a devserver older
than the assets is the tell.

Kill the app, wipe every `bin`/`obj`, restore, build, then start the run configuration again:

```bash
# 1. kill the app - AppHost FIRST, or DCP restarts the children you just killed.
#    Match on the built binary path, never `pkill -f GSBC.ImpactKids`: agent sessions carry
#    these project names in their command lines and would be killed too.
ah=$(ps ax -o pid=,command= | awk '$2 ~ /GSBC\.ImpactKids\.AppHost\/bin\/Debug/ {print $1}')
kill -9 $ah
until ! ps ax -o pid=,command= | awk '$2 ~ /GSBC\.ImpactKids.*bin\/Debug/ {f=1} END{exit !f}'; do sleep 2; done

# 2. wipe every bin/ and obj/
./clean.sh

# 3. restore and build the whole solution
dotnet restore
```

Then build through Rider (`mcp__rider__build_solution`) and start
`GSBC.ImpactKids.AppHost: https` through `execute_run_configuration` as usual.

Budget two attempts at the browser-side explanations (service worker, cache) before doing
this. Once `fetch()` says 200 and the runtime still says 404, more browser tricks are wasted
turns — this session burned about a dozen of them proving that.

## Browser: restart it after an app restart

The app registers a **Blazor PWA service worker**. After the app restarts with new asset
hashes, the SW keeps serving the old manifest and the page fails to boot — even in a brand
new tab, and even though `curl` and a normal browser fetch both return 200 for the asset it
claims is missing. Unregistering the SW from JS is not enough.

Open a **fresh nodeterm browser node** — do not reuse the one that was pointed at the old
build:

```sh
sh "$S" open-browser --url "https://localhost:7263/bff/dev-login?returnUrl=/Games/Points"
```

A node you opened before the restart may also answer `no drivable browser node "<id>"`, which
means the same thing: open a new one.

If the stale worker survives that, ask the user to close every Chrome tab on the origin as well
— a new worker stays in *waiting* while any client of the old one exists, and reloading does
not release it.

The diagnostic that identifies this (`navigator.serviceWorker.controller` non-null while a
`_framework` asset 404s in the console but fetches 200) needs the console and JS, so it needs
Chrome. Before asking for it, weigh the clean rebuild above: it fixes this without a browser,
and two attempts at browser-side explanations is the documented budget.

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
the browser; the BFF cookie then persists for the session and authed pages work.

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

## Always drive the app as a user

**Never drive the UI with JavaScript. Never.** Clicks go through `browser --click @ref`, text
through `browser --type "..." --into @ref`, keys through `browser --press`. On the Chrome
fallback (which you should only be in for a *read*), the equivalents are `computer left_click`,
`computer type` and `computer key`; `javascript_tool` is for reading state and `form_input` is
not a substitute for typing. If a control cannot be reached that way, say so and ask - do not
reach for `.click()` as a fallback.

This is not a style preference. Blazor binds on the events a real interaction raises, so a
JS-set value or a synthetic `.click()` updates the DOM and changes nothing underneath:

- `form_input` on a MudBlazor text field sets `input.value`, the screen shows the new text,
  and the component never sees it. Save and the old value is still in the database.
- A field can be `readOnly` until an edit mode is entered. Writing to it with JS "succeeds"
  and is silently discarded.
- `.click()` on a Mud button often does nothing at all, and MudMenu popovers never open.

Every one of those has already produced a confident, wrong conclusion here - a "saved" edit
that was never saved, and an interceptor declared broken when it had simply never been given a
real edit to observe. A test driven by JavaScript proves nothing about the app.

The nodeterm node dispatches through CDP (`Input.dispatchMouseEvent`), so its clicks are real
events and Blazor does see them — verified on this app. That is why it is the default driver.

So: `--read map` to find the control, `--click` it, `--type` into it, click save, then verify
the result in the database or by re-reading the page. Slower, and the only way the answer means
anything.

## Inspecting the UI

`browser --read map` is the fastest way to check accessibility — it lists every interactive
element with its `aria-label`, and gives you the `@ref`s you click with.

**Screenshots are for judging design, not facts.** A capture can catch the page mid-layout, and
this app's sticky header makes `--screenshot --full true` stitch into something that looks like
a broken layout when the page is fine. Use the viewport screenshot and scroll. Anything
*measurable* — a rect, a computed style, an attribute — needs JS, which means Chrome: ask for it
with `AskUserQuestion`, and say plainly that a layout claim is drawn from a screenshot if the
user declines.

### Mobile width: a second node the user resizes once

You cannot resize a browser node, but **its viewport follows its frame on the canvas**, so a node
the user drags narrow renders at phone width for the rest of the session. Confirmed: a node
dragged in came back at 371×519 with the page genuinely reflowed — header wrapped to two lines,
the toolbar stacked onto its own row — not merely cropped.

So when a change needs a mobile check, **check for a mobile node first** — one the user already
resized is the whole point, and its title should say so. Probe it as above, and only if there
isn't a live one, **open a second node and ask once**:

```sh
out=$(sh "$S" open-browser --url "https://localhost:7263/bff/dev-login?returnUrl=<page>")
id=$(echo "$out" | awk '{print $3}')
sh "$S" rename --node "$id" --title "MOBILE — drag me to ~375px wide"
```

Then ask the user to drag it narrow and say when it is done — the rename is what tells them which
node to grab, so do not skip it. Screenshot afterwards and **check the reported dimensions**: if
it still says 787×490 the drag has not happened yet, so ask again rather than reporting a mobile
layout you never saw.

Keep that node for the whole session and reuse it — it is a one-time setup cost, and every later
mobile check is free. Keep the desktop node open alongside it; comparing the same page at both
widths is the point. Each node has its own cookie jar, so the mobile node needs its own
`/bff/dev-login` (the snippet above does that by opening straight at it).

If the user does not want to resize anything, say the design was judged at desktop width only —
do not quietly skip the check. Chrome's `resize_window preset: "mobile"` remains a fallback, but
it needs Chrome open, which this whole approach exists to avoid.

The nav drawer stays open when going desktop → mobile and overlays the page; it is not a
responsive bug.

**A blank-looking page is usually mid-load, not empty data.** The WASM app boots, then
every store fetches over gRPC; read the page in that window and you get real chrome with
empty slots — no service, no rows. This reads exactly like an empty database and has
already produced a confident, wrong "the DB is empty" (it had 1731 people). Re-read after a
beat before concluding anything about the data. (Confirming it from the network side —
`BasicReadMultiple` returning 200 — needs Chrome, and re-reading the page is usually enough.)

**MudMenu popovers did not open under Chrome's synthetic clicks**, so menu items and the
dialogs behind them were hard to reach. Retry those on the nodeterm node before concluding they
are unreachable — its CDP input opens things Chrome's did not — and if it still will not open,
ask the user rather than claiming a dialog works because it compiled.

**Verify the effect, never the acknowledgement.** Both tools report success for a dispatch, not
for Blazor receiving it. In Chrome this is fatal on a MudButton: `computer left_click` with a
`ref` says "Clicked on element ref_N" and **nothing happens**, and only a click at the
screenshot coordinates fires — which cost a confused minute on `ROTATE THE KEY`, where the
success path is a page that rewrites itself, so "no visible change" read as a broken handler
rather than a click that never landed. The nodeterm node does not have that failure (a `@ref`
click lands, and one click produces exactly one effect, both confirmed), but it *can* refuse a
click as off-screen, sometimes spuriously. Either way: re-read the page, or check the row in
Postgres.

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

That check is JavaScript, so it needs Chrome — `AskUserQuestion` first. Cheaper alternative when
Chrome is not available: the rule is dead whenever the class sits on a Mud component
(`<MudText>`, `<MudButton>`, `<MudStack>`, `<MudPaper>`) rather than a plain element, so read the
`.razor` and apply one of the two fixes below rather than asking for a browser to prove it.

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
A working stream is a single `POST …/Games.Display/WatchScoreboard` held open at 200; several
requests means it is falling back or reconnecting. Seeing that requires `read_network_requests`,
so it is a Chrome job — ask for it with `AskUserQuestion`, and only when the *transport* is what
you are doubting.

**Test the behaviour first — it needs no network read and no Chrome.** Two nodeterm browser
nodes, no auth on the display side:

1. `open-browser` the tracker in one node and `/Display/Scores` in another.
2. Score in the tracker: `browser --node <tracker> --read map`, then `--click` the team tile.
3. `browser --node <display> --read text` — it should have changed with no reload.
4. Undo to clean up: `--click` the `Undo last` button once per award, then re-read to confirm
   the count actually went back down.

If the display changes, the stream works and the network read adds nothing. Only ask for Chrome
if it does not.

Bars animate via a 0.5s CSS transition, so a width measured immediately after an update is a
mid-transition value. Wait before measuring or it looks like a bug — and measuring it at all
needs JS, i.e. Chrome.
