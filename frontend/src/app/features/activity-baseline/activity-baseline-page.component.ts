import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  Signal,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, map, of, scan, startWith, switchMap } from 'rxjs';

import { ApiError } from '../../core/api-error';
import {
  BaselineParams,
  LocationBaseline,
  ViewState,
  WindowDays,
} from './activity-baseline.models';
import { ActivityBaselineService } from './activity-baseline.service';
import { readBaselineState, writeBaselineState } from './baseline-url-params';
import { BaselineControlsComponent } from './baseline-controls.component';
import { BaselineSummaryComponent } from './baseline-summary.component';
import { BaselineTableComponent } from './baseline-table.component';

/**
 * What the page holds across requests: the current view state, plus the location names the account
 * is known to have.
 */
interface PageView {
  readonly state: ViewState;
  readonly knownLocations: readonly string[];
}

const InitialView: PageView = { state: { status: 'loading' }, knownLocations: [] };

const UnexpectedFailure = 'Something went wrong loading the report.';

/**
 * The feature's only container. It reads the window and location selections from the query string,
 * asks the service for a report whenever they change, writes every change back to the URL, and
 * hands the result down to three presentational children (techspec § Frontend).
 *
 * **The URL is the store of record** (PRD FR21): there is no writable state signal for the user's
 * selections, so a reload cannot restore anything the URL does not say, and the address bar can
 * never disagree with the table. Writes use `replaceUrl` so Back leaves the dashboard rather than
 * stepping back through filter changes (FR23).
 *
 * RxJS appears here and only here, at the HTTP edge: `switchMap` so a superseded request is
 * cancelled rather than racing, and the error is caught *inside* the pipeline because `toSignal`
 * rethrows a failed source on read. The Resource API (`rxResource` / `httpResource`) is
 * deliberately not used — still experimental as of Angular 20.3.
 */
