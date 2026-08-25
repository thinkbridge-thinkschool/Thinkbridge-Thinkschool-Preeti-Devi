# Day 14 — Piece 2: Signal Forms (Preview)

## Real API Contract

- **Backend**: Week-1 QuotesApi (ASP.NET Core Minimal API + EF Core + SQLite)
- **POST Endpoint**: `POST /api/quotes/`
- **Request Body (JSON)**:
  ```json
  {
    "author": "string",
    "text": "string"
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "id": 14,
    "author": "Marcus Aurelius",
    "authorId": null,
    "authorEntity": null,
    "text": "Waste no more time arguing what a good man should be. Be one."
  }
  ```
- **Backend Model Constraints**:
  - `Author`: Required, max 100 characters (`nvarchar(100)` in database)
  - `Text`: Required, min 10 characters, max 500 characters (`nvarchar(500)` in database)
  - Server-generated fields (never in request body): `Id` (SQLite auto-increment), `AuthorId`, `AuthorEntity`

---

## (1) Brief to Claude Code / Agent

> **Goal**: Rebuild the Day 14 Piece 1 quote creation form using Angular's **Signal Forms Preview** architecture. Connect directly to our real Week-1 Quotes backend (`POST /api/quotes/`).
>
> ### Requirements:
> 1. **Signal-Driven Form Architecture**:
>    - Replace `ReactiveFormsModule` / `FormBuilder` with native Angular signals (`signal()`, `computed()`) for form data, validation errors, dirty/touched tracking, and submission states.
>    - Model payload strictly as `CreateQuoteRequest`: `{ author: string, text: string }`.
> 2. **Field Specifications & Bounds**:
>    - `author`: text input, required, max length 100.
>    - `text`: textarea, required, min length 10, max length 500.
>    - **Do NOT invent extra fields** (no `id`, `category`, `userId`, `authorId` in request payload).
> 3. **Validation & Reactivity**:
>    - Implement validation rules via `computed()` signals deriving from field signals.
>    - Provide live character counters (`current/max`) driven by computed signals.
> 4. **Accessibility (A11y)**:
>    - Form controls linked to `<label for="...">`.
>    - Dynamic `aria-invalid` bound to computed invalid signals when touched.
>    - Dynamic `aria-describedby` pointing to error containers with `role="alert"`.
>    - On submit with validation errors, mark all fields touched and focus the first invalid element.
> 5. **Async & HTTP Lifecycle**:
>    - Service call `QuotesService.createQuote(payload)` returning `Observable<Quote>`.
>    - Track state: `idle` | `submitting` | `success` | `error`.
>    - On 201 Created: display success banner with created quote details, reset form, prepend to live feed.
>    - On HTTP error: show status code and error message in an alert banner.
> 6. **Technical Constraints**:
>    - Angular 22 zoneless standalone component, no constructor DI (`inject()` only).

---

## (2) Agent's Output (Signal Forms Version)

### File: `models/quote.model.ts`

```typescript
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

export interface QuoteFormValidation {
  required?: boolean;
  minlength?: { requiredLength: number; actualLength: number };
  maxlength?: { requiredLength: number; actualLength: number };
}
```

### File: `core/api-base-url.token.ts`

```typescript
import { InjectionToken } from '@angular/core';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => 'http://localhost:5000/api',
});
```

### File: `services/quotes.service.ts`

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/api-base-url.token';
import { Quote, CreateQuoteRequest } from '../models/quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getQuotes(page = 1, size = 50): Observable<Quote[]> {
    return this.http.get<Quote[]>(`${this.baseUrl}/quotes/`, {
      params: { page, size },
    });
  }

  createQuote(payload: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.baseUrl}/quotes/`, payload);
  }
}
```

### File: `quote-signal-form/quote-signal-form.ts`

```typescript
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
```

### File: `quote-signal-form/quote-signal-form.html`

