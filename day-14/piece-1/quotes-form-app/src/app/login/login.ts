import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

interface FloatingQuote {
  text: string;
  author: string;
}

type LoginState = 'idle' | 'submitting' | 'success' | 'error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class LoginComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  readonly loginState = signal<LoginState>('idle');
  readonly errorMessage = signal<string | null>(null);
  readonly showPassword = signal(false);
  readonly activeQuoteIndex = signal(0);

  private quoteInterval: ReturnType<typeof setInterval> | null = null;

  readonly floatingQuotes: FloatingQuote[] = [
    { text: 'The only way to do great work is to love what you do.', author: 'Steve Jobs' },
    { text: 'Innovation distinguishes between a leader and a follower.', author: 'Steve Jobs' },
    { text: 'Stay hungry, stay foolish.', author: 'Steve Jobs' },
    { text: 'The future belongs to those who believe in the beauty of their dreams.', author: 'Eleanor Roosevelt' },
    { text: 'It does not matter how slowly you go as long as you do not stop.', author: 'Confucius' },
  ];

  readonly loginForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    rememberMe: [false],
  });

  ngOnInit(): void {
    this.quoteInterval = setInterval(() => {
      const next = (this.activeQuoteIndex() + 1) % this.floatingQuotes.length;
      this.activeQuoteIndex.set(next);
    }, 4000);
  }

  ngOnDestroy(): void {
    if (this.quoteInterval) {
      clearInterval(this.quoteInterval);
    }
  }

  get emailErrors() {
    return this.loginForm.get('email')!.errors;
  }

  get emailTouched() {
    return this.loginForm.get('email')!.touched;
  }

  get passwordErrors() {
    return this.loginForm.get('password')!.errors;
  }

  get passwordTouched() {
    return this.loginForm.get('password')!.touched;
  }

  isFieldInvalid(fieldName: string): boolean {
    const control = this.loginForm.get(fieldName);
    return control !== null && control.invalid && control.touched;
  }

  togglePasswordVisibility(): void {
    this.showPassword.update((val) => !val);
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loginState.set('submitting');
    this.errorMessage.set(null);

    // Simulate login API call
    setTimeout(() => {
      const { email, password } = this.loginForm.value;

      // Demo credentials check
      if (email === 'admin@quotes.app' && password === 'admin123') {
        this.loginState.set('success');
        setTimeout(() => {
          this.router.navigate(['/quotes']);
        }, 1200);
      } else {
        this.errorMessage.set('Invalid email or password. Try admin@quotes.app / admin123');
        this.loginState.set('error');
      }
    }, 1500);
  }
}
