import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { ActivityBaselineResponse } from './activity-baseline.models';
import { LocalDatePipe } from './pipes/local-date.pipe';
import { PercentDeltaPipe } from './pipes/percent-delta.pipe';
import { SignedNumberPipe } from './pipes/signed-number.pipe';

/**
 * The account-level line — "is the account normal?" (PRD FR16). Purely presentational: it renders
 * the one object it is given and owns no state of its own.
 */
@Component({
  selector: 'app-baseline-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, LocalDatePipe, PercentDeltaPipe, SignedNumberPipe],
  template: `
    <section class="summary">
      <h2>{{ report().accountName }}</h2>

      <p class="period">
        Last {{ report().windowDays }} days —
        {{ report().windowStart | localDate }} to {{ report().windowEnd | localDate }},
        {{ report().timezone }}
      </p>

      <dl>
        <div>
          <dt>Recent calls</dt>
          <dd>{{ report().summary.recentTotal | number: '1.0-0' }}</dd>
        </div>
        <div>
          <dt>Typical for this period</dt>
          <dd>{{ report().summary.baselineTotal | number: '1.0-2' }}</dd>
        </div>
        <div>
          <dt>Difference</dt>
          <dd>
            {{ report().summary.absoluteDelta | signedNumber }}
            ({{ report().summary.percentDelta | percentDelta }})
          </dd>
        </div>
      </dl>
    </section>
  `,
  styles: `
    .summary {
      border: 1px solid var(--rule);
      border-radius: 6px;
      padding: 0.75rem 1rem;
    }

    h2 {
      font-size: 1.125rem;
      margin: 0;
    }

    .period {
      color: var(--muted);
      margin: 0.25rem 0 0.75rem;
    }

    dl {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem 2rem;
      margin: 0;
    }

    dt {
      color: var(--muted);
      font-size: 0.875rem;
    }

    dd {
      font-size: 1.25rem;
      font-variant-numeric: tabular-nums;
      margin: 0.125rem 0 0;
    }
  `,
})
export class BaselineSummaryComponent {
  /** The whole report: the summary line names the account and the window as well as the totals. */
  readonly report = input.required<ActivityBaselineResponse>();
}
