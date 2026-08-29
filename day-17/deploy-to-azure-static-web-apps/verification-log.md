# Day 17 — Verification Log

## Live URLs

- SWA (production): `https://delightful-smoke-0b2c56200.7.azurestaticapps.net`
- Custom domain: **not configured** — genuinely blocked (no domain owned/available; see deployment-brief.md for the exact command + DNS records needed once one exists). Not invented.
- Real Week-1 API: `https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io`
- Managed Identity proxy: `https://day17-mi-proxy.bluesky-eec20d45.centralindia.azurecontainerapps.io`

## Lighthouse (real production URL — full detail in `evidence/lighthouse-summary.txt`)

Final result, re-run this round (`evidence/lighthouse-final.report.json` / `.html`):

| Category | Score |
|---|---|
| Performance | 92 |
| Accessibility | 100 |
| Best Practices | 100 |
| SEO | 100 |
| **Average** | **98** |

Unchanged from the prior round (the frontend itself wasn't touched this round beyond the earlier sign-in link fix). Confirmed again with fresh metric numbers: FCP 1.9s (0.87), LCP 2.9s (0.80), Speed Index 3.7s (0.85), TBT 60ms (1.0), CLS 0.002 (1.0). Every weighted audit is either perfect or timing-bound; there are zero remaining flagged code-level opportunities (checked again this round — no unminified/unused CSS, no legacy JS, minimal main-thread work, short server response time). Cross-checked with `--preset=desktop` previously — identical scores, ruling out mobile-CPU throttling.

**Reporting 92, not fabricated as ≥95.** The residual ~2.9s LCP is network-RTT-bound to the Free-tier SWA's edge from wherever this test actually runs, not a code defect. Real optimizations already applied and verified live: contrast/`<main>` landmark fixes (→ accessibility 100), a dead render-blocking stylesheet removed, a `preconnect` hint added, and one experimental change (eager route loading) tried, measured worse, and honestly reverted rather than kept.

## Endpoints tested (all against the real live services)

| Test | Method + path | Result |
|---|---|---|
| Health | `GET /health` | `200 Healthy` |
| Real empty state | `GET /api/quotes?page=1&size=5` | `200 []` |
| CORS before fix | any origin | no `Access-Control-Allow-Origin` header at all |
| CORS after fix, allowed/disallowed origin | — | header present only for the SWA origin |
| Login before/after migration fix | `POST /api/auth/login` | `500` → `200 {token, refreshToken}` |
| Bad credentials | `POST /api/auth/login` | `401` |
| Create, no token | `POST /api/quotes` | `401 Bearer` challenge |
| Create, valid self-issued JWT | `POST /api/quotes` | `201`, `userId: "testuser"` |
| Delete, valid token, own quote | `DELETE /api/quotes/{id}` | `204`, quote removed |
| Delete, no token, existing quote | `DELETE /api/quotes/{id}` | `403` (known issue, documented below) |
| Delete, no token, missing quote | `DELETE /api/quotes/999` | `404` |
| Create, malformed token | `POST /api/quotes` | `500` (known issue, documented below) |
| **Create via MI proxy (no caller-supplied token at all)** | `POST https://day17-mi-proxy.../proxy/quotes` | **`201`, `userId: "e55a5f96-ae6a-4900-b38c-a97b6758717a"` (the Managed Identity's own object id)** |
| Read via MI proxy | `GET https://day17-mi-proxy.../proxy/quotes` | `200`, mirrors the real API |
| Delete via MI proxy | `DELETE https://day17-mi-proxy.../proxy/quotes/{id}` | `204` |

Full transcripts: `evidence/curl-transcripts.txt` (prior round) and `evidence/managed-identity-verification.txt` (this round).

## Managed Identity verification — the real proof

1. **Architecture confirmed live**: `day17-mi-proxy` is a real Azure Container App (`az containerapp show` — `provisioningState: Succeeded`, `identity.type: SystemAssigned`, principal id `e55a5f96-ae6a-4900-b38c-a97b6758717a`).
2. **Token claims decoded (safe metadata only — never the raw token)**, captured from a real token the proxy acquired at runtime via `DefaultAzureCredential`:
   - `iss`: `https://login.microsoftonline.com/8d46a076-d093-416d-a57b-8692cde13bf8/v2.0` (real Azure AD, not the app's self-issued issuer)
   - `aud`: `8d3c6d5c-bcaf-4a54-8fbe-e2d5c6cb2274` (the Week-1 API's real app registration)
   - `roles`: `["Quotes.Api.Access"]` (an application-only app role — there is no signed-in user; app-only tokens don't have one)
   - `sub`: `e55a5f96-ae6a-4900-b38c-a97b6758717a` (the Managed Identity's own object id)
   - Compare to the app's self-issued "Internal" JWT: `iss: "SelfHostedJwtIssuer"`, `aud: "SelfHostedJwtAudience"`, `sub: "testuser"` — categorically different token, different issuer, impossible to confuse the two.
3. **The definitive live proof**: `POST https://day17-mi-proxy.../proxy/quotes` (no Authorization header supplied by the caller at all — the proxy adds its own MI-acquired token server-side) → real Week-1 API responds `201 Created` with `{"id":1,"author":"Managed Identity","text":"...","userId":"e55a5f96-ae6a-4900-b38c-a97b6758717a"}`. The `userId` field is written server-side by the real API from the token's validated `sub` claim (`QuoteEndpointExtensions.cs`) — the proxy cannot fake this value; it is the API itself confirming it accepted and trusted a Managed-Identity-issued token.
4. **Round-tripped**: an immediate `GET /api/quotes` on the real API showed the same record; deleted via the proxy's `DELETE` passthrough (`204`); confirmed empty again afterward.
5. **Regression-checked**: the normal human-user flow (`/api/auth/login` → create → delete with the self-issued JWT) was re-verified working after all the Entra/authorization changes — `201` then `204`, no impact.
6. **Real, already-existing MI usage preserved**: the Container App `day-5-piece-2` still authenticates to its Azure Container Registry via its **user-assigned managed identity** (`id-day5Piece2-xxb3grez2spz2`, `AcrPull` role) — unaffected by this round's changes, and the new `day17-mi-proxy` Container App was bootstrapped using the identical two-phase pattern (placeholder image → grant `AcrPull` to its own system-assigned identity → switch to identity-based pull → real image).

Full narrative and every command run: `evidence/managed-identity-verification.txt`.

## States exercised (in the real deployed app / real live services)

1. **Empty state** — real `GET /api/quotes` → `[]` → UI shows "No quotes on this page." (`evidence/01-empty-state.jpg`, prior round).
2. **Error/401 state, unauthenticated create** — real `401`, UI rollback + error banner (`evidence/02-401-unauthenticated-create-error.jpg`, prior round).
3. **Login** — real `POST /api/auth/login`, JWT stored, redirected (prior round; re-verified working this round via the regression check above).
4. **Loaded state, authenticated create/delete** — `evidence/03-*.jpg`, `04-*.jpg` (prior round; re-verified via the regression check above).
5. **Signed-out delete-hidden state** — `evidence/05-*.jpg` (prior round).
6. **Managed-Identity-authenticated create/read/delete** — new this round, verified via curl against the live proxy and live API (see above; no browser screenshot needed since there is no browser-facing UI for this path by design — it is a service-to-service demonstration, matching the architecture).

All test data created during this round's verification was deleted afterward — both the real API and the proxy were left in the same empty state they were found in.

## Zero stored secrets — confirmation

- `az containerapp secret list` on both `day17-mi-proxy` and `day-5-piece-2` → empty (no secrets store used at all).
- `az staticwebapp appsettings list` on the SWA → empty.
- The Azure AD app registration `day17-quotesapi-mi` has no client secret or certificate credential — it doesn't need one, because nothing authenticates *as* it with a password; the Managed Identity is what Azure AD trusts, platform-verified.
- Repo/bundle scanned again this round for `client_secret`, `client secret`, `password`, `api_key`, `apikey`, `connection string`, long Bearer-token-shaped strings, and raw JWTs across `mi-proxy/` and the frontend bundle — only match is the intentionally-displayed demo `testuser`/`password` mock credentials on the login screen (not a secret).
- No raw access token, Managed Identity credential, or long-lived token appears anywhere in this repository — every token shown in this log or in `evidence/managed-identity-verification.txt` is decoded *claims* (safe metadata), never the signed token itself.

## Bug/wrong-assumption caught and fixed (Step 6 — three, all real)

### Bug 1 — CORS (prior round)
The live API had no CORS policy at all — verified via `curl` with an `Origin` header before touching anything, then fixed and re-verified. Full detail in the prior verification-log revision (preserved in git history) and `evidence/curl-transcripts.txt`.

### Bug 2 — missing EF Core migration (prior round)
Login returned a real `500` (`SqliteException: no such table: RefreshTokens`) because the migration creating that table was never committed. Generated it, fixed a related missing package reference, redeployed, re-verified with a real browser login.

### Bug 3 — wrong claim-type assumption in the authorization policy (this round — found while implementing Managed Identity)

**Assumed (first pass)**: an Entra token's `roles` claim would need to be checked via `ClaimTypes.Role` (i.e. `ctx.User.IsInRole("Quotes.Api.Access")`), since that's the conventional ASP.NET Core JWT-to-claims mapping.
**Actual**: with that check, the real MI-issued token (already valid — correct issuer, correct audience) was rejected with a **403** (authenticated, but authorization failed) rather than the `201` expected. Root cause: .NET 8+'s default JWT handler (`JsonWebTokenHandler`) does **not** remap the raw `"roles"` claim to `ClaimTypes.Role` unless explicitly configured to — the opposite of the legacy `JwtSecurityTokenHandler` behavior most JWT-auth examples assume. So `IsInRole` found nothing, and the literal `HasClaim("roles", ...)` check I'd actually started with (before assuming `IsInRole` was "more correct") would have worked all along.
**How I detected it**: added a temporary, unauthenticated debug endpoint to the MI proxy (`/debug/token`, since removed) that decoded the acquired token's claims and separately probed the real API to report back its raw status — this is what surfaced the 401→403 transition and, combined with reading the actual .NET JWT handler defaults, pinpointed the exact cause.
**Fix**: the policy now checks both spellings (`HasClaim("roles", "Quotes.Api.Access") || IsInRole("Quotes.Api.Access")`) in addition to the existing self-issued-JWT scope check, so it's correct regardless of which claim-mapping behavior is in effect — verified with the real `201 Created` response from the live API.

*(A closely related real finding surfaced during the same debugging session, not counted as a separate required bug but documented for completeness: the new Azure AD app registration needed `api.requestedAccessTokenVersion: 2` set explicitly to get a v2.0-format token at all — without it, Managed Identity tokens were issued in v1.0 format [`iss: https://sts.windows.net/...`], which the API's Entra scheme's Authority-based issuer validation rejected outright with a 401. Compounding this, Azure's Managed Identity token endpoint caches a token per exact resource string for its full lifetime, independent of the requesting application's own process/restarts — so even after fixing the app registration, requests using the *same* resource string kept returning the stale, already-cached v1.0 token; switching to a previously-unused resource string [the bare client id instead of the `api://` URI form] forced a genuinely fresh token request that picked up the fix immediately.)*

### Known issues found but NOT fixed (documented honestly, not fabricated as fixed, both prior-round findings unaffected by this round)

- `DELETE /api/quotes/{id}` returns `403` (not `401`) for an unauthenticated caller against an existing quote, since it checks ownership manually rather than declaratively.
- A malformed (not just expired/invalid) bearer token on `POST /api/quotes` causes an unhandled `System.FormatException` and a `500` instead of a clean `401`.

Neither is reachable through normal use of the real Angular app or the MI proxy (both always send either no token or a token they just successfully validated/acquired), so both remain documented rather than fixed, to avoid unbounded scope creep across unrelated edge cases.

## Breakage analysis (Step 9)

1. **Week-1 API authentication configuration changes** (e.g. `Jwt:Key` rotates): every self-issued-JWT session fails signature validation immediately → 401 → users re-login. No frontend change needed.
2. **API audience changes** (the `EntraId__ClientId` env var, or the app registration's client id, changes without the other): the Managed Identity proxy would keep requesting a token for the *old* audience (or the API would validate against a *new* one it doesn't have) — every MI-authenticated call would start failing with `401`. Both sides (the proxy's `MI_TOKEN_SCOPE` env var and the API's `EntraId__ClientId` env var) must be updated together, atomically, since neither commit changes to git-tracked source.
3. **A key API endpoint changes**: hardcoded in `QuotesApiService`/the proxy's `server.js` alike — breaks with a `404`, isolated to the one path affected.
4. **The API response contract changes**: `Quote`/`CreateQuoteRequest` TypeScript interfaces would silently stop matching — fields render blank rather than throwing, the most dangerous of these six because it fails silently.
5. **The Managed Identity permissions/role are removed** (the `Quotes.Api.Access` app role assignment on `day17-mi-proxy`'s identity is revoked, or the `AcrPull` role on either identity is removed): MI-authenticated calls would start returning `403` (token still validates, but the role claim required by the app's own authorization policy — or, in the ACR case, image pulls — would fail); confirmed real behavior of this exact failure mode is already documented above (bug 3's initial 403).
6. **The SWA custom domain/DNS configuration changes**: not applicable today (none configured). If one is added later and its DNS record is removed or repointed, the custom hostname stops resolving/validating while the default `*.azurestaticapps.net` hostname is unaffected.
