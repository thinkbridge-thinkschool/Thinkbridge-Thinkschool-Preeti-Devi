# Day 17 — Agent Output

## Summary

Deployed the real Angular frontend (sourced from `day-16/piece-2/quotes-store-app`) to a real Azure Static Web App, wired to call the real, live Week-1 QuotesApi (`day-5/Day-5-Piece-2`, already running on Azure Container Apps). Built and verified a real Managed-Identity architecture end-to-end (a dedicated proxy Container App with a system-assigned identity, a new Azure AD app registration, and API-side validation), found and fixed three real bugs along the way, and confirmed zero secrets anywhere in the pipeline.

Live site: **https://delightful-smoke-0b2c56200.7.azurestaticapps.net**
Real API: **https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io**
Managed Identity proxy: **https://day17-mi-proxy.bluesky-eec20d45.centralindia.azurecontainerapps.io**

## What I implemented

### Frontend (`day-17/deploy-to-azure-static-web-apps/frontend`)

- Copied from `day-16/piece-2/quotes-store-app` (Angular `^22.1.0`, zoneless, standalone, signals-based state via `QuotesStore`).
- `core/tokens/api-base-url.token.ts` / `app.config.ts`: real HTTPS API URL, no `localhost`.
- `staticwebapp.config.json`: SPA fallback, CSP scoped to `'self'` + the real API origin, standard security headers.
- Accessibility (verified via a real Lighthouse run against the live URL): labels on the create-quote inputs, contrast fixes, `<main>` landmark → accessibility 100.
- SEO/best-practices: meta description, theme-color → both 100.
- Performance: preconnect hint, removed a dead render-blocking empty stylesheet; tried and reverted an eager-route-loading experiment after it measured worse live. Final: 92 (see verification-log.md for why this specific number is honest, not fabricated).
- Header now shows a "Sign in"/"Sign out" control (a real bug found in an earlier round of this same deployment — the login page existed but nothing linked to it).
- All 12 unit tests pass; production bundle scanned clean of secret-shaped strings.

### Backend reference copy (`day-17/deploy-to-azure-static-web-apps/backend`)

Kept in sync with the real, already-deployed `day-5/Day-5-Piece-2` source, including all fixes below. The actual running service is the existing `day-5-piece-2` Container App — this is a traceability copy, not a second deployment.

### Managed Identity proxy (`day-17/deploy-to-azure-static-web-apps/mi-proxy`) — new this round

A small Node.js HTTP service (`server.js`, no framework dependency beyond `@azure/identity`), deployed as its own Azure Container App with a **system-assigned Managed Identity**:

- `GET/POST /proxy/quotes`, `DELETE /proxy/quotes/{id}` — mirrors the real API's shape, forwards method/query/body verbatim.
- Acquires a token via `DefaultAzureCredential().getToken(scope)` on every request (the Azure SDK handles in-process caching); attaches it as `Authorization: Bearer <token>`; forwards to the real Week-1 API; returns the API's response as-is.
- **The token is never returned to the caller** — only the API's JSON response crosses back out.
- CORS scoped to the real SWA origin via an `ALLOWED_ORIGIN` env var (non-secret).

**Why a Container App and not an Azure Function**: I first built this as an Azure Function (Consumption plan) per the originally-sketched architecture. Both a first attempt (East Asia) and a retry (Central India, after a region-policy rejection in East US) resulted in the Function App's own platform host returning `503` persistently — even the Kudu/SCM management site was down, which is a platform-level symptom, not a code issue (confirmed no useful diagnostic logs were even obtainable because Kudu itself was unreachable). Rather than keep fighting an unreliable host, I switched to a Container App — the exact same hosting technology already proven reliable in this subscription for the real Week-1 API — which worked on the first real attempt. The abandoned Function App and its storage account were deleted (`az functionapp delete`, `az storage account delete`) rather than left as dead resources.

