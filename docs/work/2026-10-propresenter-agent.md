---
title: ProPresenter agent — a connected machine, enrolled from the browser, driving segment timers
kind: plan
status: in-progress
module: propresenter
opened: 2026-10-19
verified: 2026-10-19
code:
  - GSBC.ImpactKids.ProPresenterAgent
  - GSBC.ImpactKids.Grpc/Features/ProPresenter
  - GSBC.ImpactKids.WASM/Features/ProPresenter
  - GSBC.ImpactKids.Shared.Contracts/Entities/Features/ProPresenter
  - GSBC.ImpactKids.Grpc/Policies.cs
  - GSBC.ImpactKids.Grpc/Program.cs
  - GSBC.ImpactKids.Grpc/Features/Authentication/DisplayAuth
  - GSBC.ImpactKids.YARP/appsettings.json
  - GSBC.ImpactKids.YARP/RequestTransformers/AddBearerTokenToHeadersTransform.cs
  - .github/workflows/docker-publish.yml
  - .github/workflows/chart-publish.yml
---

# ProPresenter agent — a connected machine, enrolled from the browser, driving segment timers

The design for a small always-running tool on the Impact Kids ProPresenter Mac, configured entirely
from the web app, whose first job is labelled segment timers on the stage screen. Read it before
building any part of it; it records what was proven against a real ProPresenter on 2026-10-19 and
why the auth is shaped the way it is.

