import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { retryGetInterceptor } from './core/interceptors/retry-get.interceptor';
import { errorMappingInterceptor } from './core/interceptors/error-mapping.interceptor';
import { API_BASE_URL } from './core/tokens/api-base-url.token';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withViewTransitions({
        skipInitialTransition: true,
      })
    ),
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        retryGetInterceptor,
        errorMappingInterceptor,
      ])
    ),
    {
      // Real Week-1 API (day-5/Day-5-Piece-2), live on Azure Container Apps.
      // Verified reachable: GET /health -> 200 Healthy; GET /api/quotes ->
      // 200 []. See day-17 deployment-brief.md for how this was discovered.
      provide: API_BASE_URL,
      useValue:
        'https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io/api',
    },
  ],
};
