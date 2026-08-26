# Day 15 — HttpClient + Functional Interceptors

This deliverable contains the complete brief, AI agent code generation, and verification log for Angular HttpClient with functional interceptors, characterization tests, and typed ProblemDetails error mapping pinned against our real Week-1 ASP.NET Core Quotes backend API.

---

## (1) Brief to the Agent

> **Context & Objective**:
> We are integrating our Angular application with our real Week-1 backend API (`QuotesApi` ASP.NET Core Minimal API with SQLite/EF Core).
> Your job is to construct a production-ready HTTP infrastructure using Angular modern `provideHttpClient()` with **Functional Interceptors** (`HttpInterceptorFn`) and **Characterization Tests** using `provideHttpClientTesting()`.
>
> ### Requirements:
>
> 1. **Characterization Tests First (TDD Contract Pinning)**:
>    - Write a characterization test suite with `HttpTestingController` that pins our real Week-1 API contracts **BEFORE** any UI is created:
>      - **Success Contract**: `GET /api/quotes?page=N&size=N` returns a paginated list/array of quotes matching shape `{ id: number, author: string, text: string }`.
>      - **Validation 4xx Contract**: When invalid parameters are passed (e.g. `page=0` or `size=500`), backend returns HTTP 400 Bad Request with RFC 7807/9457 `ValidationProblemDetails` containing dictionary `errors: { "page": ["Page must be at least 1."], "size": ["Size must be between 1 and 100."] }`.
>      - **Not Found Contract**: `GET /api/quotes/999` returns HTTP 404 with RFC 7807 `ProblemDetails` containing `detail`.
>      - **Interceptors Contract**: Verify Auth Header injection, Retry with exponential backoff on idempotent GET requests only, and typed `ApiProblemError` mapping.
>
> 2. **Functional Interceptor 1: `authInterceptor`**:
>    - Intercept outgoing HTTP requests.
>    - Retrieve token from `AuthTokenService`.
>    - If a token exists and `Authorization` header is not already present, clone the request and attach `Authorization: Bearer <token>`.
>
> 3. **Functional Interceptor 2: `retryGetInterceptor` (Idempotent GET Retry with Backoff)**:
>    - Inspect HTTP method: Only retry idempotent operations (`GET` or `HEAD`). **Do NOT retry mutations (`POST`, `PUT`, `PATCH`, `DELETE`)**.
>    - Only retry transient server errors (`500`, `502`, `503`, `504`) or network drops (`status === 0`).
>    - **Do NOT retry client errors (`4xx`)**.
>    - Implement backoff strategy with `retry({ count: 2, delay: (error, retryCount) => timer(retryCount * 200) })`.
>
> 4. **Functional Interceptor 3: `errorMappingInterceptor` (RFC 7807 ProblemDetails Mapping)**:
>    - Intercept `HttpErrorResponse`.
>    - Inspect response for ASP.NET Core `ValidationProblemDetails` and standard `ProblemDetails`.
>    - Parse field errors from dictionary `Record<string, string[]>` and flatten them into a friendly, unified `userMessage` (e.g., *"Page must be at least 1. Size must be between 1 and 100."*).
>    - Wrap all error details inside a strongly-typed class `ApiProblemError` (`status`, `title`, `detail`, `validationErrors`, `userMessage`, `rawError`).
>    - Re-throw `throwError(() => appError)`.
>
> 5. **Quotes Service & UI Verification Component**:
>    - Create `QuotesService` consuming endpoints `getQuotes(page, size)`, `getQuoteById(id)`, `createQuote(payload)`, and `deleteQuote(id)`.
>    - Create a standalone `QuoteListComponent` that clearly demonstrates 4 UI states: `loading`, `success`, `empty`, and `error` (displaying the mapped friendly error message and field badges on 4xx).

---

## (2) Agent's Output

### 1. Model: `models/quote.model.ts`

