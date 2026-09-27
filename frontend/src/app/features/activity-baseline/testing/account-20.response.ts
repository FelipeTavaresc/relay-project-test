import { ActivityBaselineResponse } from '../activity-baseline.models';

/**
 * The live response of `GET /api/v1/accounts/20/activity-baseline?windowDays=30` against the seeded
 * database, captured verbatim rather than hand-written. Account 20 (Quiet Harbor Spa) has zero
 * events, which makes it the empty state's fixture with real data (PRD FR17): a `200` with no
 * locations and zero totals, not a `404` and not an error.
 *
 * Note the window: with no events there is no latest local event date to anchor to, so the API falls
 * back to the injected clock — which is why these dates are "today" rather than the seed's
 * 2026-07-27. Captured on 2026-09-26; the dates are not what this fixture is here to pin.
 */
export const Account20Response: ActivityBaselineResponse = {
  accountId: 20,
  accountName: 'Quiet Harbor Spa',
  timezone: 'America/Los_Angeles',
  windowDays: 30,
  windowStart: '2026-08-28',
  windowEnd: '2026-09-26',
  summary: {
    recentTotal: 0,
    baselineTotal: 0,
    absoluteDelta: 0,
    percentDelta: null,
  },
  locations: [],
};
