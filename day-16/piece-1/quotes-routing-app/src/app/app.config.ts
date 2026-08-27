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
      provide: API_BASE_URL,
      useValue: 'http://localhost:5000/api',
    },
  ],
};
