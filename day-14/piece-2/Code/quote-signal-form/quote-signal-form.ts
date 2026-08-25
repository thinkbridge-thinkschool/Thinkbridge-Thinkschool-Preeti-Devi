import { Component, ElementRef, inject, OnInit, signal, computed, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { QuotesService } from '../services/quotes.service';
import { CreateQuoteRequest, Quote } from '../models/quote.model';

type FormState = 'idle' | 'submitting' | 'success' | 'error';

interface ValidationError {
  required?: boolean;
  minlength?: { requiredLength: number; actualLength: number };
  maxlength?: { requiredLength: number; actualLength: number };
}

@Component({
  selector: 'app-quote-signal-form',
  imports: [],
  templateUrl: './quote-signal-form.html',
  styleUrl: './quote-signal-form.css',
})
export class QuoteSignalFormComponent implements OnInit {
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);
  readonly author = signal('');
  readonly text = signal('');
  readonly authorTouched = signal(false);
  readonly textTouched = signal(false);
  readonly authorDirty = computed(() => this.author().length > 0);
  readonly textDirty = computed(() => this.text().length > 0);
  readonly isDirty = computed(() => this.authorDirty() || this.textDirty());
  readonly isPristine = computed(() => !this.isDirty());
  readonly authorErrors = computed<ValidationError | null>(() => {
    const val = this.author();
    if (!val || val.trim().length === 0) {
      return { required: true };
    }
    if (val.length > 100) {
      return { maxlength: { requiredLength: 100, actualLength: val.length } };
    }
    return null;
  });

  readonly textErrors = computed<ValidationError | null>(() => {
    const val = this.text();
    if (!val || val.trim().length === 0) {
      return { required: true };
    }
    if (val.trim().length < 10) {
      return { minlength: { requiredLength: 10, actualLength: val.trim().length } };
    }
    if (val.length > 500) {
      return { maxlength: { requiredLength: 500, actualLength: val.length } };
    }
    return null;
  });

  readonly isValid = computed(() => !this.authorErrors() && !this.textErrors());
  readonly isAuthorInvalid = computed(() => !!this.authorErrors() && this.authorTouched());
  readonly isTextInvalid = computed(() => !!this.textErrors() && this.textTouched());
  readonly authorCharCount = computed(() => this.author().length);
  readonly textCharCount = computed(() => this.text().length);
  readonly formState = signal<FormState>('idle');
  readonly serverError = signal<string | null>(null);
  readonly createdQuote = signal<Quote | null>(null);
  readonly quotesList = signal<Quote[]>([]);
  readonly isLoadingQuotes = signal(false);
  readonly authorInput = viewChild<ElementRef<HTMLInputElement>>('authorInput');
  readonly textInput = viewChild<ElementRef<HTMLTextAreaElement>>('textInput');
  readonly feedSection = viewChild<ElementRef<HTMLElement>>('feedSection');

  ngOnInit(): void {
    this.loadQuotes();
  }

  loadQuotes(): void {
    this.isLoadingQuotes.set(true);
    this.quotesService.getQuotes(1, 50).subscribe({
      next: (quotes) => {
        this.quotesList.set(quotes);
        this.isLoadingQuotes.set(false);
      },
      error: () => {
        this.isLoadingQuotes.set(false);
      },
    });
  }
  onAuthorInput(value: string): void {
    this.author.set(value);
  }

  onAuthorBlur(): void {
    this.authorTouched.set(true);
  }

  onTextInput(value: string): void {
    this.text.set(value);
  }

  onTextBlur(): void {
    this.textTouched.set(true);
  }

  markAllAsTouched(): void {
    this.authorTouched.set(true);
    this.textTouched.set(true);
  }

  onSubmit(): void {
    if (!this.isValid()) {
      this.markAllAsTouched();
      this.focusFirstInvalidField();
      return;
    }

    this.formState.set('submitting');
    this.serverError.set(null);

    const payload: CreateQuoteRequest = {
      author: this.author().trim(),
      text: this.text().trim(),
    };

    this.quotesService.createQuote(payload).subscribe({
      next: (created) => {
        this.createdQuote.set(created);
        this.formState.set('success');
        this.resetFormState();
        this.quotesList.update((prev) => [created, ...prev.filter((q) => q.id !== created.id)]);
      },
      error: (err) => {
        const message =
          err.status === 0
            ? 'Network error — check your connection and backend server.'
            : `Server returned ${err.status}: ${err.statusText || 'Bad Request'}`;
        this.serverError.set(message);
        this.formState.set('error');
      },
    });
  }

  resetForm(): void {
    this.resetFormState();
    this.formState.set('idle');
    this.serverError.set(null);
    this.createdQuote.set(null);
    this.authorInput()?.nativeElement.focus();
  }

  private resetFormState(): void {
    this.author.set('');
    this.text.set('');
    this.authorTouched.set(false);
    this.textTouched.set(false);
  }

  private focusFirstInvalidField(): void {
    if (this.authorErrors()) {
      this.authorInput()?.nativeElement.focus();
      return;
    }
    if (this.textErrors()) {
      this.textInput()?.nativeElement.focus();
    }
  }

  scrollToFeed(): void {
    this.feedSection()?.nativeElement.scrollIntoView({ behavior: 'smooth' });
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }
}
