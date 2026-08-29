import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { QuotesStore } from '../../core/state/quotes-store.service';
import { AuthTokenService } from '../../core/services/auth-token.service';

type CreateState = 'idle' | 'submitting' | 'error';

@Component({
  selector: 'app-quotes-panel',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './quotes-panel.component.html',
  styleUrls: ['./quotes-panel.component.css'],
})
export class QuotesPanelComponent {
  readonly store = inject(QuotesStore);
  readonly authService = inject(AuthTokenService);
  private readonly fb = inject(FormBuilder);

  readonly page = signal(1);
  readonly createState = signal<CreateState>('idle');
  readonly createError = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    author: ['', [Validators.required, Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  constructor() {
    this.store.load(this.page());
  }

  reload(): void {
    this.store.load(this.page());
  }

  /**
   * Clears the stored session and refreshes the list.
   *
   * The reload matters: the delete controls are rendered from
   * `authService.isAuthenticated()`, and the list itself is anonymous, so without it the
   * page would keep showing rows exactly as before with buttons that now 401.
   */
  signOut(): void {
    this.authService.clearToken();
    this.store.load(this.page());
  }

  nextPage(): void {
    this.page.update((p) => p + 1);
    this.store.load(this.page());
  }

  prevPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
      this.store.load(this.page());
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.createState.set('submitting');
    this.createError.set(null);

    this.store.create(this.form.getRawValue()).subscribe({
      next: () => {
        this.createState.set('idle');
        this.form.reset({ author: '', text: '' });
      },
      error: (err) => {
        this.createState.set('error');
        this.createError.set(
          err.status === 401
            ? 'You need to be signed in to create a quote.'
            : err.userMessage || 'Unable to create the quote right now.'
        );
      },
    });
  }

  deleteQuote(id: number): void {
    // Ownership isn't known client-side — the button is shown to any
    // signed-in user, and the server's 403 (enforced ownership) or 401
    // (session expired) is what the store's rollback message reports.
    this.store.remove(id).subscribe({ error: () => {} }); // rollback + message handled entirely inside the store
  }
}
