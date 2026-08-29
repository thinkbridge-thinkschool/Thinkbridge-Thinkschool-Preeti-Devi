import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { QUOTES_BASE_URL } from '../core/tokens/api-base-url.token';
import { CreateQuoteRequest, Quote } from '../models/quote.model';

/**
 * Thin HTTP client — no state here. QuotesStore (core/state/quotes-store.service.ts)
 * owns state; this only knows how to talk to the real Week-1 endpoints.
 */
@Injectable({ providedIn: 'root' })
export class QuotesApiService {
  private readonly http = inject(HttpClient);
  // In production this resolves to the Managed Identity proxy, not the API —
  // see core/tokens/api-base-url.token.ts. The paths below are unchanged either
  // way: the proxy exposes /quotes and /quotes/{id} under its own /proxy root.
  private readonly baseUrl = inject(QUOTES_BASE_URL);

  /** Week-1 Endpoint: GET /api/quotes?page={page}&size={size} — anonymous. */
  getQuotes(page: number, size: number): Observable<Quote[]> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('size', size.toString());
    return this.http.get<Quote[]>(`${this.baseUrl}/quotes`, { params });
  }

  /** Week-1 Endpoint: POST /api/quotes — requires an authenticated quotes.write token. */
  createQuote(payload: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.baseUrl}/quotes`, payload);
  }

  /**
   * Week-1 Endpoint: DELETE /api/quotes/{id:int} — requires authentication
   * (401 if signed out) AND resource ownership (403 if signed in as a
   * different user than the quote's owner). 204 on success, 404 if it's
   * already gone.
   */
  deleteQuote(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/quotes/${id}`);
  }
}
