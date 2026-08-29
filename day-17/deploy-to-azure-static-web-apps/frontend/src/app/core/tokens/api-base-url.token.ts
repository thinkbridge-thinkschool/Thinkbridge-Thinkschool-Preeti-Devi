import { InjectionToken, inject } from '@angular/core';

/**
 * Base URL of the Week-1 QuotesApi, including the `/api` segment, no trailing slash.
 * This is what /auth/login is called against.
 *
 * The default factory keeps the token usable in tests and in any injector that does
 * not override it; app.config.ts provides the real value from `environment`.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => 'http://localhost:5000/api',
});

/**
 * Base URL that quote reads/writes go to.
 *
 * In development this is the API itself. In production it is the Managed Identity
 * proxy (`.../proxy`), which mints an Entra token from its own system-assigned
 * identity server-side and forwards the call — see mi-proxy/server.js.
 *
 * It defaults to API_BASE_URL so every existing test and injector that only knows
 * about API_BASE_URL keeps working unchanged.
 */
export const QUOTES_BASE_URL = new InjectionToken<string>('QUOTES_BASE_URL', {
  providedIn: 'root',
  factory: () => inject(API_BASE_URL),
});

/**
 * URL prefixes the auth interceptor must NOT attach the user's Bearer token to.
 *
 * The Managed Identity proxy carries its own identity and ignores an inbound
 * Authorization header — and it only allows `Content-Type` in its CORS
 * `Access-Control-Allow-Headers`, so sending one would fail the preflight outright
 * rather than being harmlessly dropped.
 */
export const NO_AUTH_HEADER_PREFIXES = new InjectionToken<readonly string[]>(
  'NO_AUTH_HEADER_PREFIXES',
  { providedIn: 'root', factory: () => [] }
);
