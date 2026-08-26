import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/api-base-url.token';
import { Quote, CreateQuoteRequest } from '../models/quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  createQuote(payload: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.baseUrl}/quotes/`, payload);
  }
}
