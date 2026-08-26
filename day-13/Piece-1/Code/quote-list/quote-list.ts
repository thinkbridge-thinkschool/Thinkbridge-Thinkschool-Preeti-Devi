import { Component, computed, effect, inject, signal } from '@angular/core';
import { Quote } from '../models/quote.model';
import { QuotesService } from '../services/quotes.service';
type ViewState = 'loading' | 'error' | 'empty' | 'success';

@Component({
  selector: 'app-quote-list',
  imports: [],
  templateUrl: './quote-list.html',
  styleUrl: './quote-list.css',
})
export class QuoteListComponent {
  private readonly quotesService = inject(QuotesService);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly pageSizeOptions = [5, 10, 20, 50] as const;

  readonly quotes = signal<Quote[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly pageSummary = computed(
    () => `Page ${this.page()} · ${this.pageSize()} quotes per page`,
  );
  readonly viewState = computed<ViewState>(() => {
    if (this.loading()) return 'loading';
    if (this.error()) return 'error';
    return this.quotes().length === 0 ? 'empty' : 'success';
  });
  private readonly refetchOnPagingChange = effect(() => {
    const page = this.page();
    const pageSize = this.pageSize();
    this.fetchQuotes(page, pageSize);
  });
  nextPage(): void {
    this.page.update((current) => current + 1);
  }

  previousPage(): void {
    this.page.update((current) => Math.max(1, current - 1));
  }

  onPageSizeChange(event: Event): void {
    const size = Number((event.target as HTMLSelectElement).value);
    this.pageSize.set(size);
  }

  authorLabel(quote: Quote): string {
    return quote.authorEntity?.name ?? quote.author;
  }
  private fetchQuotes(page: number, pageSize: number): void {
    this.loading.set(true);
    this.error.set(null);

    this.quotesService.getQuotes(page, pageSize).subscribe({
      next: (quotes) => {
        this.quotes.set(quotes);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load quotes. Please try again.');
        this.loading.set(false);
      },
    });
  }
}
