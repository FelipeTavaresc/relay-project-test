# Tech Spec — Activity Baseline ("Is this normal for us?")

**Ticket:** DASH-247 · **PRD:** [`prd.md`](./prd.md) · **Date:** 2026-09-26

## Executive Summary

One read-only endpoint (`GET /api/v1/accounts/{accountId}/activity-baseline`) returns every location's
recent call total, its trailing same-weekday baseline, and a verdict, pre-sorted so the largest
deviation is first. PostgreSQL does the timezone-correct day bucketing — `occurred_at AT TIME ZONE
'UTC' AT TIME ZONE accounts.timezone` accepts the seed's IANA strings natively, which is the decisive
reason Postgres was chosen over SQL Server (see Key Decisions). Everything downstream of that GROUP BY
— zero-fill, the trailing-4 average, the volume-aware band, the verdict, and the ranking — lives in a
`BaselineCalculator` domain service in `Relay.Application`, a pure function over a list of daily
counts with no database, no clock, and no I/O.

The Angular SPA is a single lazy-loaded feature: one container that owns window and location state as
signals mirrored into the query string, and three presentational children. Analysis of the seed data
during specification surfaced one finding that changes the build (§ Key Decisions #1): a single
anomalous day, `2026-06-03`, inflates the baseline of all 15 locations of account 6 — the PRD's own
reference multi-location case — making every one of them read "below typical" at the default window.
The response therefore carries an outlier flag alongside the verdict.

## System Architecture

### Component Overview

```
qualitara/
  backend/                      .NET 8 solution — Relay.sln, Directory.Build.props, Dockerfile
    src/Relay.Api/              controller · request DTOs + validation · ProblemDetails · DI wiring
    src/Relay.Application/      IBaselineCalculator (pure math) · domain records · IActivityQueries
    src/Relay.Infrastructure/   RelayDbContext · EF Core migrations · ActivityQueries (FromSql)
    tests/Relay.UnitTests/      BaselineCalculator edge cases + the reconciliation fixture
  frontend/                     Angular workspace — angular.json, package.json, Dockerfile
    src/app/core/               ApiConfig provider, HTTP error interceptor
    src/app/features/activity-baseline/    the only feature
  tasks/prd-activity-baseline/  prd.md · techspec.md · schema.sql + seed.sql (mounted into db init)
  docker-compose.yml            db · api (build context ./backend) · web (build context ./frontend)
  README.md                     run + test commands, decisions, documented deviations
```

**Two build roots, one repository.** `backend/` and `frontend/` are independent: each owns its
toolchain, its solution/lockfile, its Dockerfile, and its own one-line build and test commands in the
README. Nothing crosses the boundary except the HTTP contract in § API Endpoints — no shared generated
client, no generated TypeScript from the C# DTOs. The folder split is a prefix, not a redesign: inside
`backend/` the `src/` + `tests/` layout `csharp-dotnet.md` prescribes is kept verbatim, and `frontend/`
is the Angular workspace root, so `angular.md`'s `src/app/{core,features,shared}` is unchanged. Every
path in this spec is written relative to the repository root so it is unambiguous which root it lives in.

Dependency direction is one-way: `Api → Application`, with `Infrastructure` implementing the
`IActivityQueries` interface that `Application` declares. `Application` references no EF Core type.

**Data flow.** Controller validates `windowDays ∈ {30,60,90}` and the location filter → service asks
`IActivityQueries` for the account's location history (first/last local activity date per location)
and for daily call counts over the fetch span → `BaselineCalculator.Evaluate` zero-fills, computes
baselines and verdicts, ranks → controller maps to response DTOs.

**The fetch span is wider than the window.** The baseline for the *first* day of the window looks back
28 days, so the query spans `anchor − (windowDays − 1) − 28 … anchor`. Fetching only the window is the
most likely implementation bug here; it silently yields a baseline of zero for the window's early days.

## Implementation Design

### Main Interfaces

```csharp
// Relay.Application — declared by the consumer, implemented in Infrastructure.
public interface IActivityQueries
{
    Task<AccountContext?> GetAccountAsync(int accountId, CancellationToken ct);

    // Distinct locations with first/last local activity date — sources the row list and
    // distinguishes "no occurrence existed" (insufficient history) from "genuine zero".
    Task<IReadOnlyList<LocationHistory>> GetLocationHistoryAsync(int accountId, CancellationToken ct);

    Task<IReadOnlyList<DailyLocationCount>> GetDailyCallCountsAsync(
        int accountId, DateOnly fromLocalDate, DateOnly toLocalDate, CancellationToken ct);
}

// Pure, synchronous, no I/O — every PRD edge case is a unit test against this.
public interface IBaselineCalculator
{
    BaselineReport Evaluate(BaselineRequest request);
}
```

### Data Models

```csharp
public sealed record AccountContext(int Id, string Name, string Timezone, DateOnly? LatestLocalDate);
public sealed record LocationHistory(string Location, DateOnly FirstLocalDate, DateOnly LastLocalDate);
public sealed record DailyLocationCount(string Location, DateOnly LocalDate, int Calls);

public enum Verdict { Above, Typical, Below, LowVolume, InsufficientHistory }

public sealed record LocationBaseline(
    string Location,
    int RecentTotal,
    decimal BaselineTotal,
    decimal AbsoluteDelta,
    decimal? PercentDelta,      // null for low_volume, insufficient_history, zero baseline
    decimal? BandPercent,
    Verdict Verdict,
    OutlierDay? BaselineOutlier);

/// A single source day supplying a disproportionate share of BaselineTotal.
public sealed record OutlierDay(DateOnly Date, decimal SharePercent);

public sealed record BaselineReport(
    DateOnly WindowStart, DateOnly WindowEnd,
    int RecentTotal, decimal BaselineTotal,
    IReadOnlyList<LocationBaseline> Locations);
```

`decimal` throughout for baselines and percentages — these are displayed and compared, per
`csharp-dotnet.md`. `DateOnly` for calendar values; `occurred_at` stays `DateTimeOffset` at the EF layer.

### The aggregation query

```csharp
// Postgres AT TIME ZONE accepts IANA names directly, so accounts.timezone is used as stored.
// Not expressible in LINQ; parameterised FromSql per csharp-dotnet.md's raw-SQL exception.
const string Sql = """
    SELECT e.location AS "Location",
           (e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date AS "LocalDate",
           COUNT(*)::int AS "Calls"
    FROM activity_events e
    JOIN accounts a ON a.id = e.account_id
    WHERE e.account_id = {0}
      AND e.event_type = {1}
      AND (e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date BETWEEN {2} AND {3}
    GROUP BY e.location, 2
    """;
```

The predicate on the computed local date is not sargable, but the dataset is 12,626 rows total and
7,780 `call_received`; an index on `(account_id, event_type, occurred_at)` carries the selective part.
Note there is no `DISTINCT` — the 12 exact-duplicate rows (9 of them `call_received`) are counted
as-is per PRD Open Question #2, and a unit test pins that choice so it can't drift silently.

### Baseline algorithm

Per location, over the `windowDays` days ending at the anchor:

1. `recentTotal += count(d)`, zero-filled.
2. `priors = [count(d−7), count(d−14), count(d−21), count(d−28)]`. If any of those dates precedes the
   location's `FirstLocalDate`, that occurrence never existed → `InsufficientHistory` for the location.
3. `baselineTotal += mean(priors)`; accumulate each source day's contribution for outlier detection.
4. Verdict, in order: `InsufficientHistory` → `baselineTotal < 10` ⇒ `LowVolume` → `baselineTotal == 0`
   ⇒ percentage `null`, absolute delta only → otherwise
   `band = max(10m, 100m / sqrt(baselineTotal))`, and `Above`/`Below` when `|pct| > band`, else `Typical`.
5. Outlier flag when `max(contribution) / baselineTotal > 0.25`.

Ranking: `|PercentDelta|` descending, with `LowVolume` and `InsufficientHistory` last (FR14).

### API Endpoints

`GET /api/v1/accounts/{accountId}/activity-baseline?windowDays=30&locations=Site%20A,Site%20B`

| Case | Status |
|---|---|
| Valid query, rows found | `200` |
| Valid query, account has no activity (account 20) | `200`, empty `locations`, zero totals |
| `windowDays` not in {30,60,90}; unknown location name | `400` + `ProblemDetails` naming the bound |
| Unknown `accountId` | `404` |

```jsonc
{
  "accountId": 6, "accountName": "Metro Collision Centers", "timezone": "America/New_York",
  "windowDays": 30, "windowStart": "2026-06-28", "windowEnd": "2026-07-27",
  "summary": { "recentTotal": 175, "baselineTotal": 311.75, "absoluteDelta": -136.75, "percentDelta": -43.87 },
  "locations": [
    { "location": "Site O", "recentTotal": 9, "baselineTotal": 21.25, "absoluteDelta": -12.25,
      "percentDelta": -57.65, "bandPercent": 21.69, "verdict": "below",
      "baselineOutlier": { "date": "2026-06-03", "sharePercent": 41.2 } }
  ]
}
```

Signature: `Task<Results<Ok<ActivityBaselineResponse>, BadRequest<ProblemDetails>, NotFound>>`.

### Frontend

```
frontend/src/app/features/activity-baseline/
  activity-baseline-page.component.ts     container: owns state, syncs URL, fetches
  baseline-controls.component.ts          window radio group + location checkboxes
  baseline-summary.component.ts           account-level line
  baseline-table.component.ts             semantic <table scope>, verdict as text
  activity-baseline.service.ts            the only file that knows the URL and DTO shapes
  activity-baseline.models.ts
```

State is **signals in the container**, RxJS only at the HTTP edge — `toSignal` over a `switchMap`
from a params signal. The Resource API (`rxResource` / `httpResource`) is deliberately **not** used:
it is still marked experimental as of Angular 20.3, and `angular.md` already prescribes
`toSignal`. View state is a discriminated union, so a stale table can't render under an error:

```ts
type ViewState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; data: ActivityBaselineResponse };
```

URL is the store of record. `window` is validated against the literal set `[30,60,90]` and falls back
to `30`; `locations` is a comma-separated list intersected with the names the API returned, and
unknown names are dropped rather than forwarded. Writes use `replaceUrl: true` (FR23).
`ChangeDetectionStrategy.OnPush` everywhere; `@if`/`@for` with `track`; `@empty` for the empty state;
`aria-live="polite"` on the results region.

## Integration Points

None. No external services, no auth — a single hardcoded admin identity and a configured default
account id, per the PRD's out-of-scope list.

## Testing Approach

### Unit Tests

`BaselineCalculator` is where correctness lives, and it takes plain lists, so every PRD edge case is a
table-driven xUnit test with no database and no clock:

- empty input; single-row baseline; zero-activity days counted as zero (FR9)
- **`low_volume` and `insufficient_history`** — unreachable through the UI on this seed data (PRD Open
  Question #1), so these tests are the *only* coverage they get and are not optional
- baseline of zero ⇒ no division, absolute delta only (FR8)
- band boundary: values immediately either side of `max(10, 100/√baseline)`
- out-of-order and duplicate input rows produce identical totals (FR10)
- ranking: ties, and `low_volume`/`insufficient_history` sorting last
- outlier flag fires above 25% share and not below it

Frontend (Jest, run from `frontend/`): service contract and error path via `HttpTestingController`;
URL↔state round trip including malformed `window=13` and an unknown location name; each of loading /
empty / error / ready rendering, asserted by role and text.

**Reconciliation.** Account 6 at 30 days must return the figures derived from the seed during
specification — `Site O` recent `9`, baseline `21.25`, `−57.65%`; account totals `175` / `311.75`.
These are committed as a fixture so the numbers are checked by a test, not by eye (PRD Goal 3).

### Integration Tests

**None — an accepted deviation, see Standards Compliance.** The timezone GROUP BY has no automated
coverage. Mitigation: generated SQL inspected by hand and recorded in the task file, plus a documented
one-off reconciliation script comparing endpoint output against the seed-derived figures above.

## Development Sequencing

### Build Order

1. **Repository skeleton — the two roots before any feature code.** `dotnet new sln` plus the three
   projects and the test project under `backend/`; `ng new` into `frontend/`. Each root must build and
   run its (empty) test suite green on its own first, so a later failure is never ambiguous between
   the code and the scaffolding.
2. **Database + migrations.** EF Core model from `schema.sql` in `backend/src/Relay.Infrastructure`,
   seed loaded from `tasks/prd-activity-baseline/seed.sql` mounted into the db container's init
   directory — one copy of the seed, no duplicate to drift. Next because every later step needs real rows.
3. **`BaselineCalculator` + its unit tests.** Pure and dependency-free, so it can be written and
   proven before any SQL exists — and it is the part most likely to be subtly wrong.
4. **`ActivityQueries` + the `AT TIME ZONE` query.** Verify against the reconciliation fixture.
5. **Controller, DTOs, validation, ProblemDetails.**
6. **Angular feature** in `frontend/`, then URL persistence, then the loading/empty/error states.
7. **Docker Compose** (`db`; `api` from `./backend`; `web` from `./frontend`) and the README, which
   documents one build and one test command per root.

### Technical Dependencies

Docker for Postgres. Nothing else blocks.

## Monitoring and Observability

`ILogger<T>` with structured templates at the controller boundary only:
`LogWarning("Baseline window rejected for account {AccountId}: {WindowDays} days", …)` on a 400, and
one `LogInformation` per served request carrying account, window, and location count. No PII — location
labels and account ids only. Aggregation internals are silent; the unit tests are the instrument there.

## Technical Considerations

### Key Decisions

**1. A single anomalous day distorts the PRD's own reference account — flag it, don't smooth it.**
`2026-06-03` carries 882 events, 485 of them `call_received` for account 6 spread across all 15 of its
locations, against ~5–15/day typical for that account; the next-busiest day in the whole dataset is 110.
At a 30-day window that day sits outside the window but inside the baseline lookback, and all 15 of
account 6's locations read "below typical" (−26% to −58%) for that reason alone. That is arithmetically
correct and substantively misleading, and it collides with PRD Goal 5.

Two obvious robust estimators were prototyped against the seed and **both are worse**, because
per-location volume is ~0.5 calls/day and lookback quadruples look like `{0,0,1,2}`, where robust
estimators bias low against a sparse count process:

| Baseline | 30d flagged | 60d | 90d |
|---|---|---|---|
| **mean of 4 (PRD)** | **39%** | **10%** | **3%** |
| median of 4 | 29% | 68% | 80% |
| trimmed mean (drop max) | 49% | 90% | 99% |

So the PRD's mean-of-4 stands unchanged. Instead the response carries `baselineOutlier` when one source
day supplies more than **25%** of a location's baseline, and the row renders a line of text naming the
date. The threshold is empirical: it fires on exactly account 6's 15 locations at all three windows and
on nothing else — the highest share anywhere else is 20% (30d), 14% (60d), 9% (90d). An even
contribution across a 30-day window would be ~3%, so 25% is roughly 7× an even share.

**2. PostgreSQL over SQL Server, on a timezone fact rather than preference.** SQL Server's `AT TIME
ZONE` takes **Windows** zone names even in a Linux container — it maps the host's IANA zone through a
fixed table rather than accepting IANA in T-SQL. The seed stores IANA strings, so SQL Server would need
an IANA→Windows mapping table joined into every aggregation query. Postgres accepts
`AT TIME ZONE 'America/Chicago'` directly and handles the 8 March 2026 transition, `America/Phoenix`
(no shift), and `UTC` with no special-casing. This is not academic: 176 `call_received` events fall
before 08:00 UTC and therefore land on the *previous* local day in US zones.

**3. Anchor to the latest local event date per account, not wall clock.** Seed data ends 2026-07-27 and
today is 2026-09-26; a wall-clock anchor returns zero everywhere. The anchor also differs per account —
account 8's latest is 2026-07-24 and account 2's is 2026-07-26 — so it is computed per account, not
globally.

**4. Baseline math in C#, day bucketing in SQL.** *This reconciles two choices that overlap.* The
timezone conversion and the daily GROUP BY happen in Postgres; zero-fill, the trailing-4 average, the
band, the verdict, and the ranking all happen in `BaselineCalculator`. Taken literally, "all in C#"
would have pulled raw rows and bucketed local dates in C# too, discarding the `AT TIME ZONE` GROUP BY —
these are recorded as chosen, but the SQL day-bucketing is retained because it is the other chosen
decision. Flagged for correction if the intent was the stricter reading.

### Known Risks

| Risk | Mitigation |
|---|---|
| **No integration test covers the `AT TIME ZONE` GROUP BY** — the most breakable part of the feature, and an in-memory provider cannot prove SQL translation | Accepted deviation. Hand-inspect generated SQL; reconcile against committed seed-derived figures |
| Fetch span narrowed to the window during implementation ⇒ silently zero baselines on early days | Explicit unit test asserting the first window day has a non-zero baseline |
| `timestamp` vs `timestamptz` column type changes `AT TIME ZONE` semantics | Migration pins `timestamp without time zone` (UTC, as the seed states); asserted by the reconciliation fixture |
| Outlier threshold tuned on one dataset | Documented server-side constant with its derivation, not a hidden magic number |

### Special Requirements

None. 12,626 rows; no latency target beyond a view that loads without a stall (PRD).

### Standards Compliance

Followed: thin controller with typed results and `ProblemDetails`; DTOs separate from EF entities;
`IReadOnlyList<T>` and records on every public surface; `AsNoTracking` reads projecting to records;
`CancellationToken` throughout; `TimeProvider` injected rather than `DateTime.UtcNow`; parameterised
`FromSql` (never `FromSqlRaw`) with the raw-SQL choice justified in a comment; `IEntityTypeConfiguration`
for model config; migrations checked in and indexed for the aggregation's filter columns. Frontend:
standalone components, `OnPush`, signals with RxJS only at the HTTP edge, new control flow with `track`,
typed reactive form, URL-backed state validated on read, no `any`, no `HttpClient` in a component, no
client-side aggregation.

**Deviation — integration tests omitted.** `project-standards.md` and `csharp-dotnet.md` both require
at least one integration test per endpoint over a real database, explicitly because the in-memory
provider cannot evaluate SQL translation. This build ships unit tests only, by decision. The compliant
alternative is a Testcontainers Postgres fixture that applies migrations, loads `seed.sql`, and asserts
the endpoint totals against the reconciliation figures — roughly one test file, and the single highest-
value addition if the budget allows. Recorded here and to be repeated in the README rather than left
implicit.

### Relevant Files

| Path | Role |
|---|---|
| `tasks/prd-activity-baseline/prd.md` | Source requirements |
| `tasks/prd-activity-baseline/schema.sql`, `seed.sql` | Schema and seed — source of truth |
| `backend/src/Relay.Api/Controllers/ActivityBaselineController.cs` | Endpoint, validation, ProblemDetails |
| `backend/src/Relay.Application/Baseline/BaselineCalculator.cs` | All baseline math and ranking |
| `backend/src/Relay.Application/Baseline/IActivityQueries.cs` | Abstraction declared by the consumer |
| `backend/src/Relay.Infrastructure/Queries/ActivityQueries.cs` | `AT TIME ZONE` aggregation query |
| `backend/src/Relay.Infrastructure/RelayDbContext.cs`, `Migrations/` | EF Core model and migrations |
| `backend/tests/Relay.UnitTests/BaselineCalculatorTests.cs` | Edge cases + reconciliation fixture |
| `frontend/src/app/features/activity-baseline/` | Angular feature |
| `docker-compose.yml`, `README.md` | Local run, test command, documented decisions |
