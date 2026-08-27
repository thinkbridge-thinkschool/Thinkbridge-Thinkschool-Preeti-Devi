import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthTokenService {
  private readonly storageKey = 'quotes_app_jwt';
  private readonly refreshStorageKey = 'quotes_app_refresh_token';
  private readonly tokenSignal = signal<string | null>(
    localStorage.getItem(this.storageKey)
  );

  readonly token = this.tokenSignal.asReadonly();

  getToken(): string | null {
    return this.tokenSignal();
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(this.refreshStorageKey);
  }

  setSession(token: string, refreshToken: string): void {
    localStorage.setItem(this.storageKey, token);
    localStorage.setItem(this.refreshStorageKey, refreshToken);
    this.tokenSignal.set(token);
  }

  clearToken(): void {
    localStorage.removeItem(this.storageKey);
    localStorage.removeItem(this.refreshStorageKey);
    this.tokenSignal.set(null);
  }

  isAuthenticated(): boolean {
    const token = this.tokenSignal();
    return token !== null && token.trim().length > 0;
  }
}
