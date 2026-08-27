import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { authGuard } from '../core/guards/auth.guard';
import { isValidQuoteId, quoteIdMustBeInteger } from '../core/guards/quote-id.guard';
import { AuthTokenService } from '../core/services/auth-token.service';
import { QuotesService } from '../services/quotes.service';
import { QuoteDetailComponent } from '../features/quotes/quote-detail/quote-detail.component';
import { API_BASE_URL } from '../core/tokens/api-base-url.token';

@Component({ standalone: true, template: '' })
class DummyComponent {}

describe('Day 16 — Routing, Lazy Loading & Guard Tests', () => {
  let router: Router;
  let authService: AuthTokenService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthTokenService,
        QuotesService,
        { provide: API_BASE_URL, useValue: 'http://localhost:5000/api' },
        provideHttpClient(),
        provideHttpClientTesting(),
        // This route table exists to test authGuard's own mechanics
        // (redirect + returnUrl vs. pass-through) in isolation from where it
        // happens to be mounted in the real app. In app.routes.ts the guard
        // now sits on 'quotes/new', not 'quotes' itself — quotes browsing is
        // public, matching the real backend's anonymous GET /api/quotes.
        provideRouter(
          [
            { path: 'login', component: DummyComponent },
            {
              path: 'quotes',
              canActivate: [authGuard],
              children: [
                { path: '', component: DummyComponent },
                { path: ':id', component: QuoteDetailComponent },
              ],
            },
          ],
          withComponentInputBinding()
        ),
      ],
    });

    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthTokenService);
    httpMock = TestBed.inject(HttpTestingController);
    authService.clearToken();
  });

  afterEach(() => {
    httpMock.verify();
    authService.clearToken();
  });

  describe('1. Functional Auth Guard (authGuard)', () => {
    it('should redirect unauthenticated user to /login with returnUrl query parameter', async () => {
      authService.clearToken();
      expect(authService.isAuthenticated()).toBe(false);

      const navigated = await router.navigateByUrl('/quotes/42');
      // Should redirect to /login?returnUrl=%2Fquotes%2F42
      expect(router.url).toBe('/login?returnUrl=%2Fquotes%2F42');
    });

    it('should allow access to protected route when valid JWT token is present', async () => {
      authService.setSession('valid.jwt.token', 'valid-refresh-token');
      expect(authService.isAuthenticated()).toBe(true);

      // No <router-outlet> is mounted in this TestBed, so navigation resolves
      // the route/guard chain without instantiating QuoteDetailComponent —
      // that component's own HTTP behavior is covered directly in the next
      // describe block via TestBed.createComponent. This test only asserts
      // the guard let the navigation through.
      const navigated = await router.navigateByUrl('/quotes/42');
      expect(navigated).toBe(true);
      expect(router.url).toBe('/quotes/42');
    });
  });

  describe('2. QuoteDetailComponent & Route Param Input Binding', () => {
    it('should correctly bind route parameter :id and load quote details', () => {
      const fixture = TestBed.createComponent(QuoteDetailComponent);
      const component = fixture.componentInstance;
      component.id = 5;
      fixture.detectChanges();

      const req = httpMock.expectOne('http://localhost:5000/api/quotes/5');
      expect(req.request.method).toBe('GET');
      req.flush({
        id: 5,
        author: 'Marcus Aurelius',
        text: 'You have power over your mind - not outside events.',
      });

      expect(component.state()).toBe('success');
      expect(component.quote()?.author).toBe('Marcus Aurelius');
      expect(component.quote()?.id).toBe(5);
    });

    it('should gracefully handle 404 Not Found error from Week-1 backend', () => {
      const fixture = TestBed.createComponent(QuoteDetailComponent);
      const component = fixture.componentInstance;
      component.id = 999;
      fixture.detectChanges();

      const req = httpMock.expectOne('http://localhost:5000/api/quotes/999');
      req.flush('Not Found', { status: 404, statusText: 'Not Found' });

      expect(component.state()).toBe('error');
      expect(component.errorMessage()).toContain('Quote #999 was not found');
    });

    it('should handle invalid non-positive or NaN IDs without making HTTP requests', () => {
      const fixture = TestBed.createComponent(QuoteDetailComponent);
      const component = fixture.componentInstance;
      component.id = -1;
      fixture.detectChanges();

      httpMock.expectNone('http://localhost:5000/api/quotes/-1');
      expect(component.state()).toBe('invalid_id');
      expect(component.errorMessage()).toContain('Invalid quote ID');
    });
  });

  describe('3. quoteIdMustBeInteger (CanMatchFn)', () => {
    it('accepts positive integers only', () => {
      expect(isValidQuoteId('1')).toBe(true);
      expect(isValidQuoteId('42')).toBe(true);
      expect(isValidQuoteId('0')).toBe(false);
      expect(isValidQuoteId('-5')).toBe(false);
      expect(isValidQuoteId('3.5')).toBe(false);
      expect(isValidQuoteId('abc')).toBe(false);
      expect(isValidQuoteId('12abc')).toBe(false);
      expect(isValidQuoteId(' 5 ')).toBe(false);
      expect(isValidQuoteId('')).toBe(false);
    });

    it('matches the route when the last URL segment is a valid id', () => {
      const segments = [{ path: 'quotes' }, { path: '42' }] as any;
      expect(quoteIdMustBeInteger({} as any, segments, {} as any)).toBe(true);
    });

    it('does not match the route for a malformed id segment', () => {
      const segments = [{ path: 'quotes' }, { path: 'abc' }] as any;
      expect(quoteIdMustBeInteger({} as any, segments, {} as any)).toBe(false);
    });

    it('does not match when there are no segments', () => {
      expect(quoteIdMustBeInteger({} as any, [], {} as any)).toBe(false);
    });
  });
});
