import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { API_CONFIG } from '../../core/api-config';
import { httpErrorInterceptor } from '../../core/http-error.interceptor';
import { ActivityBaselinePageComponent } from './activity-baseline-page.component';
import { Account6Response } from './testing/account-6.response';

describe('ActivityBaselinePageComponent', () => {
  let fixture: ComponentFixture<ActivityBaselinePageComponent>;
  let http: HttpTestingController;

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  const rowHeaders = (): (string | undefined)[] =>
    Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody th')).map((cell) =>
      cell.firstChild?.textContent?.trim(),
    );

  const pendingRequest = (): TestRequest =>
    http.expectOne(
      (request) =>
        request.url === 'http://api.test/api/v1/accounts/6/activity-baseline',
    );

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: 'http://api.test', accountId: 6 } },
      ],
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ActivityBaselinePageComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('asks for the default 30-day window with no location filter', () => {
    const request = pendingRequest();

    expect(request.request.params.get('windowDays')).toBe('30');
    expect(request.request.params.has('locations')).toBe(false);

    request.flush(Account6Response);
  });

  it('renders account 6 with the largest deviation first and verdicts as text', () => {
    pendingRequest().flush(Account6Response);
    fixture.detectChanges();

    expect(rowHeaders().length).toBe(15);
    expect(rowHeaders()[0]).toBe('Site O');
    expect(text()).toContain('Below typical');
    expect(text()).toContain('Metro Collision Centers');
    expect(text()).toContain('311.75');
  });

  it('offers every location as a checkbox once the report has loaded', () => {
    pendingRequest().flush(Account6Response);
    fixture.detectChanges();

    const boxes = (fixture.nativeElement as HTMLElement).querySelectorAll('input[type="checkbox"]');

    expect(boxes.length).toBe(15);
  });

  it('shows what failed and no table at all, rather than a stale one', () => {
    pendingRequest().flush(Account6Response);
    fixture.detectChanges();
    expect(rowHeaders().length).toBe(15);

    fixture.componentInstance.onParamsChange({ windowDays: 90, locations: [] });
    fixture.detectChanges();

    pendingRequest().flush(
      { title: 'Invalid window', detail: 'windowDays must be one of 30, 60, 90; 13 is not allowed.' },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(text()).toContain('windowDays must be one of 30, 60, 90');
    expect(rowHeaders().length).toBe(0);
  });

  it('reissues the same request on retry', () => {
    pendingRequest().flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    fixture.componentInstance.retry();
    fixture.detectChanges();

    pendingRequest().flush(Account6Response);
    fixture.detectChanges();

    expect(rowHeaders().length).toBe(15);
  });
});
