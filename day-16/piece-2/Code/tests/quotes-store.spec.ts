import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QuotesStore } from '../core/state/quotes-store.service';
import { QuotesApiService } from '../services/quotes-api.service';
import { API_BASE_URL } from '../core/tokens/api-base-url.token';

describe('QuotesStore', () => {
  let store: QuotesStore;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        QuotesStore,
        QuotesApiService,
        { provide: API_BASE_URL, useValue: 'http://localhost:5000/api' },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    store = TestBed.inject(QuotesStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('starts idle, then loading, then loaded with real quote shape', () => {
    expect(store.status()).toBe('idle');

    store.load(1, 5);
    expect(store.status()).toBe('loading');

    const req = httpMock.expectOne('http://localhost:5000/api/quotes?page=1&size=5');
    req.flush([{ id: 1, author: 'Marcus Aurelius', text: 'You have power over your mind.', userId: 'testuser' }]);

    expect(store.status()).toBe('loaded');
    expect(store.quotes().length).toBe(1);
  });

  it('sets status to empty when the server returns zero quotes for a page', () => {
    store.load(3, 5);
    httpMock.expectOne('http://localhost:5000/api/quotes?page=3&size=5').flush([]);
    expect(store.status()).toBe('empty');
    expect(store.quotes()).toEqual([]);
  });

  it('sets status to error and surfaces a message on a failed request', () => {
    store.load(1, 5);
    httpMock
      .expectOne('http://localhost:5000/api/quotes?page=1&size=5')
      .flush('Server error', { status: 500, statusText: 'Internal Server Error' });

    expect(store.status()).toBe('error');
    expect(store.errorMessage()).toBeTruthy();
  });

  it('discards a stale response that resolves after a newer load() has already superseded it', () => {
    // The bug this test pins: page 1 is requested first, page 2 second —
    // but the network resolves them OUT OF ORDER (page 2's response lands
    // before page 1's slower one). Without the requestId guard, whichever
    // response arrives LAST always wins, so the late page-1 response would
    // silently clobber the already-correct page-2 state.
    store.load(1, 5);
    const firstReq = httpMock.expectOne('http://localhost:5000/api/quotes?page=1&size=5');

    store.load(2, 5);
    const secondReq = httpMock.expectOne('http://localhost:5000/api/quotes?page=2&size=5');

    // Resolve the NEWER request first...
    secondReq.flush([{ id: 6, author: 'Seneca', text: 'Page 2 quote.', userId: 'testuser' }]);
    expect(store.quotes()[0].id).toBe(6);

    // ...then the OLDER, slower request resolves late. It must be ignored.
    firstReq.flush([{ id: 1, author: 'Marcus Aurelius', text: 'Page 1 quote.', userId: 'testuser' }]);
    expect(store.quotes()[0].id).toBe(6); // still page 2's data, not clobbered
    expect(store.status()).toBe('loaded');
  });

  it('adds an optimistic quote immediately, then reconciles it with the server-assigned id on success', () => {
    store.load(1, 5);
    httpMock.expectOne('http://localhost:5000/api/quotes?page=1&size=5').flush([]);
    expect(store.quotes().length).toBe(0);

    let resolved: unknown;
    store.create({ author: 'Epictetus', text: 'Optimistic create test.' }).subscribe((q) => (resolved = q));

    // Visible immediately, before the network round-trip completes.
    expect(store.quotes().length).toBe(1);
    expect(store.quotes()[0].id).toBeLessThan(0);

    const req = httpMock.expectOne('http://localhost:5000/api/quotes');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 42, author: 'Epictetus', text: 'Optimistic create test.', userId: 'testuser' });

    expect(store.quotes().length).toBe(1);
    expect(store.quotes()[0].id).toBe(42); // real id replaces the negative placeholder
    expect(resolved).toEqual({ id: 42, author: 'Epictetus', text: 'Optimistic create test.', userId: 'testuser' });
  });

  it('rolls back the optimistic quote if the create request fails (e.g. 401 when signed out)', () => {
    store.load(1, 5);
    httpMock.expectOne('http://localhost:5000/api/quotes?page=1&size=5').flush([]);

    let errored: unknown;
    store.create({ author: 'Anonymous', text: 'Should be rolled back.' }).subscribe({
      error: (err) => (errored = err),
    });

    expect(store.quotes().length).toBe(1); // optimistic entry present

    httpMock
      .expectOne('http://localhost:5000/api/quotes')
      .flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(store.quotes().length).toBe(0); // rolled back
    expect(errored).toBeTruthy();
  });

  function seedTwoQuotes() {
    store.load(1, 5);
    httpMock.expectOne('http://localhost:5000/api/quotes?page=1&size=5').flush([
      { id: 2, author: 'Marcus Aurelius', text: 'Owned by testuser.', userId: 'testuser' },
      { id: 3, author: 'Seneca', text: 'Owned by someone else.', userId: 'other-user' },
    ]);
  }

  it('hides a quote immediately on remove(), then removes it for real on 204', () => {
    seedTwoQuotes();

    store.remove(2).subscribe();
    expect(store.quotes().map((q) => q.id)).toEqual([3]); // hidden immediately, before the response

    httpMock.expectOne('http://localhost:5000/api/quotes/2').flush(null, { status: 204, statusText: 'No Content' });
    expect(store.quotes().map((q) => q.id)).toEqual([3]); // stays gone
    expect(store.failureFor().has(2)).toBe(false);
  });

  it('restores the row and records a message on 403 (deleting a quote you do not own)', () => {
    seedTwoQuotes();

    let errored: unknown;
    store.remove(3).subscribe({ error: (err) => (errored = err) });
    expect(store.quotes().map((q) => q.id)).toEqual([2]); // 3 hidden optimistically

    httpMock.expectOne('http://localhost:5000/api/quotes/3').flush('Forbidden', { status: 403, statusText: 'Forbidden' });

    expect(store.quotes().map((q) => q.id).sort()).toEqual([2, 3]); // restored
    expect(store.failureFor().get(3)).toContain('belongs to a different user');
    expect(errored).toBeTruthy();
  });

  it('restores the row and records a session-expired message on 401', () => {
    seedTwoQuotes();
    store.remove(2).subscribe({ error: () => {} });
    httpMock.expectOne('http://localhost:5000/api/quotes/2').flush('Unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(store.quotes().map((q) => q.id).sort()).toEqual([2, 3]);
    expect(store.failureFor().get(2)).toContain('session expired');
  });

  it('treats a 404 as already-gone — no rollback, no failure message', () => {
    seedTwoQuotes();
    store.remove(2).subscribe({ error: () => {} });
    httpMock.expectOne('http://localhost:5000/api/quotes/2').flush('Not Found', { status: 404, statusText: 'Not Found' });

    expect(store.quotes().map((q) => q.id)).toEqual([3]); // stays gone, not restored
    expect(store.failureFor().has(2)).toBe(false); // not surfaced as a failure
  });

  it('ignores a second remove() on the same id while the first is still in flight', () => {
    seedTwoQuotes();
    store.remove(2).subscribe();
    store.remove(2).subscribe(); // double click

    httpMock.expectOne('http://localhost:5000/api/quotes/2').flush(null, { status: 204, statusText: 'No Content' });
    httpMock.verify(); // only ONE DELETE request was ever made
  });

  it('does not resurrect a row mid-delete when a load() refresh resolves in between', () => {
    // The race the derived-list design (serverQuotes + a separate removingIds
    // overlay, see quotes-store.service.ts) exists to survive: a delete is
    // in flight for id 2 when an unrelated refresh completes and re-fetches
    // the full list — which still legitimately contains id 2 server-side,
    // since the delete hasn't resolved yet. id 2 must stay hidden regardless.
    seedTwoQuotes();

    store.remove(2).subscribe();
    expect(store.quotes().map((q) => q.id)).toEqual([3]);

    // A refresh lands WHILE the delete above is still pending.
    store.load(1, 5);
    httpMock.expectOne('http://localhost:5000/api/quotes?page=1&size=5').flush([
      { id: 2, author: 'Marcus Aurelius', text: 'Owned by testuser.', userId: 'testuser' },
      { id: 3, author: 'Seneca', text: 'Owned by someone else.', userId: 'other-user' },
      { id: 4, author: 'Epictetus', text: 'A new quote that appeared during the refresh.', userId: 'testuser' },
    ]);

    // id 2 is back in serverQuotes (the refresh legitimately re-fetched it),
    // but must still be hidden — the delete is still pending.
    expect(store.quotes().map((q) => q.id).sort((a, b) => a - b)).toEqual([3, 4]);

    // Now the original delete resolves.
    httpMock.expectOne('http://localhost:5000/api/quotes/2').flush(null, { status: 204, statusText: 'No Content' });
    expect(store.quotes().map((q) => q.id).sort((a, b) => a - b)).toEqual([3, 4]);
  });
});
