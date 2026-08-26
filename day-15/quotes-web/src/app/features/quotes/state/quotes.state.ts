import { Injectable, computed, inject, signal } from '@angular/core';
import { QuotesService } from '../services/quotes.service';
import { Quote } from '../models/quote.model';
import { AppError } from '../../../core/models/app-error.model';

export type LoadingState = 'idle' | 'loading' | 'success' | 'empty' | 'error';

@Injectable({ providedIn: 'root' })
export class QuotesState {
  private readonly quotesService = inject(QuotesService);

  private readonly _quotes = signal<Quote[]>([]);
  private readonly _loadingState = signal<LoadingState>('idle');
  private readonly _error = signal<AppError | null>(null);
  private readonly _page = signal<number>(1);
  private readonly _size = signal<number>(5);

  // Readonly selectors
  readonly quotes = this._quotes.asReadonly();
  readonly loadingState = this._loadingState.asReadonly();
  readonly error = this._error.asReadonly();
  readonly page = this._page.asReadonly();
  readonly size = this._size.asReadonly();

  readonly hasQuotes = computed(() => this._quotes().length > 0);
  readonly friendlyErrorMessage = computed(() => this._error()?.friendlyMessage ?? null);

  loadQuotes(): void {
    this._loadingState.set('loading');
    this._error.set(null);

    this.quotesService.getQuotes(this._page(), this._size()).subscribe({
      next: (data) => {
        this._quotes.set(data);
        if (data.length === 0) {
          this._loadingState.set('empty');
        } else {
          this._loadingState.set('success');
        }
      },
      error: (err: AppError) => {
        this._error.set(err);
        this._loadingState.set('error');
      },
    });
  }

  setPage(page: number): void {
    this._page.set(page);
    this.loadQuotes();
  }

  setSize(size: number): void {
    this._size.set(size);
    this.loadQuotes();
  }

  nextPage(): void {
    this._page.update((p) => p + 1);
    this.loadQuotes();
  }

  prevPage(): void {
    if (this._page() > 1) {
      this._page.update((p) => p - 1);
      this.loadQuotes();
    }
  }

  triggerInvalidPage(): void {
    this._page.set(0);
    this.loadQuotes();
  }

  triggerInvalidSize(): void {
    this._size.set(999);
    this.loadQuotes();
  }

  resetParams(): void {
    this._page.set(1);
    this._size.set(5);
    this.loadQuotes();
  }
}
