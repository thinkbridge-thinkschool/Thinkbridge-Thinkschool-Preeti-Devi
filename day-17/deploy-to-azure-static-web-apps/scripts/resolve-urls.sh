# Resolves the live hostnames from Azure. Sourced by deploy.sh and verify.sh so
# neither one hard-codes a URL that a redeploy could change.
#
# Sets: API_URL, PROXY_URL, SWA_URL (all https, no trailing slash).

API_HOST=$(az containerapp show -n "$APP_API" -g "$RG_API" \
  --query "properties.configuration.ingress.fqdn" -o tsv)
PROXY_HOST=$(az containerapp show -n "$APP_PROXY" -g "$RG_DAY17" \
  --query "properties.configuration.ingress.fqdn" -o tsv)
SWA_HOST=$(az staticwebapp show -n "$SWA_NAME" -g "$RG_DAY17" \
  --query "defaultHostname" -o tsv)

API_URL="https://${API_HOST}"
PROXY_URL="https://${PROXY_HOST}"
SWA_URL="https://${SWA_HOST}"
