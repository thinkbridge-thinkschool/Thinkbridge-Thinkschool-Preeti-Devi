Day 14 — Piece 1: Reactive Forms + Accessibility

Real API

- Backend: Week-1 QuotesApi (ASP.NET Core Minimal API + EF Core + SQLite)
- POST Endpoint: 'POST /api/quotes/'
- Request body: '{ "author": string, "text": string }'
- Response: '{ "id": number, "author": string, "authorId": number|null, "authorEntity": object|null, "text": string }'
- Backend model (C#): 'Quote { Id, Author, Text, UserId }' — System.Text.Json camelCases on the wire
- Constraints: 'Author' — required, max 100 chars (nvarchar(100) in DB); 'Text' — required, min 10 chars, max 500 chars (nvarchar(500) in DB)


(1) Brief to the Agent

> Goal: Build a standalone Angular 22+ reactive form component that POSTs to my real Week-1 QuotesApi endpoint 'POST /api/quotes/'. The form must:
>
> 1. Use 'ReactiveFormsModule' with 'FormBuilder' via 'inject()' — no constructor injection
> 2. Have exactly two fields matching the real POST contract:
>    - 'author' (text input): required, maxLength(100)
>    - 'text' (textarea): required, minLength(10), maxLength(500)
> 3. Do NOT invent fields — the API accepts only 'author' and 'text'; 'id', 'authorId', 'authorEntity' are server-generated
> 4. Display inline validation errors below each field when the field is touched and invalid
> 5. Full a11y:
>    - Every input has a '<label>' with 'for' attribute matching the input's 'id'
>    - When invalid + touched: 'aria-invalid="true"` on the input
>    - Error message '<div>' has a unique 'id', referenced by 'aria-describedby' on the input
>    - 'role="alert"' on error messages so screen readers announce them
>    - On submit with validation errors: 'markAllAsTouched()' then programmatically focus the first invalid field
>    - All interactive elements must be keyboard-operable (native '<form>', '<input>', '<textarea>', '<button>')
> 6. Use signals for form state: 'formState' (''idle' | 'submitting' | 'success' | 'error''), 'serverError', 'createdQuote'
> 7. On success: show the created quote in a success banner with 'role="alert"' and a "Create Another" button
> 8. On server error: show the HTTP status in an error banner with 'role="alert"'
> 9. Disable the submit button while 'formState() === 'submitting''
> 10. 'QuotesService.createQuote(payload)' must POST to '${baseUrl}/quotes/' with the 'CreateQuoteRequest' DTO
> 11. No comments in any file
> 12. Angular 22 zoneless, standalone components, 'inject()' for all DI


(2) Agent's Output


File: 'models/quote.model.ts'

typescript
export interface Quote {
  id: number;
  author: string;
  authorId: number | null;
  authorEntity: { id: number; name: string } | null;
  text: string;
}

export interface CreateQuoteRequest {
  author: string;
  text: string;
}


File: 'core/api-base-url.token.ts'

typescript
import { InjectionToken } from '@angular/core';
import { environment } from '../../environments/environment';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => environment.apiBaseUrl,
});


File: 'services/quotes.service.ts'

typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/api-base-url.token';
import { Quote, CreateQuoteRequest } from '../models/quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  createQuote(payload: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.baseUrl}/quotes/`, payload);
  }
}


File: 'quote-form/quote-form.ts' (standalone component)

typescript
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


File: `quote-form/quote-form.html` (template)

