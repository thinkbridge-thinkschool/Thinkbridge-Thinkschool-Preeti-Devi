# Day 17 — Agent Output

## Summary

Deployed the real Angular frontend (sourced from `day-16/piece-2/quotes-store-app`) to a real Azure Static Web App, wired to call the real, live Week-1 QuotesApi (`day-5/Day-5-Piece-2`, already running on Azure Container Apps). Along the way, found and fixed two real, pre-existing bugs in the backend that blocked the deployment from actually working, and made the frontend Lighthouse-ready. No client secret is stored anywhere in the pipeline.

Live site: **https://delightful-smoke-0b2c56200.7.azurestaticapps.net**
Real API: **https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io**

## What I implemented

### Frontend (`day-17/deploy-to-azure-static-web-apps/frontend`)

- Copied from `day-16/piece-2/quotes-store-app` (Angular `^22.1.0`, zoneless, standalone, signals-based state via `QuotesStore`).
- `core/tokens/api-base-url.token.ts` and `app.config.ts`: replaced `http://localhost:5000/api` with the real HTTPS API URL. No `localhost` reference remains in this copy.
- `staticwebapp.config.json` (new): SPA navigation fallback to `index.html` for client-side routes (`/login`), plus a `Content-Security-Policy` scoped to `'self'` + the real API origin, and standard security headers (`X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`).
- Accessibility fixes (all verified via a real Lighthouse run against the live URL, not assumed): added `<label>`s (visually-hidden) for the create-quote `author`/`text` inputs, darkened two low-contrast text colors (`#94a3b8`/`#64748b` → `#475569`) to clear WCAG AA 4.5:1, added a `<main>` landmark.
- SEO/best-practices: added a `<meta name="description">` and `theme-color`.
- Performance: added `<link rel="preconnect">` to the real API origin; removed a dead render-blocking `<link rel="stylesheet">` pointing at a permanently-empty compiled `styles.css` (the source file only ever contained the default Angular CLI placeholder comment — this app has no real global CSS, everything is component-scoped). Tried eagerly loading the default route instead of lazy `loadComponent` to save a round trip; measured it live and it made LCP/FCP worse (bigger initial bundle), so reverted rather than keep a change that didn't actually help.
- Production build output: `dist/quotes-store-app/browser` (confirmed by an actual `ng build --configuration production` run, not assumed from convention).
- All 12 existing unit tests still pass unmodified.
- Production bundle scanned for secret-shaped strings (`client_secret`, `password`, `api_key`, `connection string`, etc.) — the only match is the intentionally-displayed demo `testuser`/`password` mock credentials shown on the login screen, not a real secret.

### Backend reference copy (`day-17/deploy-to-azure-static-web-apps/backend`)

A synced copy of the real, already-deployed `day-5/Day-5-Piece-2` source, kept in sync with the two live fixes below, for documentation/traceability. **The actual running service is the existing `day-5-piece-2` Container App** — this copy does not deploy a second instance.

### SWA configuration and CI/CD

- Created the real Azure Static Web App via `az staticwebapp create` (Free tier, East Asia — had to register the `Microsoft.Web` resource provider on the subscription first).
- `.github/workflows/day17-azure-static-web-apps.yml` (new, repo root — GitHub Actions only triggers on workflows at the repo root): on push to `main` touching `day-17/deploy-to-azure-static-web-apps/frontend/**`, installs deps, runs unit tests, builds production, scans the build output for secret-shaped strings (fails the build if any are found), then deploys via `Azure/static-web-apps-deploy@v1` using the deployment token secret. Also handles PR preview environments and their cleanup on close.
- The only secret involved is the SWA deployment token (`AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17`), set via `gh secret set` (value never displayed/logged/committed). It is a write-only static-file deployment credential, not an Azure AD client secret or an API key for the Week-1 API.
- Deployed and verified for real via the Azure Static Web Apps CLI (`swa deploy`) in addition to the workflow being in place for future pushes.

## Managed Identity — what I found, and the honest architecture

The task (and the linked reference example) call for a browser → SWA-Managed-Identity-token → Azure-AD-protected-API pattern. I checked whether this is genuinely achievable here before claiming it:

- `appsettings.json` on the real API has an `EntraId` section (`TenantId: 6e138bc2-...`, `ClientId: api://2e6ac830-...`) that the code's "Entra" JWT scheme validates against.
- I ran `az ad app show --id api://2e6ac830-9686-4770-ae19-c9e93ee44da5` against the actual Azure CLI session for this deployment (tenant `8d46a076-...`, "Azure for Students") — **the application does not exist in this tenant.** It's either from a different tenant this deployment has no access to, or stale example configuration.
- Building the reference repo's exact pattern (SWA Standard-plan Managed Identity → a managed Function proxy → an Entra app-role-protected downstream API) would require either access to that other tenant, or registering a brand-new Azure AD app in this tenant and then **reconfiguring and redeploying the live, shared `day-5-piece-2` Container App's Entra settings** — a change to Day 5's own graded artifact, beyond what a frontend deployment task should silently do, and not something I did without it being asked for.

**What I did instead, honestly:** documented the Managed Identity usage that is real and already live in this exact system — the Container App's **user-assigned managed identity** (`id-day5Piece2-xxb3grez2spz2`) is what it uses to pull its own image from Azure Container Registry (`AcrPull` role), with zero registry credentials stored anywhere. Separately, and independent of Managed Identity: the browser-to-API path in this app was *already* secretless before I touched it — it uses a username/password login that returns a short-lived bearer token, never an OAuth client secret. A browser SPA cannot hold Managed Identity credentials at all (MI is only usable by Azure-hosted compute), so the "no client secret in the browser" requirement is satisfied by this system's existing design, not by inventing an MI flow that isn't actually wired up end-to-end.

## Two real bugs found and fixed (see verification-log.md for full detail)

1. **CORS**: the live API had no CORS policy at all (verified via `curl` with an `Origin` header before touching anything). Added a policy scoped to the real SWA origin + local dev origins, rebuilt/pushed the container image, updated the live Container App, re-verified.
2. **Missing EF Core migration**: login returned a real `500` because `RefreshTokens` (used by the login/refresh endpoints) had no migration ever creating it in the git-tracked repo. Generated the missing migration, fixed a second smaller issue (a missing `Azure.Monitor.OpenTelemetry.Exporter` package reference that also broke the build from a clean checkout), rebuilt/pushed, updated the live Container App, re-verified end-to-end including a real browser login.

## Files changed

- `day-17/deploy-to-azure-static-web-apps/` — new folder: `deployment-brief.md`, `agent-output.md`, `verification-log.md`, `frontend/` (full Angular app), `backend/` (reference copy), `evidence/` (screenshots, Lighthouse reports, curl transcripts).
- `.github/workflows/day17-azure-static-web-apps.yml` — new.
- `day-5/Day-5-Piece-2/Program.cs` — added the CORS policy (bug #1 fix).
- `day-5/Day-5-Piece-2/QuotesApi.csproj` — added the missing `Azure.Monitor.OpenTelemetry.Exporter` package reference.
- `day-5/Day-5-Piece-2/Migrations/20260829074302_AddRefreshTokenTable.*` — new migration (bug #2 fix).