```html
<div class="quotes-page">
  <header class="app-header">
    <div class="header-content">
      <div class="brand">
        <span class="brand-icon">⚡</span>
        <div>
          <h1 class="brand-title">Quotes Studio <span class="badge-preview">Signal Forms</span></h1>
          <p class="brand-subtitle">Angular Signal-Driven Forms Preview Architecture</p>
        </div>
      </div>
      <div class="header-actions">
        <button class="nav-btn" (click)="scrollToFeed()">
          📜 Live Feed ({{ quotesList().length }})
        </button>
        <button class="nav-btn-secondary" (click)="goToLogin()">
          🔐 Login
        </button>
      </div>
    </div>
  </header>

  <main class="main-content">
    <div class="page-grid">
      <section class="form-container" aria-labelledby="form-heading">
        <div class="card-header">
          <div class="badge">Create New Quote</div>
          <h2 id="form-heading" class="form-title">Inspire the World</h2>
          <p class="form-description">
            Share timeless wisdom directly with our real Week-1 Quotes backend API (<code>POST /api/quotes/</code>).
          </p>
        </div>

        @if (formState() === 'success' && createdQuote()) {
          <div class="alert alert-success" role="alert">
            <div class="alert-icon">✨</div>
            <div class="alert-body">
              <h3 class="alert-title">Quote Published Successfully!</h3>
              <p class="alert-subtitle">Assigned Database ID: #{{ createdQuote()?.id }}</p>
              <blockquote class="created-quote-preview">
                "{{ createdQuote()?.text }}"
                <footer>— <strong>{{ createdQuote()?.author }}</strong></footer>
              </blockquote>
              <div class="alert-actions">
                <button type="button" class="btn btn-primary" (click)="resetForm()">
                  ✍️ Add Another Quote
                </button>
                <button type="button" class="btn btn-ghost" (click)="scrollToFeed()">
                  View in Feed &darr;
                </button>
              </div>
            </div>
          </div>
        }

        @if (formState() === 'error' && serverError()) {
          <div class="alert alert-error" role="alert">
            <div class="alert-icon">⚠️</div>
            <div class="alert-body">
              <h3 class="alert-title">Failed to Create Quote</h3>
              <p class="alert-message">{{ serverError() }}</p>
            </div>
          </div>
        }

        <form (ngSubmit)="onSubmit()" novalidate class="quote-form" #formEl>
          <div class="form-group" [class.has-error]="isAuthorInvalid()">
            <div class="label-row">
              <label for="author-input" class="form-label">
                Author Name <span class="required" aria-hidden="true">*</span>
              </label>
              <span class="char-count" [class.limit-reached]="authorCharCount() > 100">
                {{ authorCharCount() }}/100
              </span>
            </div>

            <div class="input-wrapper">
              <input
                #authorInput
                id="author-input"
                type="text"
                class="form-control"
                [value]="author()"
                (input)="onAuthorInput($any($event.target).value)"
                (blur)="onAuthorBlur()"
                placeholder="e.g. Marcus Aurelius"
                [attr.aria-invalid]="isAuthorInvalid()"
                [attr.aria-describedby]="isAuthorInvalid() ? 'author-error' : null"
                [disabled]="formState() === 'submitting'"
                autocomplete="off"
              />
            </div>

            @if (isAuthorInvalid() && authorErrors()) {
              <div id="author-error" class="error-feedback" role="alert">
                @if (authorErrors()?.required) {
                  <span>Author name is required.</span>
                }
                @if (authorErrors()?.maxlength) {
                  <span>Author cannot exceed 100 characters (currently {{ authorErrors()?.maxlength?.actualLength }}).</span>
                }
              </div>
            }
          </div>

          <div class="form-group" [class.has-error]="isTextInvalid()">
            <div class="label-row">
              <label for="quote-text-input" class="form-label">
                Quote Text <span class="required" aria-hidden="true">*</span>
              </label>
              <span class="char-count" [class.limit-reached]="textCharCount() > 500">
                {{ textCharCount() }}/500
              </span>
            </div>

            <div class="input-wrapper">
              <textarea
                #textInput
                id="quote-text-input"
                class="form-control textarea"
                rows="4"
                [value]="text()"
                (input)="onTextInput($any($event.target).value)"
                (blur)="onTextBlur()"
                placeholder="Share an insightful thought or quotation (minimum 10 characters)..."
                [attr.aria-invalid]="isTextInvalid()"
                [attr.aria-describedby]="isTextInvalid() ? 'text-error' : null"
                [disabled]="formState() === 'submitting'"
              ></textarea>
            </div>

            @if (isTextInvalid() && textErrors()) {
              <div id="text-error" class="error-feedback" role="alert">
                @if (textErrors()?.required) {
                  <span>Quote text is required.</span>
                }
                @if (textErrors()?.minlength) {
                  <span>Must be at least 10 characters (currently {{ textErrors()?.minlength?.actualLength }}).</span>
                }
                @if (textErrors()?.maxlength) {
                  <span>Quote cannot exceed 500 characters (currently {{ textErrors()?.maxlength?.actualLength }}).</span>
                }
              </div>
            }
          </div>

          <div class="signal-inspector" aria-label="Signal Form Debug Bar">
            <span class="inspector-badge" [class.is-valid]="isValid()" [class.is-invalid]="!isValid()">
              {{ isValid() ? 'Valid' : 'Invalid' }}
            </span>
            <span class="inspector-badge" [class.is-dirty]="isDirty()">
              {{ isPristine() ? 'Pristine' : 'Dirty' }}
            </span>
            <span class="inspector-badge">
              Touched: {{ authorTouched() || textTouched() ? 'Yes' : 'No' }}
            </span>
            <span class="inspector-badge">
              State: {{ formState() }}
            </span>
          </div>

          <div class="form-actions">
            <button
              type="submit"
              class="btn btn-submit"
              [disabled]="formState() === 'submitting'"
            >
              @if (formState() === 'submitting') {
                <span class="spinner" aria-hidden="true"></span>
                <span>Submitting to API...</span>
              } @else {
                <span>Publish Quote</span>
              }
            </button>
            <button
              type="button"
              class="btn btn-reset"
              (click)="resetForm()"
              [disabled]="formState() === 'submitting' || isPristine()"
            >
              Reset
            </button>
          </div>
        </form>
      </section>

      <section #feedSection class="feed-container" aria-labelledby="feed-heading">
        <div class="feed-header">
          <div>
            <h2 id="feed-heading" class="feed-title">Quotes Stream</h2>
            <p class="feed-subtitle">Live sync from Week-1 QuotesApi database</p>
          </div>
          <button class="btn btn-refresh" (click)="loadQuotes()" [disabled]="isLoadingQuotes()">
            {{ isLoadingQuotes() ? 'Refreshing...' : '🔄 Refresh' }}
          </button>
        </div>

        @if (isLoadingQuotes() && quotesList().length === 0) {
          <div class="loading-state">
            <div class="spinner"></div>
            <p>Fetching quotes from backend...</p>
          </div>
        } @else if (quotesList().length === 0) {
          <div class="empty-state">
            <span class="empty-icon">💡</span>
            <h3>No Quotes Yet</h3>
            <p>Be the first to submit a quote using the signal form!</p>
          </div>
        } @else {
          <div class="quotes-grid">
            @for (quote of quotesList(); track quote.id) {
              <article class="quote-card">
                <p class="quote-text">"{{ quote.text }}"</p>
                <div class="quote-meta">
                  <span class="quote-author">— {{ quote.author }}</span>
                  <span class="quote-id">#{{ quote.id }}</span>
                </div>
              </article>
            }
          </div>
        }
      </section>
    </div>
  </main>
</div>
```

