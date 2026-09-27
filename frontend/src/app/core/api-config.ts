import { InjectionToken, Provider } from '@angular/core';

import { environment } from '../../environments/environment';

/** What the data layer needs to know about its backend. Nothing else in the app reads it. */
export interface ApiConfig {
  readonly baseUrl: string;
  readonly accountId: number;
}

export const API_CONFIG = new InjectionToken<ApiConfig>('API_CONFIG');

/**
 * Injected rather than imported directly by the service, so a test can supply a base URL without
 * touching the environment file.
 */
export function provideApiConfig(): Provider {
  return {
    provide: API_CONFIG,
    useValue: { baseUrl: environment.apiBaseUrl, accountId: environment.accountId },
  };
}
