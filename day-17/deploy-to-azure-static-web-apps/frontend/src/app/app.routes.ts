import { Routes } from '@angular/router';

// Minimal on purpose — this app exists to demonstrate QuotesStore
// (core/state/quotes-store.service.ts), not routing/guards (that's Day 16
// Piece 1). Login is kept only so the create-with-a-real-session and
// create-while-signed-out scenarios in the verification log are both
// reachable in one running app.
export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/quotes-panel/quotes-panel.component').then(
        (m) => m.QuotesPanelComponent
      ),
    title: 'Quotes Store Demo',
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent),
    title: 'Login | Quotes Store Demo',
  },
  {
    path: '**',
    redirectTo: '',
  },
];
