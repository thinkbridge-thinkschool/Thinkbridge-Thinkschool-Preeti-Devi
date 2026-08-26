import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { QuotesService } from '../services/quotes.service';
import { AuthTokenService } from '../core/auth-token.service';
import { Quote } from '../models/quote.model';
import { ApiProblemError } from '../core/app-error.model';

type ViewState = 'loading' | 'success' | 'empty' | 'error';

@Component({
  selector: 'app-quote-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './quote-list.component.html',
  styleUrls: ['./quote-list.component.css'],
})
export class QuoteListComponent implements OnInit {
  private readonly quotesService = inject(QuotesService);
  readonly authService = inject(AuthTokenService);

  readonly state = signal<ViewState>('loading');
  readonly quotes = signal<Quote[]>([]);
  readonly currentError = signal<ApiProblemError | null>(null);

  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(5);

  ngOnInit(): void {
    this.authService.setToken('eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.quotes-user');
    this.fetchQuotes();
  }

  fetchQuotes(): void {
    this.state.set('loading');
    this.currentError.set(null);

    this.quotesService.getQuotes(this.currentPage(), this.pageSize()).subscribe({
      next: (data) => {
        this.quotes.set(data);
        if (data.length === 0) {
          this.state.set('empty');
        } else {
          this.state.set('success');
        }
      },
      error: (err: ApiProblemError) => {
        this.currentError.set(err);
        this.state.set('error');
      },
    });
  }

  triggerInvalidPageError(): void {
    this.currentPage.set(0);
    this.fetchQuotes();
  }

  triggerInvalidSizeError(): void {
    this.pageSize.set(999);
    this.fetchQuotes();
  }

  resetPagination(): void {
    this.currentPage.set(1);
    this.pageSize.set(5);
    this.fetchQuotes();
  }

  toggleAuth(): void {
    if (this.authService.getToken()) {
      this.authService.clearToken();
    } else {
      this.authService.setToken('eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.quotes-user');
    }
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