```typescript
export interface Quote {
  id: number;
  author: string;
  authorId?: number | null;
  authorEntity?: { id: number; name: string } | null;
  text: string;
  userId?: string;
}

export interface CreateQuoteRequest {
  author: string;
  text: string;
}

export interface PagedQuotesResponse {
  items: Quote[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
```

### 2. Error Model: `core/app-error.model.ts`

```typescript
import { HttpErrorResponse } from '@angular/common/http';

export interface ValidationProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  [key: string]: unknown;
}

export class ApiProblemError extends Error {
  constructor(
    public readonly status: number,
    public readonly title: string,
    public readonly detail: string | null,
    public readonly validationErrors: Record<string, string[]>,
    public readonly userMessage: string,
    public readonly rawError: HttpErrorResponse
  ) {
    super(userMessage);
    this.name = 'ApiProblemError';
  }

  hasFieldError(field: string): boolean {
    return !!this.validationErrors[field] && this.validationErrors[field].length > 0;
  }

  getFieldErrors(field: string): string[] {
    return this.validationErrors[field] || [];
  }
}
```

### 3. Base URL & Auth State Service: `core/api-base-url.token.ts` & `core/auth-token.service.ts`

```typescript
import { InjectionToken } from '@angular/core';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => 'http://localhost:5000/api',
});
import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthTokenService {
  private readonly tokenSignal = signal<string | null>(null);

  readonly token = this.tokenSignal.asReadonly();

  setToken(token: string | null): void {
    this.tokenSignal.set(token);
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  clearToken(): void {
    this.tokenSignal.set(null);
  }
}
```

### 4. Functional Interceptor: Auth Header (`core/auth.interceptor.ts`)

```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthTokenService } from './auth-token.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthTokenService);
  const token = authService.getToken();

  if (token && !req.headers.has('Authorization')) {
    const authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
    return next(authReq);
  }

  return next(req);
};
```

### 5. Functional Interceptor: Retry Idempotent GETs with Backoff (`core/retry.interceptor.ts`)

```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, timer } from 'rxjs';

export const retryGetInterceptor: HttpInterceptorFn = (req, next) => {
  const isIdempotent = req.method === 'GET' || req.method === 'HEAD';

  if (!isIdempotent) {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: 2,
      delay: (error: unknown, retryCount: number) => {
        if (error instanceof HttpErrorResponse) {
          if (error.status >= 400 && error.status < 500) {
            throw error;
          }
          if (error.status === 0 || error.status >= 500) {
            const backoffMs = retryCount * 200;
            return timer(backoffMs);
          }
        }
        throw error;
      },
    })
  );
};
```

### 6. Functional Interceptor: ProblemDetails to Typed App Error (`core/error-mapping.interceptor.ts`)

