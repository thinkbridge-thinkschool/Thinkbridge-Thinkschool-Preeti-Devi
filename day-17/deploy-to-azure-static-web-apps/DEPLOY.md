# Day 17 — deployment runbook

Everything below is either scripted or a one-time setup step. No step creates, reads, or
stores a client secret, a certificate, or a connection string.

## What already exists

| Resource | Name | Resource group |
|---|---|---|
| Static Web App (Free) | `quotes-store-day17` | `rg-day17-swa` |
| Container App — MI proxy | `day17-mi-proxy` | `rg-day17-swa` |
| Container App — Week-1 API | `day-5-piece-2` | `rg-day5-piece2-azure` |
| Container Apps environment | `thinkschool-env` | `thinkschool-rg` |
| Container registry | `crxxb3grez2spz2` | `rg-day5-piece2-azure` |

Subscription `ac177eb4-4211-4f5d-af55-555a3fbed197` ("Azure for Students"), tenant
`8d46a076-d093-416d-a57b-8692cde13bf8`, region `centralindia`. All of it is in
`scripts/env.sh` and every value can be overridden from the environment:

```bash
RG_DAY17=my-rg SWA_NAME=my-swa ./scripts/deploy.sh
```

## Prerequisites

- Azure CLI, signed in: `az login`
- .NET 10 SDK, Node 22+, Docker (for `az containerapp up --source`)

## Routine deploy

```bash
./scripts/deploy.sh                # backend, then proxy, then frontend
./scripts/deploy.sh frontend       # or just one tier
./scripts/verify.sh | tee evidence/verification-run.txt
```

> **`deploy.sh backend` redeploys the live Week-1 API.** `backend/` here is a copy kept in
> step with `day-5/Day-5-Piece-2` for traceability, and `day-5-piece-2` is a real Container
> App that Day 5 and Day 16 both point at. Run `./scripts/deploy.sh frontend` on its own
> unless you actually intend to ship the API too.

What `deploy.sh` does, in order:

1. **backend** — `az containerapp up --source backend/` builds the Dockerfile in ACR (no
   local registry push, no registry credential on the machine), then sets four plain env
   vars: `EntraId__TenantId`, `EntraId__ClientId`, and the two `Cors__AllowedOrigins__*`
   entries. They are set as env vars and deliberately *not* as Container App secrets — a
   tenant GUID, an app's client id and a public hostname identify who to ask; none of them
   proves who you are.
2. **proxy** — same build path for `mi-proxy/`, then `az containerapp identity assign
   --system-assigned`, then `WEEK1_API_BASE_URL`, `MI_TOKEN_SCOPE` and `ALLOWED_ORIGIN`.
   It prints the identity's `principalId`; the role grant below is a one-time step per
   identity.
3. **frontend** — stamps the resolved API URL into
   `frontend/src/environments/environment.production.ts` (`scripts/set-api-url.mjs`),
   runs `npm ci` and a production build, fetches the Static Web Apps deployment token,
   and uploads `dist/quotes-store-app/browser`. The token is fetched, used and discarded
   inside that shell — never written to a file, never echoed.

Hostnames are read back from Azure by `scripts/resolve-urls.sh` on every run, so nothing
in the deploy path depends on a URL someone typed months ago.

## One-time setup

### The Entra app registration

The API is represented by app registration `day17-quotesapi-mi`, client id
`8d3c6d5c-bcaf-4a54-8fbe-e2d5c6cb2274`, with an app role `Quotes.Api.Access`
(`allowedMemberTypes: ["Application"]`) and `requestedAccessTokenVersion: 2` — v2 is
required for a v2.0-format issuer, which is what the API's `Entra` scheme validates
against.

### Granting the role to the managed identity

This is a direct application-permission grant on your own app registration, so it needs
no admin-consent flow.

```bash
source scripts/env.sh

PRINCIPAL_ID=$(az containerapp show -n "$APP_PROXY" -g "$RG_DAY17" \
  --query "identity.principalId" -o tsv)

API_SP_ID=$(az ad sp show --id "$ENTRA_API_CLIENT_ID" --query id -o tsv)
ROLE_ID=$(az ad sp show --id "$ENTRA_API_CLIENT_ID" \
  --query "appRoles[?value=='$MI_APP_ROLE'].id | [0]" -o tsv)

az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$PRINCIPAL_ID/appRoleAssignments" \
  --headers 'Content-Type=application/json' \
  --body "{\"principalId\":\"$PRINCIPAL_ID\",\"resourceId\":\"$API_SP_ID\",\"appRoleId\":\"$ROLE_ID\"}"
```

