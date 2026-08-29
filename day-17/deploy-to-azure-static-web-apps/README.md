# Day 17 — Deploy to Azure Static Web Apps

The Day 16 Piece 2 Angular app, live on Azure Static Web Apps, calling the Day 5 Piece 2
Quotes API — with a **system-assigned managed identity** proving out a server-to-server
write path that stores no client secret, no certificate, and no connection string
anywhere.

## Live

|  |  |
|---|---|
| **Frontend** | https://delightful-smoke-0b2c56200.7.azurestaticapps.net |
| Week-1 API | https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io |
| MI proxy | https://day17-mi-proxy.bluesky-eec20d45.centralindia.azurecontainerapps.io |

Lighthouse **92 / 100 / 100 / 100** (performance, accessibility, best practices, SEO)
against the live URL — the residual performance number is network-RTT-bound to the Free
tier's edge, not a code defect, and is reported rather than rounded up; the reasoning and
the reverted experiment are in [`verification-log.md`](verification-log.md) and
[`evidence/lighthouse-summary.txt`](evidence/lighthouse-summary.txt).

Verified live on 2026-08-29 — `./scripts/verify.sh`, **18 passed, 1 failed**. The one
failure is the `index.html` cache rule: Static Web Apps consumes
`frontend/public/staticwebapp.config.json` at deploy time, and the currently-live
deployment predates the `routes[]` cache rules in the restored file. It clears on the
next `./scripts/deploy.sh frontend`. Full run:
[evidence/verification-run.txt](evidence/verification-run.txt).

## Layout

```
day-17/deploy-to-azure-static-web-apps/
├── frontend/    Angular 22 app       (copied from day-16/piece-2/quotes-store-app)
├── backend/     .NET 10 Quotes API   (copied from day-5/Day-5-Piece-2)
├── mi-proxy/    managed-identity token broker, its own Container App
├── scripts/     deploy · verify · local smoke · URL stamping
├── evidence/    screenshots, curl transcripts, Lighthouse, the managed-identity proof
├── deployment-brief.md   the brief — target, real endpoints, what was blocked and why
├── agent-output.md       what was built, and why a Container App rather than a Function
└── verification-log.md   the round-by-round log, including the three bugs found
```

