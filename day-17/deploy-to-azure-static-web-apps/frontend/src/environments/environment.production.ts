import { Environment } from './environment.model';

/**
 * Production configuration — swapped in by the `fileReplacements` entry in angular.json.
 *
 * These are the hostnames actually deployed (verified against the live resources on
 * 2026-08-29):
 *
 *   Static Web App   https://delightful-smoke-0b2c56200.7.azurestaticapps.net
 *   Week-1 API       day-5-piece-2      (Container App, rg-day5-piece2-azure)
 *   MI proxy         day17-mi-proxy     (Container App, rg-day17-swa)
 *
 * `quotesBaseUrl` deliberately equals `apiBaseUrl`: the shipped bundle calls the API
 * directly for everything, which is why the API carries a CORS policy naming the
 * Static Web App's origin. The Managed Identity path (mi-proxy/) is a separate,
 * server-to-server route — it is what proves an Entra token is minted from a
 * system-assigned identity with no stored secret, and it is exercised directly, not
 * through the browser. See evidence/managed-identity-verification.txt.
 *
 * To route quote traffic through the proxy instead, change `quotesBaseUrl` to
 * 'https://day17-mi-proxy.bluesky-eec20d45.centralindia.azurecontainerapps.io/proxy'.
 * Two things then apply, and both are already handled:
 *   • the proxy exposes only /quotes and /quotes/{id} — /auth/login stays on
 *     apiBaseUrl, which is why these are two settings and not one;
 *   • the proxy allows only Content-Type through CORS preflight, so the user's
 *     Bearer token must not be attached to it — app.config.ts wires
 *     NO_AUTH_HEADER_PREFIXES from exactly this difference.
 *
 * Both are public hostnames, not secrets. There is nothing in this bundle worth
 * extracting, because the credential that opens the API is never in it.
 */
export const environment: Environment = {
  production: true,
  apiBaseUrl:
    'https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io/api',
  quotesBaseUrl:
    'https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io/api',
};
