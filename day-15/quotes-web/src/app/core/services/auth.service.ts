import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenSignal = signal<string | null>('mock-jwt-token-quotes-user');

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

  isAuthenticated(): boolean {
    return !!this.tokenSignal();
  }
}
