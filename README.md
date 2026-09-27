# Relay — Activity Baseline (DASH-247)

> *"Is this normal for us?"*

The dashboard showed raw totals and nothing to interpret them against. This adds a **baseline
comparison**: each location's recent call volume against its own history, as a plain verdict — above
typical, typical, below typical — ranked so the location most needing attention is the first row.

"Normal" is the **trailing same-weekday average**: each day in the window is compared against the
average of that location's prior 4 same weekdays. The seed justifies it — weekday volume runs 78–97
events/day against 23–25 at weekends — and it is explainable to a customer in one sentence.

One endpoint (`GET /api/v1/accounts/{accountId}/activity-baseline`), one Angular view. Requirements in
[`prd.md`](tasks/prd-activity-baseline/prd.md), design in
[`techspec.md`](tasks/prd-activity-baseline/techspec.md), the raw AI log in [`AI_LOG.md`](AI_LOG.md) and
[`tasks/prd-activity-baseline/AI_LOG.md`](tasks/prd-activity-baseline/AI_LOG.md).

## Running it

```bash
docker compose up --build            # repository root
```

Then open **<http://localhost:4200>**. No host .NET or Node toolchain, no manual migration, no seed
load to remember. Start over with `docker compose down -v && docker compose up --build`.

| Service | What it does | Published on |
|---|---|---|
| `db` | Postgres 16, empty on first start | `localhost:5432` |
| `migrate` | one-shot `dotnet ef database update` — **creates the schema** | — |
| `seed` | one-shot `psql` load of `seed.sql` — **loads the data**, only if empty | — |
| `api` | the .NET 8 API (Swagger at `/swagger`) | `localhost:5141` |
| `web` | the built Angular bundle behind nginx | `localhost:4200` |

`migrate` and `seed` are one-shot containers that `api` waits on with
`condition: service_completed_successfully`, so the dashboard never comes up against a half-loaded
database. `seed` checks whether `activity_events` is already populated, so a repeated
`docker compose up` is a no-op rather than a duplicate-key failure — the seed inserts explicit primary
keys and is not idempotent.

### Running the two roots directly (the development path)

`backend/` and `frontend/` are independent build roots. Requires the .NET 8 SDK, **Node 24 (npm 11)**
and Docker. The npm version matters: `package-lock.json` was resolved by npm 11 and npm 10 rejects it,
which is why the `web` image builds on `node:24-alpine`.

```bash
docker compose up -d db                                                    # database only
cd backend && dotnet ef database update -p src/Relay.Infrastructure -s src/Relay.Infrastructure && cd ..
docker compose exec -T db psql -v ON_ERROR_STOP=1 -U relay -d relay -f /seed/seed.sql

cd backend  && dotnet run --project src/Relay.Api    # API on http://localhost:5141
cd frontend && npm ci && npm start                   # SPA on http://localhost:4200
```

`Relay.Infrastructure` is both the migrations project **and** the `dotnet ef` startup project: it
carries `DesignTimeRelayDbContextFactory` (reading `RELAY_DB_CONNECTION`, falling back to the local
default), which keeps `Microsoft.EntityFrameworkCore.Design` out of `Relay.Api`. `dotnet run` picks up
the `http` launch profile, which sets `ASPNETCORE_ENVIRONMENT=Development` and so supplies the local
connection string; with `--no-launch-profile` startup fails fast by design, since `appsettings.json`
ships none.

> From **Git Bash on Windows** the `psql` line needs `MSYS_NO_PATHCONV=1` in front of it, or MSYS
> rewrites `/seed/seed.sql` into a Windows path.

## Build and test

One build command and one test command per root:

| | `backend/` | `frontend/` |
|---|---|---|
| build | `dotnet build` | `npm run build` |
| test | `dotnet test` — 72 unit tests | `npm test` — 55 tests, 7 suites |

```bash
cd backend  && dotnet test --filter "FullyQualifiedName~BaselineCalculatorTests"   # a single test
cd frontend && npm test -- -t "restores the window from the URL"
```

Frontend tests are **Jest** (`jest-preset-angular`), not the Angular default Karma/Jasmine —
configured in `frontend/jest.config.js`.

## Reconciliation

Every figure is reconciled against the seed data (PRD Goal 3), at two levels. **By unit test:**
`backend/tests/Relay.UnitTests/Fixtures/` holds 300 seed-derived daily counts for account 6, asserted
by `BaselineCalculatorTests` — runs in `dotnet test`, no database. **Against the live endpoint**, which
is the part no unit test here proves (the `AT TIME ZONE` GROUP BY, the EF mapping and the JSON contract
end to end):

