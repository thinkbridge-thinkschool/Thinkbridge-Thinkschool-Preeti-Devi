# Shared, non-secret identifiers for the Day 17 deployment.
#
# Everything here is a name or a public hostname. There is no credential in this file
# and there must never be one: the whole point of Day 17 is that the only identity in
# the system is a system-assigned Managed Identity the platform mints at runtime.
#
# Override any value from the environment, e.g. RG_DAY17=my-rg ./scripts/deploy.sh

: "${SUBSCRIPTION_ID:=ac177eb4-4211-4f5d-af55-555a3fbed197}"
: "${LOCATION:=centralindia}"

# Week-1 API — Day 5 Piece 2, deployed with azd into its own resource group.
: "${RG_API:=rg-day5-piece2-azure}"
: "${APP_API:=day-5-piece-2}"

# Day 17's own resource group: the Static Web App and the Managed Identity proxy.
: "${RG_DAY17:=rg-day17-swa}"
: "${APP_PROXY:=day17-mi-proxy}"
: "${SWA_NAME:=quotes-store-day17}"

# Shared Container Apps environment (lives in thinkschool-rg, reused by both apps).
: "${ACA_ENV_RG:=thinkschool-rg}"
: "${ACA_ENV:=thinkschool-env}"

# Entra app registration that represents the Week-1 API. The Managed Identity is
# granted its Quotes.Api.Access app role; the API validates tokens against it.
# A tenant id and a client id identify who to ask — they are not credentials.
: "${ENTRA_TENANT_ID:=8d46a076-d093-416d-a57b-8692cde13bf8}"
: "${ENTRA_API_CLIENT_ID:=8d3c6d5c-bcaf-4a54-8fbe-e2d5c6cb2274}"
: "${MI_APP_ROLE:=Quotes.Api.Access}"
