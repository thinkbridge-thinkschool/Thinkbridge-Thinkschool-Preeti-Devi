import { Component, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { QuotesService } from '../services/quotes.service';
import { CreateQuoteRequest, Quote } from '../models/quote.model';

type FormState = 'idle' | 'submitting' | 'success' | 'error';

@Component({
  selector: 'app-quote-form',
  imports: [ReactiveFormsModule],
  templateUrl: './quote-form.html',
  styleUrl: './quote-form.css',
})
export class QuoteFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  readonly formState = signal<FormState>('idle');
  readonly serverError = signal<string | null>(null);
  readonly createdQuote = signal<Quote | null>(null);
  readonly quotesList = signal<Quote[]>([]);
  readonly isLoadingQuotes = signal(false);

  readonly authorInput = viewChild<ElementRef<HTMLInputElement>>('authorInput');
  readonly textInput = viewChild<ElementRef<HTMLTextAreaElement>>('textInput');
  readonly feedSection = viewChild<ElementRef<HTMLElement>>('feedSection');

  readonly quoteForm: FormGroup = this.fb.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
  });

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

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

  scrollToFeed(): void {
    this.feedSection()?.nativeElement.scrollIntoView({ behavior: 'smooth' });
  }

  get authorErrors() {
    return this.quoteForm.get('author')!.errors;
  }

  get authorTouched() {
    return this.quoteForm.get('author')!.touched;
  }

  get textErrors() {
    return this.quoteForm.get('text')!.errors;
  }

  get textTouched() {
    return this.quoteForm.get('text')!.touched;
  }

  isFieldInvalid(fieldName: string): boolean {
    const control = this.quoteForm.get(fieldName);
    return control !== null && control.invalid && control.touched;
  }

  onSubmit(): void {
    if (this.quoteForm.invalid) {
      this.quoteForm.markAllAsTouched();
      this.focusFirstInvalidField();
      return;
    }

    this.formState.set('submitting');
    this.serverError.set(null);

    const payload: CreateQuoteRequest = {
      author: this.quoteForm.value.author.trim(),
      text: this.quoteForm.value.text.trim(),
    };

    this.quotesService.createQuote(payload).subscribe({
      next: (created) => {
        this.createdQuote.set(created);
        this.formState.set('success');
        this.quoteForm.reset();
        // Immediately add to top of feed list
        this.quotesList.update((prev) => [created, ...prev.filter((q) => q.id !== created.id)]);
      },
      error: (err) => {
        const message = err.status === 0
          ? 'Network error — check your connection and try again.'
          : `Server returned ${err.status}: ${err.statusText || 'unknown error'}`;
        this.serverError.set(message);
        this.formState.set('error');
      },
    });
  }

  resetForm(): void {
    this.quoteForm.reset();
    this.formState.set('idle');
    this.serverError.set(null);
    this.createdQuote.set(null);
    this.authorInput()?.nativeElement.focus();
  }

  private focusFirstInvalidField(): void {
    if (this.quoteForm.get('author')?.invalid) {
      this.authorInput()?.nativeElement.focus();
      return;
    }
    if (this.quoteForm.get('text')?.invalid) {
      this.textInput()?.nativeElement.focus();
    }
  }
}