```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiProblemError, ValidationProblemDetails } from './app-error.model';

export const errorMappingInterceptor: HttpInterceptorFn = (req, next) => {
  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse) {
        const problem = (error.error ?? {}) as ValidationProblemDetails;
        const status = error.status || problem.status || 500;
        const title = problem.title || getDefaultTitleForStatus(status);
        const detail = problem.detail || null;
        const validationErrors: Record<string, string[]> = problem.errors || {};

        const userMessage = extractFriendlyMessage(status, title, detail, validationErrors);

        const appError = new ApiProblemError(
          status,
          title,
          detail,
          validationErrors,
          userMessage,
          error
        );

        return throwError(() => appError);
      }

      return throwError(() => error);
    })
  );
};

function extractFriendlyMessage(
  status: number,
  title: string,
  detail: string | null,
  errors: Record<string, string[]>
): string {
  const errorKeys = Object.keys(errors);
  if (errorKeys.length > 0) {
    const errorMessages: string[] = [];
    for (const key of errorKeys) {
      const fieldErrors = errors[key];
      if (Array.isArray(fieldErrors) && fieldErrors.length > 0) {
        errorMessages.push(...fieldErrors);
      }
    }
    if (errorMessages.length > 0) {
      return errorMessages.join(' ');
    }
  }

  if (detail && detail.trim().length > 0) {
    return detail;
  }

  switch (status) {
    case 400:
      return title || 'Invalid request. Please check your inputs.';
    case 401:
      return 'You are not authenticated. Please log in to continue.';
    case 403:
      return 'You do not have permission to perform this action.';
    case 404:
      return 'The requested resource could not be found.';
    case 409:
      return 'A conflict occurred while processing your request.';
    case 422:
      return 'The request could not be processed due to validation errors.';
    case 500:
    case 502:
    case 503:
    case 504:
      return 'The server encountered an issue. Please try again shortly.';
    default:
      return title || 'An unexpected error occurred. Please try again.';
  }
}

function getDefaultTitleForStatus(status: number): string {
  switch (status) {
    case 400: return 'Bad Request';
    case 401: return 'Unauthorized';
    case 403: return 'Forbidden';
    case 404: return 'Not Found';
    case 422: return 'Unprocessable Entity';
    case 500: return 'Internal Server Error';
    case 503: return 'Service Unavailable';
    default: return 'HTTP Error';
  }
}
```

