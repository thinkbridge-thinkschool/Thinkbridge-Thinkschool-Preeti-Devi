import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { retryGetInterceptor } from './core/interceptors/retry-get.interceptor';
import { errorMappingInterceptor } from './core/interceptors/error-mapping.interceptor';
import {
  API_BASE_URL,
  NO_AUTH_HEADER_PREFIXES,
  QUOTES_BASE_URL,
} from './core/tokens/api-base-url.token';
import { environment } from '../environments/environment';

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
    // Both URLs come from src/environments/*, which angular.json swaps for the
    // production file at build time. Nothing here is environment-aware at runtime.
    { provide: API_BASE_URL, useValue: environment.apiBaseUrl },
    { provide: QUOTES_BASE_URL, useValue: environment.quotesBaseUrl },
    {
      provide: NO_AUTH_HEADER_PREFIXES,
      useValue:
        environment.quotesBaseUrl === environment.apiBaseUrl
          ? []
          : [environment.quotesBaseUrl],
    },
  ],
};
