import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/tokens/api-base-url.token';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthSession {
  token: string;
  refreshToken: string;
}

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /**
   * Week-1 Endpoint: POST /api/auth/login
   * Body: { username, password } — 401 on bad credentials.
   */
  login(request: LoginRequest): Observable<AuthSession> {
    return this.http.post<AuthSession>(`${this.baseUrl}/auth/login`, request);
  }
}
