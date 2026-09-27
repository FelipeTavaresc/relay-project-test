import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, scan, startWith, switchMap } from 'rxjs';

import { ApiError } from '../../core/api-error';
import {
  BaselineParams,
  DefaultWindowDays,
  LocationBaseline,
  ViewState,
  WindowDays,
} from './activity-baseline.models';
import { ActivityBaselineService } from './activity-baseline.service';
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
 * The feature's only container. It owns the window and location selections as signals, asks the
 * service for a report whenever they change, and hands the result down to three presentational
 * children (techspec § Frontend).
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

  /** Validated user input. Task 7.0 mirrors this into the query string (FR21). */
  private readonly params = signal<BaselineParams>({
    windowDays: DefaultWindowDays,
    locations: [],
  });

  /** Bumped by {@link retry} so a failed request can be reissued with identical parameters. */
  private readonly attempt = signal(0);

  private readonly request = computed(() => ({
    params: this.params(),
    attempt: this.attempt(),
  }));

  private readonly view = toSignal(
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

  readonly knownLocations = computed(() => this.view().knownLocations);

  readonly windowDays = computed<WindowDays>(() => this.params().windowDays);

  /** An empty filter means every location; the checkboxes need that spelled out to tick them. */
  readonly selectedLocations = computed<readonly string[]>(() => {
    const chosen = this.params().locations;

    return chosen.length === 0 ? this.knownLocations() : chosen;
  });

  onParamsChange(params: BaselineParams): void {
    this.params.set(params);
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

function merge(known: readonly string[], rows: readonly LocationBaseline[]): readonly string[] {
  const names = new Set(known);

  for (const row of rows) {
    names.add(row.location);
  }

  // Alphabetical so the checkbox order is stable. The *table* keeps the API's ranking (FR14).
  return [...names].sort((left, right) => left.localeCompare(right));
}

function describe(error: unknown): string {
  return error instanceof ApiError ? error.message : UnexpectedFailure;
}
