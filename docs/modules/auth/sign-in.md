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

There is a **second caller type**: a wall display, which enrols with `__gsbc_display`, a cookie in
its own scheme. It is not a weaker leader and must never be modelled as one — it has no user row, no
`UserId` and no `Enabled` claim, it may read only what is explicitly opened to it, and it can never
write anything at all. It has its own sections below; do not extend the leader session to cover a
screen.

The proxy routes (`appsettings.json`) decide which applies:

| Route | Policy | Notes |
|---|---|---|
| `/gRPC/GSBC.ImpactKids.{service}/**` | `LeaderOrDisplay` | either caller; a token for whichever one it is, is attached |
| `/api/**` | `LeaderOrDisplay` | same. Carries `/api/stream`, which a display refreshes off |
| `{**catch-all}` | none | the WASM app itself |

**There are no `public/` routes any more, and no anonymous gRPC service.** There used to be two, on
the reasoning that a screen on a wall cannot sign in — true, but it led somewhere bad: the gRPC
services behind them had no authorization at all, so anything that could reach `http://grpc` could
read them. The proxy was the only control, which is not defence in depth.

**The proxy route is now deliberately permissive, and that is not the weakening it looks like.** It
proves only "you are one of the two callers this app has". *Which* caller may do *what* is decided at
the gRPC service, per method — where the code can actually see what is being asked for. Before, the
gate was in the one place that could not.

**A gRPC service with no matching proxy route fails silently and misleadingly**, which is why the
prefix is a wildcard rather than a list. A path with no route falls through to the WASM catch-all,
which answers `index.html` with a **200**, and grpc-web reports
`Bad gRPC response. Invalid content-type value: text/html` — the same symptom as an expired session
below, and on signage with nobody standing at it, it reads as "Connecting…" forever.

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

## The display key — how a screen with no login is still not public

`/Display/Pickup` shows children's names; `/Display/Scores` and `/Display/Reveal` show the game
board. All three enrol on the same key. The page route is **not** the control point and never can
be: the `{**catch-all}` route serves the same `index.html` for every path and the Blazor router
picks the page client side, so the bundle is public and always was. A key checked on the page route
would look like security and be none. **The thing worth gating is the data call**, and that is where
both policies sit — one at the proxy, one at the gRPC service.

### Enrol on a query string, run on a cookie

```
TV bookmark ──► /bff/display-login?key=…  ──► gRPC validates the key, mints a display token
                                                          │
                                              sets __gsbc_display, carrying
                                              the generation AND that token
                                                          │
                                              302 ──► /Display/Pickup
                                                          │
                     gRPC/…  ◄── cookie at the proxy, token at the service ──┘
```

A wall holds its session for months and re-reads on every change all night. A key left in the query
string is a credential written into proxy and CDN access logs on **every one of those requests,
forever**; a key spent once at enrolment appears there once. `DevAuthEndpoints` is the existing precedent for the shape — mint a
session, redirect to a clean URL — and `DisplayAuthEndpoints` follows it, including stripping the key
from the redirect. Unlike the dev bypass these routes are **not** environment-gated: a wall display
is a production thing and there is no other way to set one up.

The TV bookmarks the *keyed* URL, so a cookie lost to a browser restart or a wiped profile re-enrols
itself with nobody involved. That is deliberate, and it is also the answer to the data-protection key
ring being in memory: a proxy restart drops every cookie, and this one comes back on its own.

### The display token — the gRPC service authenticates the screen itself

The proxy proving a screen is enrolled is **not enough**, because the proxy is not the only way to
reach the gRPC service. Anything inside the cluster — a compromised pod, a `kubectl port-forward`, a
route added by mistake — talks to `http://grpc` directly and skips the proxy entirely. So the
service authenticates the display on its own.

