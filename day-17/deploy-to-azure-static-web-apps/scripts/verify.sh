#!/usr/bin/env bash
#
# Day 17 — verify the live deployment. Read-only except for one quote it creates
# through the Managed Identity proxy and deletes again.
#
# Every check hits the real, deployed services. Nothing is mocked, and nothing is
# asserted that the output below does not show.
#
# Usage: ./scripts/verify.sh | tee evidence/verification-run.txt

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=env.sh
source "$HERE/env.sh"
az account set --subscription "$SUBSCRIPTION_ID" >/dev/null
# shellcheck source=resolve-urls.sh
source "$HERE/resolve-urls.sh"

PASS=0; FAIL=0

check() { # check <name> <expected> <actual>
  if [ "$2" = "$3" ]; then
    printf '  PASS  %-58s %s\n' "$1" "$3"; PASS=$((PASS+1))
  else
    printf '  FAIL  %-58s expected %s, got %s\n' "$1" "$2" "$3"; FAIL=$((FAIL+1))
  fi
}

status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
header() { curl -s -D - -o /dev/null "$@" | tr -d '\r' | grep -i "^$1:" | head -1 | cut -d' ' -f2-; }

echo "Day 17 verification — $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
echo "  site:  $SWA_URL"
echo "  api:   $API_URL"
echo "  proxy: $PROXY_URL"
echo

echo "1. Reachability"
check "static web app serves the app"        200 "$(status "$SWA_URL/")"
check "client route falls back to index.html" 200 "$(status "$SWA_URL/login")"
check "api health"                            200 "$(status "$API_URL/health")"
check "proxy health"                          200 "$(status "$PROXY_URL/health")"
echo

echo "2. Static Web Apps host configuration (public/staticwebapp.config.json)"
check "CSP present"       "present" "$([ -n "$(header content-security-policy "$SWA_URL/")" ] && echo present || echo missing)"
check "HSTS present"      "present" "$([ -n "$(header strict-transport-security "$SWA_URL/")" ] && echo present || echo missing)"
check "nosniff"           "nosniff" "$(header x-content-type-options "$SWA_URL/")"
# Asserts the routes[] cache rules in public/staticwebapp.config.json. Static Web
# Apps consumes that file at deploy time rather than serving it, so this reflects
# the config of the *last frontend deploy* — it fails until ./scripts/deploy.sh
# frontend has shipped a build containing the restored file.
check "index.html not cached" "no-cache, must-revalidate" "$(header cache-control "$SWA_URL/index.html")"
echo

echo "3. CORS — the browser calls the API cross-origin from the Static Web App"
check "GET /api/quotes allows the SWA origin" "$SWA_URL" \
  "$(header access-control-allow-origin -H "Origin: $SWA_URL" "$API_URL/api/quotes")"
check "login preflight allowed"               204 \
  "$(status -X OPTIONS -H "Origin: $SWA_URL" -H 'Access-Control-Request-Method: POST' \
     -H 'Access-Control-Request-Headers: content-type' "$API_URL/api/auth/login")"
check "an unknown origin is not allowed"      "" \
  "$(header access-control-allow-origin -H 'Origin: https://not-our-site.example' "$API_URL/api/quotes")"
echo

echo "4. Authorization is enforced (no token = no write)"
check "POST /api/quotes without a token" 401 \
  "$(status -X POST -H 'Content-Type: application/json' \
     -d '{"author":"anon","text":"should not be created"}' "$API_URL/api/quotes")"
check "GET /api/quotes is anonymous"     200 "$(status "$API_URL/api/quotes")"
echo

echo "5. Managed Identity — the proxy writes with an Entra token it mints itself"
CREATED=$(curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"author":"Managed Identity","text":"verify.sh round-trip."}' "$PROXY_URL/proxy/quotes")
NEW_ID=$(printf '%s' "$CREATED" | sed -n 's/.*"id":\([0-9]*\).*/\1/p')
OWNER=$(printf '%s' "$CREATED" | sed -n 's/.*"userId":"\([^"]*\)".*/\1/p')
PRINCIPAL_ID=$(az containerapp show -n "$APP_PROXY" -g "$RG_DAY17" --query "identity.principalId" -o tsv)

check "proxy created a quote"                  "created" "$([ -n "$NEW_ID" ] && echo created || echo failed)"
# The API sets userId from the validated token's `sub`. An app-only token's sub IS the
# Managed Identity's object id — so this equality is the API itself attesting that it
# validated a real Entra/MI token. The proxy cannot fake it.
check "quote owner == the MI's principal id"   "$PRINCIPAL_ID" "$OWNER"
check "proxy still has no secrets"             "0" \
  "$(az containerapp secret list -n "$APP_PROXY" -g "$RG_DAY17" -o tsv | wc -l | tr -d ' ')"
check "api still has no secrets"               "0" \
  "$(az containerapp secret list -n "$APP_API" -g "$RG_API" -o tsv | wc -l | tr -d ' ')"
# `-o tsv` on an empty object still prints a blank line, so count the keys instead.
check "static web app has no app settings"     "0" \
  "$(az staticwebapp appsettings list -n "$SWA_NAME" -g "$RG_DAY17" --query "length(keys(properties))" -o tsv)"

if [ -n "$NEW_ID" ]; then
  check "cleanup: DELETE via the proxy" 204 "$(status -X DELETE "$PROXY_URL/proxy/quotes/$NEW_ID")"
fi
echo

echo "-------------------------------------------------------------"
printf '%d passed, %d failed\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ]
