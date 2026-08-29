# Day 17 — Deployment Brief

All values below were discovered by inspecting the actual repository and querying the actual live Azure subscription (`az` CLI, logged in as the real "Azure for Students" account) — none are invented.

## Target (live)

```
https://delightful-smoke-0b2c56200.7.azurestaticapps.net
```

Azure Static Web App `quotes-store-day17`, resource group `rg-day17-swa`, Free tier, region East Asia, subscription `ac177eb4-4211-4f5d-af55-555a3fbed197` ("Azure for Students").

## Custom domain

**None.** No custom domain was found referenced anywhere in the repository, and none was supplied. The task explicitly said not to invent DNS records, so this step is intentionally skipped — the app is served on the default `*.azurestaticapps.net` hostname above, which already has a valid, automatically-provisioned TLS certificate.

## Real Week-1 API

```
https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io
```

Azure Container App `day-5-piece-2`, resource group `rg-day5-piece2-azure`, region Central India, same subscription. Source: `day-5/Day-5-Piece-2` (a .NET 10 minimal API, "QuotesApi"). Confirmed live:

- `GET /health` → `200 Healthy`
- `GET /api/quotes?page=1&size=5` → `200 []`

## Endpoints actually used by the frontend

| Method | Path | Auth | Request body | Response |
|---|---|---|---|---|
| GET | `/api/quotes?page={page}&size={size}` | anonymous | — | `Quote[]` |
| POST | `/api/quotes` | Bearer JWT, `quotes.write` scope | `{ author, text }` | `Quote` (id, author, text, userId) |
| DELETE | `/api/quotes/{id:int}` | Bearer JWT + resource ownership | — | 204 / 404 / 403 (see verification-log.md for the exact status matrix) |
| POST | `/api/auth/login` | none (this endpoint issues the session) | `{ username, password }` | `{ token, refreshToken }` |
| POST | `/api/auth/refresh` | none (refresh token in body) | `{ token, refreshToken }` | `{ token, refreshToken }` |

Demo login is a hardcoded mock check server-side (`testuser` / `password`) — intentionally shown on the login screen, not a real account system, and not a secret.

## Authentication model — what's real here

The API supports two JWT validation schemes, selected per-request by the token's issuer claim:

1. **"Internal"** — a self-issued, symmetric (HS256) JWT minted by `/api/auth/login` from a server-side signing key. This is what the Angular app actually uses. It requires **no client secret anywhere in the browser** — the user supplies a username/password once, gets back a short-lived (5-minute) access token + a refresh token, and the app attaches it as `Authorization: Bearer <token>` on subsequent write calls. No OAuth client id/secret exists in this flow at all.
2. **"Entra"** — validates Azure AD (Entra ID) v2.0 tokens against a `TenantId`/`ClientId` configured in `appsettings.json`. **This path is not currently usable**: that specific Azure AD app registration does not exist / is not accessible from the Azure tenant this deployment actually has CLI access to (verified with `az ad app show` — not found). See `agent-output.md` for the full explanation of why the "SWA Managed Identity → Entra-protected API" pattern from the reference example could not be honestly wired up here, and what the real Managed Identity story in this system is instead.

## Managed Identity / no client secret

- **Real, already-verified Managed Identity usage**: the Container App `day-5-piece-2` authenticates to its Azure Container Registry using a **user-assigned managed identity** (`id-day5Piece2-xxb3grez2spz2`, role `AcrPull`) — zero registry credentials are stored anywhere in the app or its infrastructure config.
- **No client secret exists anywhere in this system**: not in the Angular bundle, not in `staticwebapp.config.json`, not in the GitHub Actions workflow, not in any Static Web Apps application setting, not in the Container App's environment variables.
- The only credential-shaped value in the whole pipeline is a **Static Web Apps deployment token** — a write-only, revocable token scoped to pushing static files to this one SWA, stored as a GitHub Actions secret (`AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17`) and never displayed, printed, or committed. It is not an Azure AD client secret and grants no access to the Week-1 API or any data.