**There is no shared secret to distribute.** The gRPC service is both issuer and validator: the
signing key lives on the `PickupDisplayKeys` row, alongside the key hash, and never leaves the
cluster. At enrolment `internal/pickup-display-key/validate` mints a JWT signed with it; the proxy
carries that token on the cookie and attaches it as the bearer, without ever seeing what signed it.
`DisplaySigningKeyProvider` holds the current key in memory for validation, because JwtBearer
resolves signing keys synchronously and a wall re-reads all night.

The token deliberately carries **no subject claim**. JwtBearer maps an inbound `sub` onto the
nameidentifier claim, and `CustomClaimsTransformation` creates a `DbUser` row for any nameidentifier
it does not recognise — a subject would manufacture a user row for every wall in the building. The
display scheme also sets `MapInboundClaims = false`; both halves guard the same mistake.

### A display is read-only, and that is enforced twice

`Policies.cs` in the gRPC service is the one place that says who may call what. Every policy **names
its schemes**, which is what makes the separation structural: a display token does not merely fail
`EnabledOnly`, it is never authenticated against it, so no claim it could ever carry satisfies it.

| Policy | Admits | Used for |
|---|---|---|
| `EnabledOnly` | a signed-in, enabled leader | the fallback — everything, unless stated otherwise |
| `DisplayOnly` | an enrolled screen | nothing at present; available for a display-only read |
| `EnabledOrDisplay` | either | the handful of reads a wall makes |

**There is no class-level `[Authorize]` anywhere in the gRPC service, on purpose.** The fallback
policy is `EnabledOnly`, so a method with no attribute is leader-only and a forgotten annotation
fails *closed*. This inverts the annotation burden: you never mark a write, you mark only the reads a
display may make. A class-level attribute would undo it — broad on the class plus a narrow one
missing from a method is exactly the fail-open case this arrangement exists to prevent.

Because a policy cannot tell a read from a write, `DisplayReadOnlyInterceptor` enforces the other
half at the database: if the caller on the current request is a display, `SaveChanges` throws. Even
`EnabledOrDisplay` mistakenly put on a write method cannot let a screen mutate anything. Its known
limit is that a `SaveChanges` interceptor does not see `ExecuteUpdateAsync` or raw SQL — the two such
calls in the service are on the Elvanto sync path, which no display policy reaches.

A signed-in leader does not satisfy `DisplayOnly`, and a display does not satisfy `EnabledOnly`. The
wall is opened from its setup link, by anybody or nobody, and that is the only way in.

### Displays read the ordinary services, not services of their own

There is no pickup display service, contract or stream any more. `/Display/Pickup` reads the
attendance, people and service stores — the same ones every signed-in page reads — and works out who
is waiting **client side**, refreshing off `/api/stream` like every other page. Roughly 400 lines of
server code, a gRPC service, three contracts and a bespoke keep-alive stream went away with it.

**This was a deliberate trade and it should not be quietly reversed.** The old service returned a
display name and a time and nothing else, with "never add a field here that could identify a child"
written into it. A display can now read everything a `Person` carries — date of birth, allergies,
medical notes, family. The owner accepted that knowingly on 2026-08-28: the enrolment key is his and
the screens are ones he controls, and the control that matters is the key, not the response shape.
The read-only guarantee is about **integrity**, not confidentiality — it stops a screen changing
anything; it does not narrow what a screen can see.

If that ever needs narrowing, the cheap move is to put the medical, allergy and person reads back
behind `EnabledOnly` and leave the rest — not to rebuild a display-shaped API.

### Rotation is immediate and total

The key lives in the database (`PickupDisplayKeys`, one row), not in config — "rotated on admin
request" means somebody presses a button on `/Attendance/PickupDisplaySetup`, not that somebody
redeploys. **Only a SHA-256 hash is stored**; the key itself comes back once, from the rotation that
minted it, and is unrecoverable after that. Comparison is `CryptographicOperations.FixedTimeEquals`,
and the key is never logged — not on success, not on failure, not in the redirect.

**Rotation now replaces two things, not one.** The row carries a `TokenSigningKey` beside the key
hash, minted fresh on every rotation, so every display token issued under the old key stops verifying
the moment the new one is loaded — no revocation list, no expiry to wait out. The signature *is* the
generation check at the gRPC service.

