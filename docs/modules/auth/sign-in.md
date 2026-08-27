---
title: Sign in — the cookie, the bearer token, and the local bypass
kind: reference
status: current
module: auth
verified: 2026-08-27
code:
  - GSBC.ImpactKids.YARP
  - GSBC.ImpactKids.Grpc/Services/CustomClaimsTransformation.cs
  - GSBC.ImpactKids.Grpc/Features/Attendance/PickupDisplayKeyServices
  - GSBC.ImpactKids.WASM/Authentication/BffAuthenticationStateProvider.cs
---

# Sign in — the cookie, the bearer token, and the local bypass

How a signed-in user reaches the gRPC service, and how the Development-only bypass produces the same
result without Auth0. Read this before changing anything under `GSBC.ImpactKids.YARP`, adding a route to
the proxy, or touching the dev bypass.

**There are two layers, not one.** The browser holds a cookie; the proxy attaches a bearer token; the
gRPC service trusts only the token. Anything that fakes just the cookie produces a UI that looks signed
in and whose every call returns 401 — which reads as a broken app rather than a failed sign-in.

## The two layers

| Layer | Who checks it | What it is |
|---|---|---|
| Cookie | YARP, on routes with an `AuthorizationPolicy` | `__gsbc_yarp`, `SameSite=Strict`, `Secure`, `HttpOnly` (`Extensions/HostExtensions.cs`) |
| Bearer | The gRPC service | An Auth0-signed JWT, attached by `AddBearerTokenToHeadersTransform` |

There is a **third, much smaller thing** that is not a layer of this at all: `__gsbc_display`, a
cookie in its own scheme that a wall display enrols with. It never carries a bearer token and never
reaches a `gRPC/` route. It has its own section below; do not extend the leader session to cover a
screen.

The proxy routes (`appsettings.json`) decide which applies:

| Route | Policy | Notes |
|---|---|---|
| `/gRPC/GSBC.ImpactKids.{service}/**` | `Default` | cookie required, bearer attached |
| `/api/**` | `Default` | same |
| `/public/GSBC.ImpactKids.Games.Display/**` | none | anonymous on purpose — the score wall cannot sign in. Aggregate scores only |
| `/public/GSBC.ImpactKids.Attendance.Display/**` | `PickupDisplay` | anonymous *at the gRPC service* — no bearer is attached — but **not open**: the enrolment cookie is required. Display names of children currently requested and not yet signed out, nothing else |
| `{**catch-all}` | none | the WASM app itself |

**The two `public/` routes are not the same shape, and the difference is the point.** Aggregate
scores may be read by anyone; a list of which named children are in a known building at a known hour
may not. `public/` says "no Auth0 login is involved here", not "no credential is involved here".

**`public/` is routed one named service at a time, never as a prefix.** Two services are listed
above because two exist, not because the prefix is open. A `/public/{**catch-all}` match would
make every service anyone later names under that prefix anonymous by default, and the mistake
would be invisible — it is a route that already works.

**A service under `public/` with no route entry fails silently and misleadingly.** It falls
through to the WASM catch-all, which answers `index.html` with a **200**, and grpc-web reports
`Bad gRPC response. Invalid content-type value: text/html` — the same symptom as an expired
session below, and on signage with nobody standing at it, it reads as "Connecting…" forever.
Adding a service under `public/` means adding its route here in the same commit.

The transform attaches a token only when the route has a policy, and Duende's token manager handles
refresh (`AddBearerTokenToHeadersTransform.cs:38`).

## The cookie answers with status codes, not redirects

`OnRedirectToLogin` returns **401** instead of a 302, and `OnRedirectToAccessDenied` returns 403
(`HostExtensions.cs:62`). This is load bearing, not tidiness: everything behind the cookie policy is an
API call from the SPA, and the default redirect falls through to the WASM catch-all route, so the caller
gets `index.html` with a 200. grpc-web then reports
`Bad gRPC response. Invalid content-type value: text/html`, which looks like a serialisation bug and is
really an expired session. The client drives `/bff/login` itself.

A 401 from `/bff/user` on an anonymous page is expected, not a fault.

**The display scheme below answers the same way, for a sharper version of the same reason.** Its
caller is grpc-web on a TV with nobody standing at it, so a 302 does not merely look like a
serialisation bug — it reads as "Connecting…" on a wall, forever.

## The pickup display key — how a screen with no login is still not public

`/Display/Pickup` shows children's display names. The page route is **not** the control point and
never can be: the `{**catch-all}` route serves the same `index.html` for every path and the Blazor
router picks the page client side, so the bundle is public and always was. A key checked on the page
route would look like security and be none. **The thing worth gating is the data call**,
`public/GSBC.ImpactKids.Attendance.Display/**`, and that is where the policy sits.

### Enrol on a query string, run on a cookie

