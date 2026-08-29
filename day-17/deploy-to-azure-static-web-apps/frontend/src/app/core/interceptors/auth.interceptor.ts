import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthTokenService } from '../services/auth-token.service';
import { NO_AUTH_HEADER_PREFIXES } from '../tokens/api-base-url.token';

// Auth endpoints authenticate the caller by credentials/refresh-token in the
// body, not by an existing session — attaching a stale or expired Bearer
// token here is meaningless at best and confusing to debug at worst.
const AUTH_ENDPOINTS = ['/auth/login', '/auth/refresh'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthTokenService);
  const token = authService.getToken();
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.includes(path));

  // Day 17: the Managed Identity proxy authenticates itself and allows only
  // Content-Type through CORS preflight, so an Authorization header sent to it is
  // not merely useless — it makes the request fail before it is ever dispatched.
  const skipsAuthHeader = inject(NO_AUTH_HEADER_PREFIXES).some((prefix) =>
    req.url.startsWith(prefix)
  );

  if (token && !isAuthEndpoint && !skipsAuthHeader && !req.headers.has('Authorization')) {
    const cloned = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
    return next(cloned);
  }

  return next(req);
};
