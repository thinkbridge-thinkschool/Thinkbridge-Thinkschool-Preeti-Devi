import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthTokenService } from '../services/auth-token.service';

// Auth endpoints authenticate the caller by credentials/refresh-token in the
// body, not by an existing session — attaching a stale or expired Bearer
// token here is meaningless at best and confusing to debug at worst.
const AUTH_ENDPOINTS = ['/auth/login', '/auth/refresh'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthTokenService);
  const token = authService.getToken();
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.includes(path));

  if (token && !isAuthEndpoint && !req.headers.has('Authorization')) {
    const cloned = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
    return next(cloned);
  }

  return next(req);
};
