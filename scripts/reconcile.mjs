/**
 * Reconciliation check — the live endpoint against the figures derived from the seed during
 * specification (techspec § Testing Approach).
 *
 *   node scripts/reconcile.mjs [baseUrl]        # default http://localhost:5141
 *
 * This is the mitigation for the one accepted deviation in this build: there are no integration
 * tests, so nothing automated proves that the `AT TIME ZONE` GROUP BY in Postgres, the EF Core
 * mapping, the calculator and the JSON contract line up end to end. The unit tests pin the same
 * numbers against committed CSV fixtures; this pins them against a real database over HTTP.
 *
 * Exits 0 when every figure matches, 1 otherwise. No dependencies — Node 20+ for global fetch.
 */

const baseUrl = (process.argv[2] ?? process.env.RELAY_API_BASE_URL ?? 'http://localhost:5141')
  .replace(/\/+$/, '');

/** Account 6 (Metro Collision Centers) at the default 30-day window — the PRD's reference case. */
const ACCOUNT_ID = 6;
const WINDOW_DAYS = 30;

/** Derived from the seed before the calculator existed, and committed as a unit-test fixture. */
const expected = {
  windowStart: '2026-06-28',
  windowEnd: '2026-07-27',
  locationCount: 15,
  summary: { recentTotal: 175, baselineTotal: 311.75 },
  firstRow: {
    location: 'Site O',
    recentTotal: 9,
    baselineTotal: 21.25,
    percentDelta: -57.65,
    verdict: 'below',
    outlierDate: '2026-06-03',
  },
};

const results = [];

function check(label, actual, want) {
  const ok = actual === want;
  results.push({ ok, label, want, actual });
  return ok;
}

const url = `${baseUrl}/api/v1/accounts/${ACCOUNT_ID}/activity-baseline?windowDays=${WINDOW_DAYS}`;
console.log(`GET ${url}\n`);

let response;
try {
  response = await fetch(url);
} catch (cause) {
  console.error(`Could not reach the API at ${baseUrl}: ${cause.message}`);
  console.error('Start it with `docker compose up` (or `dotnet run --project backend/src/Relay.Api`).');
  process.exit(1);
}

if (!response.ok) {
  console.error(`Expected 200, got ${response.status} ${response.statusText}`);
  console.error(await response.text());
  process.exit(1);
}

const report = await response.json();

check('windowStart', report.windowStart, expected.windowStart);
check('windowEnd', report.windowEnd, expected.windowEnd);
check('locations returned', report.locations.length, expected.locationCount);
check('summary.recentTotal', report.summary.recentTotal, expected.summary.recentTotal);
check('summary.baselineTotal', report.summary.baselineTotal, expected.summary.baselineTotal);

// The first row is asserted positionally on purpose: FR14 says the largest deviation ranks first,
// so "Site O is row 0" is part of the contract, not an incidental ordering.
const first = report.locations[0] ?? {};
check('locations[0].location (ranked first)', first.location, expected.firstRow.location);
check('locations[0].recentTotal', first.recentTotal, expected.firstRow.recentTotal);
check('locations[0].baselineTotal', first.baselineTotal, expected.firstRow.baselineTotal);
check('locations[0].percentDelta', first.percentDelta, expected.firstRow.percentDelta);
check('locations[0].verdict', first.verdict, expected.firstRow.verdict);
check('locations[0].baselineOutlier.date', first.baselineOutlier?.date, expected.firstRow.outlierDate);

// The 2026-06-03 anomaly inflates the baseline of every one of account 6's locations, so the flag
// must be on all 15 — if it fires on some subset, the threshold or the attribution has drifted.
const flagged = report.locations.filter((l) => l.baselineOutlier?.date === expected.firstRow.outlierDate);
check(`rows flagged on ${expected.firstRow.outlierDate}`, flagged.length, expected.locationCount);

const width = Math.max(...results.map((r) => r.label.length));
for (const { ok, label, want, actual } of results) {
  const status = ok ? 'ok  ' : 'FAIL';
  const detail = ok ? `${actual}` : `expected ${want}, got ${actual}`;
  console.log(`${status} ${label.padEnd(width)}  ${detail}`);
}

const failures = results.filter((r) => !r.ok).length;
console.log(
  failures === 0
    ? `\nAll ${results.length} figures reconcile against the seed-derived values.`
    : `\n${failures} of ${results.length} figures do not reconcile.`,
);
process.exit(failures === 0 ? 0 : 1);
