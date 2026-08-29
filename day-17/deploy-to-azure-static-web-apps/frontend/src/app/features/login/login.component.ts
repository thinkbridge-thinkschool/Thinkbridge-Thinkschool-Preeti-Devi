import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthTokenService } from '../../core/services/auth-token.service';
import { AuthApiService } from '../../services/auth-api.service';

type LoginState = 'idle' | 'submitting' | 'error';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="login-container">
      <div class="login-card">
        <h2>Sign In</h2>
        @if (returnUrl) {
          <div class="info-alert" id="return-url-banner">
            You tried to access: <code>{{ returnUrl }}</code>. Please log in to continue.
          </div>
        }

        <p class="desc">
          Signs in against the real Week-1 <code>POST /api/auth/login</code> endpoint.
        </p>

        <div class="mock-creds-box" id="mock-credentials-banner">
          <span class="mock-creds-label">Demo credentials</span>
          <div class="mock-creds-row">
            <span>Username: <code>{{ mockUsername }}</code></span>
            <span>Password: <code>{{ mockPassword }}</code></span>
          </div>
        </div>

        @if (state() === 'error') {
          <div class="error-alert" id="login-error-banner" role="alert">
            {{ errorMessage() }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="login()" novalidate>
          <div class="field">
            <label for="username-input">Username</label>
            <input
              id="username-input"
              type="text"
              formControlName="username"
              autocomplete="username"
              [placeholder]="mockUsername"
              [attr.aria-invalid]="usernameInvalid ? 'true' : null"
              [attr.aria-describedby]="usernameInvalid ? 'username-error' : null"
            />
            @if (usernameInvalid) {
              <div class="field-error" id="username-error" role="alert">Username is required.</div>
            }
          </div>

          <div class="field">
            <label for="password-input">Password</label>
            <input
              id="password-input"
              type="password"
              formControlName="password"
              autocomplete="current-password"
              [placeholder]="mockPassword"
              [attr.aria-invalid]="passwordInvalid ? 'true' : null"
              [attr.aria-describedby]="passwordInvalid ? 'password-error' : null"
            />
            @if (passwordInvalid) {
              <div class="field-error" id="password-error" role="alert">Password is required.</div>
            }
          </div>

          <button
            type="submit"
            class="btn-login"
            id="login-btn"
            [disabled]="state() === 'submitting'"
          >
            {{ state() === 'submitting' ? 'Signing in…' : 'Log In' }}
          </button>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .login-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 60vh;
      font-family: 'Segoe UI', system-ui, sans-serif;
    }
    .login-card {
      background: white;
      padding: 2.5rem;
      border-radius: 12px;
      box-shadow: 0 4px 15px rgba(0,0,0,0.08);
      border: 1px solid #e2e8f0;
      max-width: 440px;
      width: 100%;
      text-align: center;
    }
    h2 {
      margin-top: 0;
      color: #0f172a;
    }
    .info-alert {
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      color: #1e40af;
      padding: 0.75rem;
      border-radius: 6px;
      font-size: 0.88rem;
      margin-bottom: 1.25rem;
    }
    .info-alert code {
      font-weight: bold;
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
    .desc {
      color: #64748b;
      margin-bottom: 1.5rem;
    }
    .desc code {
      font-size: 0.85em;
      background: #f1f5f9;
      padding: 1px 5px;
      border-radius: 4px;
    }
    form {
      text-align: left;
    }
    .field {
      margin-bottom: 1.1rem;
    }
    .field label {
      display: block;
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
      margin-bottom: 0.35rem;
    }
    .field input {
      width: 100%;
      padding: 0.65rem 0.8rem;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      font-size: 1rem;
      box-sizing: border-box;
    }
    .field input:focus {
      outline: 2px solid #2563eb;
      outline-offset: 1px;
      border-color: #2563eb;
    }
    .field input[aria-invalid='true'] {
      border-color: #dc2626;
    }
    .field-error {
      color: #b91c1c;
      font-size: 0.8rem;
      margin-top: 0.3rem;
    }
    .mock-creds-box {
      background: #f8fafc;
      border: 1px dashed #cbd5e1;
      border-radius: 8px;
      padding: 0.65rem 0.85rem;
      margin-bottom: 1.25rem;
    }
    .mock-creds-label {
      display: block;
      font-size: 0.72rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: #475569;
      margin-bottom: 0.35rem;
    }
    .mock-creds-row {
      display: flex;
      gap: 1.25rem;
      font-size: 0.85rem;
      color: #475569;
    }
    .mock-creds-row code {
      background: #eef2f7;
      padding: 1px 6px;
      border-radius: 4px;
      font-weight: 600;
      color: #1e293b;
    }
    .btn-login {
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
    .btn-login:hover:not(:disabled) {
      background: #1d4ed8;
    }
    .btn-login:disabled {
      background: #93c5fd;
      cursor: not-allowed;
    }
  `]
})
export class LoginComponent implements OnInit {
  private readonly authService = inject(AuthTokenService);
  private readonly authApi = inject(AuthApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  returnUrl: string | null = null;

  // Mock credentials the real Week-1 backend's /api/auth/login accepts —
  // surfaced on-screen since this is a demo login, not a real account system.
  readonly mockUsername = 'testuser';
  readonly mockPassword = 'password';

  readonly state = signal<LoginState>('idle');
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || null;
  }

  get usernameInvalid(): boolean {
    const control = this.form.controls.username;
    return control.invalid && control.touched;
  }

  get passwordInvalid(): boolean {
    const control = this.form.controls.password;
    return control.invalid && control.touched;
  }

  login(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.state.set('submitting');
    this.errorMessage.set(null);

    const { username, password } = this.form.getRawValue();

    this.authApi.login({ username, password }).subscribe({
      next: (session) => {
        this.authService.setSession(session.token, session.refreshToken);
        const destination = this.returnUrl || '/';
        this.router.navigateByUrl(destination);
      },
      error: (err) => {
        this.state.set('error');
        this.errorMessage.set(
          err.status === 401
            ? 'Invalid username or password.'
            : err.userMessage || 'Unable to reach the authentication server.'
        );
      },
    });
  }
}
