import { ActivityBaselineResponse } from '../activity-baseline.models';

/**
 * The live response of `GET /api/v1/accounts/6/activity-baseline?windowDays=30` against the
 * seeded database, captured verbatim rather than hand-written. Account 6 is the PRD's reference
 * multi-location case: 15 locations, every one of them below typical because of the 2026-06-03
 * anomaly sitting in the baseline lookback (techspec § Key Decisions #1).
 */
export const Account6Response: ActivityBaselineResponse = {
  accountId: 6,
  accountName: 'Metro Collision Centers',
  timezone: 'America/New_York',
  windowDays: 30,
  windowStart: '2026-06-28',
  windowEnd: '2026-07-27',
  summary: {
    recentTotal: 175,
    baselineTotal: 311.75,
    absoluteDelta: -136.75,
    percentDelta: -43.87
  },
  locations: [
    {
      location: 'Site O',
      recentTotal: 9,
      baselineTotal: 21.25,
      absoluteDelta: -12.25,
      percentDelta: -57.65,
      bandPercent: 21.69,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 31.76
      }
    },
    {
      location: 'Site F',
      recentTotal: 8,
      baselineTotal: 18.5,
      absoluteDelta: -10.5,
      percentDelta: -56.76,
      bandPercent: 23.25,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 51.35
      }
    },
    {
      location: 'Site M',
      recentTotal: 7,
      baselineTotal: 15.25,
      absoluteDelta: -8.25,
      percentDelta: -54.1,
      bandPercent: 25.61,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 42.62
      }
    },
    {
      location: 'Site D',
      recentTotal: 8,
      baselineTotal: 17.25,
      absoluteDelta: -9.25,
      percentDelta: -53.62,
      bandPercent: 24.08,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 52.17
      }
    },
    {
      location: 'Site N',
      recentTotal: 8,
      baselineTotal: 16.25,
      absoluteDelta: -8.25,
      percentDelta: -50.77,
      bandPercent: 24.81,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 49.23
      }
    },
    {
      location: 'Site G',
      recentTotal: 9,
      baselineTotal: 17.5,
      absoluteDelta: -8.5,
      percentDelta: -48.57,
      bandPercent: 23.9,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 44.29
      }
    },
    {
      location: 'Site J',
      recentTotal: 9,
      baselineTotal: 17,
      absoluteDelta: -8,
      percentDelta: -47.06,
      bandPercent: 24.25,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 41.18
      }
    },
    {
      location: 'Site B',
      recentTotal: 13,
      baselineTotal: 24,
      absoluteDelta: -11,
      percentDelta: -45.83,
      bandPercent: 20.41,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 41.67
      }
    },
    {
      location: 'Site L',
      recentTotal: 14,
      baselineTotal: 25.25,
      absoluteDelta: -11.25,
      percentDelta: -44.55,
      bandPercent: 19.9,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 46.53
      }
    },
    {
      location: 'Site H',
      recentTotal: 13,
      baselineTotal: 22.5,
      absoluteDelta: -9.5,
      percentDelta: -42.22,
      bandPercent: 21.08,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 30
      }
    },
    {
      location: 'Site I',
      recentTotal: 12,
      baselineTotal: 20,
      absoluteDelta: -8,
      percentDelta: -40,
      bandPercent: 22.36,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 40
      }
    },
    {
      location: 'Site A',
      recentTotal: 14,
      baselineTotal: 22.5,
      absoluteDelta: -8.5,
      percentDelta: -37.78,
      bandPercent: 21.08,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 25.56
      }
    },
    {
      location: 'Site E',
      recentTotal: 18,
      baselineTotal: 28,
      absoluteDelta: -10,
      percentDelta: -35.71,
      bandPercent: 18.9,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 28.57
      }
    },
    {
      location: 'Site K',
      recentTotal: 13,
      baselineTotal: 19.5,
      absoluteDelta: -6.5,
      percentDelta: -33.33,
      bandPercent: 22.65,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 37.18
      }
    },
    {
      location: 'Site C',
      recentTotal: 20,
      baselineTotal: 27,
      absoluteDelta: -7,
      percentDelta: -25.93,
      bandPercent: 19.25,
      verdict: 'below',
      baselineOutlier: {
        date: '2026-06-03',
        sharePercent: 34.26
      }
    }
  ]
};
