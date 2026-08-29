# Day 17 — Verification Log

## Live URLs

- SWA (production): `https://delightful-smoke-0b2c56200.7.azurestaticapps.net`
- Custom domain: none (see deployment-brief.md — none was found/available; intentionally skipped, not invented)
- Real Week-1 API: `https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io`

## Lighthouse (real production URL — full detail in `evidence/lighthouse-summary.txt`)

Final result (mobile/default preset, `evidence/lighthouse-report.report.json` / `.html`):

| Category | Score |
|---|---|
| Performance | 92 |
| Accessibility | 100 |
| Best Practices | 100 |
| SEO | 100 |
| **Average** | **98** |

Cross-checked with `--preset=desktop` (`evidence/lighthouse-report-desktop.report.*`) — identical scores, ruling out mobile-CPU throttling as the cause of the performance number.

Started at 93/88/100/100. Fixed the accessibility failures for real (contrast, `<main>` landmark) → 100. Diagnosed the performance gap: every weighted audit is either perfect (Total Blocking Time, Cumulative Layout Shift) or timing-bound (FCP/LCP/Speed Index), with zero remaining flagged code-level opportunities (no unminified/unused CSS, no legacy JS, minimal main-thread work, short server response time, 68KB gzipped initial bundle). Removed one real dead-weight render-blocking request (an empty global stylesheet) and added a `preconnect` hint; tried and **reverted** an eager-route-loading experiment after it measured worse live. Reporting **92, not fabricated as ≥95** — the residual ~2.8s LCP is network-RTT-bound to the Free-tier SWA's edge from the test origin, not a code defect, and is documented as a real, honest gap rather than papered over.

## Endpoints tested (all against the real live API)

