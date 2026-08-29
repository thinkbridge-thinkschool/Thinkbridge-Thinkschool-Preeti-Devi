import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, of, tap, throwError } from 'rxjs';
import { QuotesApiService } from '../../services/quotes-api.service';
import { CreateQuoteRequest, Quote } from '../../models/quote.model';

type Status = 'idle' | 'loading' | 'loaded' | 'empty' | 'error';

interface QuotesState {
  status: Status;
  serverQuotes: Quote[];
  error: string | null;
}

const INITIAL_STATE: QuotesState = { status: 'idle', serverQuotes: [], error: null };

/**
 * Signals-first store for the quotes-list feature. A plain @Injectable
 * service, not a library — one signal of raw server state, everything else
 * derived via computed(). Promoted out of the list component (see README §2)
 * once the create form needed to update the SAME list the moment a quote is
 * created, without the create form and the list component knowing about
 * each other directly.
 */
@Injectable({ providedIn: 'root' })
export class QuotesStore {
  private readonly api = inject(QuotesApiService);

  private readonly state = signal<QuotesState>(INITIAL_STATE);

  // Ids currently mid-delete, and the last failure message per id (if any).
  // Kept SEPARATE from state.serverQuotes rather than removing the row
  // in-place — see remove() below for why: a plain "remove now, splice it
  // back in on failure" approach breaks the moment a load() refresh
  // completes while the delete is still in flight, because the splice-back
  // targets an array index that a concurrent refresh may have already
  // replaced wholesale.
  private readonly removingIds = signal<ReadonlySet<number>>(new Set());
  private readonly removalFailures = signal<ReadonlyMap<number, string>>(new Map());

  readonly quotes = computed(() =>
    this.state().serverQuotes.filter((q) => !this.removingIds().has(q.id))
  );
  readonly status = computed(() => this.state().status);
  readonly isLoading = computed(() => this.state().status === 'loading');
  readonly errorMessage = computed(() => this.state().error);
  readonly failureFor = computed(() => this.removalFailures());

  // Monotonically increasing request id. If load() is called again before an
  // earlier call's response arrives (e.g. clicking "Next page" twice fast),
  // only the response matching the CURRENT id is allowed to write into
  // state — an older, slower response that resolves late is discarded
  // instead of clobbering newer data. See README §4 for the bug this fixes.
  private latestRequestId = 0;

  /** Week-1 Endpoint: GET /api/quotes?page={page}&size={size} */
  load(page = 1, size = 5): void {
    const requestId = ++this.latestRequestId;

    // Only show the loading skeleton if the list is currently empty — a
    // background refresh of an already-populated list (e.g. re-syncing
    // after a delete) shouldn't blank the UI it's refreshing.
    if (this.state().serverQuotes.length === 0) {
      this.state.update((s) => ({ ...s, status: 'loading', error: null }));
    }

    this.api.getQuotes(page, size).subscribe({
      next: (quotes) => {
        if (requestId !== this.latestRequestId) return; // stale — a newer load() already superseded this one
        this.state.set({
          status: quotes.length === 0 ? 'empty' : 'loaded',
          serverQuotes: quotes,
          error: null,
        });
      },
      error: (err) => {
        if (requestId !== this.latestRequestId) return;
        this.state.update((s) => ({
          ...s,
          status: 'error',
          error: err.userMessage || 'Failed to load quotes from the server.',
        }));
      },
    });
  }

  /**
   * Week-1 Endpoint: POST /api/quotes
   * Optimistic: prepends a locally-tagged quote immediately (visible in the
   * list before the network round-trip completes), then either reconciles
   * it with the server-assigned id on success or removes it on failure —
   * the caller (the create form) only needs to know whether the request
   * ultimately succeeded, for its own submitting/error UI.
   */
  create(payload: CreateQuoteRequest): ReturnType<QuotesApiService['createQuote']> {
    const optimisticId = -(this.state().serverQuotes.length + 1) - (Date.now() % 1000);
    const optimisticQuote: Quote = { id: optimisticId, author: payload.author, text: payload.text };

    this.state.update((s) => ({
      status: 'loaded',
      error: null,
      serverQuotes: [optimisticQuote, ...s.serverQuotes],
    }));

    return this.api.createQuote(payload).pipe(
      tap((created) => {
        this.state.update((s) => ({
          ...s,
          serverQuotes: s.serverQuotes.map((q) => (q.id === optimisticId ? created : q)),
        }));
      }),
      catchError((err) => {
        this.state.update((s) => ({
          ...s,
          serverQuotes: s.serverQuotes.filter((q) => q.id !== optimisticId),
        }));
        return throwError(() => err);
      })
    );
  }

  /**
   * Week-1 Endpoint: DELETE /api/quotes/{id:int} — requires an authenticated
   * token (401 if signed out) AND resource ownership (403 if signed in as
   * someone other than the quote's owner; enforced server-side, since the
   * client has no reliable way to know ownership up front).
   *
   * Optimistic via a HIDE overlay, not an array mutation: `id` is added to
   * removingIds, which the `quotes` computed() filters out immediately —
   * serverQuotes itself is untouched until the request actually resolves.
   * That means a load() refresh that completes mid-delete just replaces
   * serverQuotes as normal; removingIds still hides the row being deleted
   * either way, and there's no stale array index to reconcile on rollback.
   *
   * A negative id is a not-yet-confirmed optimistic create — nothing exists
   * server-side to delete yet, so it's dropped locally with no API call.
   */
  remove(id: number): Observable<void> {
    if (id < 0) {
      this.state.update((s) => ({ ...s, serverQuotes: s.serverQuotes.filter((q) => q.id !== id) }));
      return of(undefined);
    }

    if (this.removingIds().has(id)) {
      return of(undefined); // already mid-delete — ignore a double click
    }

    this.removingIds.update((set) => new Set(set).add(id));
    this.removalFailures.update((map) => {
      const next = new Map(map);
      next.delete(id);
      return next;
    });

    return this.api.deleteQuote(id).pipe(
      tap(() => {
        this.removingIds.update((set) => {
          const next = new Set(set);
          next.delete(id);
          return next;
        });
        this.state.update((s) => ({ ...s, serverQuotes: s.serverQuotes.filter((q) => q.id !== id) }));
      }),
      catchError((err) => {
        this.removingIds.update((set) => {
          const next = new Set(set);
          next.delete(id);
          return next;
        });

        if (err.status === 404) {
          // Already gone server-side — reconcile local state to match.
          // Not a failure the user needs a banner for.
          this.state.update((s) => ({ ...s, serverQuotes: s.serverQuotes.filter((q) => q.id !== id) }));
        } else {
          const message =
            err.status === 403
              ? 'This quote belongs to a different user.'
              : err.status === 401
                ? 'Your session expired — sign in again.'
                : err.userMessage || 'Unable to delete this quote right now.';
          this.removalFailures.update((map) => new Map(map).set(id, message));
          // Row reappears automatically: it was never removed from
          // serverQuotes, only hidden via removingIds, which was just cleared.
        }

        return throwError(() => err);
      })
    );
  }
}