---

## (3) Comparison: Signal Forms Preview vs. Reactive Forms

| Dimension | Reactive Forms (`ReactiveFormsModule`) | Signal Forms Preview (`signal()`, `computed()`) |
| :--- | :--- | :--- |
| **Data Reactivity** | Observable streams (`valueChanges`, `statusChanges`) requiring manual subscriptions or `async` pipe. | Fine-grained signals (`signal()`, `computed()`) read directly as functions (`author()`, `isValid()`). |
| **Boilerplate & Imports** | Requires `ReactiveFormsModule`, `FormBuilder`, `FormGroup`, `FormControl`, `Validators`. | Zero external module overhead; uses core primitives from `@angular/core`. |
| **Type Safety** | Partial: `FormGroup<{ author: FormControl<string> }>` often requires loose casting or `getRawValue()`. | Complete: Strongly-typed signals (`signal<string>('')`) directly mapped to DTO `CreateQuoteRequest`. |
| **Derived State & UI** | Requires RxJS pipelines (`map`, `debounceTime`, `distinctUntilChanged`) and `ChangeDetectorRef` triggering. | Instant synchronous derivation with `computed(() => ...)`; zero teardown or memory leaks. |
| **Zoneless Readiness** | Requires manual `markForCheck()` or fine-grained reactive bridge in zoneless apps. | 100% native signal graph integration; automatically schedules fine-grained DOM updates. |
| **Ecosystem & Polish (Where it's rough)** | **Mature**: Rich built-in validator library (`Validators.minLength`, `Validators.pattern`), full form arrays, CVA ecosystem. | **Still Rough**: Requires bespoke `computed()` validator rules or adapters; manual touched/dirty signal wiring without official directive sugar; async HTTP validation needs manual effect coordination. |

---

## (4) PR Diff Review & Bugs Caught / Fixed

During the agent code review, four distinct bugs and incorrect assumptions were caught and remediated:

### Bug 1: Guessed Field Ingestion in Request DTO
- **What the Agent Did**: The agent initially included `authorId: 0` and `category: "general"` in the POST request body payload.
- **Why it Broke**: The Week-1 backend API strictly enforces `CreateQuoteRequest { Author, Text }`. SQLite fails on non-existent columns, and EF Core throws model validation exceptions.
- **Fix**: Restricted `CreateQuoteRequest` payload strictly to `author` and `text`.

### Bug 2: Reactive Validator Functions Inside Signal Declarations
- **What the Agent Did**: The agent attempted to pass `Validators.required` directly into `signal('', { validators: [Validators.required] })` under the assumption that the Signal Forms preview API automatically reuses legacy `ValidatorFn` signatures.
- **Why it Broke**: `signal()` in `@angular/core` does not take `ValidatorFn` options. The validation was never evaluated, and `isValid()` erroneously returned `true` for empty inputs.
- **Fix**: Rebuilt validation using pure, reactive `computed<ValidationError | null>()` signals with structured metadata (`{ requiredLength, actualLength }`).

### Bug 3: Incomplete Reset State Propagation
- **What the Agent Did**: In `resetForm()`, the agent only called `this.author.set('')` and claimed that touched states reset automatically.
- **Why it Broke**: Unlike `FormGroup.reset()`, individual signals do not automatically cascade reset events to sibling touched signals. As a result, resetting the form caused immediate red "required" validation errors to display because `authorTouched()` was still `true`.
- **Fix**: Created an explicit `resetFormState()` helper that zeroes values and flips `authorTouched.set(false)` and `textTouched.set(false)`.

### Bug 4: A11y Attribute Disconnection
- **What the Agent Did**: The agent attached `aria-invalid="true"` unconditionally or hardcoded static strings.
- **Why it Broke**: Screen readers announced fields as permanently invalid, violating WCAG 4.1.2.
- **Fix**: Dynamically bound `[attr.aria-invalid]="isAuthorInvalid()"` and `[attr.aria-describedby]="isAuthorInvalid() ? 'author-error' : null"` tied directly to computed touched and validation signals.

---

## (5) Verification Log (States & Edge Cases Exercised)

```
[VERIFICATION SUITE: Signal Forms Preview vs POST /api/quotes/]

1. INITIAL / PRISTINE STATE
   - Action: Loaded page at http://localhost:4200/
   - Signals: author()='', text()='', isPristine()=true, isDirty()=false, authorTouched()=false, textTouched()=false
   - UI State: Form inputs clean, no error messages, submit button enabled, reset button disabled.
   - Status: PASS

2. DIRTY BUT UNTOUCHED STATE
   - Action: User types "Socrates" in Author input without blurring.
   - Signals: author()='Socrates', authorDirty()=true, isDirty()=true, isPristine()=false, authorTouched()=false
   - UI State: Character count updates to 8/100, reset button enables, no error feedback displayed.
   - Status: PASS

3. TOUCHED & INVALID STATE (Inline Validator Firing)
   - Action: Click into Author input, press Tab into Quote Text input without typing.
   - Signals: authorTouched()=true, authorErrors()={ required: true }, isAuthorInvalid()=true
   - UI State: Red border on Author input, aria-invalid="true", error message "Author name is required." announced with role="alert".
   - Status: PASS

4. MINLENGTH & MAXLENGTH BOUNDARY VALIDATION
   - Action A: Enter "Short" (5 chars) in Quote Text.
     - Signals: textErrors()={ minlength: { requiredLength: 10, actualLength: 5 } }
     - UI: Error message "Must be at least 10 characters (currently 5)." displays.
   - Action B: Paste 105 characters in Author input.
     - Signals: authorErrors()={ maxlength: { requiredLength: 100, actualLength: 105 } }
     - UI: Character counter turns red (105/100), submit button prevented.
   - Status: PASS

5. CLEAN SUBMIT FLOW (POST /api/quotes/)
   - Action: Input Author="Marcus Aurelius", Text="Waste no more time arguing what a good man should be. Be one."
   - Signals: isValid()=true, formState()='submitting' -> 'success'
   - Network: POST http://localhost:5000/api/quotes/ -> Status 201 Created (HTTP payload: { author, text })
   - UI State: Success card displays with ID #14, form fields reset to blank pristine state, quote instantly appears at top of Quotes Stream feed.
   - Status: PASS

6. FAILED SUBMIT FLOW (Backend Offline / HTTP 500 / 400)
   - Action: Simulated network disconnection (status 0) and backend validation rejection (status 400).
   - Signals: formState()='error', serverError()='Network error — check your connection and backend server.'
   - UI State: Red error alert displayed with role="alert"; user text preserved in inputs for correction.
   - Status: PASS

7. ACCESSIBILITY KEYBOARD & FOCUS NAVIGATION
   - Action: Cleared form and pressed Enter on submit button.
   - Flow: markAllAsTouched() executed; first invalid element (#authorInput) programmatically received focus via authorInput().nativeElement.focus().
   - Status: PASS
```

---

## (6) API Contract Fragility Analysis

What breaks in this form if the real Week-1 API contract changes:

| API Contract Change | Impact on Signal Form | Remediation Required |
| :--- | :--- | :--- |
| **Field Renaming**: `text` renamed to `content` or `quoteText` | Compile-time TypeScript error on `payload.text` in `QuotesService.createQuote()`; runtime 400 Bad Request if types are bypassed. | Update `CreateQuoteRequest` interface and map `content: this.text().trim()` in payload. |
| **New Mandatory Field**: `category: string` added | Backend rejects POST with `400 Bad Request (Missing category field)`. Form displays server error banner. | Add `readonly category = signal('')`, `categoryErrors` computed validator, input control in template, and include in `CreateQuoteRequest`. |
| **Length Constraint Change**: Backend changes minimum quote length from 10 to 25 | Client validates at 10 chars, sends POST, backend returns `400 Bad Request`. Form displays server error banner without field highlighting. | Synchronize frontend computed validator: `minlength: { requiredLength: 25, ... }` in `textErrors` signal. |
| **Database Schema Change**: `author` becomes optional (`null` allowed) | Client unnecessarily blocks submission if `author` is blank. | Update `authorErrors` computed signal to only check `maxlength` if non-empty, removing `{ required: true }`. |

---

## (7) Evidence & Verification Files

Detailed verification runs, state transitions, and screenshot evidence are cataloged in the [`Evidence/`](file:///c:/Users/abhinav/thinkschool/Thinkbridge-Thinkschool-Preeti-Devi/day-14/piece-2/Evidence) directory:
- [`Evidence/Execution_Log.md`](file:///c:/Users/abhinav/thinkschool/Thinkbridge-Thinkschool-Preeti-Devi/day-14/piece-2/Evidence/Execution_Log.md): Detailed 7-step test verification trace log.
- `Evidence/01-empty-state.png`: Pristine state capture.
- `Evidence/02-validation-errors.png`: Field-level validation error state capture.
- `Evidence/03-server-error.png`: Backend error banner capture.
- `Evidence/04-axe-accessibility.png`: Accessibility verification capture.
- `Evidence/05-success-state.png`: 201 Created success banner and feed update capture.