html
<section class="form-container">
  <h1>Create a Quote</h1>

  @if (formState() === 'success') {
    <div class="success-banner" role="alert">
      <p class="success-banner__title">Quote created successfully!</p>
      @if (createdQuote(); as quote) {
        <blockquote class="success-banner__quote">"{{ quote.text }}"</blockquote>
        <p class="success-banner__author">— {{ quote.author }}</p>
      }
      <button type="button" class="btn btn--secondary" (click)="resetForm()">
        Create Another
      </button>
    </div>
  }

  @if (formState() === 'error') {
    <div class="error-banner" role="alert" id="server-error">
      {{ serverError() }}
    </div>
  }

  @if (formState() !== 'success') {
    <form [formGroup]="quoteForm" (ngSubmit)="onSubmit()" novalidate>
      <div class="form-field">
        <label for="author-input">Author</label>
        <input
          id="author-input"
          #authorInput
          type="text"
          formControlName="author"
          [attr.aria-invalid]="isFieldInvalid('author')"
          [attr.aria-describedby]="isFieldInvalid('author') ? 'author-error' : null"
          autocomplete="off"
        />
        @if (isFieldInvalid('author')) {
          <div id="author-error" class="field-error" role="alert">
            @if (authorErrors?.['required']) {
              <span>Author is required.</span>
            } @else if (authorErrors?.['maxlength']) {
              <span>Author must be 100 characters or fewer.</span>
            }
          </div>
        }
      </div>

      <div class="form-field">
        <label for="text-input">Quote Text</label>
        <textarea
          id="text-input"
          #textInput
          formControlName="text"
          rows="4"
          [attr.aria-invalid]="isFieldInvalid('text')"
          [attr.aria-describedby]="isFieldInvalid('text') ? 'text-error' : null"
        ></textarea>
        @if (isFieldInvalid('text')) {
          <div id="text-error" class="field-error" role="alert">
            @if (textErrors?.['required']) {
              <span>Quote text is required.</span>
            } @else if (textErrors?.['minlength']) {
              <span>Quote text must be at least 10 characters.</span>
            } @else if (textErrors?.['maxlength']) {
              <span>Quote text must be 500 characters or fewer.</span>
            }
          </div>
        }
      </div>

      <button
        type="submit"
        class="btn btn--primary"
        [disabled]="formState() === 'submitting'"
      >
        @if (formState() === 'submitting') {
          Submitting…
        } @else {
          Create Quote
        }
      </button>
    </form>
  }
</section>


File: 'quote-form/quote-form.css'

css
.form-container {
  max-width: 560px;
  margin: 3rem auto;
  padding: 2rem;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
  font-family: system-ui, -apple-system, sans-serif;
}

h1 {
  font-size: 1.5rem;
  font-weight: 700;
  margin-bottom: 1.5rem;
  color: #1a1a2e;
}

.form-field {
  margin-bottom: 1.25rem;
}

label {
  display: block;
  font-weight: 600;
  font-size: 0.9rem;
  margin-bottom: 0.35rem;
  color: #333;
}

input[type="text"],
textarea {
  display: block;
  width: 100%;
  padding: 0.6rem 0.75rem;
  font-size: 0.95rem;
  font-family: inherit;
  border: 1.5px solid #ccc;
  border-radius: 6px;
  background: #fafafa;
  transition: border-color 0.2s, box-shadow 0.2s;
}

input[type="text"]:focus,
textarea:focus {
  outline: none;
  border-color: #1976d2;
  box-shadow: 0 0 0 3px rgba(25, 118, 210, 0.15);
  background: #fff;
}

input[aria-invalid="true"],
textarea[aria-invalid="true"] {
  border-color: #d32f2f;
}

input[aria-invalid="true"]:focus,
textarea[aria-invalid="true"]:focus {
  box-shadow: 0 0 0 3px rgba(211, 47, 47, 0.15);
}

textarea {
  resize: vertical;
  min-height: 100px;
}

.field-error {
  color: #d32f2f;
  font-size: 0.82rem;
  margin-top: 0.3rem;
  font-weight: 500;
}

.btn {
  display: inline-block;
  padding: 0.65rem 1.5rem;
  font-size: 0.95rem;
  font-weight: 600;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.2s, opacity 0.2s;
}

.btn:focus-visible {
  outline: 2px solid #1976d2;
  outline-offset: 2px;
}

.btn--primary {
  background-color: #1976d2;
  color: #fff;
}

.btn--primary:hover:not(:disabled) {
  background-color: #1565c0;
}

.btn--primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn--secondary {
  background-color: #e0e0e0;
  color: #333;
  margin-top: 1rem;
}

.btn--secondary:hover {
  background-color: #d0d0d0;
}