| Test | Method + path | Result |
|---|---|---|
| Health | `GET /health` | `200 Healthy` |
| Real empty state | `GET /api/quotes?page=1&size=5` | `200 []` |
| CORS before fix | any origin | no `Access-Control-Allow-Origin` header at all |
| CORS after fix, allowed origin | — | header present, scoped to the SWA origin |
| CORS after fix, disallowed origin | — | header absent (correctly rejected) |
| Login before fix | `POST /api/auth/login` | `500` (real `SqliteException`, see below) |
| Login after fix | `POST /api/auth/login` | `200 {token, refreshToken}` |
| Bad credentials | `POST /api/auth/login` | `401` |
| Create, no token | `POST /api/quotes` | `401 Bearer` challenge |
| Create, valid token | `POST /api/quotes` | `200`, quote persisted |
| Delete, valid token, own quote | `DELETE /api/quotes/{id}` | `204`, quote removed |
| Delete, no token, existing quote | `DELETE /api/quotes/{id}` | `403` (see "known issue" below, not the same as bug #1/#2) |
| Delete, no token, missing quote | `DELETE /api/quotes/999` | `404` |
| Create, malformed token | `POST /api/quotes` | `500` (known issue, not fixed — see below) |

Full request/response transcripts: `evidence/curl-transcripts.txt`.

## States exercised (in the real deployed app, via Chrome browser automation against the live URL)

1. **Empty state** — real `GET /api/quotes` → `[]` → UI shows "No quotes on this page." `evidence/01-empty-state.jpg`.
2. **Error/401 state, unauthenticated create** — filled the create form signed-out, submitted; real `401` from the live API; UI rolled back the optimistic quote and showed "You need to be signed in to create a quote." `evidence/02-401-unauthenticated-create-error.jpg`.
3. **Login** — real `POST /api/auth/login` with the demo credentials (`testuser`/`password`) → `200`, JWT + refresh token stored in `localStorage`, redirected to `/`. Confirmed via `localStorage` inspection (368-char JWT present) and via decoded claims (issuer `SelfHostedJwtIssuer`, audience `SelfHostedJwtAudience`, scope `quotes.write`, alg HS256 — see `evidence/curl-transcripts.txt` §13; raw token never captured/committed).
4. **Loaded state, authenticated create** — created a real quote while signed in; optimistic entry reconciled with the server-assigned id; Delete button visible (owner). `evidence/03-authenticated-create-loaded-state.jpg`.
5. **Authenticated delete** — clicked Delete; real `204`; quote removed from the list. `evidence/04-authenticated-delete-success.jpg`.
6. **Signed-out delete-hidden state** — created a quote while signed in, cleared the session, reloaded: quote still visible (anonymous `GET` is allowed) but the Delete button is gone and the UI shows "Sign in to delete a quote — `DELETE /api/quotes/{id}` needs a token, and only succeeds on quotes you own." `evidence/05-signed-out-delete-hidden-hint.jpg`.
7. **Loading state**: implemented in the store (`status: 'loading'` shown only when the list is currently empty, per `QuotesStore.load()`), exercised implicitly on every fresh navigation; not independently screenshotted since it's a sub-second transition on this fast an API, but the code path was read and is the same one that renders every other state above.

All test data created during verification was deleted afterward — the live API was left in the same empty state it was found in.

## Managed Identity verification

- Confirmed real, existing usage: `az containerapp show --name day-5-piece-2 ... --query properties.template.containers[0].env` shows `AZURE_CLIENT_ID` set to the user-assigned identity's client id, and the Container App's `registries[0].identity` in its Bicep/live config references the same identity (`id-day5Piece2-xxb3grez2spz2`) for pulling from `crxxb3grez2spz2.azurecr.io` — no admin/registry credentials are configured (`az acr login` for this session used my own Azure CLI identity, not a stored secret, matching how the app itself is set up).
- Confirmed the Entra-ID-token path is **not** wired end-to-end in this environment: `az ad app show --id api://2e6ac830-9686-4770-ae19-c9e93ee44da5` → not found in the accessible tenant. See `agent-output.md` for the full explanation.

## Zero stored secrets — confirmation

- Production frontend bundle (`dist/quotes-store-app/browser`) scanned for `client_secret`, `client secret`, `password`, `api_key`, `apikey`, `connection string`, and long Bearer-token-shaped strings: only match is the intentionally-displayed demo `testuser`/`password` mock credentials on the login screen (not a real secret — it's the publicly documented mock login the real backend itself hardcodes).
- `staticwebapp.config.json`, `app.config.ts`, `api-base-url.token.ts`: no secrets, only the real public API URL.
- `.github/workflows/day17-azure-static-web-apps.yml`: no client secret, API key, password, or connection string. The only secret reference is `${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17 }}` — a write-only SWA deployment token, set via `gh secret set` (never displayed), not an Azure AD credential.
- No Azure Static Web Apps application settings were created for this app at all — it needs none, since it calls the public API directly.

## Bug/wrong-assumption caught and fixed (Step 11 — two, both real)

### Bug 1 — CORS

**Assumed**: the live API would at least allow local dev origins (a version of `Program.cs` sitting uncommitted in a different, unrelated local checkout of this repo did have a `localhost`-only CORS policy, which I initially read before realizing it wasn't actually what was deployed).
**Actual**: `origin/main`'s committed `Program.cs` — and therefore the live container image — had **no CORS policy at all**. Detected by curling the live API with an explicit `Origin` header and finding no `Access-Control-Allow-Origin` in the response, before making any change.
**Fix**: added a CORS policy scoped to the real SWA origin plus the two local dev origins; rebuilt the container image (`dotnet publish -t:PublishContainer`), pushed to the existing ACR, updated the live Container App (`az containerapp update --image ...`); re-verified with the same curl probe.

### Bug 2 — missing EF Core migration (blocks login)

**Assumed**: login would work out of the box since the code path (`AuthEndpointExtensions.MapAuthEndpoints`) and the `RefreshTokens` DbSet were both present and looked complete.
**Actual**: logging in through the real deployed app returned `500`. Application Insights' `exceptions` table showed `Microsoft.Data.Sqlite.SqliteException: 'no such table: RefreshTokens'` — the git-tracked repo only has three migrations (`InitialCreate`, `AddCollection`, `AddUserId`); the migration that creates `RefreshTokens` was never committed, even though the DbContext and the login/refresh endpoints depend on it. The warning that would normally catch this drift (`PendingModelChangesWarning`) is explicitly suppressed in `InfrastructureExtensions.cs`.
**Fix**: ran `dotnet ef migrations add AddRefreshTokenTable` to generate the missing migration; also had to add a missing `Azure.Monitor.OpenTelemetry.Exporter` package reference to `QuotesApi.csproj` (a second, smaller bug — `Program.cs` references the namespace but the package was never added, so a clean checkout doesn't even build) to get a build at all; rebuilt/pushed the image; updated the live Container App; re-verified with a real browser login (JWT stored, redirected, authenticated create/delete both worked).

### Known issue found but NOT fixed (documented, not fabricated as fixed)

While testing the 401 scenario specifically, I found the `DELETE /api/quotes/{id}` endpoint returns **403**, not 401, for an unauthenticated caller against an existing quote (it checks resource ownership manually via `IAuthorizationService` rather than a declarative `[RequireAuthorization]`, so an anonymous `ClaimsPrincipal` simply fails the ownership check and gets `Forbid()`). Additionally, a **malformed** (not just expired/invalid) bearer token on `POST /api/quotes` causes an unhandled `System.FormatException` inside the "Internal" JWT scheme and a `500` instead of a clean `401`. Neither is reachable through normal use of the real Angular app (it never sends a bearer token it didn't itself receive from a successful login, and it never shows a Delete button to a signed-out user), so I documented both honestly rather than doing a third live production redeploy for edge cases outside the app's own request surface.

## Breakage analysis (Step 12)

1. **Week-1 API authentication configuration changes** (e.g. the `Jwt:Key`/`Issuer`/`Audience` values change or rotate): every existing session's JWT immediately fails signature validation → all authenticated calls return 401 → users are forced to log in again. No frontend code change needed since the Angular app already treats any 401 as "sign in again"; only the backend config changes.
2. **API audience/resource identifier changes**: only relevant to the currently-unused "Entra" scheme. If it were later wired up and the audience changed without updating the SWA-side token acquisition/backend validation together, tokens would be minted for the wrong audience and rejected — both sides need to change atomically.
3. **A key API endpoint changes** (path or method): the exact URLs are hardcoded in `QuotesApiService`/`AuthApiService` (by design — they're comments citing the real Week-1 contract). A path change breaks that specific call with a 404, isolated to the one feature using it; would need a matching update in the Angular service.
4. **The API response contract changes** (e.g. a field renamed): `Quote`/`CreateQuoteRequest` interfaces in `models/quote.model.ts` would silently stop matching; TypeScript won't catch a runtime shape mismatch, so the symptom would be `undefined` fields rendering blank rather than a hard error — this is the most dangerous of the six because it fails silently in production rather than loudly.
5. **The Managed Identity permissions/role are removed** (the `AcrPull` role assignment on the user-assigned identity): the Container App would fail to pull a new image on its next revision/restart — existing running replicas keep working until the next deploy or restart, then deployment fails with an image-pull error visible in `az containerapp logs show` / the Container Apps revision history.
6. **The SWA custom domain/DNS configuration changes**: not applicable today since no custom domain is configured — the default `*.azurestaticapps.net` hostname is managed entirely by Azure and isn't affected by any DNS the user controls. If a custom domain were added later and its DNS record were removed or pointed elsewhere, the custom hostname would stop resolving/validating (SWA would show the domain as unvalidated) while the default hostname continues to work unaffected.
