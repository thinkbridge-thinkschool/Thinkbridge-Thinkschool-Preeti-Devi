import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { QuotesService } from '../../../services/quotes.service';

type CreateState = 'idle' | 'submitting' | 'error';

@Component({
  selector: 'app-quote-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="create-container">
      <div class="nav-bar">
        <a routerLink="/quotes" class="btn-back" id="back-to-list-link">&larr; Back to Quotes List</a>
      </div>

      <div class="create-card">
        <h2>New Quote</h2>
        <p class="desc">
          Posts to the real Week-1 <code>POST /api/quotes</code> endpoint, which
          requires an authenticated <code>quotes.write</code> token.
        </p>

        @if (state() === 'error') {
          <div class="error-alert" id="create-error-banner" role="alert">
            {{ errorMessage() }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field">
            <label for="author-input">Author</label>
            <input
              id="author-input"
              type="text"
              formControlName="author"
              maxlength="100"
              [attr.aria-invalid]="authorInvalid ? 'true' : null"
              [attr.aria-describedby]="authorInvalid ? 'author-error' : null"
            />
            @if (authorInvalid) {
              <div class="field-error" id="author-error" role="alert">
                Author is required (max 100 characters).
              </div>
            }
          </div>

          <div class="field">
            <label for="text-input">Quote text</label>
            <textarea
              id="text-input"
              rows="4"
              formControlName="text"
              maxlength="1000"
              [attr.aria-invalid]="textInvalid ? 'true' : null"
              [attr.aria-describedby]="textInvalid ? 'text-error' : null"
            ></textarea>
            @if (textInvalid) {
              <div class="field-error" id="text-error" role="alert">
                Quote text is required (max 1000 characters).
              </div>
            }
          </div>

          <button
            type="submit"
            class="btn-create"
            id="create-quote-btn"
            [disabled]="state() === 'submitting'"
          >
            {{ state() === 'submitting' ? 'Creating…' : 'Create Quote' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .create-container {
      max-width: 640px;
      margin: 2rem auto;
      padding: 0 1rem;
      font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
    }
    .nav-bar { margin-bottom: 1.5rem; }
    .btn-back {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 0.5rem 0.9rem;
      border-radius: 8px;
      color: #334155;
      text-decoration: none;
      font-weight: 600;
      font-size: 0.9rem;
    }
    .create-card {
      background: white;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 2rem;
      box-shadow: 0 2px 4px rgba(0,0,0,0.02);
    }
    h2 { margin-top: 0; color: #0f172a; }
    .desc {
      color: #64748b;
      font-size: 0.9rem;
      margin-bottom: 1.5rem;
    }
    .desc code {
      font-size: 0.85em;
      background: #f1f5f9;
      padding: 1px 5px;
      border-radius: 4px;
    }
    .error-alert {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.75rem;
      border-radius: 6px;
      font-size: 0.88rem;
      margin-bottom: 1.25rem;
    }
    .field { margin-bottom: 1.1rem; }
    .field label {
      display: block;
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
      margin-bottom: 0.35rem;
    }
    .field input,
    .field textarea {
      width: 100%;
      padding: 0.65rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 1rem;
      font-family: inherit;
      box-sizing: border-box;
      resize: vertical;
    }
    .field input:focus,
    .field textarea:focus {
      outline: 2px solid #2563eb;
      outline-offset: 1px;
      border-color: #2563eb;
    }
    .field input[aria-invalid='true'],
    .field textarea[aria-invalid='true'] {
      border-color: #dc2626;
    }
    .field-error {
      color: #b91c1c;
      font-size: 0.8rem;
      margin-top: 0.3rem;
    }
    .btn-create {
      background: #2563eb;
      color: white;
      border: none;
      padding: 0.8rem 1.5rem;
      border-radius: 8px;
      font-weight: 600;
      font-size: 1rem;
      cursor: pointer;
      width: 100%;
      transition: background 0.2s;
    }
    .btn-create:hover:not(:disabled) { background: #1d4ed8; }
    .btn-create:disabled { background: #93c5fd; cursor: not-allowed; }
  `]
})
export class QuoteCreateComponent {
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  readonly state = signal<CreateState>('idle');
  readonly errorMessage = signal<string | null>(null);

  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  get authorInvalid(): boolean {
    const control = this.form.controls.author;
    return control.invalid && control.touched;
  }

  get textInvalid(): boolean {
    const control = this.form.controls.text;
    return control.invalid && control.touched;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.state.set('submitting');
    this.errorMessage.set(null);

    this.quotesService.createQuote(this.form.getRawValue()).subscribe({
      next: (created) => {
        this.router.navigate(['/quotes', created.id]);
      },
      error: (err) => {
        this.state.set('error');
        this.errorMessage.set(
          err.status === 401
            ? 'You need to be signed in to create a quote.'
            : err.userMessage || 'Unable to create the quote right now.'
        );
      },
    });
  }
}
