# Current-State Screenshots

Captured live against the real backend (`day-5/Day-5-Piece-2`, `http://localhost:5000`) and this app running via `npm start` (`http://localhost:4200`), after all fixes in README sections (8)–(10) — i.e. this is what the app actually looks like *now*, not the earlier state `../01-guard-redirect-unauthenticated.jpg` through `../05-404-not-found-state.jpg` were captured against (those predate the backend swap, the public-quotes change, the filter, and the not-found page — kept as historical record, see README section (7)).

| # | File | Claim it proves |
|---|---|---|
| 1 | `01-quotes-list-public.png` | `/quotes` loads with **no sign-in required** — real data from the backend, "○ Browsing anonymously" shown correctly (not the old always-on "Authenticated" bug), filter bar and "+ New Quote" link both present. |
| 2 | `02-filter-in-the-url.png` | Navigating to `/quotes?q=Seneca` filters the grid client-side to the one matching card, filter input reflects the URL value on load. |
| 3 | `03-detail-route.png` | `/quotes/4` renders a real quote fetched from the backend. Paired network evidence below — the component backing this route is not in the initial bundle. |
| 4 | `04-guard-redirect-with-returnurl.png` | Visiting `/quotes/new` while signed out redirects to `/login` with `returnUrl=/quotes/new` shown in the banner — the guard, still doing real work on the one route that needs it. |
| 5 | `05-invalid-id-not-found-keeps-url.png` | `/quotes/abc` renders the not-found page and echoes back the actual attempted URL (`/quotes/abc`) instead of silently rewriting the address bar — the `canMatch` guard + real 404 page added in section (10). |

## Lazy-chunk evidence for #3

`ng build --configuration development` output (this build, this session):

```
Initial chunk files | Names                  |  Raw size
main.js              | main                   |   7.90 kB
chunk-AEEVR33B.js    | -                      |   1.70 kB
chunk-WS44DCE4.js    | -                      |    349 B
styles.css           | styles                 |     95 B

Lazy chunk files    | Names                  |  Raw size
chunk-7HEAE3W4.js    | quote-detail-component |  19.33 kB   ← not in the initial bundle above
chunk-LYRWQ6DO.js    | quote-list-component   |  27.42 kB
chunk-MHUUHYLY.js    | login-component        |  19.08 kB
chunk-ICYG5HY4.js    | quote-create-component |  15.37 kB
chunk-V5WYRQ4N.js    | not-found-component    |   5.43 kB
```

`quote-detail-component` (`chunk-7HEAE3W4.js`) is absent from every file in the "Initial chunk files" block — it only exists as a lazy chunk, fetched on navigation to `/quotes/:id`, exactly as `app.routes.ts`'s `loadComponent: () => import(...)` declares it.
