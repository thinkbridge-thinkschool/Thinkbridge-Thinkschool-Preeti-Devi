#!/usr/bin/env bash
#
# Day 17 — deploy all three tiers.
#
#   1. backend/    -> Container App  day-5-piece-2   (the real Week-1 API)
#   2. mi-proxy/   -> Container App  day17-mi-proxy  (system-assigned Managed Identity)
#   3. frontend/   -> Static Web App quotes-store-day17
#
# Nothing here creates or reads a client secret, a certificate, or a connection
# string. The only credential involved anywhere is the proxy's system-assigned
# Managed Identity, which the platform mints at runtime and which is never a value
# this script (or any file in this repo) can see.
#
# Prerequisites: az CLI logged in (`az login`), node 22+, .NET 10 SDK, Docker.
# Usage: ./scripts/deploy.sh [backend|proxy|frontend|all]   (default: all)

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(dirname "$HERE")"
# shellcheck source=env.sh
source "$HERE/env.sh"

TARGET="${1:-all}"

az account set --subscription "$SUBSCRIPTION_ID"

deploy_backend() {
  echo "==> backend -> $APP_API ($RG_API)"
  # --source builds the Dockerfile in ACR, so no local registry push and no
  # registry credential on this machine.
  az containerapp up \
    --name "$APP_API" \
    --resource-group "$RG_API" \
    --environment "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$ACA_ENV_RG/providers/Microsoft.App/managedEnvironments/$ACA_ENV" \
    --source "$ROOT/backend" \
    --target-port 8080 \
    --ingress external

  # shellcheck source=resolve-urls.sh
  source "$HERE/resolve-urls.sh"

  # The API validates Entra tokens against the app registration the Managed Identity
  # was granted a role on, and allows the Static Web App's origin through CORS.
  # All four values are public identifiers, set as plain env vars — deliberately not
  # as Container App secrets, because none of them is one.
  az containerapp update \
    --name "$APP_API" --resource-group "$RG_API" \
    --set-env-vars \
      "EntraId__TenantId=$ENTRA_TENANT_ID" \
      "EntraId__ClientId=$ENTRA_API_CLIENT_ID" \
      "Cors__AllowedOrigins__0=$SWA_URL" \
      "Cors__AllowedOrigins__1=http://localhost:4201" \
    >/dev/null
  echo "    API:   $API_URL"
}

deploy_proxy() {
  echo "==> mi-proxy -> $APP_PROXY ($RG_DAY17)"
  # shellcheck source=resolve-urls.sh
  source "$HERE/resolve-urls.sh"

  az containerapp up \
    --name "$APP_PROXY" \
    --resource-group "$RG_DAY17" \
    --environment "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$ACA_ENV_RG/providers/Microsoft.App/managedEnvironments/$ACA_ENV" \
    --source "$ROOT/mi-proxy" \
    --target-port 8080 \
    --ingress external

  az containerapp identity assign \
    --name "$APP_PROXY" --resource-group "$RG_DAY17" --system-assigned >/dev/null

  az containerapp update \
    --name "$APP_PROXY" --resource-group "$RG_DAY17" \
    --set-env-vars \
      "WEEK1_API_BASE_URL=$API_URL" \
      "MI_TOKEN_SCOPE=$ENTRA_API_CLIENT_ID/.default" \
      "ALLOWED_ORIGIN=$SWA_URL" \
    >/dev/null

  PRINCIPAL_ID=$(az containerapp show -n "$APP_PROXY" -g "$RG_DAY17" \
    --query "identity.principalId" -o tsv)
  echo "    proxy: $PROXY_URL"
  echo "    identity principalId: $PRINCIPAL_ID"
  echo "    (grant it the $MI_APP_ROLE app role once — see DEPLOY.md 'Granting the role')"
}

deploy_frontend() {
  echo "==> frontend -> $SWA_NAME ($RG_DAY17)"
  # shellcheck source=resolve-urls.sh
  source "$HERE/resolve-urls.sh"

  node "$HERE/set-api-url.mjs" "$API_URL/api"

  ( cd "$ROOT/frontend" && npm ci && npx ng build --configuration production )

  # The deployment token is fetched, used, and discarded in this shell. It is never
  # written to a file and never echoed.
  local token
  token=$(az staticwebapp secrets list -n "$SWA_NAME" -g "$RG_DAY17" \
    --query "properties.apiKey" -o tsv)

  npx --yes @azure/static-web-apps-cli deploy \
    "$ROOT/frontend/dist/quotes-store-app/browser" \
    --deployment-token "$token" \
    --env production

  echo "    site:  $SWA_URL"
}

case "$TARGET" in
  backend)  deploy_backend ;;
  proxy)    deploy_proxy ;;
  frontend) deploy_frontend ;;
  all)      deploy_backend; deploy_proxy; deploy_frontend ;;
  *) echo "usage: $0 [backend|proxy|frontend|all]" >&2; exit 2 ;;
esac

echo
echo "Done. Verify with: ./scripts/verify.sh"
