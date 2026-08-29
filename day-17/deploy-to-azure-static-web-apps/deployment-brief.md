# Day 17 — Deployment Brief

All values below were discovered by inspecting the actual repository and querying the actual live Azure subscription (`az` CLI, logged in as the real "Azure for Students" account), or created live during this deployment. None are invented.

## Target (live)

```
https://delightful-smoke-0b2c56200.7.azurestaticapps.net
```

Azure Static Web App `quotes-store-day17`, resource group `rg-day17-swa`, Free tier, region East Asia, subscription `ac177eb4-4211-4f5d-af55-555a3fbed197` ("Azure for Students").

## Custom domain

**Not configured — genuinely blocked, not skipped by choice this round.**

Checked exhaustively before concluding this:
- The repository has no domain/DNS reference anywhere (grepped the whole `day-17` tree).
- `az staticwebapp show ... --query customDomains` → `[]`.
- `az network dns zone list` → no DNS zones exist in this subscription at all (no domain is owned/managed here).

**What's needed to complete this**: a real domain name you own, plus one Azure command and one DNS change:

1. You tell me the domain (or subdomain) you want, e.g. `quotes.yourdomain.com`.
2. I run: `az staticwebapp hostname set --name quotes-store-day17 --resource-group rg-day17-swa --hostname quotes.yourdomain.com`
3. Azure returns a validation token. At your DNS provider, you add:
   - `CNAME quotes` → `delightful-smoke-0b2c56200.7.azurestaticapps.net`
   - `TXT _dnsauth.quotes` → `<the validation token Azure returns>`
4. Azure validates and auto-provisions a free TLS certificate — no manual cert work.

Until step 1 happens, this cannot be completed without inventing a domain, which was explicitly disallowed. Everything else in this brief is complete and does not depend on this.

## Real Week-1 API

```
https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io
```

Azure Container App `day-5-piece-2`, resource group `rg-day5-piece2-azure`, region Central India, same subscription. Source: `day-5/Day-5-Piece-2` (a .NET 10 minimal API, "QuotesApi"). Confirmed live:

- `GET /health` → `200 Healthy`
- `GET /api/quotes?page=1&size=5` → `200 []`

## Managed Identity proxy (new this round)

```
https://day17-mi-proxy.bluesky-eec20d45.centralindia.azurecontainerapps.io
```

Azure Container App `day17-mi-proxy`, resource group `rg-day17-swa`, same Container Apps environment (`thinkschool-env`) as the real API — a small Node.js service whose only job is: acquire a Managed-Identity-issued Azure AD token server-side, attach it to the request, forward to the real Week-1 API, return the response. It never returns the token to the caller. See the "Managed Identity architecture" section below and `agent-output.md` for the full implementation.

## Endpoints

| Method | Path | Auth | Request body | Response |
|---|---|---|---|---|
| GET | `/api/quotes?page={page}&size={size}` | anonymous | — | `Quote[]` |
| POST | `/api/quotes` | Bearer JWT: self-issued `quotes.write` scope **or** Managed-Identity token with the `Quotes.Api.Access` app role | `{ author, text }` | `Quote` (id, author, text, userId) |
| DELETE | `/api/quotes/{id:int}` | Bearer JWT + resource ownership | — | 204 / 404 / 403 (see verification-log.md for the exact status matrix) |
| POST | `/api/auth/login` | none (this endpoint issues the session) | `{ username, password }` | `{ token, refreshToken }` |
| POST | `/api/auth/refresh` | none (refresh token in body) | `{ token, refreshToken }` | `{ token, refreshToken }` |
| GET / POST / DELETE | `https://day17-mi-proxy.../proxy/quotes[/{id}]` | none from the caller — the proxy itself authenticates to the real API via Managed Identity | mirrors the real API | mirrors the real API's response verbatim |

Demo login is a hardcoded mock check server-side (`testuser` / `password`) — intentionally shown on the login screen, not a real account system, and not a secret.

## Authentication model

Two ways to reach the write-protected endpoints, both secretless:

1. **Human users (the Angular app's real UX)** — self-issued, symmetric (HS256) JWT minted by `/api/auth/login` from a server-side signing key. The user supplies a username/password once, gets back a short-lived access token + refresh token, and the app attaches it as `Authorization: Bearer <token>`. No OAuth client id/secret exists in this flow.
2. **Service-to-service (Managed Identity)** — the `day17-mi-proxy` Container App's system-assigned Managed Identity acquires a real Azure AD access token for the Week-1 API's app registration and presents it. No credential of any kind is stored to make this happen; Azure AD issues the token to the platform-verified identity.

A browser cannot hold a Managed Identity — that's an Azure-platform-only capability for Azure-hosted compute. So Managed Identity is correctly implemented **server-side, in a dedicated proxy**, not pretended to exist in the browser.

## Managed Identity architecture

```
Angular (browser, on the SWA)
        │  (optional: could call the proxy directly for MI-authenticated actions)
        ▼
day17-mi-proxy (Azure Container App, system-assigned Managed Identity)
        │  DefaultAzureCredential.getToken("<clientId>/.default")
        │  — acquires a real Azure AD access token, server-side only
        ▼
day-5-piece-2 (real Week-1 API, Azure Container App)
        │  validates the token: issuer = Azure AD v2.0, audience = the
        │  app registration's client id, requires the "Quotes.Api.Access"
        │  app role claim
        ▼
SQLite (per-instance, ephemeral — see verification-log.md breakage analysis)
```

- **Azure AD tenant**: `8d46a076-d093-416d-a57b-8692cde13bf8` — the tenant this deployment's Azure CLI session actually has access to. (The tenant/app referenced in the original `appsettings.json` does not exist here — see `agent-output.md`.)
- **App registration**: `day17-quotesapi-mi`, client id `8d3c6d5c-bcaf-4a54-8fbe-e2d5c6cb2274`, `requestedAccessTokenVersion: 2` (required to get a v2.0-issuer token), one application-only app role `Quotes.Api.Access`.
- **Identity used**: the Container App `day17-mi-proxy`'s **system-assigned managed identity** (principal id `e55a5f96-ae6a-4900-b38c-a97b6758717a`), granted the `Quotes.Api.Access` app role directly via a Microsoft Graph `appRoleAssignments` call (no admin-consent flow needed — this is a direct application-permission grant on our own app).
- **How the API validates it**: the existing dual-scheme `"Smart"` policy in `InfrastructureExtensions.cs` already routed any token whose issuer contains `login.microsoftonline.com` to the `"Entra"` JwtBearer scheme (no change needed there); `EntraId__TenantId`/`EntraId__ClientId` were repointed via Container App **environment variables only** (no source change, no secret) to the new tenant/app; the `"can-edit-quotes"` authorization policy was extended to also accept the app role claim (a real bug found and fixed — see verification-log.md).
- **No client secret**: confirmed via `az containerapp secret list` on both Container Apps (empty) and `az staticwebapp appsettings list` on the SWA (empty). The only identifiers stored anywhere are the tenant id, client id, API base URL, and CORS origin — none of these prove identity on their own; only the platform-verified Managed Identity does.
