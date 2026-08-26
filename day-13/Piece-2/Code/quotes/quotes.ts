import { Component, computed, effect, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { Quote } from '../models/quote.model';
import { QuotesService } from '../services/quotes.service';

type ListState = 'loading' | 'error' | 'empty' | 'loaded';
type DetailState = 'idle' | 'loading' | 'error' | 'not-found' | 'loaded';

@Component({
  selector: 'app-quotes',
  imports: [],
  templateUrl: './quotes.html',
  styleUrl: './quotes.css',
})
export class QuotesComponent {
  private readonly quotesService = inject(QuotesService);

  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly quotes = signal<Quote[]>([]);
  readonly listLoading = signal(false);
  readonly listError = signal<string | null>(null);

  readonly listState = computed<ListState>(() => {
    if (this.listLoading()) return 'loading';
    if (this.listError()) return 'error';
    return this.quotes().length === 0 ? 'empty' : 'loaded';
  });

  readonly selectedId = signal<number | null>(null);
  readonly detailQuote = signal<Quote | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<string | null>(null);

  readonly detailState = computed<DetailState>(() => {
    if (this.selectedId() === null) return 'idle';
    if (this.detailLoading()) return 'loading';
    if (this.detailError()?.includes('404')) return 'not-found';
    if (this.detailError()) return 'error';
    return this.detailQuote() ? 'loaded' : 'idle';
  });

  private detailSub: Subscription | null = null;

  private readonly refetchList = effect(() => {
    const page = this.page();
    const pageSize = this.pageSize();
    this.fetchList(page, pageSize);
  });

  selectQuote(id: number): void {
    this.selectedId.set(id);
    this.fetchDetail(id);
  }

  closeDetail(): void {
    this.cancelDetailFlight();
    this.selectedId.set(null);
    this.detailQuote.set(null);
    this.detailError.set(null);
  }

  nextPage(): void {
    this.page.update((p) => p + 1);
  }

  previousPage(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  authorDisplay(quote: Quote): string {
    return quote.authorEntity?.name ?? quote.author;
  }

  private fetchList(page: number, pageSize: number): void {
    this.listLoading.set(true);
    this.listError.set(null);

    this.quotesService.getQuotes(page, pageSize).subscribe({
      next: (data) => {
        this.quotes.set(data);
        this.listLoading.set(false);
      },
      error: (err) => {
        this.listError.set(
          `Failed to load quotes: ${err.status ?? 'network error'}`,
        );
        this.listLoading.set(false);
      },
    });
  }

  private fetchDetail(id: number): void {
    this.cancelDetailFlight();

    this.detailLoading.set(true);
    this.detailError.set(null);
    this.detailQuote.set(null);

    this.detailSub = this.quotesService.getQuoteById(id).subscribe({
      next: (quote) => {
        if (this.selectedId() === id) {
          this.detailQuote.set(quote);
          this.detailLoading.set(false);
        }
      },
      error: (err) => {
        if (this.selectedId() === id) {
          this.detailError.set(
            `Error loading quote: ${err.status ?? 'network error'}`,
          );
          this.detailLoading.set(false);
        }
      },
    });
  }

  private cancelDetailFlight(): void {
    if (this.detailSub && !this.detailSub.closed) {
      this.detailSub.unsubscribe();
      this.detailSub = null;
    }
  }
}