```bash
docker compose up -d && node scripts/reconcile.mjs      # default http://localhost:5141
```

Account 6 at the default 30-day window, against the values derived during specification:

| Figure | Expected |
|---|---|
| account `summary.recentTotal` / `baselineTotal` | `175` / `311.75` |
| `Site O` recent / baseline / percent | `9` / `21.25` / `−57.65%` |
| `Site O` rank | first row — largest absolute deviation (FR14) |
| window | `2026-06-28` … `2026-07-27` |
| locations carrying the `2026-06-03` outlier flag | all `15` |

It exits non-zero on a mismatch and names the figure that moved. No dependencies — plain Node, using
global `fetch`. The same figures were originally derived from the seed in pure SQL (a `generate_series`
zero-fill plus self-join in `psql`), owing nothing to the C# calculator, and agree row for row — so the
C# is not merely reproducing its own assumptions.

## Architecture

```
backend/                     .NET 8 — Relay.sln, Directory.Build.props (warnings as errors)
  src/Relay.Api/             controller · DTOs · validation · ProblemDetails · DI
  src/Relay.Application/     BaselineCalculator (pure math) · records · IActivityQueries
  src/Relay.Infrastructure/  RelayDbContext · migrations · ActivityQueries (FromSql)
  tests/Relay.UnitTests/
frontend/                    Angular 20 standalone workspace
  src/app/core/              ApiConfig, HTTP error interceptor
  src/app/features/activity-baseline/
```

Dependency direction is one-way: `Api → Application`, with `Infrastructure` implementing the
`IActivityQueries` interface that `Application` declares. `Relay.Application` references no EF Core
type. Nothing crosses the backend/frontend boundary except the HTTP contract.

**The split that matters: SQL buckets days, C# does the math.** Postgres does the timezone-correct day
bucketing — `(occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone)::date`, grouped by
location and local date. The zone is a *column*, not a constant, so it is not expressible in LINQ; it
is a parameterised `FromSql` (never `FromSqlRaw`), with the reason in a comment at the call site.
`BaselineCalculator` — pure and synchronous, no database, no clock, no I/O — does the zero-fill, the
trailing-4 average, the band, the verdict, the outlier flag and the ranking.

**The fetch span is wider than the window.** The baseline for the window's *first* day looks back 28
days, so the query spans `anchor − (windowDays − 1) − 28 … anchor`. Fetching only the window fails
silently — early days just get a baseline of zero. Reproduced deliberately in Task 4.0: narrowing the
span leaves `recentTotal` at `175` and collapses the baseline from `311.75` to `74.75`. A unit test
pins the span for all three window lengths.

### Frontend state management

**Signals**, in the container component and in services; **RxJS only at the HTTP edge** (`toSignal`
over a `switchMap`). One approach, no second store library. The Resource API (`rxResource` /
`httpResource`) is deliberately not used — still experimental as of Angular 20.3. View state is a
discriminated union (`{ status: 'loading' } | { status: 'error'; message } | { status: 'ready'; data }`)
so a stale table can never render underneath an error.

**The query string is the store of record.** Window and location filter are written to the URL with
`replaceUrl: true` (so Back leaves the dashboard) and restored on load, which makes any view shareable.
Values read from the URL are user input and are validated (FR22), with documented fallbacks:

| URL value | Behaviour |
|---|---|
| `window` not one of `30`, `60`, `90` (e.g. `?window=13`) | falls back to **30**; the malformed value never reaches the API |
| `locations` containing a name the account does not have | that name is **dropped**, the rest are kept |
| `locations` where *no* name is recognised | falls back to **all locations**, not an empty table |

The URL is not rewritten to erase a bad value — FR22 asks for a documented fallback, not a tidy address
bar. **A filtered deep link issues two requests, on purpose:** the API rejects an unknown location with
a `400` and a filtered response only contains what it was asked for, so the first request carries the
window alone, and the names it returns are both what the URL's list is intersected against and what the
checkboxes are built from.

## Constants and their derivation

All four live server-side in
[`BaselineThresholds`](backend/src/Relay.Application/Baseline/BaselineThresholds.cs). A configuration
UI is out of scope, so these are documented constants rather than settings.

