# Day 16 Piece 1 — Live Verification Log

This verification was run for real: the Angular app (`Code/`, mounted into a disposable Angular 22 zoneless workspace) served on `http://localhost:4200`, talking to a real Week-1 `QuotesApi` (ASP.NET Core Minimal API + EF Core + SQLite) served on `http://localhost:5000` with CORS enabled for the dev origin. Interaction was driven through an actual Chromium browser (Claude in Chrome), not simulated.

## Build verification (lazy loading)

`ng build --configuration development` output:

```
Initial chunk files | Names                  |  Raw size
chunk-OOZVXTDA.js   | -                      |   1.43 MB
main.js             | main                   |   6.06 kB
...
Lazy chunk files    | Names                  |  Raw size
chunk-R3YYDYHJ.js   | quote-detail-component |  20.24 kB
chunk-SB4AY3EF.js   | quote-list-component   |  19.16 kB
chunk-WGTZSYFZ.js   | login-component        |   6.44 kB
```

`QuoteDetailComponent`, `QuoteListComponent`, and `LoginComponent` are each their own chunk, absent from the initial bundle — confirms `loadComponent` code-splitting is real, not just declared.

## Bug found and fixed during live verification

**Bug**: `app.config.ts` used `provideZoneChangeDetection({ eventCoalescing: true })`. The moment the app was actually loaded in a browser, this crashed on bootstrap:

```
RuntimeError: NG0908: In this configuration Angular requires Zone.js
```

**Root cause**: this codebase targets Angular 22 zoneless (confirmed by Day 14's own brief: "Angular 22 zoneless, standalone components"), and the host workspace has no `zone.js` dependency. `provideZoneChangeDetection` is the Zone-based API and requires `zone.js` to be loaded as a polyfill — it was never installed. The agent had generated `app.config.ts` from an older, zone-based Angular pattern instead of the zoneless one this app actually needs. This produced a blank white screen with no visible error to a user — nothing in the six-row verification table in section (3) of this README would have caught it, because that table was written narratively and never actually run in a browser.

**Fix**: swapped `provideZoneChangeDetection({ eventCoalescing: true })` for `provideZonelessChangeDetection()` in `Code/app.config.ts`. After the fix, the app boots and every route/guard/detail scenario below passes for real.

**A second, minor issue** was caught by the production build: `QuoteDetailComponent` imported `RouterLink` but never used it (`NG8113` warning) — the "Back" button uses `(click)="goBack()"`, not a `routerLink`. Removed the unused import.

## Live-verified scenarios (screenshots in this folder)

| # | Scenario | Action | Result | Evidence |
|---|---|---|---|---|
| 1 | Guard redirect (unauthenticated) | Cleared `localStorage`, navigated to `/quotes/7` | Redirected to `/login?returnUrl=%2Fquotes%2F7`. Network tab: **zero** requests to `/api/quotes/7`. | `01-guard-redirect-unauthenticated.jpg` |
| 2 | Guard pass + returnUrl restore | Clicked "Log In & Continue" from the state in row 1 | Token stored, navigated to `/quotes/7`, real `GET http://localhost:5000/api/quotes/7` → 200, rendered "Quote 7 by Author 1". | `02-guard-pass-returnurl-detail.jpg` |
| 3 | Authenticated quote list | Navigated to `/quotes` | Real paginated data from the SQLite-backed API rendered in cards. | `03-quote-list-authenticated.jpg` |
| 4 | Invalid (non-numeric) id | Navigated to `/quotes/abc` | `numberAttribute` transform produced `NaN`; component showed `invalid_id` state; confirmed **zero** network requests. | `04-invalid-id-state.jpg` |
| 5 | Negative id | Navigated to `/quotes/-5` | Same `invalid_id` guard path, zero requests (screenshot not included, behavior identical to row 4). | — |
| 6 | Missing id (404) | Navigated to `/quotes/99999` | Real `GET /api/quotes/99999` → 404 from the backend; component rendered "Quote #99999 was not found in the database." | `05-404-not-found-state.jpg` |
| 7 | View transition wiring | Inspected DOM after navigating to `/quotes/3` | `document.querySelector('.detail-card').style.viewTransitionName === 'quote-card-3'`, matching the name assigned to the same quote's card on the list page — confirmed the pairing the View Transitions API needs to morph between routes. | (DOM check, not a screenshot) |
| 8 | In-app SPA navigation (no page reload) | Clicked a quote card on the list page | Routed to the detail view client-side, view transition ran with no console errors (`window.onerror`/`unhandledrejection` listeners empty). | — |
| 9 | Logout re-arms the guard | Clicked "Log Out" on the list page | `localStorage` token cleared, page reloaded, guard redirected back to `/login?returnUrl=%2Fquotes`. | — |

## What this replaces

Section (3) of `README.md` ("Verification & Defense Log") was written as a narrative table before this app had ever actually been run. It happened to describe the correct *intended* behavior, but the zoneless bug above proves the app could not have passed row 1 of that table as originally written — it would not have rendered at all. This log and the screenshots in this folder are the first real evidence behind those claims.
