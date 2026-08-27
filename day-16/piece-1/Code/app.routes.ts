import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { quoteIdMustBeInteger } from './core/guards/quote-id.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'quotes',
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent),
    title: 'Login | Quotes App',
  },
  {
    // Read access matches the real Week-1 API: GET /api/quotes and
    // GET /api/quotes/{id} are anonymous on the backend, so browsing quotes
    // requires no sign-in. POST /api/quotes DOES require an authenticated
    // quotes.write token, so only the 'new' child route below is guarded.
    path: 'quotes',
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/quotes/quote-list/quote-list.component').then(
            (m) => m.QuoteListComponent
          ),
        title: 'Browse Quotes | Quotes App',
      },
      {
        // Must come before ':id' — otherwise the wildcard param route would
        // swallow '/quotes/new' as an (invalid) id instead of matching this.
        path: 'new',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/quotes/quote-create/quote-create.component').then(
            (m) => m.QuoteCreateComponent
          ),
        title: 'New Quote | Quotes App',
      },
      {
        // canMatch (not canActivate): an invalid id means this route never
        // matches at all, so the router falls through to '**' below instead
        // of activating the component and cancelling navigation mid-flight.
        // The bad URL stays visible and zero HTTP requests fire.
        path: ':id',
        canMatch: [quoteIdMustBeInteger],
        loadComponent: () =>
          import('./features/quotes/quote-detail/quote-detail.component').then(
            (m) => m.QuoteDetailComponent
          ),
        title: 'Quote Detail | Quotes App',
      },
    ],
  },
  {
    // A real page, not redirectTo: 'quotes' — redirectTo silently rewrites
    // the address bar and loses whatever the user actually typed, making a
    // typo undiagnosable. This renders in place and echoes the attempted URL.
    path: '**',
    loadComponent: () =>
      import('./features/shell/not-found/not-found.component').then(
        (m) => m.NotFoundComponent
      ),
    title: 'Not Found | Quotes App',
  },
];
