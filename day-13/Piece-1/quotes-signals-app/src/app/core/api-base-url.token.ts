import { InjectionToken } from '@angular/core';
import { environment } from '../../environments/environment';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => environment.apiBaseUrl,
});

export const USE_MOCK_DATA = new InjectionToken<boolean>('USE_MOCK_DATA', {
  factory: () => (environment as any).useMockData ?? false,
});
