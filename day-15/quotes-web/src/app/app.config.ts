import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { retryGetInterceptor } from './core/interceptors/retry.interceptor';
import { errorMappingInterceptor } from './core/interceptors/error-mapping.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        retryGetInterceptor,
        errorMappingInterceptor,
      ])
    ),
  ],
};
