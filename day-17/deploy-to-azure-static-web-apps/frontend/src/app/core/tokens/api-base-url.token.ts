import { InjectionToken } from '@angular/core';

// Day 17: this copy of the app is deployed to Azure Static Web Apps and
// talks to the REAL, live Week-1 QuotesApi Container App — not a local dev
// server. The origin app (day-16/piece-2) keeps localhost:5000 for local
// dev; this factory default is overridden explicitly in app.config.ts below
// so the deployed bundle never falls back to a loopback address.
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () =>
    'https://day-5-piece-2.bluesky-eec20d45.centralindia.azurecontainerapps.io/api',
});