The row's `Id` doubles as the key's **generation** at the proxy. It rides on the cookie, and
`OnValidatePrincipal` checks it against the current one on every request, so rotating does not merely
stop new enrolments — every screen already enrolled falls to the unauthorised state and has to be
re-opened from the new link. The proxy caches "which generation is current" for 30 seconds
(`DisplayAuthOptions.GenerationCacheLifetime`), which is the real upper bound on "immediate", and
falls back to the last answer that arrived if the gRPC service is briefly unreachable rather than
signing every wall in the building out.

The proxy asks the gRPC service over two cluster-internal endpoints,
`internal/pickup-display-key/validate` and `.../generation`. **They have no proxy route, on purpose**,
and are the only endpoints in the service marked `AllowAnonymous` beside the health checks and the
root signpost — the proxy calls them with no credential, because validating the key is the step that
happens *before* there is one. `validate` is a key oracle, and it also mints tokens; the only thing
stopping it being brute-forced from the internet is that the internet cannot reach it. Adding an
`internal/` route to `appsettings.json` would undo that silently.

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

**The display key and the display token were driven end to end on 2026-08-28**, on the full stack
(`GSBC.ImpactKids.AppHost: https`), in a browser and over grpc-web:

| Check | Result |
|---|---|
| Rotate mints a key | new key returned once, `TokenSigningKey` written beside the hash |
| Enrolment on the keyed link | 302 to a clean `/Display/Pickup`, `__gsbc_display` set, key not in the redirect |
| The wall renders | full name, service title and waiting time, off the ordinary services — no display service exists |
| Display reads | `Services`, `Person`, `Attendance.Records` `BasicReadMultiple` → 200 |
| Display writes | `Update`, `RequestPickup`, `GetKeyInfo`, `Users` → **401** |
| Rotation | old cookie → 401 immediately; old key link → 401 with readable words; new key enrols and reads |
| An unenrolled wall | renders "This screen needs setting up again", never "Connecting…" and never an Auth0 redirect |
| Leader unaffected | attendance tool and `PickupDisplaySetup` load normally |

Two things were **found by running it** that reading could not have settled, and both are recorded where
they bite rather than only here:

- **protobuf-net.Grpc drops `[Authorize]` from methods inherited from `[SubService]` base interfaces.**
  `GetScoreboard`, declared directly on its contract, kept its attribute; `BasicReadMultiple` silently
  lost it and fell to the `EnabledOnly` fallback, so every display read 401'd while leaders were fine.
  The allow-list therefore lives at the mapping site in `Program.cs` — see `DisplayEndpointExtensions`.
  It still fails closed, and **do not move it back onto the methods**.
- **A leader session must win when both cookies are present**, which is the normal case rather than an
  edge — the person who sets a TV up enrols it from the browser they work in. The proxy transform
  attached the display token first, which would have demoted that person to read-only on every write.
  Fixed in `AddBearerTokenToHeadersTransform`, and verified with both cookies in one browser.

**The read-only interceptor was verified by deliberately mis-configuring it**: a delete was opened to
displays, a throwaway row inserted, and the call made with a display cookie. The row survived. Note
that the caller saw `grpc-status: 2` ("Exception was thrown by handler") rather than the
`PermissionDenied` the interceptor throws, because the handler wraps it — so the interceptor logs an
error naming the path, and **that log line, not the status on the wire, is the diagnostic**.

**The migration deletes any existing `PickupDisplayKeys` row**, because a pre-existing key has no
signing key and could not mint a token. Every display is offline until somebody presses rotate and
re-enrols the screens — deliberate, and the honest state rather than a wall that fails obscurely.

**The games walls (`/Display/Scores`, `/Display/Reveal`) were anonymous and now require enrolment.**
They show "This screen needs setting up again" until the setup link is opened on them once. That path
has *not* been driven yet — only the pickup wall has.

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
