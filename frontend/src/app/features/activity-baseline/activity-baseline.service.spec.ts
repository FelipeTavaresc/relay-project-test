import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ApiError } from '../../core/api-error';
import { API_CONFIG } from '../../core/api-config';
import { httpErrorInterceptor } from '../../core/http-error.interceptor';
import { ActivityBaselineResponse } from './activity-baseline.models';
import { ActivityBaselineService } from './activity-baseline.service';
import { Account6Response } from './testing/account-6.response';

/**
 * The service's contract with the API: the URL it builds, the parameters it sends, and what a
 * caller sees when the request fails. `HttpTestingController` throughout — a unit test never
 * reaches a real backend (`angular.md` § Tests).
 */
describe('ActivityBaselineService', () => {
  const endpoint = 'http://api.test/api/v1/accounts/6/activity-baseline';

  let service: ActivityBaselineService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: 'http://api.test', accountId: 6 } },
      ],
    });

    service = TestBed.inject(ActivityBaselineService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets the configured account from the versioned endpoint', () => {
    service.getBaseline({ windowDays: 30, locations: [] }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === endpoint);

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('windowDays')).toBe('30');
    request.flush(Account6Response);
  });

  it('omits the locations parameter entirely when no filter is in force', () => {
    service.getBaseline({ windowDays: 90, locations: [] }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === endpoint);

    expect(request.request.params.get('windowDays')).toBe('90');
    expect(request.request.params.has('locations')).toBe(false);
    request.flush(Account6Response);
  });

  it('sends a filtered selection as one comma-separated parameter', () => {
    service.getBaseline({ windowDays: 60, locations: ['Site O', 'Site F'] }).subscribe();

    const request = http.expectOne((candidate) => candidate.url === endpoint);

    expect(request.request.params.get('locations')).toBe('Site O,Site F');
    request.flush(Account6Response);
  });

  it('passes the response through untouched — no client-side aggregation', () => {
    let received: ActivityBaselineResponse | undefined;

    service.getBaseline({ windowDays: 30, locations: [] }).subscribe((response) => {
      received = response;
    });

    http.expectOne((candidate) => candidate.url === endpoint).flush(Account6Response);

    expect(received).toEqual(Account6Response);
  });

  it("surfaces the API's ProblemDetails detail as the error message", () => {
    let failure: unknown;

    service.getBaseline({ windowDays: 30, locations: [] }).subscribe({
      error: (error: unknown) => {
        failure = error;
      },
    });

    http.expectOne((candidate) => candidate.url === endpoint).flush(
      { title: 'Unknown location', detail: 'Account 6 has no location named Nowhere.' },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).status).toBe(400);
    expect((failure as ApiError).message).toBe('Account 6 has no location named Nowhere.');
  });

  it('reports an unreachable API rather than a status code nobody can act on', () => {
    let failure: unknown;

    service.getBaseline({ windowDays: 30, locations: [] }).subscribe({
      error: (error: unknown) => {
        failure = error;
      },
    });

    http
      .expectOne((candidate) => candidate.url === endpoint)
      .error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).message).toContain('Could not reach the API');
  });
});
