import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { LocationBaseline } from './activity-baseline.models';
import { LocalDatePipe } from './pipes/local-date.pipe';
import { PercentDeltaPipe } from './pipes/percent-delta.pipe';
import { SignedNumberPipe } from './pipes/signed-number.pipe';
import { VerdictLabelPipe } from './pipes/verdict-label.pipe';

/**
 * One row per location (PRD FR13), rendered in the order it arrived. The ranking is the API's
 * (FR14, PRD Goal 2) — re-sorting here would quietly move the row the feature exists to surface.
 * Nothing is recomputed either: every number below is printed as received (FR12).
 */
@Component({
  selector: 'app-baseline-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, LocalDatePipe, PercentDeltaPipe, SignedNumberPipe, VerdictLabelPipe],
  template: `
    <div class="scroll">
      <table>
        <caption>
          Locations, furthest from their own typical level first
        </caption>
        <thead>
          <tr>
            <th scope="col">Location</th>
            <th scope="col" class="numeric">Recent calls</th>
            <th scope="col" class="numeric">Typical</th>
            <th scope="col" class="numeric">Difference</th>
            <th scope="col" class="numeric">% difference</th>
            <th scope="col">Verdict</th>
          </tr>
        </thead>
        <tbody>
          @for (row of locations(); track row.location) {
            <tr>
              <th scope="row">
                {{ row.location }}
                @if (row.baselineOutlier; as outlier) {
                  <!-- Why account 6 reads "below typical" everywhere: one planted day inflates the
                       baseline. Flagged in words, never smoothed away (techspec § Key Decisions #1). -->
                  <span class="note">
                    One day ({{ outlier.date | localDate }}) supplies
                    {{ outlier.sharePercent | number: '1.0-2' }}% of this typical figure.
                  </span>
                }
              </th>
              <td class="numeric">{{ row.recentTotal | number: '1.0-0' }}</td>
              <td class="numeric">{{ row.baselineTotal | number: '1.0-2' }}</td>
              <td class="numeric">{{ row.absoluteDelta | signedNumber }}</td>
              <td class="numeric">{{ row.percentDelta | percentDelta }}</td>
              <td>
                {{ row.verdict | verdictLabel }}
                @if (row.bandPercent; as band) {
                  <span class="note">
                    Normal variation here is up to ±{{ band | number: '1.0-2' }}%.
                  </span>
                }
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="6" class="empty">
                No activity for this account in the selected window. Nothing to compare yet.
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: `
    .scroll {
      overflow-x: auto;
    }

    table {
      border-collapse: collapse;
      min-width: 40rem;
      width: 100%;
    }

    caption {
      color: var(--muted);
      padding-bottom: 0.5rem;
      text-align: left;
    }

    th,
    td {
      border-bottom: 1px solid var(--rule);
      padding: 0.5rem 0.75rem;
      text-align: left;
      vertical-align: top;
    }

    thead th {
      border-bottom-width: 2px;
      font-size: 0.875rem;
    }

    .numeric {
      font-variant-numeric: tabular-nums;
      text-align: right;
    }

    tbody th {
      font-weight: 600;
    }

    .note {
      color: var(--muted);
      display: block;
      font-size: 0.8125rem;
      font-weight: 400;
      margin-top: 0.125rem;
      max-width: 22rem;
    }

    .empty {
      color: var(--muted);
      padding: 1rem 0.75rem;
    }
  `,
})
export class BaselineTableComponent {
  readonly locations = input.required<readonly LocationBaseline[]>();
}
