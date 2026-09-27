import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
  TestRequest,
} from '@angular/common/http/testing';
import { Location } from '@angular/common';
import { provideLocationMocks, SpyLocation } from '@angular/common/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { API_CONFIG } from '../../core/api-config';
import { httpErrorInterceptor } from '../../core/http-error.interceptor';
import { ActivityBaselinePageComponent } from './activity-baseline-page.component';
import { Account6Response } from './testing/account-6.response';
import { Account20Response } from './testing/account-20.response';

/**
 * The container, exercised through the real `Router` rather than a stubbed `ActivatedRoute`: the URL
 * is the store of record (PRD FR21), so a test that fakes the route would be testing the fake.
 * `provideLocationMocks` supplies a `SpyLocation` that records whether each URL change was a push or
 * a replace, which is how `replaceUrl` (FR23) gets asserted without spying on our own call.
 *
 * Queries are by role and text, never by CSS class — a restyle must not break a behaviour test
 * (`angular.md` § Tests).
 */
describe('ActivityBaselinePageComponent', () => {
  const endpoint = 'http://api.test/api/v1/accounts/6/activity-baseline';

  let harness: RouterTestingHarness;
  let http: HttpTestingController;
  let location: SpyLocation;

  const element = (): HTMLElement => harness.routeNativeElement as HTMLElement;

  const text = (): string => element().textContent ?? '';

  /** Row headers are the location names — `<th scope="row">`, so a role query finds them. */
  const locationNames = (): (string | undefined)[] =>
    Array.from(element().querySelectorAll('tbody th')).map((cell) =>
      cell.firstChild?.textContent?.trim(),
    );

  const results = (): HTMLElement => {
    const region = element().querySelector<HTMLElement>('[aria-live="polite"]');

    if (region === null) {
      throw new Error('No live region for the results.');
    }

    return region;
  };

  const button = (label: string): HTMLButtonElement => {
    const match = Array.from(element().querySelectorAll('button')).find(
      (candidate) => candidate.textContent?.trim() === label,
    );

    if (match === undefined) {
      throw new Error(`No button labelled "${label}".`);
    }

    return match as HTMLButtonElement;
  };

  const checkbox = (label: string): HTMLInputElement => {
    const match = Array.from(element().querySelectorAll('label')).find((candidate) =>
      candidate.textContent?.includes(label),
    );
    const input = match?.querySelector<HTMLInputElement>('input[type="checkbox"]');

    if (input === undefined || input === null) {
      throw new Error(`No checkbox labelled "${label}".`);
    }

    return input;
  };

  const radio = (label: string): HTMLInputElement => {
    const match = Array.from(element().querySelectorAll('label')).find(
      (candidate) => candidate.textContent?.trim() === label,
    );
    const input = match?.querySelector<HTMLInputElement>('input[type="radio"]');

    if (input === undefined || input === null) {
      throw new Error(`No window option labelled "${label}".`);
    }

    return input;
  };

  const pending = (): TestRequest => http.expectOne((request) => request.url === endpoint);

  /** Loads the page at a URL, the way a shared link or a reload would. */
  const load = async (url: string): Promise<void> => {
    harness = await RouterTestingHarness.create(url);
    location = TestBed.inject(Location) as SpyLocation;

    // The harness's own setup navigation is not the component's doing; from here on, every entry
    // in urlChanges is one this page wrote.
    location.urlChanges.length = 0;
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '', component: ActivityBaselinePageComponent }]),
        provideLocationMocks(),
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: 'http://api.test', accountId: 6 } },
      ],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('reading the URL', () => {
    it('asks for the default 30-day window when the URL says nothing', async () => {
      await load('/');

      const request = pending();

      expect(request.request.params.get('windowDays')).toBe('30');
      expect(request.request.params.has('locations')).toBe(false);
      request.flush(Account6Response);
    });

    it('restores a 90-day window from the URL, checked in the controls', async () => {
      await load('/?window=90');

      const request = pending();
      expect(request.request.params.get('windowDays')).toBe('90');

      request.flush(Account6Response);
      harness.detectChanges();

      expect(radio('90 days').checked).toBe(true);
      expect(radio('30 days').checked).toBe(false);
    });

    // The task's named case: 13 must never reach the API (PRD FR22).
    it('falls back to 30 days on window=13 and never sends 13', async () => {
      await load('/?window=13');

      const request = pending();

      expect(request.request.params.get('windowDays')).toBe('30');

      request.flush(Account6Response);
      harness.detectChanges();

      expect(radio('30 days').checked).toBe(true);
    });

    it('restores a location filter from the URL once the account names are known', async () => {
      await load('/?locations=Site%20O,Site%20F');

      // The first request is deliberately unfiltered: the names to intersect against only exist in
      // a response, and forwarding an unchecked name risks a 400.
      const discovery = pending();
      expect(discovery.request.params.has('locations')).toBe(false);

      discovery.flush(Account6Response);
      harness.detectChanges();

      const filtered = pending();
      expect(filtered.request.params.get('locations')).toBe('Site F,Site O');

      filtered.flush({
        ...Account6Response,
        locations: Account6Response.locations.filter((row) =>
          ['Site O', 'Site F'].includes(row.location),
        ),
      });
      harness.detectChanges();

      expect(locationNames()).toEqual(['Site O', 'Site F']);
      expect(checkbox('Site O').checked).toBe(true);
      expect(checkbox('Site A').checked).toBe(false);
    });

    it('drops a location the account does not have instead of forwarding it', async () => {
      await load('/?locations=Site%20O,Nowhere');

      pending().flush(Account6Response);
      harness.detectChanges();

      const filtered = pending();

      expect(filtered.request.params.get('locations')).toBe('Site O');

      filtered.flush({
        ...Account6Response,
        locations: Account6Response.locations.filter((row) => row.location === 'Site O'),
      });
      harness.detectChanges();

      expect(locationNames()).toEqual(['Site O']);
    });

    it('shows every location when the URL names none the account has', async () => {
      await load('/?locations=Nowhere');

      pending().flush(Account6Response);
      harness.detectChanges();

      // Nothing recognised means no filter, not an empty table and not a 400.
      expect(locationNames().length).toBe(15);
      http.expectNone((request) => request.url === endpoint);
    });

    it('stops requesting once the filtered response has settled', async () => {
      await load('/?locations=Site%20O');

      pending().flush(Account6Response);
      harness.detectChanges();

      pending().flush({
        ...Account6Response,
        locations: Account6Response.locations.filter((row) => row.location === 'Site O'),
      });
      harness.detectChanges();

      // The response feeds the known names, which feed the filter, which feeds the request. A third
      // request here would mean that circle never closes.
      http.expectNone((request) => request.url === endpoint);
    });
  });

  describe('writing the URL', () => {
    it('replaces the history entry on every change rather than stacking them', async () => {
      await load('/');

      pending().flush(Account6Response);
      harness.detectChanges();

      radio('60 days').click();
      harness.detectChanges();
      pending().flush(Account6Response);
      harness.detectChanges();

      radio('90 days').click();
      harness.detectChanges();

      // Both changes replaced; neither pushed. Two filter changes therefore leave one history
      // entry, so Back leaves the dashboard instead of stepping through them (PRD FR23).
      expect(location.urlChanges).toEqual(['replace: /?window=60', 'replace: /?window=90']);

      pending().flush(Account6Response);
    });

    it('writes an unticked location out as a filter and sends it', async () => {
      await load('/');

      pending().flush(Account6Response);
      harness.detectChanges();

      checkbox('Site A').click();
      harness.detectChanges();

      expect(location.path()).toContain('window=30');
      expect(location.path()).toContain('locations=');
      expect(location.path()).not.toContain('Site%20A');

      const filtered = pending();

      expect(filtered.request.params.get('locations')).not.toContain('Site A');
      filtered.flush(Account6Response);
    });

    it('drops the locations parameter when nothing is left ticked', async () => {
      await load('/?locations=Site%20O');

      pending().flush(Account6Response);
      harness.detectChanges();

      pending().flush({
        ...Account6Response,
        locations: Account6Response.locations.filter((row) => row.location === 'Site O'),
      });
      harness.detectChanges();

      checkbox('Site O').click();
      harness.detectChanges();

      // Unticking the only remaining location means "all of them" — an absent parameter, not an
      // empty filter the API would have to interpret.
      expect(location.path()).toBe('/?window=30');

      const unfiltered = pending();

      expect(unfiltered.request.params.has('locations')).toBe(false);
      unfiltered.flush(Account6Response);
      harness.detectChanges();

      expect(locationNames().length).toBe(15);
    });
  });

  describe('view states', () => {
    it('renders the loading state before a response arrives', async () => {
      await load('/');

      expect(text()).toContain('Loading the report…');
      expect(results().getAttribute('aria-busy')).toBe('true');
      expect(element().querySelector('table')).toBeNull();

      pending().flush(Account6Response);
    });

    it('renders the ready state with the largest deviation first and verdicts as text', async () => {
      await load('/');

      pending().flush(Account6Response);
      harness.detectChanges();

      expect(locationNames().length).toBe(15);
      expect(locationNames()[0]).toBe('Site O');
      expect(text()).toContain('Below typical');
      expect(text()).toContain('Metro Collision Centers');
      expect(text()).toContain('311.75');
      expect(results().getAttribute('aria-busy')).toBe('false');
    });

    /** Account 20 (Quiet Harbor Spa) has zero events — the empty state with real data (PRD FR17). */
    it('renders an explicit empty state for an account with no activity', async () => {
      await load('/');

      pending().flush(Account20Response);
      harness.detectChanges();

      expect(text()).toContain('No activity for this account in the selected window');
      expect(locationNames()).toEqual([]);
      // An empty result is not a failure: the summary line still reports the zero.
      expect(element().querySelector('[role="alert"]')).toBeNull();
      expect(text()).toContain('Quiet Harbor Spa');
    });

    it('names what failed and offers a retry', async () => {
      await load('/');

      pending().flush(
        { title: 'Invalid window', detail: 'windowDays must be one of 30, 60, 90.' },
        { status: 400, statusText: 'Bad Request' },
      );
      harness.detectChanges();

      const alert = element().querySelector('[role="alert"]');

      expect(alert?.textContent).toContain('windowDays must be one of 30, 60, 90.');
      expect(button('Try again')).toBeDefined();
    });

    it('shows no table at all under an error, rather than a stale one', async () => {
      await load('/');

      pending().flush(Account6Response);
      harness.detectChanges();
      expect(locationNames().length).toBe(15);

      radio('60 days').click();
      harness.detectChanges();

      pending().flush({}, { status: 500, statusText: 'Server Error' });
      harness.detectChanges();

      expect(element().querySelector('table')).toBeNull();
      expect(text()).toContain('The API responded with 500');
    });

    it('reissues the same request when retry is pressed', async () => {
      await load('/?window=60');

      pending().flush({}, { status: 500, statusText: 'Server Error' });
      harness.detectChanges();

      button('Try again').click();
      harness.detectChanges();

      const retried = pending();

      expect(retried.request.params.get('windowDays')).toBe('60');

      retried.flush(Account6Response);
      harness.detectChanges();

      expect(locationNames().length).toBe(15);
    });
  });
});
