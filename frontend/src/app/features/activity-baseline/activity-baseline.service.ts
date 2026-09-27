import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_CONFIG } from '../../core/api-config';
import { ActivityBaselineResponse, BaselineParams } from './activity-baseline.models';

/**
 * The only file in the app that knows the endpoint's URL and its response shape
 * (techspec § Frontend). Components depend on this surface, never on `HttpClient`
 * (`angular.md` § Data fetching).
 *
 * It fetches and types; it does not aggregate. Totals, deltas, and the ranking arrive computed
 * from the API and are passed through untouched (PRD FR12, FR14).
 */
@Injectable({ providedIn: 'root' })
export class ActivityBaselineService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);

  /** The account this dashboard shows. Configuration, not user input — there is no tenant picker. */
  get accountId(): number {
    return this.config.accountId;
  }

  getBaseline(params: BaselineParams): Observable<ActivityBaselineResponse> {
    return this.http.get<ActivityBaselineResponse>(this.endpoint(), {
      params: this.toQuery(params),
    });
  }

  private endpoint(): string {
    return `${this.config.baseUrl}/api/v1/accounts/${this.config.accountId}/activity-baseline`;
  }

  /** An empty location list means "all locations": the parameter is omitted rather than sent blank. */
  private toQuery(params: BaselineParams): HttpParams {
    const query = new HttpParams().set('windowDays', params.windowDays);

    return params.locations.length === 0
      ? query
      : query.set('locations', params.locations.join(','));
  }
}
