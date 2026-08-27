import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/tokens/api-base-url.token';
import { CreateQuoteRequest, Quote } from '../models/quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /**
   * Week-1 Endpoint: GET /api/quotes?page={page}&size={size}
   */
  getQuotes(page: number = 1, size: number = 10): Observable<Quote[]> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('size', size.toString());

    return this.http.get<Quote[]>(`${this.baseUrl}/quotes`, { params });
  }

  /**
   * Week-1 Endpoint: GET /api/quotes/{id:int}
   */
  getQuoteById(id: number): Observable<Quote> {
    return this.http.get<Quote>(`${this.baseUrl}/quotes/${id}`);
  }

  /**
   * Week-1 Endpoint: POST /api/quotes
   */
  createQuote(payload: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.baseUrl}/quotes`, payload);
  }

  /**
   * Week-1 Endpoint: DELETE /api/quotes/{id:int}
   */
  deleteQuote(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/quotes/${id}`);
  }
}