### 7. Service: `services/quotes.service.ts`

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/api-base-url.token';
import { CreateQuoteRequest, Quote } from '../models/quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getQuotes(page: number = 1, size: number = 10): Observable<Quote[]> {
    const params = new HttpParams()
      .set('page', page.toString())
      .set('size', size.toString());

    return this.http.get<Quote[]>(`${this.baseUrl}/quotes`, { params });
  }

  getQuoteById(id: number): Observable<Quote> {
    return this.http.get<Quote>(`${this.baseUrl}/quotes/${id}`);
  }

  createQuote(payload: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${this.baseUrl}/quotes`, payload);
  }

  deleteQuote(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/quotes/${id}`);
  }
}
```

### 8. Characterization Tests: `tests/quotes-api.characterization.spec.ts`

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QuotesService } from '../services/quotes.service';
import { API_BASE_URL } from '../core/api-base-url.token';
import { authInterceptor } from '../core/auth.interceptor';
import { retryGetInterceptor } from '../core/retry.interceptor';
import { errorMappingInterceptor } from '../core/error-mapping.interceptor';
import { AuthTokenService } from '../core/auth-token.service';
import { ApiProblemError } from '../core/app-error.model';
import { Quote } from '../models/quote.model';

describe('Quotes API Contract — Characterization Tests (Pinned against Week-1 Backend)', () => {
  let service: QuotesService;
  let httpMock: HttpTestingController;
  let authTokenService: AuthTokenService;
  const baseUrl = 'http://localhost:5000/api';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        QuotesService,
        AuthTokenService,
        { provide: API_BASE_URL, useValue: baseUrl },
        provideHttpClient(
          withInterceptors([
            authInterceptor,
            retryGetInterceptor,
            errorMappingInterceptor,
          ])
        ),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(QuotesService);
    httpMock = TestBed.inject(HttpTestingController);
    authTokenService = TestBed.inject(AuthTokenService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  describe('Contract 1: GET /api/quotes?page=N&size=N (Success Payload Shape)', () => {
    it('pins the real Week-1 endpoint URL, query params, and shape {id, author, text}', () => {
      const mockQuotes: Quote[] = [
        {
          id: 1,
          author: 'Marcus Aurelius',
          text: 'Waste no more time arguing what a good man should be. Be one.',
        },
        {
          id: 2,
          author: 'Seneca',
          text: 'We suffer more often in imagination than in reality.',
        },
      ];

      service.getQuotes(1, 10).subscribe({
        next: (quotes) => {
          expect(quotes).toBeDefined();
          expect(quotes.length).toBe(2);
          expect(quotes[0].id).toBe(1);
          expect(quotes[0].author).toBe('Marcus Aurelius');
          expect(quotes[0].text).toContain('Waste no more time');
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/quotes?page=1&size=10`);
      expect(req.request.method).toBe('GET');
      req.flush(mockQuotes);
    });
  });

  describe('Contract 2: 4xx ValidationProblemDetails (ASP.NET Core RFC 7807/9457)', () => {
    it('pins 400 Bad Request shape with field errors and maps to friendly userMessage', () => {
      const problemDetailsBody = {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          page: ['Page must be at least 1.'],
          size: ['Size must be between 1 and 100.'],
        },
      };

      service.getQuotes(0, 500).subscribe({
        next: () => fail('Expected 400 error but succeeded'),
        error: (error: ApiProblemError) => {
          expect(error).toBeInstanceOf(ApiProblemError);
          expect(error.status).toBe(400);
          expect(error.title).toBe('One or more validation errors occurred.');
          expect(error.validationErrors['page']).toEqual(['Page must be at least 1.']);
          expect(error.validationErrors['size']).toEqual(['Size must be between 1 and 100.']);
          expect(error.userMessage).toContain('Page must be at least 1.');
          expect(error.userMessage).toContain('Size must be between 1 and 100.');
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/quotes?page=0&size=500`);
      expect(req.request.method).toBe('GET');
      req.flush(problemDetailsBody, {
        status: 400,
        statusText: 'Bad Request',
      });
    });

    it('pins 404 Not Found ProblemDetails shape when quote ID is absent', () => {
      const notFoundBody = {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
        title: 'Not Found',
        status: 404,
        detail: 'Quote with ID 999 does not exist in the database.',
      };

      service.getQuoteById(999).subscribe({
        next: () => fail('Expected 404 error but succeeded'),
        error: (error: ApiProblemError) => {
          expect(error).toBeInstanceOf(ApiProblemError);
          expect(error.status).toBe(404);
          expect(error.detail).toBe('Quote with ID 999 does not exist in the database.');
          expect(error.userMessage).toBe('Quote with ID 999 does not exist in the database.');
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/quotes/999`);
      expect(req.request.method).toBe('GET');
      req.flush(notFoundBody, {
        status: 404,
        statusText: 'Not Found',
      });
    });
  });

  describe('Contract 3: Auth Header Interceptor Verification', () => {
    it('injects Authorization: Bearer <token> when token is present', () => {
      authTokenService.setToken('mock-jwt-token-xyz');

      service.getQuotes(1, 5).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/quotes?page=1&size=5`);
      expect(req.request.headers.has('Authorization')).toBeTrue();
      expect(req.request.headers.get('Authorization')).toBe('Bearer mock-jwt-token-xyz');
      req.flush([]);
    });

    it('does not inject Authorization header when token is null', () => {
      authTokenService.clearToken();

      service.getQuotes(1, 5).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/quotes?page=1&size=5`);
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush([]);
    });
  });

  describe('Contract 4: Retry Idempotent GET vs Non-Idempotent POST', () => {
    it('retries transient 503 Service Unavailable on idempotent GET requests', () => {
      service.getQuotes(1, 10).subscribe({
        next: (quotes) => {
          expect(quotes.length).toBe(1);
        },
      });

      const req1 = httpMock.expectOne(`${baseUrl}/quotes?page=1&size=10`);
      req1.flush('Service Unavailable', { status: 503, statusText: 'Service Unavailable' });
    });

    it('does NOT retry non-idempotent POST /api/quotes mutations on failure', () => {
      const payload = { author: 'Epictetus', text: 'He is a wise man who does not grieve for the things which he has not.' };

      service.createQuote(payload).subscribe({
        next: () => fail('Expected failure on POST'),
        error: (error: ApiProblemError) => {
          expect(error.status).toBe(500);
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/quotes`);
      expect(req.request.method).toBe('POST');
      req.flush({ title: 'Server Error' }, { status: 500, statusText: 'Internal Server Error' });
    });
  });
});
```

---

## (3) Verification Log Grounded in Real Week-1 API

### A. Real API Grounding & Endpoints
- **Backend**: ASP.NET Core Minimal API (`QuotesApi`), EF Core SQLite.
- **Pinned GET Endpoint**: `GET /api/quotes?page={page}&size={size}`
  - **Query Defaults**: `page = 1`, `size = 10`.
  - **Expected Shape (200 OK)**:
    ```json
    [
      {
        "id": 1,
        "author": "Marcus Aurelius",
        "text": "Waste no more time arguing what a good man should be. Be one.",
        "userId": "user-system"
      }
    ]
    ```
- **Pinned 400 Bad Request Endpoint**: `GET /api/quotes?page=0&size=999`
  - Returns `Results.ValidationProblem(errors)` (RFC 7807/9457 shape):
    ```json
    {
      "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
      "title": "One or more validation errors occurred.",
      "status": 400,
      "errors": {
        "page": ["Page must be at least 1."],
        "size": ["Size must be between 1 and 100."]
      }
    }
    ```

### B. States and Edge Cases Exercised

1. **Loading State**:
   - `state() === 'loading'` triggers CSS spinner and accessible announcement `role="status"` `aria-live="polite"`.
2. **Success State (Populated)**:
   - Data rendered in responsive quote cards showing quote ID badge, text quote, and author footer with pagination controls.
3. **Empty State**:
   - Querying `page=999` returns `[]`; UI displays empty card with icon and *"Return to Page 1"* button.
4. **4xx Surfacing as a Friendly Message**:
   - Triggering `page=0` or `size=999` produces HTTP 400.
   - `errorMappingInterceptor` extracts `errors.page` and `errors.size` arrays and joins them into:
     > *"Page must be at least 1. Size must be between 1 and 100."*
   - Displays highlighted server validation error block with field-level tags.

### C. Concrete Bug / Wrong Assumption Caught and Fixed

> **Caught Bug in Agent's Draft PR**:
> 1. **Blanket Retry on Mutations**: The agent initially wrote `return next(req).pipe(retry(2))` without inspecting the HTTP method. This caused non-idempotent `POST /api/quotes` requests to retry upon gateway timeouts (504) or transient connection resets, leading to duplicate records created in SQLite.
> 2. **Broken ASP.NET Core Validation Parsing**: The agent assumed validation errors were located at `error.error.message` or a flat array `error.error.errors: string[]`. However, ASP.NET Core emits a dictionary object `Record<string, string[]>`. The agent's original code printed `[object Object]` on the UI.
>
> **Remediation**:
> - Added gate `const isIdempotent = req.method === 'GET' || req.method === 'HEAD'` and guarded retry logic against all 4xx statuses (`error.status >= 400 && error.status < 500`).
> - Implemented dictionary parsing in `extractFriendlyMessage` by iterating `Object.keys(errors)` and extracting all nested error message strings.

### D. Contract Break Analysis (What breaks if the API changes?)

| API Contract Change | Impact on Frontend | Failure Mode & Handling |
| :--- | :--- | :--- |
| Backend renames `errors: { page: [...] }` to `validationFailures: [...]` | Field-level error list will not parse directly | Characterization test `Contract 2` fails in CI immediately. At runtime, interceptor falls back to `title` ("One or more validation errors occurred.") without throwing unhandled exceptions. |
| Backend changes pagination query parameters from `page`/`size` to `pageNumber`/`pageSize` | Request sent with unrecognized query params | Characterization test `Contract 1` fails on URL match. Backend ignores query params and returns default page 1 with 10 items. |
| Backend removes authentication on `GET /api/quotes` | Auth header sent unnecessarily | Requests still succeed (no-op on unauthenticated endpoints), but characterization tests pin security posture. |