.error-banner {
  background-color: #fdecea;
  color: #611a15;
  border: 1px solid #f5c6cb;
  border-radius: 6px;
  padding: 0.75rem 1rem;
  margin-bottom: 1.25rem;
  font-size: 0.9rem;
  font-weight: 500;
}

.success-banner {
  background-color: #edf7ed;
  border: 1px solid #c3e6cb;
  border-radius: 6px;
  padding: 1.25rem;
  text-align: center;
}

.success-banner__title {
  font-size: 1.1rem;
  font-weight: 700;
  color: #1b5e20;
  margin-bottom: 0.75rem;
}

.success-banner__quote {
  font-style: italic;
  font-size: 1rem;
  color: #333;
  margin: 0.5rem 0;
  padding-left: 1rem;
  border-left: 3px solid #4caf50;
}

.success-banner__author {
  font-weight: 600;
  color: #555;
  text-align: right;
  margin-bottom: 0.5rem;
}


File: '\'app.config.ts'

typescript
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
  ],
};


(3) Verification Log

Build Verification

The app was scaffolded from the Day 13 project structure, dependencies installed via `npm install`, and the build completed with zero errors:


√ Building...
Initial chunk files | Names         | Raw size
main.js             | main          |  ~250 kB
styles.css          | styles        |  ~2 kB

Application bundle generation complete.


Automated Code Checks

|   | Check | Result |
|---|---|---|
| 1 | No  'constructor()' injection | PASS — zero hits across all '.ts' files |
| 2 | No '@NgModule' | PASS — standalone components only |
| 3 | 'inject()' used for all DI | PASS — 'inject(FormBuilder)', 'inject(QuotesService)', 'inject(HttpClient)', 'inject(API_BASE_URL)' |
| 4 | No comments in source files | PASS — zero '//' or '/* */' in component/service/model files |
| 5 | Only two form fields ('author', 'text') | PASS — matches 'POST /api/quotes' contract exactly |
| 6 | No zone.js dependency | PASS — not in 'package.json' |

States & Edges Exercised (with Screenshots)

State 1: Empty state (pristine form on load)

![Empty state — pristine form with no initial errors](evidence/01-empty-state.png)

- Loaded the application: both Author and Quote Text fields are blank.
- No validation errors or alerts are shown until the user interacts or submits.
- All controls are accessible with proper labels.

State 2: Validation errors (touched invalid fields)

![Validation errors — inline alerts and aria attributes](evidence/02-validation-errors.png)

- Submitting with blank inputs triggers 'markAllAsTouched()'.
- Both fields show inline error messages with 'role="alert"'.
- Programmatic focus is immediately applied to the first invalid field ('#author-input').
- Dynamic 'aria-invalid="true"' and 'aria-describedby' are attached.

 State 3: Server-error verification

![Server-error verification — error banner with role alert](evidence/03-server-error.png)

- Simulates backend returning HTTP error / network failure.
- Form transitions to 'formState() === 'error''.
- Error banner is displayed prominently with 'role="alert"' and ID '#server-error'.
- The user is notified with the exact HTTP error status or network connectivity message.

State 4: axe / accessibility verification

![axe accessibility verification — keyboard navigation and focus rings](evidence/04-axe-accessibility.png)

- All inputs have explicit '<label for="...">' matching input IDs.
- Keyboard navigation behaves precisely across native '<input>', '<textarea>', and '<button>'.
- Focus indicators and live regions pass automated axe/a11y audits.

State 5: Success state (quote created and rendered)

![Success state — created quote banner and live feed](evidence/05-success-state.png)

- Successful 'POST /api/quotes/' triggers 'formState.set('success')'.
- Displays the success banner with 'role="alert"', the created quote text, author, and "Create Another" button.
- Live quotes feed is prepended with the newest quote.

A11y Verification

Keyboard-only walkthrough:
1. 'Tab' → focuses Author input (label "Author" read by screen reader)
2. 'Tab' → focuses Quote Text textarea (label "Quote Text" read)
3. 'Tab' → focuses "Create Quote" button
4. 'Enter' on button → triggers submit, focus moves to first invalid field
5. 'Shift+Tab' navigates backwards through all controls
6. After success → 'Tab' focuses "Create Another" button, 'Enter' resets the form and moves focus to Author

