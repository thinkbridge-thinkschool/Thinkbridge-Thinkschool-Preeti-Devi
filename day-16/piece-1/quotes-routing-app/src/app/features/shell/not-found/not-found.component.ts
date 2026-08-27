import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="not-found-container">
      <div class="not-found-card">
        <span class="code">404</span>
        <h2>Page Not Found</h2>
        <p class="attempted-url">
          Nothing here matches <code>{{ attemptedUrl }}</code>.
        </p>
        <a routerLink="/quotes" class="btn-back" id="back-to-quotes-link">
          &larr; Back to Quotes
        </a>
      </div>
    </div>
  `,
  styles: [`
    .not-found-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 60vh;
      font-family: 'Segoe UI', system-ui, sans-serif;
    }
    .not-found-card {
      background: white;
      padding: 2.5rem;
      border-radius: 12px;
      box-shadow: 0 4px 15px rgba(0,0,0,0.08);
      border: 1px solid #e2e8f0;
      max-width: 440px;
      width: 100%;
      text-align: center;
    }
    .code {
      display: block;
      font-size: 3rem;
      font-weight: 800;
      color: #cbd5e1;
      line-height: 1;
      margin-bottom: 0.5rem;
    }
    h2 {
      margin: 0 0 0.75rem 0;
      color: #0f172a;
    }
    .attempted-url {
      color: #64748b;
      margin-bottom: 1.75rem;
      word-break: break-all;
    }
    .attempted-url code {
      background: #f1f5f9;
      padding: 1px 6px;
      border-radius: 4px;
      color: #1e293b;
      font-weight: 600;
    }
    .btn-back {
      display: inline-block;
      background: #2563eb;
      color: white;
      padding: 0.7rem 1.4rem;
      border-radius: 8px;
      font-weight: 600;
      text-decoration: none;
    }
  `]
})
export class NotFoundComponent {
  // Read once at construction — the URL the router actually failed to
  // match, so a typo is visible/diagnosable instead of silently rewritten.
  readonly attemptedUrl = inject(Router).url;
}
