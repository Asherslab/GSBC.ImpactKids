---
title: Sign in — the cookie, the bearer token, and the local bypass
kind: reference
status: current
module: auth
verified: 2026-08-24
code:
  - GSBC.ImpactKids.YARP
  - GSBC.ImpactKids.Grpc/Services/CustomClaimsTransformation.cs
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
| Cookie | YARP, on routes with an `AuthorizationPolicy` | `__gsbc_yarp`, `SameSite=Strict`, `Secure`, `HttpOnly` (`Extensions/HostExtensions.cs:51`) |
| Bearer | The gRPC service | An Auth0-signed JWT, attached by `AddBearerTokenToHeadersTransform` |

The proxy routes (`appsettings.json`) decide which applies:

| Route | Policy | Notes |
|---|---|---|
| `/gRPC/GSBC.ImpactKids.{service}/**` | `Default` | cookie required, bearer attached |
| `/api/**` | `Default` | same |
| `/public/GSBC.ImpactKids.Games.Display/**` | none | anonymous on purpose — the wall display cannot sign in |
| `{**catch-all}` | none | the WASM app itself |

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