`frontend/` and `backend/` are **copies**, so Day 5 and Day 16 stay as their own days'
record. What the copies gained is listed under
[Changes to the copied code](#changes-to-the-copied-code).

## CI/CD

[`.github/workflows/day17-ci.yml`](../../.github/workflows/day17-ci.yml) — at the
repository root, because that is the only place GitHub Actions looks. Any push or pull
request touching `day-17/**` runs the frontend tests and production build, the backend
build and publish, and a parse check over every script. None of that needs a secret.

Deployment is a separate job, wired to the repository secret
`AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17` — so a push to
`day17-azure-static-web-apps` that touches `day-17/**` redeploys the live site. It never
runs from a pull request, and it ships the artifact the test job produced rather than
rebuilding, so the bytes that go live are the bytes that passed. Deleting the secret
switches deployment off without editing the workflow: the job then reports
`skipped, no token` and stays green. Details in [DEPLOY.md](DEPLOY.md#cicd).

## How the pieces fit

```
browser ──► Static Web App          Angular bundle. Static files only, no identity.
              │                     Host config: frontend/public/staticwebapp.config.json
              │ cross-origin (CORS), Authorization: Bearer <self-issued JWT>
              ▼
            day-5-piece-2 (Container App)   the real Week-1 API

curl ─────► day17-mi-proxy (Container App)  ← system-assigned managed identity
              │  DefaultAzureCredential gets an Entra token, attaches it, forwards
              ▼
            day-5-piece-2 (Container App)   validates it, records the caller
```

Two separate identities reach the same API and the API tells them apart by issuer:

| Caller | Token | Issued by | Scheme that validates it |
|---|---|---|---|
| a signed-in human | self-issued JWT, `scope: quotes.write` | the Quotes API itself | `Internal` |
| the MI proxy | Entra access token, role `Quotes.Api.Access` | Microsoft Entra | `Entra` |

The `Smart` policy scheme in `backend/Extensions/InfrastructureExtensions.cs` routes on
the token's issuer, so both work at once without either masking the other.

**Why a proxy at all.** Neither a browser nor a Free-plan Static Web App can hold a
managed identity — SWA's identity feature only retrieves Key Vault secrets, and the
managed-functions route needs the Standard plan. A Container App with a system-assigned
identity costs nothing extra and is the only component that ever sees an Entra token. The
token is acquired server-side, attached to the outbound call, and never returned to the
caller.

## Running it locally

The two tiers run independently; the proxy is not needed to use the app.

```bash
# API — the signing key is deliberately never defaulted to a usable value
cd backend
Jwt__Key="$(openssl rand -base64 48)" dotnet run          # :5263

# Frontend — proxies /api to :5263, so no CORS and no proxy involved
cd frontend
npm ci && npm start                                        # :4201
```

Or check everything that does not need Azure in one go:

```bash
./scripts/smoke-local.sh
```

## Deploying

```bash
./scripts/deploy.sh            # backend, proxy, frontend
./scripts/verify.sh | tee evidence/verification-run.txt
```

Resource names and the Entra ids live in `scripts/env.sh` — all public identifiers, each
overridable from the environment. The full runbook, including the one-time managed
identity role grant, is in [DEPLOY.md](DEPLOY.md).

## Changes to the copied code

**`backend/` (from `day-5/Day-5-Piece-2`)** — five changes, all in the deployment and
identity path, none to the domain logic:

1. **`QuotesApi.csproj`** — restored the missing `Azure.Monitor.OpenTelemetry.Exporter`
   package reference (1.8.3). `Program.cs` calls `UseAzureMonitorExporter()`, the
   reference had been dropped from the project file, and the Day 5 copy therefore does
   not compile as it stands: `error CS0246: The type or namespace name 'Azure' could not
   be found`. The version is the one Day 5's own last build actually shipped, taken from
   its `bin/Debug/net10.0/QuotesApi.deps.json`.
2. **CORS** (`Extensions/InfrastructureExtensions.cs`, `Program.cs`) — the Angular bundle
   is served from a different origin than the API, so every browser call is preflighted.
   Origins come from configuration (`Cors:AllowedOrigins`) rather than a literal, because
   the Static Web App hostname is assigned at deploy time; `scripts/deploy.sh` sets
   `Cors__AllowedOrigins__0`. No `AllowCredentials` — the session travels in a header,
   not a cookie.
3. **`can-edit-quotes` accepts an app role** (`Extensions/InfrastructureExtensions.cs`) —
   an app-only Entra token has no user and therefore no `scope` claim, so requiring
   `scope: quotes.write` alone rejected the managed identity with a 403. The policy now
   accepts either that scope or the `Quotes.Api.Access` app role. This is the bug the
   managed-identity path actually hit; see
   `evidence/managed-identity-verification.txt`.
4. **Azure Monitor export is conditional** (`Program.cs`) — `UseAzureMonitorExporter()`
   throws at startup if `APPLICATIONINSIGHTS_CONNECTION_STRING` is unset, taking the
   whole host down before it ever listens. Correct for the deployed Container App, where
   that setting is always present; wrong for `dotnet run` on a laptop, where telemetry
   export is not the point. The exporter is now attached only when there is somewhere to
   export to. The console exporters are untouched, so local traces still print.
5. **`Dockerfile` + `.dockerignore`** — the csproj's container properties still work, but
   a Dockerfile lets `az containerapp up --source` build the image, which is what
   `scripts/deploy.sh` uses.

**`frontend/` (from `day-16/piece-2/quotes-store-app`)** — an environments folder, the
deployment wiring, and the fixes the live site already carries.

*Deployment wiring:*

1. **`src/environments/`** — `environment.ts`, `environment.production.ts` and the shared
   `environment.model.ts`. `angular.json` swaps the first for the second in the production
   configuration. The URLs were hard-coded to `http://localhost:5000/api` in two places
   before.
2. **Two base URLs, not one** (`src/app/core/tokens/api-base-url.token.ts`) —
   `API_BASE_URL` for `/auth/login` and `QUOTES_BASE_URL` for quote traffic.
   `QUOTES_BASE_URL` defaults to `API_BASE_URL`, so every existing test and injector keeps
   working untouched, and in production both currently point at the API. The split exists
   so routing quotes through the proxy is a one-line change rather than a refactor — the
   proxy only exposes `/quotes` and `/quotes/{id}`, so login could never move with them.
3. **`NO_AUTH_HEADER_PREFIXES`** (same file, applied in
   `core/interceptors/auth.interceptor.ts`) — the proxy authenticates itself and allows
   only `Content-Type` through CORS preflight, so attaching the user's Bearer token to it
   would fail the request before it was ever dispatched. Empty whenever the two base URLs
   are equal, which is the current production setting.
4. **`angular.json` / `proxy.conf.json`** — production file replacement, `outputPath`, and
   an `ng serve` proxy sending `/api` to `localhost:5263` (the port in the backend's
   `Properties/launchSettings.json`) on port 4201, Day 16's port.
5. **`public/staticwebapp.config.json`** — the Static Web Apps host configuration: client
   routing fallback with asset exclusions, cache rules, and the security headers including
   a `connect-src` that names the two back ends explicitly.

*Fixes the live site already carries.* The Day 16 app is not what is deployed —
`verification-log.md` and `agent-output.md` record five changes made during the original
deployment, and a plain copy of Day 16 would have regressed every one of them on the next
`deploy.sh frontend`. Each was read back out of the live bundle
(`main-Q7EF2BXN.js` and its chunks) rather than guessed at, so the rebuilt output now
matches the deployed one:

6. **A sign-in / sign-out control** (`features/quotes-panel/quotes-panel.component.*`) —
   the `/login` route existed from Day 16 but nothing linked to it, so a signed-out
   visitor had no way to reach it and a signed-in one had no way out. The header now
   shows `Sign out` (`#logout-btn`) or a `Sign in` link (`#login-link`). `signOut()`
   clears the session *and* reloads the list, because the delete controls render off
   `isAuthenticated()`.
7. **Labelled form inputs** (same component, plus a `.visually-hidden` rule) — a
   placeholder is a hint, not an accessible name, and it disappears once the field has
   content. Real Lighthouse accessibility finding.
8. **Contrast** (`quotes-panel.component.css`) — `.subtitle` and `.signed-out-hint` moved
   off `#64748b`/`#94a3b8` to `#475569`, which clears WCAG AA for small text.
9. **A `<main>` landmark** (`app.ts`) — without it every routed page is anonymous content
   with nothing for assistive tech to skip to. Items 6–9 are what took accessibility to
   100.
10. **`index.html` + the dead stylesheet** — added a meta description, `theme-color`, and
    a `preconnect` to the API origin (its DNS/TCP/TLS otherwise sit on the critical path);
    removed `src/styles.css` from `angular.json`'s `styles` array, because it holds no
    rules and was shipping an empty render-blocking stylesheet. The file stays, with a
    comment saying how to put it back.

`ng test` — **12/12 passing**, unchanged from Day 16. `ng build --configuration production`
and `dotnet build` — both clean. The rebuilt `index.html` is byte-identical to the live
one apart from one two-line comment.

## The written record

| File | What it is |
|---|---|
| [`verification-log.md`](verification-log.md) | The round-by-round verification log — Lighthouse scores, the three bugs found and fixed, and the zero-stored-secrets confirmation. |
| [`deployment-brief.md`](deployment-brief.md) | The design brief: endpoints, the two authentication paths, and what was deliberately left undone (a custom domain, for want of a domain to point at). |
| [`agent-output.md`](agent-output.md) | The build summary for the session that produced the deployment. |
| [`evidence/`](evidence/README.md) | Screenshots, curl transcripts, Lighthouse runs, and the managed-identity proof. Indexed in its own README. |

`evidence/verification-run.txt` is a fresh `scripts/verify.sh` run against the live
deployment, independent of the transcripts captured while it was being built.