| Constant | Value | Why that value |
|---|---|---|
| Prior occurrences per baseline | **4** same weekdays | A month of like-for-like history; weekday/weekend volume differs 4×, so comparing a Monday to anything but other Mondays is noise. |
| Low-volume floor | baseline **< 10** ⇒ `low_volume` | Chosen so no 30-day seed row is suppressed (the smallest baseline is `11.25`) while still refusing a confident −40% for a site where two missing calls explain it. A judgement call, not a derived threshold. |
| Neutral band | **`±max(10%, 100/√baseline)`** | The band must widen where counts are small: at a baseline of 4, one call is 25%. A flat ±10% band labelled **67–84%** of locations unusual and destroyed the signal. The 10% floor stops the band being tighter than ordinary weekly variation. |
| Outlier share | one source day **> 25%** of a baseline | Empirical: at 25% it fires on exactly account 6's 15 locations at all three windows and nothing else — the highest share anywhere else is 20% (30d), 14% (60d), 9% (90d). An even contribution over 30 days is ~3%, so this is ~7× even. |

Verdict order, easy to get wrong: `insufficient_history` → `baseline < 10` ⇒ `low_volume` →
`baseline == 0` ⇒ absolute delta only, percentage `null` → else band, `above`/`below` when
`|pct| > band`, else `typical`. Ranking is `|percentDelta|` descending, `low_volume` and
`insufficient_history` last.

## Decisions

**1. PostgreSQL, on a timezone fact rather than a preference.** SQL Server's `AT TIME ZONE` takes
**Windows** zone names even in a Linux container; the seed stores IANA strings, so SQL Server would
need an IANA→Windows mapping table joined into every aggregation query. Postgres accepts
`AT TIME ZONE 'America/Chicago'` directly and handles the 8 March 2026 DST transition,
`America/Phoenix` (no shift) and `UTC` with no special-casing. Not academic: **176 `call_received`
events fall before 08:00 UTC** and so land on the *previous* local day in US zones — verified event by
event (seed event `9395`, `2026-06-10 03:43` UTC, counts on `2026-06-09` local).

