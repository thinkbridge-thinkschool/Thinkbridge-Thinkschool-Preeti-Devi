import { Environment } from './environment.model';

/**
 * Development configuration.
 *
 * Both URLs stay relative so `ng serve` proxies them to the local API
 * (proxy.conf.json -> http://localhost:5263, the port in the backend's
 * Properties/launchSettings.json) and the browser never makes a cross-origin
 * request. Nothing about the deployed topology leaks into local development,
 * and the Managed Identity proxy is not needed to run the app locally.
 */
export const environment: Environment = {
  production: false,

  /** Week-1 QuotesApi, including the `/api` segment, no trailing slash. Serves /auth/login. */
  apiBaseUrl: '/api',

  /**
   * Where quote reads/writes go. Locally this is the same API; in production it
   * is the Managed Identity proxy instead — see environment.production.ts.
   */
  quotesBaseUrl: '/api',
};