**Where it stands (2026-10-19).** Machines are built and verified end to end: enrolment, the
machines admin page, the agent with its LaunchAgent install, the release job, and — as the first
feature, to prove the whole path — a feed of first-timers' first names. The verification table is in
[../modules/auth/sign-in.md](../modules/auth/sign-in.md#a-propresenter-machine--the-third-caller).
**Segment timers are built (2026-10-20)** as one schedule timer per machine driven from the service's
schedule, rather than the "slots" this plan first sketched — see
[2026-10-service-schedules.md](2026-10-service-schedules.md#segment-timing-decided-2026-10-19-built-2026-10-20)
for the rules, the event-stream path, and what ProPresenter's 10-second feed poll forced on the
timer. Not yet proven: the release job itself (it runs on the first push to `master`) and
`install.sh` on a real Mac. **The agent also copies songs** out of chosen ProPresenter libraries
(2026-10-20) — see [2026-10-songs-library.md](2026-10-songs-library.md). **The machines page re-reads
the list every 30 seconds while open** (2026-10-20): the heartbeat raises no change event, so before
that an open page showed a steadily reporting Mac as offline 90 seconds after it loaded. Verified both
ways - online for minutes while the agent reported, offline 90 seconds after it stopped.

**The enrolment flow is a credential issuer, and the one rule that keeps it safe is that a code may
only ever be redirected to a loopback address.** PKCE protects a code from being intercepted; it does
nothing against an attacker who starts the flow *themselves* and gets a leader to press Approve. The
loopback restriction is what defeats that: the code can only land on the computer whose browser
approved it. Accepting any other `redirect_uri` turns the approval page into a phishing kit.

## What the tool is

A third caller type, beside a leader and a wall display — and **not** a display.

| | Leader | Display | ProPresenter machine |
|---|---|---|---|
| Has a person at it | always | never | once, at setup |
| Enrols by | Auth0 sign in | keyed URL | browser approval, loopback + PKCE |
| Writes | anything | **nothing**, enforced by `DisplayReadOnlyInterceptor` | its own status, through its own service only |
| Identity | user row | none (no `sub`) | machine row (no `sub`) |

A display is read-only and non-interactive by design, and both properties would be wrong here: the
machine reports its own health, and someone is sitting at it the one time it is set up. Do not extend
the display scheme to cover it, for the same reason the auth doc gives for not extending the leader
session to cover a screen.

- **Runs as** a .NET worker, a new project `GSBC.ImpactKids.ProPresenterAgent` in this solution,
  published self-contained for macOS and started at login by a `launchd` LaunchAgent with `RunAtLoad`
  and `KeepAlive`. No window: setup happens in the browser, configuration on the admin page.
- **Stores locally** only its credential, in a `0600` file (see "On the Mac" for why not the
  Keychain), and the Impact Kids base URL, in the LaunchAgent plist. Everything else — the
  ProPresenter host and port, the timer slots — is pulled from Impact Kids.
- **Connects out**, never in. It sits behind the church NAT, so it opens one long-lived stream to
  Impact Kids for configuration and commands, and Impact Kids never needs to reach it.

`propresenter-utilities` (`github.com/Asherslab/propresenter-utilities`) is another church's tool and
stays untouched. Its `ProPresenterClient` and `TimerStream` are good references for the ProPresenter
half — especially the bracket-matching framing of the chunked timer stream — and may be copied, not
referenced.

## Enrolment — OAuth 2.0 for native apps (RFC 8252) with PKCE (RFC 7636)

```
Agent (no credential)                       Browser on the same Mac                 Impact Kids
─────────────────────                       ───────────────────────                 ───────────
listen on 127.0.0.1:{random}
make verifier, challenge = S256(verifier)
make state
open ─────────────────────────────────────► /ProPresenter/Connect?redirect_uri=
                                              http://127.0.0.1:{port}/callback
                                              &state=…&code_challenge=…
                                            leader signs in if needed (Auth0)
                                            names the machine, presses Approve ───► ApproveMachine
                                                                                    creates machine row,
                                                                                    single-use code bound to
                                                                                    challenge + redirect_uri
                                            ◄── navigate to redirect_uri?code&state ─┘
◄── GET /callback?code=…&state=… ───────────┘
check state
POST /machine/token {code, verifier, redirect_uri} ───────────────────────────────► check code unused, unexpired,
                                                                                    S256(verifier) == challenge,
                                                                                    redirect_uri matches
◄──────────────────────────────────────────── machine id + machine secret (once) ──┘
secret → 0600 file, close listener
```

The page is an ordinary leader page; the approval call is an ordinary leader-only gRPC method. No new
kind of *human* sign in exists anywhere in this design.

Rules the implementation must keep:

| Rule | Why |
|---|---|
| `redirect_uri` must be `http://127.0.0.1:{port}/…` or `http://[::1]:{port}/…`, any port, checked server side when the code is minted | the phishing defence above; RFC 8252 §7.3 |
| Not `localhost` | RFC 8252 §8.3 — the name can resolve elsewhere |
| Code single-use, lives ≤ 60 s, stored as a hash | a replayed or leaked code is worthless |
| `code_challenge_method` is `S256` only | `plain` gives no protection |
| `state` checked by the agent before it exchanges anything | a forged callback must not be exchanged |
| The page says plainly "connect **the computer you are using now**" | it is the only cue a leader gets |
| The token endpoint is the one new anonymous endpoint, rate limited | like `internal/pickup-display-key/validate`, it is an oracle — but this one must be internet-reachable |

If a phone-only setup is ever needed, the Device Authorization Grant (RFC 8628) — the agent shows
`KDRT-WQPL`, the leader types it into Impact Kids — runs on the same machine row and token endpoint.
Not built until asked for.

### Why not Auth0 for this

Considered and rejected on 2026-10-19, on maintenance rather than security.

- **The tool would be the leader who approved it.** Auth0's native-app flow issues *that person's*
  token. The service would have to tell the tool from the person by `azp`, and disabling the person
  would silently take the ProPresenter machine down with them.
- **Refresh tokens expire on Auth0's clock** — with rotation, 15 days idle and 30 days absolute by
  default. A machine off over the summer holidays comes back signed out on a Sunday morning.
- **Revoking one machine** would live in Auth0's dashboard or Management API, not on our admin page.

The display scheme already proves the alternative: the gRPC service issues and validates its own
tokens with a key held on a database row (`Features/Authentication/DisplayAuth`).

## Credentials after enrolment

| Credential | Lifetime | Stored as | Used for |
|---|---|---|---|
| Machine secret | until revoked | SHA-256 hash on the machine row; plaintext only in the agent's `0600` credential file | `POST /machine/token` → an access token |
| Access token | 15 minutes | not stored | bearer on every agent call |

- **The access token is a JWT signed with a key on the machine's own row**, with the machine id as
  `kid`. Revoking deletes the row, and with it the only thing that verifies that machine's tokens —
  the same "the signature *is* the generation check" property the display token has.
- **No `sub` claim, and `MapInboundClaims = false`**, for the reason in the auth doc:
  `CustomClaimsTransformation` creates a user row for any unknown nameidentifier.
- **The secret is not rotated on use.** Rotation buys little for a credential that never leaves one
  Mac, and costs a lock-out whenever the agent dies between the server rotating and the file write.
  Revocation is the control.
- **A revoked or unknown machine** gets a definite 401 from the token endpoint. The agent then deletes
  its credential file and starts enrolment again — opening the browser on the Mac — rather than retrying
  forever.

## What changes in the existing services

**Proxy.** Both existing gRPC routes carry `AuthorizationPolicy: LeaderOrDisplay`, a cookie check,
and `AddBearerTokenToHeadersTransform` *overwrites* `Authorization` on any route with a policy. A
native agent has no cookie and brings its own bearer, so it needs its own route:

- `/machine/{**catch-all}` → the `grpc` cluster, prefix removed, **no `AuthorizationPolicy`**, so no
  cookie is demanded and the transform never runs, and the agent's `Authorization` header passes
  through untouched.
- This is less of an opening than it looks, for the reason the auth doc already gives: the proxy route
  proves little and the gRPC service authorizes every method. An Auth0 or display bearer arriving on
  this route is judged exactly as it would be on the others.
- The new route must not reach `internal/` paths. Scope it to the agent service and `machine/token`
  rather than all of `/gRPC`, and verify a request for `internal/pickup-display-key/validate` through it
  is a 404.

**gRPC service.**

- A `ProPresenterMachine` JwtBearer scheme, configured like the display scheme, whose key resolver
  looks the `kid` up in an in-memory cache of machine keys (few rows; refreshed like
  `DisplaySigningKeyProvider`).
- A `MachineOnly` policy naming only that scheme. **Not** added to `EnabledOrDisplay` or the fallback,
  so a machine token reaches nothing it is not explicitly mapped to.
- `IProPresenterAgentService` — machine only, mapped with an allow-list at the mapping site like
  `AllowDisplay` (protobuf-net drops method attributes from inherited interfaces; see the auth doc):
  - `GetConfiguration` — host, port, timer slots, for the calling machine only
  - `WatchCommands` — server stream of configuration changes and commands
  - `ReportStatus` — last seen, agent version, ProPresenter reachable, timer list
- `IProPresenterMachineService` — leader only, by falling back: list, `ApproveMachine`, rename,
  configure, revoke, and the segment commands below.

The machine id comes from the token, never from the request body, so a machine can only ever read its
own configuration and write its own status.

**WASM.**

- `/ProPresenter/Connect` — the approval page.
- `/ProPresenter/Machines` — connected machines: name, last seen, online, agent version, whether its
  ProPresenter answers; edit host and port; map timer slots onto ProPresenter timers picked from the
  list the agent reported; revoke.
- The segment control surface — start a slot with a label and a duration, stop, reset, live countdown.

There is no admin role in the app today — enabled is the only gate (`Policies.EnabledOnly`) — so
"admin" here means any enabled leader, the same people who can rotate the display key.

## Transport between the agent and Impact Kids

grpc-web through the proxy, using `Grpc.Net.Client.Web` and the code-first contracts in
`GSBC.ImpactKids.Shared.Contracts`. It is the one wire format the proxy is already proven to carry.

**Built as polling, not a stream.** Every 30 seconds the agent calls `GetConfiguration` and
`ReportStatus`; a settings change reached the agent within one tick in testing. `WatchCommands` is not
built — it earns its keep when segment timers need a start to land immediately. When it is, a stream
through the proxy and a cluster ingress will be cut by idle timeouts: reconnect with capped backoff,
heartbeat from the server, and re-pull `GetConfiguration` on every reconnect.

`ReportStatus` raises the change event only when something a leader sees changes (online, versions,
ProPresenter reachable) — never for the timestamp, or every phone would re-read the machines list
every 30 seconds. The admin page ages "online" on its own clock instead.

## First-timers feed — the first feature, built to prove the path

`/feeds/first-timers.rss` on the agent: the first names of children **signed in today** whose **first
time date is today**, both read as Sydney dates (`ProPresenterAgentService.GetFirstTimersToday`),
comma separated in one item's title. First names only, by design (`FirstTimer`). If Impact Kids cannot
be reached the last list stands. Verified with a temporary first-timer in the local database: the
name appeared in the feed and the agent fired the stage redraw.

## Segment timers — the first feature

### What ProPresenter actually does

Proven against ProPresenter 22 (API `v1`, port 55980 on the test machine) on 2026-10-19:

| Need | Works | Notes |
|---|---|---|
| Start a timer with a duration | `PUT /v1/timer/{uuid}/start`, body the full timer config **including `id`** | duration and start in one call. Without `id` → 400 `missing field id` |
| Start, stop, reset | `GET /v1/timer/{uuid}/{start\|stop\|reset}` | `start` on a timer created moments before returned 404, then 204 without starting. Use the `PUT` form |
| Live times | `GET /v1/timers/current?chunked=true` | JSON documents separated by a blank line, about once a second |
| Rename a timer | **does not work** | `PUT /v1/timer/{uuid}` echoes the new name; ProPresenter keeps the old one |
| Label text | a text box linked to an RSS feed, **"Title" mode** | renders every item's title concatenated with no separator — **the feed must have exactly one item** |
| Label refresh | **not by itself** | ProPresenter re-fetches the feed every 10 s but does not redraw until something forces a render |
| Forcing the redraw | re-apply each stage screen's current layout: `GET /v1/stage/screen/{id}/layout/{layoutId}` | no visible flicker on the stage screen |

"Title and Description" mode renders `title: description` per item. Change metadata (`lastBuildDate`,
`pubDate`, a new `guid`) was added during the test and did not by itself make ProPresenter redraw; the
layout re-apply is what does.

### How the agent uses that

- The agent serves one RSS feed per timer slot on `127.0.0.1` — ProPresenter is on the same Mac, so
  no feed ever needs to be reachable from outside it. One item; the title is the label.
- **A label change is shown by watching for ProPresenter's fetch, not by guessing.** The first fetch of
  the feed after the label changed means ProPresenter now holds it; ~0.5 s later the agent re-applies
  every stage screen's current layout. Worst case about 10 s from Start to the new label on screen.
- Starting a segment sets the slot's label and calls the `PUT …/start` above with the duration.
- The agent forwards the chunked timer stream's ticks for its mapped slots to Impact Kids, so the
  leader's page shows the same countdown the stage does.

On the ProPresenter side, setup is once per stage layout: a text box linked to the slot's feed in
Title mode, and a timer text element linked to the slot's ProPresenter timer.

The throwaway feed server used to prove this was a scratch script, not committed.

## On the Mac

- **The credential is a `0600` file, not a Keychain item** — changed from the plan while building.
  A Keychain item's access list is bound to the creating app's code signature, and these builds are
  ad-hoc signed, so every release is a different app to the Keychain: the first read after an upgrade
  raises a modal "allow access?" prompt, and an agent starting at login on an unattended Mac blocks
  behind it until somebody walks past. The only prompt-free alternative is an item any app may read,
  which protects nothing a `0600` file in the user's own Library does not. One file per Impact Kids
  address (`CredentialStore`), so a local run never clobbers the installed agent's credential.
- **There is no default Impact Kids address.** It comes only from `Agent__ImpactKidsUrl`, which
  `install.sh` always writes into the plist. It used to default to production in `appsettings.json`,
  and on 2026-10-19 a local run that missed its settings went straight at production and opened a
  browser on its (not yet deployed) connect page. Nothing was created; the agent now refuses to start
  without the variable, verified on the same Rider configuration.
- **No `.app`.** A LaunchAgent plist at `~/Library/LaunchAgents/au.com.baptist.impactkids.propresenter.plist`
  with `RunAtLoad` and `KeepAlive`, pointing at the binary under
  `~/Library/Application Support/ImpactKids/ProPresenterAgent/`. Logs to
  `~/Library/Logs/ImpactKids/ProPresenterAgent.log`.
- **`install.sh` does the whole install**, and rerunning it is the upgrade: pick the build for this
  Mac, copy it into place, clear `com.apple.quarantine` (`xattr -dr`), ad-hoc sign it
  (`codesign --force --sign -`, which Apple Silicon requires of any binary it will run), write the
  plist, and `launchctl bootout` / `bootstrap gui/$(id -u)` it. `uninstall.sh` reverses it, including
  the credential file.
- **Pick the build with `sysctl -n hw.optional.arm64`, not `uname -m`.** A Terminal running under
  Rosetta reports `x86_64` from `uname -m` on an Apple Silicon Mac, and would install the Intel build
  to run translated forever.

## Builds and the download link

The Impact Kids ProPresenter Mac is **Intel** today; both `osx-x64` and `osx-arm64` are built so a
replacement Mac needs nothing.

- **One zip, both architectures**: `impactkids-propresenter-agent-macos.zip`, holding
  `osx-x64/`, `osx-arm64/`, `install.sh` and `uninstall.sh`. One download link and nothing for the
  person at the Mac to choose wrongly; `install.sh` chooses.
- **Self-contained, single-file** `dotnet publish`, on a **`macos-latest`** runner — it can produce
  both runtimes, and publishing on macOS ad-hoc signs the apphost (`install.sh` signs again anyway).
- **Uploaded into the release the app build already makes.** Every push to `master` runs
  `docker-publish.yml`, whose `chart` job (`chart-publish.yml`, chart-releaser) creates the
  `impact-kids-app-{version}` release and marks it **Latest**. A new job, needing `version` and
  `chart`, publishes the agent with that version stamped in and runs
  `gh release upload impact-kids-app-{version} …zip`.
- **The machines page links to**
  `https://github.com/Asherslab/GSBC.ImpactKids/releases/latest/download/impactkids-propresenter-agent-macos.zip`.
  The repository is public, so it downloads without a GitHub sign in, and because the agent rides in
  every app release, *latest* is always the agent built from the same commit as the newest app — no
  version has to be plumbed into the WASM bundle for the link to be right.
- **The trap: that link is only as good as "Latest".** If the agent job fails, the newest release has
  no zip and the link 404s until the next green build. A separate `propresenter-agent-v*` release would
  steal Latest from chart-releaser — don't publish one. The machines page shows each machine's
  reported agent version, which is how a stale agent gets noticed.

## Decided

- **Who may approve a machine or start a segment** — any enabled leader (owner, 2026-10-19).

## Order of work

1. ~~Machine row, migration, `Approve`, token endpoint, scheme, policy, proxy route, with the negative
   cases proven.~~ Done 2026-10-19.
2. ~~The agent: enrolment, credential, LaunchAgent and `install.sh`, configuration pull, status report,
   the admin page with its download link, and the release job.~~ Done 2026-10-19, except that the
   release job and `install.sh` have not run for real yet.
3. ~~Segment timers: slots, feeds, the redraw, the control surface — and `WatchCommands`.~~ Done
   2026-10-20, differently: one timer chosen per machine, now/next feeds, the run bar on the phone,
   and the existing event stream at `machine/stream` instead of a `WatchCommands` stream. See
   [2026-10-service-schedules.md](2026-10-service-schedules.md).

Each step's behaviour lands in `docs/modules/propresenter/` in the same commit, per
[../AGENTS.md](../AGENTS.md).
