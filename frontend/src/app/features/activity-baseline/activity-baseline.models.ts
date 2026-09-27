/**
 * The HTTP contract of `GET /api/v1/accounts/{accountId}/activity-baseline`, mirroring
 * `Relay.Api.Contracts` field for field (techspec § API Endpoints). Hand-written on purpose:
 * nothing crosses the backend/frontend boundary except this contract — no generated client, no
 * TypeScript emitted from the C# records.
 *
 * `decimal` on the wire arrives as a JSON number; `DateOnly` as an ISO `yyyy-MM-dd` string, which
 * is kept as a string rather than parsed into a `Date` so no timezone is ever applied to a value
 * that is already the account's local calendar date.
 */

/** The five states FR4 defines, as the snake_case strings `BaselineResponseMapper` publishes. */
export type Verdict = 'above' | 'typical' | 'below' | 'low_volume' | 'insufficient_history';

/** One source day supplying a disproportionate share of a location's baseline (FR-adjacent, techspec § Key Decisions #1). */
export interface BaselineOutlier {
  readonly date: string;
  readonly sharePercent: number;
}

/** The account-level line (FR16). */
export interface BaselineSummary {
  readonly recentTotal: number;
  readonly baselineTotal: number;
  readonly absoluteDelta: number;
  /** Null when the baseline is zero — no percentage rather than a division (FR8). */
  readonly percentDelta: number | null;
}

/** One table row (FR13), already in the ranked order the API produced (FR14). */
export interface LocationBaseline {
  readonly location: string;
  readonly recentTotal: number;
  readonly baselineTotal: number;
  readonly absoluteDelta: number;
  /** Null for `low_volume`, `insufficient_history`, and a zero baseline. */
  readonly percentDelta: number | null;
  readonly bandPercent: number | null;
  readonly verdict: Verdict;
  readonly baselineOutlier: BaselineOutlier | null;
}

export interface ActivityBaselineResponse {
  readonly accountId: number;
  readonly accountName: string;
  readonly timezone: string;
  readonly windowDays: number;
  readonly windowStart: string;
  readonly windowEnd: string;
  readonly summary: BaselineSummary;
  readonly locations: readonly LocationBaseline[];
}

/** The windows FR19 offers. A literal tuple so an out-of-range value cannot be constructed. */
export const WindowOptions = [30, 60, 90] as const;

export type WindowDays = (typeof WindowOptions)[number];

export const DefaultWindowDays: WindowDays = 30;

/** Validated user input, ready to become a query string (FR22 rejects anything else upstream). */
export interface BaselineParams {
  readonly windowDays: WindowDays;
  /** Empty means "every location the account has" — no `locations` parameter is sent. */
  readonly locations: readonly string[];
}

/**
 * A discriminated union rather than `isLoading` + `error` + possibly-stale `data`, so a stale
 * table has nowhere to live under an error (techspec § Frontend, `angular.md` § TypeScript rules).
 */
export type ViewState =
  | { readonly status: 'loading' }
  | { readonly status: 'error'; readonly message: string }
  | { readonly status: 'ready'; readonly data: ActivityBaselineResponse };