@Component({
  selector: 'app-activity-baseline-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BaselineControlsComponent, BaselineSummaryComponent, BaselineTableComponent],
  template: `
    <h1>Is this normal for us?</h1>
    <p class="lead">
      Each location's recent calls against its own trailing same-weekday average.
    </p>

    <app-baseline-controls
      [windowDays]="windowDays()"
      [selectedLocations]="selectedLocations()"
      [availableLocations]="knownLocations()"
      (paramsChange)="onParamsChange($event)"
    />

    <!-- One live region, present from first render so an async update is announced rather than
         silently swapped in (PRD § Accessibility). The @if blocks live inside it for the same
         reason: a region added to the DOM at the same moment as its content announces nothing. -->
    <section class="results" aria-live="polite" [attr.aria-busy]="loading()">
      @let view = state();

      @if (view.status === 'loading') {
        <p class="status">Loading the report…</p>
      }

      @if (view.status === 'error') {
        <div class="status" role="alert">
          <p>{{ view.message }}</p>
          <button type="button" (click)="retry()">Try again</button>
        </div>
      }

      @if (view.status === 'ready') {
        <app-baseline-summary [report]="view.data" />
        <app-baseline-table [locations]="view.data.locations" />
      }
    </section>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    h1 {
      font-size: 1.5rem;
      margin: 0;
    }

    .lead {
      color: var(--muted);
      margin: -0.75rem 0 0;
    }

    .results {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .status {
      border: 1px solid var(--rule);
      border-radius: 6px;
      padding: 0.75rem 1rem;
    }

    .status p {
      margin: 0 0 0.5rem;
    }

    .status p:only-child {
      margin: 0;
    }
  `,
})
export class ActivityBaselinePageComponent {
  private readonly service = inject(ActivityBaselineService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  /**
   * The only reader of the query string. `requireSync` holds because `ActivatedRoute`'s parameter
   * observables are backed by a `BehaviorSubject` — the route's state is already resolved by the
   * time a routed component is constructed, so "read from the route on init" is this line.
   */
  private readonly urlState = toSignal(this.route.queryParamMap.pipe(map(readBaselineState)), {
    requireSync: true,
  });

  /** Bumped by {@link retry} so a failed request can be reissued with identical parameters. */
  private readonly attempt = signal(0);

  readonly windowDays = computed<WindowDays>(() => this.urlState().windowDays);

  /** Straight off the URL, so not yet trustworthy: these names have not been checked against the account. */
  private readonly requestedLocations = computed(() => this.urlState().requestedLocations);

  /**
   * Declaration order cannot be acyclic here — `params` feeds the request, whose response feeds the
   * known names, which feed `filter`, which feeds `params` again. Nothing reads across that circle
   * during construction: a `computed` is lazy, and `toObservable`'s effect first reads on a later
   * change-detection flush. The loop is closed *asynchronously*, by a response.
   */
  private readonly params = computed<BaselineParams>(
    () => ({ windowDays: this.windowDays(), locations: this.filter() }),
    { equal: sameParams },
  );

  private readonly request = computed(() => ({
    params: this.params(),
    attempt: this.attempt(),
  }));

  /**
   * Annotated rather than inferred, and that is load-bearing: the declarations form a circle
   * (`params` → request → response → known names → `filter` → `params`), and type inference cannot
   * traverse a circle. Without this annotation the compiler gives up and degrades the whole chain to
   * `any` — which `dotnet`-style strictness catches at build time but a transpile-only test run does
   * not.
   */
  private readonly view: Signal<PageView> = toSignal(
    toObservable(this.request).pipe(
      switchMap(({ params }) =>
        this.service.getBaseline(params).pipe(
          map((data): ViewState => ({ status: 'ready', data })),
          catchError((error: unknown) => of<ViewState>({ status: 'error', message: describe(error) })),
          startWith<ViewState>({ status: 'loading' }),
        ),
      ),
      scan(accumulate, InitialView),
    ),
    { initialValue: InitialView },
  );

  readonly state = computed(() => this.view().state);

  readonly loading = computed(() => this.state().status === 'loading');

  readonly knownLocations = computed(() => this.view().knownLocations);

  /**
   * The filter actually sent to the API: the URL's names intersected with the ones the account is
   * known to have, so an unknown name is **dropped rather than forwarded** — the endpoint answers a
   * name it doesn't recognise with a 400 (Task 5.0), and a stale or mistyped link should not be an
   * error page.
   *
   * It is empty until the first response lands, which resolves the bootstrap ordering problem: the
   * names can only come from a response, so the first request is deliberately unfiltered. That
   * request is needed regardless — a filtered response contains only the locations asked for, so a
   * filtered deep link would otherwise never learn the other names and could never offer them as
   * checkboxes. One extra round trip on a filtered link, and never a 400 on first load.
   */
  private readonly filter = computed<readonly string[]>(() => {
    const known = this.knownLocations();

    if (known.length === 0) {
      return [];
    }

    const requested = new Set(this.requestedLocations());
    const kept = known.filter((name) => requested.has(name));

    // "All of them" is no filter at all, in the URL and in the request alike — and that is also
    // what an entirely unrecognised list falls back to.
    return kept.length === known.length ? [] : kept;
  });

  /** An empty filter means every location; the checkboxes need that spelled out to tick them. */
  readonly selectedLocations = computed<readonly string[]>(() => {
    const chosen = this.filter();

    return chosen.length === 0 ? this.knownLocations() : chosen;
  });

  /**
   * The controls report a selection; this writes it to the URL and lets it come back round through
   * {@link urlState}. Nothing is stored here, so the request can only ever reflect the address bar.
   */
  onParamsChange(params: BaselineParams): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: writeBaselineState(params),
      replaceUrl: true,
    });
  }

  retry(): void {
    this.attempt.update((attempt) => attempt + 1);
  }
}

/**
 * Carries the location names forward across requests. A filtered response contains only the
 * locations asked for, so deriving the checkbox list from the latest response alone would shrink it
 * as you filter and leave no way to tick a box back on. This is option bookkeeping, not
 * aggregation — no figure shown to the user is computed here (PRD FR12).
 */
function accumulate(previous: PageView, state: ViewState): PageView {
  if (state.status !== 'ready') {
    return { state, knownLocations: previous.knownLocations };
  }

  return { state, knownLocations: merge(previous.knownLocations, state.data.locations) };
}

/**
 * Returns the *same array* when a response adds no new name. That reference stability is what stops
 * the feedback loop this design would otherwise have: response → known names → filter → request.
 * A fresh array every time would make the filtered response look like a change and request again,
 * for ever. {@link sameParams} guards the same loop from the other end.
 */
function merge(known: readonly string[], rows: readonly LocationBaseline[]): readonly string[] {
  const names = new Set(known);

  for (const row of rows) {
    names.add(row.location);
  }

  if (names.size === known.length) {
    return known;
  }

  // Alphabetical so the checkbox order is stable. The *table* keeps the API's ranking (FR14).
  return [...names].sort((left, right) => left.localeCompare(right));
}

function sameParams(left: BaselineParams, right: BaselineParams): boolean {
  return (
    left.windowDays === right.windowDays &&
    left.locations.length === right.locations.length &&
    left.locations.every((name, index) => name === right.locations[index])
  );
}

function describe(error: unknown): string {
  return error instanceof ApiError ? error.message : UnexpectedFailure;
}
