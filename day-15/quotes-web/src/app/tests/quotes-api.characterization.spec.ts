import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QuotesService } from '../features/quotes/services/quotes.service';
import { API_BASE_URL } from '../core/tokens/api-base-url.token';
import { authInterceptor } from '../core/interceptors/auth.interceptor';
import { retryGetInterceptor } from '../core/interceptors/retry.interceptor';
import { errorMappingInterceptor } from '../core/interceptors/error-mapping.interceptor';
import { AuthService } from '../core/services/auth.service';
import { AppError } from '../core/models/app-error.model';
import { Quote } from '../features/quotes/models/quote.model';

describe('Quotes API Contract — Characterization Tests (Pinned against Week-1 Backend)', () => {
  let service: QuotesService;
  let httpMock: HttpTestingController;
  let authService: AuthService;
  const baseUrl = 'http://localhost:5000/api';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        QuotesService,
        AuthService,
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
    authService = TestBed.inject(AuthService);
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
        error: (error: AppError) => {
          expect(error).toBeInstanceOf(AppError);
          expect(error.status).toBe(400);
          expect(error.title).toBe('One or more validation errors occurred.');
          expect(error.validationErrors['page']).toEqual(['Page must be at least 1.']);
          expect(error.validationErrors['size']).toEqual(['Size must be between 1 and 100.']);
          expect(error.friendlyMessage).toContain('Page must be at least 1.');
          expect(error.friendlyMessage).toContain('Size must be between 1 and 100.');
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
        error: (error: AppError) => {
          expect(error).toBeInstanceOf(AppError);
          expect(error.status).toBe(404);
          expect(error.detail).toBe('Quote with ID 999 does not exist in the database.');
          expect(error.friendlyMessage).toBe('Quote with ID 999 does not exist in the database.');
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
      authService.setToken('mock-jwt-token-xyz');

      service.getQuotes(1, 5).subscribe();

      const req = httpMock.expectOne(`${baseUrl}/quotes?page=1&size=5`);
      expect(req.request.headers.has('Authorization')).toBeTrue();
      expect(req.request.headers.get('Authorization')).toBe('Bearer mock-jwt-token-xyz');
      req.flush([]);
    });

    it('does not inject Authorization header when token is null', () => {
      authService.clearToken();

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
        error: (error: AppError) => {
          expect(error.status).toBe(500);
        },
      });

      const req = httpMock.expectOne(`${baseUrl}/quotes`);
      expect(req.request.method).toBe('POST');
      req.flush({ title: 'Server Error' }, { status: 500, statusText: 'Internal Server Error' });
    });
  });
});
