# Day 16 — Piece 2: State Management, Signals First

A small feature's state (a quotes list you can page through, create into, and delete from — each with an optimistic update) modeled with `signal()` + `computed()` in a plain `@Injectable` service — no store library. Directed to Claude Code, verified against the real Week-1 API (`day-5/Day-5-Piece-2`), with one real concurrency bug caught, reproduced, and fixed (§4), and a second real race — a delete resolving while a list refresh is in flight — caught by comparing this design against a peer's independent solution to the same exercise before it ever shipped (§7).

The runnable app is `quotes-store-app/` (`npm start`, serves on `http://localhost:4201` so it can run alongside Day 16 Piece 1's app on `4200` against the same backend). `Code/` is the scoped deliverable — the store and the two things that call it, not a duplicate of Piece 1's routing/guards, which aren't this exercise's subject.

---

## (1) Brief to the Agent

> **Context & Objective**:
> Model the state for the quotes-list feature — loading a page of quotes and creating a new one — as a signals-based store service, not local component state. Target the real Week-1 backend directly, `day-5/Day-5-Piece-2`.
>
> ### Real Week-1 Backend Contracts
> - **List**: `GET /api/quotes?page={page}&size={size}` → `Quote[]` (bare array, no envelope), anonymous access. `Quote = { id: number, author: string, text: string, userId?: string }` — matches `Models/Quote.cs` exactly.
> - **Create**: `POST /api/quotes` with `{ author, text }` (`CreateQuoteRequest` — `[Required][StringLength(100)]` author, `[Required][StringLength(1000)]` text) → `201` + the created `Quote` (server assigns `id` and `userId`), or `401` if the caller isn't authenticated with a `quotes.write` token.
>
> ### Requirements
> 1. **One signal of state**, everything else derived via `computed()` — not five independent signals that can drift out of sync with each other.
> 2. Cover **loading / loaded / empty / error** as an explicit, named status, not booleans (`isLoading` + `hasError` flags rot the moment a fifth state shows up).
> 3. **Concurrent updates**: if `load()` is called again before a previous call's response has arrived, the store must end up reflecting the *latest* call, never an older one that happens to resolve late.
> 4. **Create should feel instant**: the new quote appears in the list immediately, before the network round-trip completes, and is removed again if the request ultimately fails.
> 5. Plain `@Injectable({ providedIn: 'root' })` — no NgRx, no `@ngrx/signals`. State management, not a library integration.

---

## (2) Agent's Output

### The store (`core/state/quotes-store.service.ts`)

```typescript
type Status = 'idle' | 'loading' | 'loaded' | 'empty' | 'error';

interface QuotesState {
  status: Status;
  quotes: Quote[];
  error: string | null;
}

@Injectable({ providedIn: 'root' })
export class QuotesStore {
  private readonly api = inject(QuotesApiService);
  private readonly state = signal<QuotesState>({ status: 'idle', quotes: [], error: null });

  readonly quotes = computed(() => this.state().quotes);
  readonly status = computed(() => this.state().status);
  readonly isLoading = computed(() => this.state().status === 'loading');
  readonly errorMessage = computed(() => this.state().error);

  load(page = 1, size = 5): void {
    this.state.update((s) => ({ ...s, status: 'loading', error: null }));
    this.api.getQuotes(page, size).subscribe({
      next: (quotes) => this.state.set({
        status: quotes.length === 0 ? 'empty' : 'loaded',
        quotes,
        error: null,
      }),
      error: (err) => this.state.update((s) => ({
        ...s, status: 'error', error: err.userMessage || 'Failed to load quotes.',
      })),
    });
  }

  create(payload: CreateQuoteRequest) {
    const optimisticId = -(this.state().quotes.length + 1);
    const optimisticQuote: Quote = { id: optimisticId, author: payload.author, text: payload.text };
    this.state.update((s) => ({ ...s, quotes: [optimisticQuote, ...s.quotes] }));

    return this.api.createQuote(payload).pipe(
      tap((created) => this.state.update((s) => ({
        ...s, quotes: s.quotes.map((q) => (q.id === optimisticId ? created : q)),
      }))),
      catchError((err) => {
        this.state.update((s) => ({ ...s, quotes: s.quotes.filter((q) => q.id !== optimisticId) }));
        return throwError(() => err);
      })
    );
  }
}
```

*(This is the state actually generated, before the review in §4 below caught the missing piece of requirement 3.)*

### The consumer (`features/quotes-panel/quotes-panel.component.ts`)

A component-level `page` signal and a reactive create form. It reads `store.quotes()`, `store.status()`, `store.errorMessage()` directly in the template and calls `store.load(page)` / `store.create(form.value)` — no local copy of the quotes array, no duplicate loading flag. The full file is in `Code/features/quotes-panel/`.

---

## (3) The Rule for When to Adopt NgRx / `@ngrx/signals` (Mine, Not the Agent's)

The agent can draft a rule; it can't own the judgment call for a codebase it doesn't have to live in. Mine:

**Stay with a plain signals service as long as (a) a piece of state has exactly one service that owns writes to it, and (b) you can describe every valid state transition in a sentence without an "and also" in it.** `QuotesStore` above passes both: only `QuotesStore` ever calls `this.state.set/update`, and every transition is `idle → loading → {loaded | empty | error}`, plus the create/rollback pair. Nothing else in the app reaches into that signal.

**Move to a store library at the first of these, whichever comes first:**
- **Cross-cutting invalidation.** The moment a second, unrelated feature needs to react to "a quote was created" (a notifications badge, an activity feed, a dashboard count) without importing `QuotesStore` directly and coupling to its internals, you need a dispatched event other things can subscribe to — that's what NgRx's action/reducer/effect separation buys you, and a signals service doesn't have an equivalent without reinventing one.
- **More than ~3 services need to coordinate a single transition.** If creating a quote ever needs to also update a `CollectionsStore`, a `NotificationsStore`, and an `AnalyticsStore` in one atomic-feeling step, doing that by manually calling into three injected services from inside `create()` is exactly the tangle NgRx's effects exist to keep untangled.
- **You need time-travel debugging or serializable state for real support/QA reasons** — a signals service's state is just whatever's in memory; there's no built-in action log to replay.
- **The team, not just the code, needs the enforced structure.** NgRx's ceremony (actions, reducers, selectors) is a cost you pay to make illegal states hard to reach by convention, not just by discipline. On a team where "just don't write to the signal from outside the service" isn't reliably true in practice, that ceremony starts paying for itself.

For this app — one feature, one owner, five files — none of those are true yet. Reaching for NgRx here would be the store equivalent of guarding a page nobody unauthenticated can reach anyway (Piece 1's own lesson, restated): solving a problem the app doesn't have yet at the cost of real complexity it does have to pay for today.

---

## (4) The Bug Caught and Fixed

The store above satisfies requirements 1, 2, 4, and 5. It silently fails requirement 3.

**The bug**: `load()` unconditionally writes whatever response arrives into `state`. If it's called twice in quick succession — a user clicking "Next page" twice before the first page finishes loading, or clicking Prev right after Next — there is nothing stopping the *first, slower* request's response from arriving *after* the second and overwriting already-correct, newer state with stale data. The UI would flash back to an earlier page's content with no error, no warning, silently.

**Reproduced, not asserted**: added `Code/tests/quotes-store.spec.ts`, including a test that requests page 1 then page 2, but resolves page 2's HTTP response *first* and page 1's *second* — deliberately simulating the out-of-order arrival a slow first request would cause. Ran it against the draft above:

```
❯ src/app/tests/quotes-store.spec.ts (6 tests | 1 failed)
  × discards a stale response that resolves after a newer load() has already superseded it
    AssertionError: expected 1 to be 6
    - Expected: 6
    + Received: 1
```

Confirmed the bug is real, not theoretical — the late page-1 response really does clobber the correct page-2 state, exactly as predicted.

**The fix**: a monotonically increasing request id, captured at call time; a response only writes into state if it still matches the *current* id when it arrives.

```diff
+ private latestRequestId = 0;

  load(page = 1, size = 5): void {
+   const requestId = ++this.latestRequestId;
    this.state.update((s) => ({ ...s, status: 'loading', error: null }));
    this.api.getQuotes(page, size).subscribe({
      next: (quotes) => {
+       if (requestId !== this.latestRequestId) return; // stale — a newer load() already superseded this one
        this.state.set({ status: quotes.length === 0 ? 'empty' : 'loaded', quotes, error: null });
      },
      error: (err) => {
+       if (requestId !== this.latestRequestId) return;
        this.state.update((s) => ({ ...s, status: 'error', error: err.userMessage || '...' }));
      },
    });
  }
```

Re-ran the same test against the fix: **6/6 passing.** Then reverted the fix a second time by hand to re-confirm the exact same failure reproduces on demand (not a fluke of test ordering), and restored it. This is the version shipped in `Code/core/state/quotes-store.service.ts`.

---

## (5) Verification Log

All states below were exercised for real, not just written — either against the live backend (`http://localhost:5000`) through the running app (`http://localhost:4201`), or through `Code/tests/quotes-store.spec.ts` run with `ng test` where the state depends on network response *ordering*, which isn't something a manual click can reliably force.

| State / edge | How it was exercised | Result |
|---|---|---|
| **loading → loaded** | Live: loaded `http://localhost:4201/` fresh, backend seeded with 5 real quotes. Screenshot: `Evidence/01-loaded-state.png`. | `status()` → `'loaded'`, 5 real quotes rendered (`Marcus Aurelius` ×3, `Seneca`, `Frontend Create Test`). |
| **loading → empty** | Test: `store.load(3, 5)`, flush `[]`. | `status()` → `'empty'`, `quotes()` → `[]`. |
| **loading → error** | Test: `store.load(1, 5)`, flush a `500`. Also confirmed live: `POST /api/quotes` with no token against the real backend → real `401` (grounds the create-rollback test below in an actual server response, not just an invented mock). | `status()` → `'error'`, `errorMessage()` set from the real error-mapping pipeline. |
| **Concurrent updates (the bug)** | Test: `load(1,5)` then `load(2,5)`; flushed page 2's response *before* page 1's. | **Failed** against the first draft (`expected 6, got 1`) — proved the bug was real. **Passed** after the `requestId` fix — final state correctly reflects page 2, the stale page-1 response is discarded. |
| **Optimistic create → success** | Test: `store.create(...)`, assert the optimistic (negative-id) quote is in `quotes()` *before* the HTTP response is flushed, then flush a `201` and assert the real server `id` replaces it. | Optimistic entry visible instantly; reconciled with the server-assigned id on success; list length stays 1 throughout (no duplicate). |
| **Optimistic create → rollback** | Test: `store.create(...)`, assert the optimistic entry is present, then flush a `401`, assert it's removed. | Optimistic entry added, then removed on failure — no phantom quote left behind. |

`ng test` — **6/6 passing** in `quotes-store-app`. `ng build --configuration development` — clean.

---

## (6) What Breaks If the Week-1 API Contract Changes

**`GET /api/quotes` changes from a bare array to an envelope** (e.g. `{ items: Quote[], total: number }`, a very plausible next step once real pagination replaces the current ignore-page-and-size-if-you-feel-like-it behavior): `QuotesApiService.getQuotes()`'s return type and the one line `this.api.getQuotes(...).subscribe({ next: (quotes) => ... })` in `QuotesStore.load()` both break — `quotes` would actually be the envelope object, not an array, so `quotes.length` would be `undefined` and every page would incorrectly read as `'loaded'` with an empty-looking list. Contained to exactly those two files; `QuotesPanelComponent` and the store's public `quotes`/`status` signals don't change shape, so nothing downstream needs to know pagination metadata even exists.

**`id` changes from `int` to a `Guid`/string** (mirroring Piece 1's own contract-fragility scenario): the optimistic-create placeholder ID generation (`-(this.state().quotes.length + 1)`) assumes negative numbers are a safe, un-collidable sentinel for "not yet confirmed by the server" — that assumption dies immediately, since a string-typed real id can't be compared with `< 0` (the template's `[class.optimistic]="q.id < 0"` check) or excluded by a numeric negative-id convention at all. Would need a dedicated `pending?: boolean` flag on the optimistic entry instead of inferring "optimistic" from the id's sign — a more robust design this exercise's simpler version deliberately didn't need yet.

**`POST /api/quotes` stops requiring auth** (unlikely, but illustrative): nothing breaks — the optimistic-create/rollback logic doesn't care *why* a request might fail, only that it might. The 401-specific error message in `QuotesPanelComponent.submit()` (`'You need to be signed in...'`) would just become slightly wrong copy for a failure mode that can no longer happen, not a functional bug.

---

## (7) Delete — Added After Comparing Against a Peer's Independent Solution

The exercise's own framing is "verify and defend what it built" — that includes checking the design against more than just your own tests. Before shipping, I read a classmate's (Prakhar Sahu's) independent solution to this same exercise. His store is built around **delete** (his backend's `POST`/`PUT` require a write-scope his login never actually mints, so `DELETE` — needing only plain authentication plus a server-side ownership check — was the one write reachable with a real login on his API). Ours didn't have delete at all. Two things from his design were concretely better than a first pass at adding it would have been, and both are in the version below, not just noted and skipped:

**1. A derived list, not an in-place removal.** The naive way to add optimistic delete is: remove the row from the array immediately, and on failure splice it back in at its old index. That breaks the moment a `load()` refresh completes *while* the delete is still in flight — the splice-back targets an index into an array a concurrent refresh may have already replaced wholesale, and a `load()` that legitimately still contains the row being deleted would otherwise silently un-delete it, or the rollback would insert into the wrong list entirely. His fix, adopted here: keep the raw server list (`serverQuotes`) and a *separate* `removingIds: Set<number>` overlay; the public `quotes` signal is `computed(() => serverQuotes().filter(q => !removingIds().has(q.id)))`. A delete never touches `serverQuotes` until it actually resolves — it only ever adds/removes an id from the overlay set, which composes safely with a concurrent refresh instead of racing it.

**2. Sign-in-gated, not ownership-gated, visibility.** The client has no reliable way to know who owns a quote it hasn't tried to delete yet — the `Quote` shape returned by `GET /api/quotes` does carry a `userId`, but comparing it against "the current user" client-side would mean decoding the JWT just to hide a button, for a check the server enforces anyway (and enforces correctly, per the real curl results in §7.1 below). So: the Delete button shows for **any signed-in user, on every row**, matching the real backend's actual authorization split (401 needs *a* token, 403 needs the *right* token) — the UI's job is to let the click happen and let the server's real answer (204 vs. 403 vs. 401) drive the rollback and message, not to pre-guess ownership.

### 7.1 What was added

- `QuotesApiService.deleteQuote(id)` → `DELETE /api/quotes/{id:int}`.
- `QuotesStore.remove(id)`: adds `id` to `removingIds` (hides it immediately via the derived `quotes` computed), calls the API, and on success removes it from `serverQuotes` for real. On failure: 403 → restores the row, message `"This quote belongs to a different user."`; 401 → restores, `"Your session expired — sign in again."`; 404 → treated as already-gone, removed from `serverQuotes` with no failure message (nothing to roll back to). A double `remove()` on the same id while the first is still pending is a no-op — no second DELETE fires.
- `load()` also picked up one refinement while touching this code: it only flips `status` to `'loading'` if the list is currently empty, so a background refresh doesn't blank an already-populated list — the same reasoning as the delete fix, applied to the read side.
- UI: a Delete button per row, shown only when `AuthTokenService.isAuthenticated()`; a per-row failure message when `store.failureFor().get(q.id)` is set; a "Sign in to delete a quote" hint when signed out, naming the real endpoint.

**Real backend verification** (not just the mocked test suite below) — three genuinely different identities against the live `day-5/Day-5-Piece-2`, `testuser` from a real login and a hand-signed second JWT for `otheruser` (same HS256 key, different `sub`), so the 403 path is a real server response, not an invented one:

| Request | Result |
|---|---|
| `DELETE /api/quotes/7` as `testuser` (owns it) | **204** |
| `DELETE /api/quotes/8` as `testuser` (owned by `otheruser`) | **403** |
| `DELETE /api/quotes/4`, no token | **401** |

### 7.2 Verified live, with real screenshots — not just mocks

`Evidence/02-signed-in-list-with-delete.png` through `04-signed-out-delete-hidden.png` were captured by actually driving the running app via Chrome DevTools Protocol (no extension available this session) — a real page load, a real `localStorage` session, and for the rollback screenshot, a **real dispatched click** on the actual Delete button (`document.querySelector('#delete-btn-8').click()`), not a mocked HTTP response:

- `02-signed-in-list-with-delete.png` — signed in as `testuser`, every row shows a Delete button.
- `03-optimistic-rollback-403.png` — clicked Delete on quote `#8` (owned by `otheruser`). The row hid immediately, the real `DELETE /api/quotes/8` request went out, the real backend returned `403`, and the screenshot shows the row **back**, bordered, with "This quote belongs to a different user." — captured *after* the real network round-trip completed.
- `04-signed-out-delete-hidden.png` — `localStorage` cleared, reloaded: no Delete buttons anywhere, replaced by the sign-in hint.

### 7.3 Test coverage added

`Code/tests/quotes-store.spec.ts` gained six tests: optimistic hide → real removal on 204; restore + exact message on 403; restore + exact message on 401; 404 treated as already-gone with no rollback and no failure banner; a double-click on the same id produces exactly one DELETE request; and the one that actually proves §7's design point — **a `load()` refresh that resolves while a delete is still pending does not resurrect the row being deleted**, even though the refreshed server data legitimately still contains it. `ng test`: **12/12 passing** (6 original + 6 new).

### 7.4 What this changes in §6 ("what breaks")

One addition to the id-type-change scenario in §6: it's no longer just the optimistic-create sentinel (`id < 0`) at risk. `removingIds: Set<number>` and `removalFailures: Map<number, string>` are also keyed by `id`, so a `Guid`/string id would need both changed from `Set<number>`/`Map<number, ...>` to their string-keyed equivalents — a purely mechanical change (no logic changes), but one more place §6's scenario touches now that delete exists.

---

## (8) Files

```
Code/
  models/quote.model.ts
  services/quotes-api.service.ts        — thin HTTP client, no state
  core/state/quotes-store.service.ts    — the store (§2 base + §7 delete)
  core/tokens/api-base-url.token.ts
  features/quotes-panel/
    quotes-panel.component.ts
    quotes-panel.component.html
    quotes-panel.component.css
  tests/quotes-store.spec.ts            — §4/§5/§7's evidence, runnable (12 tests)

quotes-store-app/   — the full runnable Angular 22 workspace (npm start → :4201)
Evidence/
  01-loaded-state.png
  02-signed-in-list-with-delete.png
  03-optimistic-rollback-403.png
  04-signed-out-delete-hidden.png
```
