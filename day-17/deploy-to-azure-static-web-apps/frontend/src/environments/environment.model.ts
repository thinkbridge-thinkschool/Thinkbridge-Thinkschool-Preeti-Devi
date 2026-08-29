/**
 * The shape both environment files share.
 *
 * It exists so the two files are typed identically rather than as their own literal
 * types. Without it, `as const` narrows each URL to a string literal and TypeScript
 * rejects an ordinary comparison between them as "types have no overlap".
 */
export interface Environment {
  readonly production: boolean;

  /** Week-1 QuotesApi, including the `/api` segment, no trailing slash. Serves /auth/login. */
  readonly apiBaseUrl: string;

  /** Where quote reads/writes go — the API locally, the Managed Identity proxy in production. */
  readonly quotesBaseUrl: string;
}
