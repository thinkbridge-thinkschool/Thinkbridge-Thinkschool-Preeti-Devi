import { inject } from '@angular/core';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { AuthTokenService } from '../services/auth-token.service';

/**
 * Functional Route Guard: authGuard
 *
 * Protects routes from unauthenticated access.
 * Checks whether AuthTokenService has a valid JWT token.
 *
 * If authenticated: returns true.
 * If unauthenticated: redirects to /login preserving the intended target URL as a query param (?returnUrl=...).
 */
export const authGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot
): boolean | UrlTree => {
  const authService = inject(AuthTokenService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Redirect to login page and preserve the attempted URL in queryParams
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};
