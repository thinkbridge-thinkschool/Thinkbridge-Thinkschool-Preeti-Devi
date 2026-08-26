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
