import { CanMatchFn, UrlSegment } from '@angular/router';

/**
 * Positive integer only — matches the Week-1 API's route constraint
 * (GET /api/quotes/{id:int}). Rejects decimals, negatives, whitespace, and
 * anything a naive parseInt would silently truncate (e.g. "12abc").
 */
export function isValidQuoteId(raw: string): boolean {
  if (!/^\d+$/.test(raw)) return false;
  const value = Number(raw);
  return Number.isSafeInteger(value) && value > 0;
}

/**
 * CanMatch (not CanActivate) — an invalid id means this route never matches
 * at all, so the router falls through to the wildcard/not-found route
 * instead of activating QuoteDetailComponent and cancelling navigation
 * mid-flight. The attempted URL stays in the address bar (no redirect), and
 * zero HTTP requests fire.
 */
export const quoteIdMustBeInteger: CanMatchFn = (_route, segments: UrlSegment[]) => {
  const last = segments[segments.length - 1];
  return last !== undefined && isValidQuoteId(last.path);
};