**Deployment**: `Dockerfile` (`node:22-alpine`, `npm install --omit=dev`, `CMD node server.js`), built and pushed to the existing ACR (`crxxb3grez2spz2.azurecr.io/day17/quotesapi-mi-proxy`), deployed via `az containerapp create` using the same two-phase bootstrap pattern the existing infra already uses for `day-5-piece-2` (create with a placeholder public image first, since a brand-new identity can't pull from ACR before it has the `AcrPull` role; grant the role; switch to `--registry-identity system`; then update to the real image).

### Azure AD / Entra configuration — new this round

- **New app registration** `day17-quotesapi-mi` (`az ad app create`, tenant `8d46a076-d093-416d-a57b-8692cde13bf8` — the tenant this session's Azure CLI actually has access to; the tenant/app referenced in the original `appsettings.json` doesn't exist here, confirmed via `az ad app show` returning not-found).
- Application ID URI `api://8d3c6d5c-bcaf-4a54-8fbe-e2d5c6cb2274`, one application-only app role `Quotes.Api.Access`, and `api.requestedAccessTokenVersion: 2` (see the bug writeup — this was missing initially and caused a real failure).
- Service principal created (`az ad sp create`), then the Container App's system-assigned identity was granted the `Quotes.Api.Access` role directly via a Microsoft Graph `POST /servicePrincipals/{id}/appRoleAssignments` call (`az rest`) — no interactive admin-consent flow needed, since this is a direct application-permission grant on an app we own.
- The real Week-1 API's `EntraId__TenantId`/`EntraId__ClientId` were repointed to this new tenant/app via **Container App environment variables only** (`az containerapp update --set-env-vars`) — no source file changed, no secret involved (a tenant id and a client id are public identifiers, not credentials).

### SWA configuration and CI/CD (unchanged from the prior round)

- `az staticwebapp create` (Free tier, East Asia).
- `.github/workflows/day17-azure-static-web-apps.yml`: install, test, build, secret-scan, deploy via `Azure/static-web-apps-deploy@v1` using the deployment token secret (the only secret in the whole pipeline — a write-only static-file deployment credential, not an Azure AD client secret or API key).

## Managed Identity — the real, verified architecture

```
Angular (browser)  →  day17-mi-proxy (Container App, system-assigned MI)
                          │ DefaultAzureCredential.getToken(...) — server-side only
                          ▼
                       day-5-piece-2 (real Week-1 API)
                          │ validates: issuer = Azure AD v2.0, audience = app's
                          │ client id, requires "Quotes.Api.Access" app role
                          ▼
                       real response (quote created/read/deleted)
```

Verified with a real HTTP request/response, not simulated — see `verification-log.md` and `evidence/managed-identity-verification.txt` for the full transcript. The definitive proof: a quote created through the proxy has `userId` equal to the Managed Identity's own Azure AD object id (`e55a5f96-ae6a-4900-b38c-a97b6758717a`) — the real API wrote that value from the token's validated `sub` claim, which the proxy has no way to fake.

## Three real bugs found and fixed (see verification-log.md for full detail)

1. **CORS** (prior round): the live API had no CORS policy at all.
2. **Missing EF Core migration** (prior round): login returned a real `500` because `RefreshTokens` had no migration.
3. **Wrong claim-type assumption in the authorization policy** (this round, found while implementing Managed Identity): assumed the JWT's `roles` claim would need `ClaimTypes.Role`/`IsInRole()` to be checked (a common ASP.NET Core JWT convention), then had to correct that assumption a second time when it still failed — see verification-log.md for the full sequence (also documents a related real finding: an app registration's `api.requestedAccessTokenVersion` must be explicitly set to `2`, and Azure's Managed Identity token endpoint caches a token per exact resource-string for its full lifetime independent of application restarts, which delayed observing the fix).

## Custom domain — blocked, not fabricated

No domain is available (checked the repo, the SWA resource, and the subscription's DNS zones — none exist). Documented in `deployment-brief.md` exactly what's needed from you and the exact Azure command to run once you have one. Everything else in this deliverable is complete independent of this.

## Files changed

- `day-17/deploy-to-azure-static-web-apps/` — `deployment-brief.md`, `agent-output.md`, `verification-log.md` (updated), `frontend/` (sign-in/out control), `backend/` (synced), `mi-proxy/` (new), `evidence/` (new MI verification transcript + final Lighthouse run).
- `day-5/Day-5-Piece-2/Extensions/InfrastructureExtensions.cs` — authorization policy now accepts the Managed Identity's app role claim as well as the existing user-JWT scope claim (both claim-type spellings checked, per the bug writeup).
- Live Azure configuration only (no source/git changes): `day-5-piece-2` Container App's `EntraId__TenantId`/`EntraId__ClientId` env vars repointed to the new app registration.
- New live Azure resources: Azure AD app registration `day17-quotesapi-mi` + its service principal + app role assignment; Container App `day17-mi-proxy` (resource group `rg-day17-swa`) with system-assigned Managed Identity.