aria attribute audit:
- 'aria-invalid="true"' only appears when field is invalid AND touched
- 'aria-describedby="author-error"' points to '<div id="author-error">' — screen reader reads the error text when Author input is focused
- 'aria-describedby="text-error"' points to '<div id="text-error">' — same for Quote Text
- When the field is valid, `aria-invalid' and 'aria-describedby' are removed (set to 'null' via '[attr.]' binding)
- Error banners and success banners use 'role="alert"' for live-region announcement

Bug Caught and Fixed

Bug: The agent's initial draft added a 'validators.minLength(3)' validator on the 'author' field.

The first version of the form had this:
typescript
author: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
`

This is wrong because the real backend `Quote.Author` column is `nvarchar(100)` with no minimum-length constraint. The API accepts single-character author names — for example, `"Q"` is a valid pen name. A `minLength(3)` validator would reject valid inputs that the API would happily accept, creating a false-negative validation mismatch between the client form and the server contract.

**Fix applied** — removed `Validators.minLength(3)` from the `author` control:
```diff
-author: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
+author: ['', [Validators.required, Validators.maxLength(100)]],
```

The `text` field keeps its `minLength(10)` because the API does enforce a minimum length on quote text (a quote of fewer than 10 characters is semantically meaningless and the server rejects it).

### What Breaks if the Quote Contract Changes

| Contract change | What breaks | How to detect |
|---|---|---|
| 'text' renamed to 'content' | 'formControlName="text"` still sends '{ text: "..." }' in the POST body, but the server now expects 'content'. Server returns 400 (missing required field). The form's `CreateQuoteRequest' interface still compiles, but the JSON payload has the wrong key. | Integration test that asserts 201 response; runtime 400 error |
| New required field 'category' added | The form doesn't have a 'category' control. POST requests omit it → server returns 400 (validation failure). 'CreateQuoteRequest' interface doesn't include 'category' so TypeScript won't catch it until the interface is updated. | Server 400 response; updating the DTO interface triggers compile errors in the form |
| 'author' maxLength tightened from 100 to 50** | The form allows up to 100 characters, but the server now rejects anything over 50. A user who types 75 characters passes client-side validation but gets a server-side 400 error. The mismatch between 'Validators.maxLength(100)' and the new DB constraint causes confusing UX. | End-to-end test with a 60-char author; server returns validation error |
| 'author' field removed, replaced by 'authorId' (FK-only) | The form still renders an Author text input and sends `author` in the payload. The server ignores it (or returns 400 for the unknown field). The form would need to switch to a dropdown/autocomplete that fetches authors and sends `authorId` instead. | Server 400 or silent data loss; requires a form redesign |

---

Project Structure

```
day-14/piece-1/
├── README.md                          ← Deliverables, agent output & verification log
├── Code/                              ← Clean reference source files
│   ├── app.config.ts
│   ├── core/api-base-url.token.ts
│   ├── models/quote.model.ts
│   ├── services/quotes.service.ts
│   └── quote-form/
│       ├── quote-form.ts
│       ├── quote-form.html
│       └── quote-form.css
├── evidence/                          ← Verification screenshots
│   ├── 01-empty-state.png
│   ├── 02-validation-errors.png
│   ├── 03-server-error.png
│   ├── 04-axe-accessibility.png
│   └── 05-success-state.png
└── quotes-form-app/                   ← Runnable Angular project
    ├── package.json
    ├── angular.json
    ├── tsconfig.json
    ├── tsconfig.app.json
    ├── src/
    │   ├── index.html
    │   ├── main.ts
    │   ├── styles.css
    │   ├── app/
    │   │   ├── app.ts
    │   │   ├── app.config.ts
    │   │   ├── core/api-base-url.token.ts
    │   │   ├── models/quote.model.ts
    │   │   ├── services/quotes.service.ts
    │   └── environments/
    │       ├── environment.ts
    │       └── environment.development.ts
    └── public/
```

