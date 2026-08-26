import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { API_BASE_URL, USE_MOCK_DATA } from '../core/api-base-url.token';
import { Quote } from '../models/quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly useMock = inject(USE_MOCK_DATA);

  getQuotes(page: number, size: number): Observable<Quote[]> {
    if (this.useMock) {
      return this.http.get<Quote[]>('/mock-quotes.json').pipe(
        map((all) => {
          const start = (page - 1) * size;
          return all.slice(start, start + size);
        }),
      );
    }
    return this.http.get<Quote[]>(`${this.baseUrl}/quotes/`, {
      params: { page, size },
    });
  }
}