```
TV bookmark ──► /bff/display-login?key=…  ──► sets __gsbc_display ──► 302 ──► /Display/Pickup
                                                                                   │
                                             WatchPickups ◄── cookie ──────────────┘
```

`WatchPickups` is a long-lived stream that reconnects all night. A key left in the query string is a
credential written into proxy and CDN access logs on **every reconnect, forever**; a key spent once
at enrolment appears there once. `DevAuthEndpoints` is the existing precedent for the shape — mint a
session, redirect to a clean URL — and `DisplayAuthEndpoints` follows it, including stripping the key
from the redirect. Unlike the dev bypass these routes are **not** environment-gated: a wall display
is a production thing and there is no other way to set one up.

The TV bookmarks the *keyed* URL, so a cookie lost to a browser restart or a wiped profile re-enrols
itself with nobody involved. That is deliberate, and it is also the answer to the data-protection key
ring being in memory: a proxy restart drops every cookie, and this one comes back on its own.

### The scheme grants one thing

`DisplayAuthOptions.SchemeName` is a **second** `AddCookie` beside the leader session, never a
widening of it (`Extensions/HostExtensions.cs`). Two independent things stop it reaching anything
else:

- the `Default` policy on every `gRPC/` and `/api/` route names only the leader cookie scheme, and
  the `PickupDisplay` policy names only the display scheme — neither satisfies the other;
- `AddBearerTokenToHeadersTransform` is **not** attached on the `PickupDisplay` route, so nothing
  this cookie carries can reach the gRPC service's `EnabledOnly` policy. That policy reads a claim
  off a bearer token, and there is no bearer token.

A signed-in leader therefore does not satisfy the pickup route either. The wall is opened from its
setup link, by anybody or nobody, and that is the only way in.

### Rotation is immediate and total

The key lives in the database (`PickupDisplayKeys`, one row), not in config — "rotated on admin
request" means somebody presses a button on `/Attendance/PickupDisplaySetup`, not that somebody
redeploys. **Only a SHA-256 hash is stored**; the key itself comes back once, from the rotation that
minted it, and is unrecoverable after that. Comparison is `CryptographicOperations.FixedTimeEquals`,
and the key is never logged — not on success, not on failure, not in the redirect.

The row's `Id` doubles as the key's **generation**. It rides on the cookie, and
`OnValidatePrincipal` checks it against the current one on every request, so rotating does not merely
stop new enrolments — every screen already enrolled falls to the unauthorised state and has to be
re-opened from the new link. The proxy caches "which generation is current" for 30 seconds
(`DisplayAuthOptions.GenerationCacheLifetime`), which is the real upper bound on "immediate", and
falls back to the last answer that arrived if the gRPC service is briefly unreachable rather than
signing every wall in the building out.

The proxy asks the gRPC service over two cluster-internal endpoints,
`internal/pickup-display-key/validate` and `.../generation`. **They have no proxy route, on purpose.**
`validate` is a key oracle; the only thing stopping it being brute-forced from the internet is that
the internet cannot reach it. Adding an `internal/` route to `appsettings.json` would undo that
silently, exactly the way a `/public/{**catch-all}` would.

### The key bounds discovery, and nothing bounds disclosure

A key stops URL guessing, crawlers and a link idly shared. That is the whole of its job.

It does **not** help against anyone who has ever held the URL — a volunteer with it in their phone
history keeps it until somebody rotates. **There is no second control behind the key: no time
window, no source restriction.** The key is it.

A time-boxed response — serving names only around the service — was built and then **removed at the
owner's instruction**, on the grounds that the TV and the enrolment link are under his sole control.
That is a deliberate acceptance of the residual risk by the person holding it, recorded here so a
later reader does not mistake the absence for an oversight and quietly add one back. If the link ever
leaves that person's control, **rotating the key is the answer**, and rotation is immediate and total.

A missing or stale cookie renders as readable words telling whoever walks past that the screen needs
re-opening from its setup link, never as "Connecting…".

`/bff/display-logout` drops the cookie on one screen without touching the key.

## Being enabled is a database fact, not an Auth0 one

`CustomClaimsTransformation` (`Grpc/Services/CustomClaimsTransformation.cs:23`) looks the caller's `sub`
up in `Users` and adds `UserId` and `Enabled` claims. Auth0 never sends `Enabled`.

**An unknown `sub` is inserted as a disabled user**, so a brand-new account authenticates successfully
and then gets 403 on everything until someone enables it on the admin page. One `sub` is hard-coded to
seed as enabled (`CustomClaimsTransformation.cs:33`) — the developer's own — which is the bootstrap for
an empty database.

The client's own policy is separate and weaker: `permissions=user:enabled`, read from the access token
by `/bff/user`. It hides controls. The server re-checks every call, and that is the check that counts.

## The local bypass

Development only. It exists because Auth0 here is Google SSO, which an agent cannot drive and which is
slow to repeat by hand. Operating it — the URL, verifying it worked — is in the `run-and-inspect-app`
skill; what follows is why it is safe.

