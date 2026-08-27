import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { QuotesService } from '../../../services/quotes.service';
import { AuthTokenService } from '../../../core/services/auth-token.service';
import { Quote } from '../../../models/quote.model';

type ViewState = 'loading' | 'success' | 'empty' | 'error';

@Component({
  selector: 'app-quote-list',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './quote-list.component.html',
  styleUrls: ['./quote-list.component.css'],
})
export class QuoteListComponent implements OnInit {
  private readonly quotesService = inject(QuotesService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly authService = inject(AuthTokenService);

  readonly state = signal<ViewState>('loading');
  readonly quotes = signal<Quote[]>([]);
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(5);
  readonly errorMessage = signal<string | null>(null);

  // The real Week-1 backend ignores any query params beyond page/size (it
  // has no free-text search), so filtering happens client-side over the
  // current page and is reflected into the URL (?q=...) rather than sent
  // to the server — shareable/bookmarkable, and Back from a detail page
  // returns to the same filtered view via queryParamsHandling="preserve".
  readonly filter = signal<string>('');
  readonly filteredQuotes = computed(() => {
    const term = this.filter().trim().toLowerCase();
    if (!term) return this.quotes();
    return this.quotes().filter(
      (q) => q.author.toLowerCase().includes(term) || q.text.toLowerCase().includes(term)
    );
  });

  ngOnInit(): void {
    this.filter.set(this.route.snapshot.queryParams['q'] ?? '');
    this.fetchQuotes();
  }

  onFilterInput(value: string): void {
    this.filter.set(value);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: value || null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  fetchQuotes(): void {
    this.state.set('loading');
    this.errorMessage.set(null);

    this.quotesService.getQuotes(this.currentPage(), this.pageSize()).subscribe({
      next: (data) => {
        this.quotes.set(data);
        if (data.length === 0) {
          this.state.set('empty');
        } else {
          this.state.set('success');
        }
      },
      error: (err) => {
        this.errorMessage.set(
          err.userMessage || 'Failed to load quotes from server.'
        );
        this.state.set('error');
      },
    });
  }

  logout(): void {
    this.authService.clearToken();
    window.location.reload();
  }

  nextPage(): void {
    this.currentPage.update((p) => p + 1);
    this.fetchQuotes();
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
      this.fetchQuotes();
    }
  }
}
