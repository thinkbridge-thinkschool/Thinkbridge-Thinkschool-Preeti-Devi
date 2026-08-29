#!/usr/bin/env bash
#
# Day 17 — everything that can be checked without Azure.
#
# Builds both tiers, runs the frontend's unit tests, and boots the API against a
# throwaway SQLite file to confirm it starts, migrates, and answers /health.
#
# Usage: ./scripts/smoke-local.sh

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(dirname "$HERE")"
PORT=5263

# A bare filename, resolved against the app's content root. An absolute path would be a
# POSIX path under Git Bash on Windows, which SQLite on .NET cannot open.
DB_NAME="smoke-test-$$.db"

cleanup() {
  if [ -n "${API_PID:-}" ]; then
    kill "$API_PID" 2>/dev/null || true
    # SQLite keeps the .db/-wal/-shm files locked until the process is actually gone, and
    # on Windows a locked file cannot be deleted at all — so wait for the exit rather
    # than racing it and leaving the throwaway database behind.
    wait "$API_PID" 2>/dev/null || true
  fi
  rm -f "$ROOT/backend/$DB_NAME" "$ROOT/backend/$DB_NAME"-* 2>/dev/null || true
}
trap cleanup EXIT

echo "==> backend build"
dotnet build "$ROOT/backend" -v q --nologo

echo "==> backend boot + migrate + /health"
# A signing key is required and deliberately not defaulted to anything usable.
Jwt__Key="$(head -c 48 /dev/urandom | base64)" \
ConnectionStrings__Quotes="Data Source=$DB_NAME" \
ASPNETCORE_URLS="http://localhost:$PORT" \
  dotnet run --project "$ROOT/backend" --no-build >/dev/null 2>&1 &
API_PID=$!

for _ in $(seq 1 40); do
  if [ "$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/health" || true)" = "200" ]; then
    break
  fi
  sleep 1
done

# `|| true` on every one of these: curl exits non-zero when it cannot connect, and under
# `set -e` that would kill the script before the FAIL line below could be printed.
code=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/health" || true)
[ "$code" = "200" ] || { echo "  FAIL  /health returned '$code'"; exit 1; }
echo "  PASS  /health 200"

quotes=$(curl -s -o /dev/null -w '%{http_code}' "http://localhost:$PORT/api/quotes" || true)
[ "$quotes" = "200" ] || { echo "  FAIL  GET /api/quotes returned '$quotes'"; exit 1; }
echo "  PASS  GET /api/quotes 200 (an empty list is a valid empty state)"

unauth=$(curl -s -o /dev/null -w '%{http_code}' -X POST \
  -H 'Content-Type: application/json' -d '{"author":"a","text":"b"}' \
  "http://localhost:$PORT/api/quotes" || true)
[ "$unauth" = "401" ] || { echo "  FAIL  unauthenticated POST returned '$unauth', expected 401"; exit 1; }
echo "  PASS  unauthenticated POST 401"

echo "==> frontend install, test, production build"
cd "$ROOT/frontend"
[ -d node_modules ] || npm ci
npx ng test
npx ng build --configuration production

echo
echo "Local smoke checks passed."