### Three independent gates

`DevAuthGate.IsOpen` (`YARP/DevAuth/DevAuthOptions.cs`) is the single decision, and all three must hold:

1. `IHostEnvironment.IsDevelopment()`
2. `DevAuth:Enabled`
3. a signing key of at least 32 characters

Nothing has a usable default — a half-configured deployment gets no bypass rather than a weak one. The
gate is asked in four places, so one cannot be enabled without the others: route registration
(`YARP/Program.cs:33`), each endpoint's own re-check, the bearer transform
(`AddBearerTokenToHeadersTransform.cs:26`), and `/bff/user` (`Endpoints/AuthEndpoints.cs:51`). When it is
shut the routes **do not exist** — a 404, not a disabled handler waiting for a stray config value. Both
services log a warning at startup when it is open.

### The key is generated per run and never written down

`AppHost.cs:65` sets `DevAuth__Enabled` and a fresh `RandomNumberGenerator` key on the gRPC service and
the proxy, gated on `IsRunMode && IsDevelopment()`:

- **run mode only**, so it can never be baked into a published manifest or Helm chart
- fresh every run, so a token cannot outlive the process that issued it, and there is no key on disk to
  leak or commit

### The token is shaped like the real one

`/bff/dev-login` mints an HS256 JWT with the same audience, the `sub` the claims transformation looks up,
and `permissions: user:enabled`. The gRPC service adds that key to Auth0's issuer and keys rather than
replacing them (`Grpc/Program.cs:69`), so a real Auth0 token still validates exactly as before.

The token rides in a claim on the cookie rather than in Duende's token store — Auth0 never saw this user,
so there is nothing to refresh, and the two consuming paths short-circuit before asking the token manager.
`/bff/user` strips that claim from its response (`AuthEndpoints.cs:69`): the browser has no use for it.

`/bff/dev-logout` exists because the real `/bff/logout` goes out to Auth0 to end a session that was never
started there.

### The default subject must be an enabled one

`DevAuthOptions.Subject` defaults to the `sub` the claims transformation seeds as enabled. **Any other
subject lands as a new disabled user and every call comes back 403** — the bypass will look broken when it
is working exactly as designed.

## What has and has not been proven

Verified on 2026-08-24: a dev session drives the authed pages end to end, with every proxied gRPC call
returning 200 and `/bff/user` reporting `permissions: user:enabled`.

**The pickup display key has not been driven in a running app.** Added 2026-08-27; it compiles and the
reasoning above is checked against the code, but none of these has been seen happen: an enrolment that
sets the cookie and lands on a clean URL, a `WatchPickups` call that a 401 reaches as a readable screen
rather than "Connecting…", or a rotation dropping an enrolled screen.
Treat this section as the design until somebody watches it work.

**The closed gate is verified**, through the `GSBC.ImpactKids.AppHost: https PROD` run configuration on
2026-08-24, with the whole stack up:

| Request | Result |
|---|---|
| `/bff/dev-login`, `/bff/dev-logout` | 200 serving the WASM shell — the routes do not exist, so they fall through to the SPA catch-all. **No `Set-Cookie`, no session.** |
| `/bff/login` | 302 to Auth0 — real sign in untouched |
| `/bff/user` | 401 |
| `/gRPC/...` | 401 |

**Which gate did the work matters.** The proxy and the gRPC service still report
`ASPNETCORE_ENVIRONMENT=Development`, because Aspire honours each project's own launch profile. What shut
the bypass was `DevAuth__Enabled` and `DevAuth__SigningKey` being **absent entirely** — the AppHost ran
as Production, so it never injected them. The flag and key gates were exercised; the environment gate was
not.

The three gates are therefore not independent in practice: the AppHost's environment decides whether the
other two are ever set. A real deployment has both properties — a Production service *and* no DevAuth
configuration — but do not read this test as proof of the environment check itself.

An earlier attempt was defeated by something unrelated to auth, worth knowing because it looks alarming:
the `impact-kids` database came up unhealthy with
`Npgsql.PostgresException 28P01: password authentication failed for user "postgres"`, leaving
`migrations`, `grpc` and `yarp` all `Waiting`. See
[../infrastructure/generated-passwords.md](../infrastructure/generated-passwords.md) — a failed PROD run
is far more likely to be that than anything about auth.

## Local configuration

The Auth0 client secret lives in `GSBC.ImpactKids.YARP/appsettings.Development.json`, which is **not
tracked** — `**/appsettings.Development.json` is excluded, so the file exists only on each developer's
machine. Nothing in history contains it; `HostExtensions.cs:77` reads the key and no more.

The exclusion is in `.git/info/exclude` rather than `.gitignore`, which means it is **local to this
checkout and not shared**. A fresh clone does not inherit it, so the first developer to create that file
elsewhere can commit it by accident. Moving the pattern into `.gitignore` would close that off.