Confirm it took:

```bash
az rest --method GET \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$PRINCIPAL_ID/appRoleAssignments" \
  --query "value[].appRoleId" -o tsv
```

### CI/CD

The workflow lives at [`.github/workflows/day17-ci.yml`](../../.github/workflows/day17-ci.yml)
in the repository root, because that is the only place GitHub Actions looks.

**CI** — frontend test and production build, backend build and publish, and a `bash -n`
parse check over every script — runs on any push or pull request touching `day-17/**`.
It needs no secrets, so it works unchanged on a fork or on a branch that is never merged.

**CD is on.** It is driven by the repository secret
`AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17`, so **every push to
`day17-azure-static-web-apps` that touches `day-17/**` redeploys the live site.** To turn
deployment off again without touching the workflow, delete that secret — the job then
reports `skipped, no token` and stays green rather than failing, and CI carries on:

```bash
gh secret delete AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17
```

To refresh or re-create it, pipe the token straight from Azure into `gh` rather than
passing it as `--body`, so the value never lands in a shell history or a process listing:

```bash
az staticwebapp secrets list -n quotes-store-day17 -g rg-day17-swa \
  --query "properties.apiKey" -o tsv \
  | gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN_QUOTES_STORE_DAY17
```

That token authorises uploads to this one Static Web App and nothing else. It is not an
Azure credential and grants no access to the API, the proxy, or the subscription. The
Static Web App itself has `provider: SwaCli` and no linked repository, so this workflow
is the only thing that deploys it.

Two deliberate limits on the deploy job: it never runs from a `pull_request` (a PR build
runs the head branch's code, so deploying it would let any PR overwrite the live site),
and it uploads the artifact the frontend job already tested rather than rebuilding — so
the bytes that ship are the bytes that passed.

`scripts/verify.sh` is not part of CI. It authenticates to Azure with `az`, and wiring
that up would mean giving the workflow subscription credentials — the opposite of what
this day is about. Run it locally after a deploy.

## Verifying

`./scripts/verify.sh` runs 19 checks against the live services and is read-only apart
from one quote it creates through the proxy and deletes again. The check worth
understanding is this one:

> `quote owner == the MI's principal id`

The API sets a quote's `userId` server-side from the validated token's `sub` claim
(`QuoteEndpointExtensions.cs`). An app-only Entra token has no user, so its `sub` *is* the
managed identity's own object id. The proxy cannot fabricate that field — the API writes
it, from a token the API itself validated. That equality is the API attesting that a real
Entra-issued managed-identity token reached it.

The zero-secret checks are the other half:

```bash
az containerapp secret list -n day17-mi-proxy  -g rg-day17-swa            # empty
az containerapp secret list -n day-5-piece-2   -g rg-day5-piece2-azure    # empty
az staticwebapp appsettings list -n quotes-store-day17 -g rg-day17-swa    # {}
```

## Custom domain

Not executed — the Azure-generated hostname is used by choice. For reference, on the Free
plan a custom domain is:

```bash
az staticwebapp hostname set -n quotes-store-day17 -g rg-day17-swa \
  --hostname www.example.com
```

Then add the CNAME (or, for an apex domain, the TXT validation record Azure prints)
at the registrar. Static Web Apps provisions and renews the certificate itself; there is
no certificate to store, which is the same principle as the rest of this day.

## Rollback

Container Apps keeps revisions:

```bash
az containerapp revision list -n day-5-piece-2 -g rg-day5-piece2-azure -o table
az containerapp revision activate -n day-5-piece-2 -g rg-day5-piece2-azure \
  --revision <previous-revision-name>
```

The Static Web App has no revision history on the Free plan — roll back by redeploying
the previous commit:

```bash
git checkout <previous-sha> -- day-17/deploy-to-azure-static-web-apps/frontend
./scripts/deploy.sh frontend
```

## Known gaps

- **SQLite on a container filesystem.** The API writes `quotes.db` under the path in
  `ConnectionStrings:Quotes` (default `/tmp/quotes.db`), which does not survive a
  revision restart. Accepted here: Day 17 is about the deployment and the identity
  wiring, not durability. `verify.sh` treats an empty list as a valid empty state.
- **`index.html` cache headers.** The live Static Web App predates the `routes[]` cache
  rules in `frontend/public/staticwebapp.config.json`, so `verify.sh` reports one failure
  until the next `./scripts/deploy.sh frontend`.
