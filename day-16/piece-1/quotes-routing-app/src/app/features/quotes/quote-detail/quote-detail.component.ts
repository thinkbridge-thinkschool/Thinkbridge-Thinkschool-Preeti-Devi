import { Component, Input, OnInit, inject, signal, numberAttribute } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { QuotesService } from '../../../services/quotes.service';
import { Quote } from '../../../models/quote.model';

type DetailState = 'loading' | 'success' | 'error' | 'invalid_id';

@Component({
  selector: 'app-quote-detail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './quote-detail.component.html',
  styleUrls: ['./quote-detail.component.css'],
})
export class QuoteDetailComponent implements OnInit {
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  // Route param bound directly via withComponentInputBinding()
  // Uses Angular numberAttribute transform for type-safe parsing
  @Input({ transform: numberAttribute }) id?: number;

  readonly state = signal<DetailState>('loading');
  readonly quote = signal<Quote | null>(null);
  readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadQuote();
  }

  loadQuote(): void {
    if (this.id === undefined || Number.isNaN(this.id) || this.id <= 0) {
      this.state.set('invalid_id');
      this.errorMessage.set(
        `Invalid quote ID specified: "${this.id}". ID must be a positive integer.`
      );
      return;
    }

    this.state.set('loading');
    this.errorMessage.set(null);

    this.quotesService.getQuoteById(this.id).subscribe({
      next: (data) => {
        this.quote.set(data);
        this.state.set('success');
      },
      error: (err) => {
        this.state.set('error');
        if (err.status === 404) {
          this.errorMessage.set(`Quote #${this.id} was not found in the database.`);
        } else {
          this.errorMessage.set(
            err.userMessage || 'An unexpected error occurred while fetching quote details.'
          );
        }
      },
    });
  }

  goBack(): void {
    // Preserve the active ?q= filter (if any) — matches the routerLink into
    // this page, which uses queryParamsHandling="preserve" for the same
    // reason: an explicit Back click shouldn't lose the filter any more than
    // the browser's own Back button does.
    this.router.navigate(['/quotes'], { queryParamsHandling: 'preserve' });
  }
}