**2. The anchor is the latest local event date, per account — never the wall clock.** The seed ends
2026-07-27 and today is later, so a `DateTime.UtcNow` anchor returns zero everywhere. It also differs
per account (account 8's latest is `2026-07-24`), so it is computed per account. `TimeProvider` is
injected rather than calling `DateTime.UtcNow`, and is only reached for an account with no events.

**3. The `2026-06-03` anomaly is flagged, not smoothed.** That day carries 882 events, 485 of them
`call_received` for account 6 across all 15 of its locations, against ~5–15/day typical — the
next-busiest day in the dataset is 110. At a 30-day window it sits *outside* the window but *inside*
the baseline lookback, so all 15 locations read "below typical" (−26% to −58%) for that reason alone:
arithmetically correct, substantively misleading, a collision with PRD Goal 5. Two robust estimators
were prototyped and **both are worse**, because per-location volume is ~0.5 calls/day and a lookback
quadruple looks like `{0,0,1,2}`, where robust estimators bias low against a sparse count process:

| Baseline | 30d flagged | 60d | 90d |
|---|---|---|---|
| **mean of 4 (shipped)** | **39%** | **10%** | **3%** |
| median of 4 | 29% | 68% | 80% |
| trimmed mean (drop max) | 49% | 90% | 99% |

So the mean-of-4 stands and the response carries a `baselineOutlier` the row renders as text naming the
date. Flagging beats silently reshaping the number the customer is told.

**4. Baseline math in C#, day bucketing in SQL — with the tension named.** Two decisions that overlap.
Taken literally, "all baseline math in C#" would have pulled raw rows and bucketed local dates in C#
too, discarding the `AT TIME ZONE` GROUP BY — the one part of the query that cannot be written in LINQ
and the one part the DST boundary makes non-trivial. **Flagged here as well as in the techspec: if the
stricter reading was the intent, this is the line to correct, and it is contained** — the calculator
already takes a plain list of `(location, localDate, calls)`, so only the source of that list moves.

## Assumptions and open questions

Each stood in for something the requirements did not settle. None is hidden in the code.

1. **The 12 exact-duplicate seed rows are counted as-is** (9 of them `call_received`). Nothing in the
   schema marks them as erroneous, so there is no `DISTINCT` in the aggregation and a unit test pins the
   choice. If they are a data-quality artefact, the fix is one `DISTINCT` and one test.
2. **The DST short day is counted as an ordinary calendar date.** On 8 March 2026 a US local day is 23
   hours; it is accepted rather than normalised — account 1's March days bucket `03-06` … `03-10`
   continuously, no gap, no duplicate.
3. **The 10-call low-volume floor is a judgement call**, not a derived threshold.
4. **Two specified states are unreachable from this seed.** All 69 locations have the full ~25 weeks of
   history and the smallest 30-day baseline is `11.25`, so `insufficient_history` (FR7) and
   `low_volume` (FR6) never fire through the UI. They are correct for a real account with a new site;
   their coverage is unit tests. **The window options were deliberately not widened to manufacture a
   demo path.**
5. **`insufficient_history` rows report no baseline and no delta**, and the account summary is the sum
   of the rows' visible columns (FR16) — the PRD does not say how an unjudgeable location should affect
   the total. No seed location reaches this verdict.
6. **`windowDays` absent means 30.** A value present and out of range is rejected with `400`, never
   clamped: clamping `13` to `30` would silently change what the numbers mean.
7. **One hardcoded account.** Auth and multi-account switching are out of scope, so the account is
   configuration (`frontend/src/environments/environment.ts`, overridable as the `web` build's
   `ACCOUNT_ID`). Account 6 (15 locations) is the reference case; **account 20 (Quiet Harbor Spa) has
   zero events** and is the empty-state fixture with real data.

## Deviations from the project standards

**1. No integration tests — the one that matters.** `project-standards.md` and `csharp-dotnet.md` both
require at least one integration test per endpoint over a real database, *explicitly* because the
in-memory provider cannot evaluate SQL translation. This build ships unit tests only, by decision under
the time budget, and the consequence is worth stating plainly: **the `AT TIME ZONE` GROUP BY — the most
breakable part of the feature — has no automated coverage.**

Mitigated, not ignored: the generated SQL was hand-inspected and recorded in
`tasks/prd-activity-baseline/4_task.md`, the timezone conversion was spot-checked event by event, and
`node scripts/reconcile.mjs` reconciles the live endpoint on demand. The compliant alternative, named
so it does not stay implicit: **a Testcontainers Postgres fixture that applies the migrations, loads
`seed.sql`, and asserts the endpoint's account-6 totals against the figures above** — roughly one test
file, and the single highest-value addition to this build.

**2. `seed.sql` is not loaded through `/docker-entrypoint-initdb.d`.** The techspec said "mounted into
the db container's init directory", but that runs the seed *before* any table exists, since EF Core
migrations create the schema. The order is empty container → migrations → seed, encoded as the
`migrate` and `seed` services: one source of schema truth, one copy of the seed (mounted, not copied).

**3. `occurred_at` is `DateTime`, not the techspec's `DateTimeOffset`.** Npgsql maps `DateTimeOffset` to
`timestamptz` unconditionally, so § Data Models and § Known Risks ("pin `timestamp without time zone`")
cannot both hold. The column type wins: with `timestamptz` the first half of
`occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone` becomes a no-op and the day bucketing
silently changes meaning. Pinned explicitly in `ActivityEventConfiguration`.

**4. `dotnet ef` uses `Relay.Infrastructure` as its own startup project**, because
`Microsoft.EntityFrameworkCore.Design` is a `PrivateAssets="all"` reference there and never flows to
`Relay.Api` — preferable to adding design-time tooling to the API project.

**5. The controller's `400` bodies are `application/json`, not `application/problem+json`.**
`TypedResults.BadRequest<ProblemDetails>` applies the RFC 9110 defaults but not the content type;
keeping the prescribed signature was judged more important than the header, and the fields are the same.

**6. No end-to-end suite.** The browser pass over the loading / empty / error / reload states was driven
with Playwright from a scratch directory rather than added to `frontend/package.json` — the techspec
calls for no e2e suite. Results in `tasks/prd-activity-baseline/7_task.md`.

## Configuration and secrets

No secrets are committed, and none exist to commit — the only credentials anywhere are a throwaway
local Postgres container's, fixed in `docker-compose.yml`.

- `appsettings.json` ships `Database:ConnectionString` **empty**. `appsettings.Development.json`
  supplies the local value for `dotnet run`; compose supplies `Database__ConnectionString` as an
  environment variable and runs the API as `ASPNETCORE_ENVIRONMENT=Production`, where the development
  file is not loaded at all. Startup fails fast (`ValidateOnStart`) when neither provides one.
- CORS is an explicit allowlist bound from configuration (`Frontend:AllowedOrigins`), never a wildcard.
- The API container runs as the base image's non-root `APP_UID` (1654).

## Scope

Out of scope by the brief and by decision: auth/login (a single trusted admin identity), alerting,
ML/forecasting, production infra, visual polish beyond legibility, multi-account switching, CSV export,
charts, a baseline configuration UI, and the `lead_created` / `appointment_set` metrics. Deferred:
per-metric breakdown, per-location trend history, an account-level "typical range" band.
