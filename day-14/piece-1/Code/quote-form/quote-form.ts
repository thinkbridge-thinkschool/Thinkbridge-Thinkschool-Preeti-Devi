import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { QuotesService } from '../services/quotes.service';
import { CreateQuoteRequest, Quote } from '../models/quote.model';

type FormState = 'idle' | 'submitting' | 'success' | 'error';

@Component({
  selector: 'app-quote-form',
  imports: [ReactiveFormsModule],
  templateUrl: './quote-form.html',
  styleUrl: './quote-form.css',
})
export class QuoteFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly quotesService = inject(QuotesService);

  readonly formState = signal<FormState>('idle');
  readonly serverError = signal<string | null>(null);
  readonly createdQuote = signal<Quote | null>(null);

  readonly authorInput = viewChild<ElementRef<HTMLInputElement>>('authorInput');
  readonly textInput = viewChild<ElementRef<HTMLTextAreaElement>>('textInput');

  readonly quoteForm: FormGroup = this.fb.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
  });

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
