---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>infrastructure/queries</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>database</dependencies>
</task_context>

# Task 4.0: ActivityQueries and the AT TIME ZONE aggregation

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

The data side of the feature: the `IActivityQueries` abstraction declared in `Relay.Application` and
its `ActivityQueries` implementation in `Relay.Infrastructure`. Postgres does the timezone-correct day
bucketing — `occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone` — and hands the calculator
from Task 3.0 a flat list of daily counts (techspec § Development Sequencing, step 4).

This is the most breakable part of the feature and the part with no automated integration coverage
(techspec § Known Risks — accepted deviation). Verification here is by hand-inspected SQL and by
reconciliation against the seed figures, and both must be recorded in this file.

<requirements>
- `IActivityQueries` is declared in `Relay.Application` — the consumer defines the abstraction; `Application` references no EF Core type (`csharp-dotnet.md` § Project layout).
- Three members exactly as in techspec § Main Interfaces: `GetAccountAsync`, `GetLocationHistoryAsync`, `GetDailyCallCountsAsync`, each taking a `CancellationToken`.
- The aggregation uses parameterised `FromSql` — **never** `FromSqlRaw` with interpolation. The raw-SQL choice carries a comment explaining why LINQ can't express it.
- **No `DISTINCT`.** The 12 exact-duplicate rows are counted as-is, per PRD Open Question #2.
- `AsNoTracking()` on every read; project straight to the records — no `ToListAsync()` then in-memory `Where`/`Sum`.
- The anchor is the **latest local event date for that account**, computed per account, not globally and not from wall clock (techspec § Key Decisions #3).
- **The fetch span is wider than the window**: `anchor − (windowDays − 1) − 28 … anchor`. Fetching only the window silently yields zero baselines for the window's early days.
- Returns empty collections for accounts with no activity, never null.
- Event type filter is `call_received` — a named constant, passed as a parameter.
</requirements>

## Subtasks

- [x] 4.1 Declare `IActivityQueries` and its parameter/result records in `Relay.Application`.
- [x] 4.2 Implement `GetAccountAsync` — account id, name, timezone, and the latest **local** activity date (the anchor). Returns null for an unknown account id so the controller can answer 404.
- [x] 4.3 Implement `GetLocationHistoryAsync` — distinct locations with first/last local activity date. This sources the row list and is what distinguishes "no occurrence existed" from "a genuine zero" in Task 3.0's insufficient-history rule.
- [x] 4.4 Implement `GetDailyCallCountsAsync` with the `AT TIME ZONE` query in techspec § The aggregation query.
- [x] 4.5 Wire the fetch span: callers ask for the window, the service requests `windowDays − 1 + 28` days back from the anchor.
- [x] 4.6 Add the application-layer service that composes queries + calculator into a `BaselineReport`, honouring the optional location filter.
- [x] 4.7 Register `ActivityQueries` in DI with a `Scoped` lifetime (it depends on the scoped `DbContext`).
- [x] 4.8 **Capture the generated SQL by hand** and paste it into this file with a note on which index it uses.
- [x] 4.9 Reconcile against the seed: account 6, 30-day window ⇒ totals `175` / `311.75`, `Site O` recent `9` / baseline `21.25` / `−57.65%`. Record the real numbers produced.
- [x] 4.10 Spot-check the timezone conversion against a known boundary case — one of the 176 `call_received` events before 08:00 UTC must land on the previous local day in a US zone (techspec § Key Decisions #2).

## Implementation Details

The query, verbatim, plus the sargability note and the no-`DISTINCT` decision: techspec § The
aggregation query. Anchor rule: techspec § Key Decisions #3 (account 8's latest is 2026-07-24,
account 2's is 2026-07-26 — per account, genuinely). Fetch span: techspec § System Architecture →
Data flow. Raw SQL rules: `csharp-dotnet.md` § EF Core and data access.

`America/Phoenix` (accounts 7, 15) does not shift and `UTC` (account 18) does not either; the window
spans the 8 March 2026 US DST transition. The conversion must be real, not a fixed offset (PRD FR11).
On the DST day a local day is 23 hours and events are counted by local calendar date, short day
accepted (PRD Open Question #4).

**Verification is manual here, by decision.** Techspec § Testing Approach → Integration Tests records
that no integration test covers this GROUP BY, because the in-memory provider cannot prove SQL
translation. Subtasks 4.8–4.10 are the mitigation and are therefore mandatory, not optional polish.

## Success Criteria

- `dotnet build` clean; `dotnet test` still green.
- The generated SQL is pasted into this file, inspected, and its index usage noted.
- Account 6 at 30 days reconciles to `175` / `311.75` and `Site O` at `9` / `21.25` / `−57.65%`, with the produced output recorded.
- Account 20 (zero events) returns an empty location list and zero totals without throwing.
- Deliberately narrowing the fetch span to the window makes an early-window baseline go to zero — confirming the span is doing real work.
- `Relay.Application` still has no EF Core package reference.
- No `FromSqlRaw` anywhere; no string-concatenated SQL.

## Verification record (subtasks 4.8–4.10)

These three subtasks are the agreed mitigation for having no integration test over the
`AT TIME ZONE` GROUP BY (techspec § Known Risks). Everything below is captured output, not
intention. Database: `relay-db` (postgres:16) with the Task 2.0 migration applied and `seed.sql`
loaded — 12,626 events, 20 accounts.

### 4.8 Generated SQL, as logged by EF Core

Captured from `Microsoft.EntityFrameworkCore.Database.Command` at `Information` level while running
all three queries against the real database. Every value arrives as a `DbParameter` (`@p0`…`@p3`);
nothing is concatenated.

**`GetAccountAsync`** — EF wraps the raw SQL in a subquery because `SingleOrDefaultAsync` composes
a `LIMIT 2` over it:

```sql
-- Parameters=[p0='?', p1='?' (DbType = Int32)]
SELECT r."Id", r."LatestLocalDate", r."Name", r."Timezone"
FROM (
    SELECT a.id       AS "Id",
           a.name     AS "Name",
           a.timezone AS "Timezone",
           MAX((e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date)
               AS "LatestLocalDate"
    FROM accounts a
    LEFT JOIN activity_events e
           ON e.account_id = a.id
          AND e.event_type = @p0
    WHERE a.id = @p1
    GROUP BY a.id, a.name, a.timezone
) AS r
LIMIT 2
```

**`GetLocationHistoryAsync`** — no wrapping subquery; `ToListAsync` does not compose:

```sql
-- Parameters=[p0='?' (DbType = Int32), p1='?']
SELECT e.location AS "Location",
       MIN((e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date) AS "FirstLocalDate",
       MAX((e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date) AS "LastLocalDate"
FROM activity_events e
JOIN accounts a ON a.id = e.account_id
WHERE e.account_id = @p0
  AND e.event_type = @p1
GROUP BY e.location
ORDER BY e.location
```

**`GetDailyCallCountsAsync`** — the aggregation. No `DISTINCT`:

```sql
-- Parameters=[p0='?' (DbType = Int32), p1='?', p2='?' (DbType = Date), p3='?' (DbType = Date)]
SELECT e.location AS "Location",
       (e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date AS "LocalDate",
       COUNT(*)::int AS "Calls"
FROM activity_events e
JOIN accounts a ON a.id = e.account_id
WHERE e.account_id = @p0
  AND e.event_type = @p1
  AND (e.occurred_at AT TIME ZONE 'UTC' AT TIME ZONE a.timezone)::date
      BETWEEN @p2 AND @p3
GROUP BY e.location, 2
```

#### Index usage

`EXPLAIN (ANALYZE, BUFFERS)` on the aggregation, account 6 over the 30-day fetch span:

```
HashAggregate (actual time=1.485..1.517 rows=300 loops=1)
  Group Key: e.location, (((e.occurred_at AT TIME ZONE 'UTC') AT TIME ZONE a.timezone))::date
  ->  Nested Loop (actual time=0.674..1.340 rows=849 loops=1)
        Join Filter: ((((e.occurred_at AT TIME ZONE 'UTC') AT TIME ZONE a.timezone))::date >= '2026-05-31'
                 AND (((e.occurred_at AT TIME ZONE 'UTC') AT TIME ZONE a.timezone))::date <= '2026-07-27')
        Rows Removed by Join Filter: 752
        ->  Index Scan using "PK_accounts" on accounts a  (rows=1)
              Index Cond: (id = 6)
        ->  Bitmap Heap Scan on activity_events e  (rows=1601)
              Recheck Cond: ((account_id = 6) AND ((event_type)::text = 'call_received'::text))
              ->  Bitmap Index Scan on ix_activity_events_account_id_event_type_occurred_at  (rows=1601)
                    Index Cond: ((account_id = 6) AND ((event_type)::text = 'call_received'::text))
Execution Time: 1.617 ms
```

**The index used is `ix_activity_events_account_id_event_type_occurred_at`** — the composite added
by the Task 2.0 migration — as a Bitmap Index Scan on its `(account_id, event_type)` prefix. It cuts
12,626 rows to 1,601 before any timezone arithmetic happens.

This is exactly the behaviour the techspec predicted: **the local-date `BETWEEN` is not sargable**.
The planner cannot use `occurred_at` for it, so it demotes the predicate to a Join Filter applied
after the index scan — visible as `Rows Removed by Join Filter: 752`. At 1.6 ms over the whole
dataset that is the accepted trade. The usual fix, an expression index on
`((occurred_at AT TIME ZONE 'UTC' AT TIME ZONE …)::date)`, is not available here because the zone
comes from a joined column rather than a constant, so the expression is not immutable.
`GetLocationHistoryAsync` uses the same index the same way; `GetAccountAsync` additionally seeks
`PK_accounts`.

### 4.9 Reconciliation — account 6, 30-day window

Produced by running the real `ActivityBaselineService` (real queries, real database, real
calculator) and printing every row. The anchor resolved to **2026-07-27** from the data.

```
window 2026-06-28 .. 2026-07-27
TOTALS recent=175 baseline=311.75

location recent  baseline      pct    band verdict  outlier
Site O        9     21.25   -57.65   21.69 Below    2026-06-03 @ 31.76%
Site F        8     18.50   -56.76   23.25 Below    2026-06-03 @ 51.35%
Site M        7     15.25   -54.10   25.61 Below    2026-06-03 @ 42.62%
Site D        8     17.25   -53.62   24.08 Below    2026-06-03 @ 52.17%
Site N        8     16.25   -50.77   24.81 Below    2026-06-03 @ 49.23%
Site G        9     17.50   -48.57   23.90 Below    2026-06-03 @ 44.29%
Site J        9     17.00   -47.06   24.25 Below    2026-06-03 @ 41.18%
Site B       13     24.00   -45.83   20.41 Below    2026-06-03 @ 41.67%
Site L       14     25.25   -44.55   19.90 Below    2026-06-03 @ 46.53%
Site H       13     22.50   -42.22   21.08 Below    2026-06-03 @ 30.00%
Site I       12     20.00   -40.00   22.36 Below    2026-06-03 @ 40.00%
Site A       14     22.50   -37.78   21.08 Below    2026-06-03 @ 25.56%
Site E       18     28.00   -35.71   18.90 Below    2026-06-03 @ 28.57%
Site K       13     19.50   -33.33   22.65 Below    2026-06-03 @ 37.18%
Site C       20     27.00   -25.93   19.25 Below    2026-06-03 @ 34.26%
```

**Matches the target exactly:** account totals `175` / `311.75`; `Site O` recent `9`, baseline
`21.25`, `−57.65%`, ranked first as the largest deviation. The `2026-06-03` anomaly is flagged on
all 15 locations and on nothing else, as techspec § Key Decisions #1 predicted.

These figures were also derived **independently in pure SQL** — a `generate_series` zero-fill plus
self-join written directly in psql, owing nothing to the C# calculator — and agree row for row, so
the C# is not merely reproducing its own assumptions.

Other results from the same run:

- **The anchor is genuinely per account**, taken from the data and never from the clock:
  account 2 → `2026-07-26`, account 6 → `2026-07-27`, account 8 → `2026-07-24`,
  account 18 → `2026-07-27`. Matches techspec § Key Decisions #3.
- **Account 20 (zero events)**: `Ready` with `locations=0`, `recent=0`, `baseline=0`, no exception.
  `GetLocationHistoryAsync(20)` returns an empty list, not null. It has no anchor in its data, so
  the window falls back to today in the account's own zone, read from the injected `TimeProvider`.
- **Unknown account 999** → `AccountNotFound`. **Unknown location** `Site ZZ` → `UnknownLocations`,
  rejected rather than silently dropped. **Filter `[Site O, Site C]`** → 2 rows, recent `29`,
  baseline `48.25` — the two rows above, added up.

### 4.10 Timezone conversion spot-check

**Day boundary.** Seed event id `9395`: account 6 (`America/New_York`), `Site J`,
`occurred_at = 2026-06-10 03:43:00` UTC. Local time is `2026-06-09 23:43`, so it must count on
**2026-06-09**, the previous local day. Counting `Site J` over those two days both ways:

| bucketed by | 2026-06-09 | 2026-06-10 |
|---|---|---|
| raw UTC date (wrong) | 1 | 4 |
| `AT TIME ZONE` local date (the query) | **2** | **3** |

Exactly one event moves back a day, and `GetDailyCallCountsAsync` returns the local-day figures
(`Site J 2026-06-09 → 2`, `Site J 2026-06-10 → 3`). This is one of the 176 `call_received` events
falling before 08:00 UTC — count re-confirmed against the loaded seed.

**DST is a real conversion, not a fixed offset.** The same expression the query uses, evaluated
across the 8 March 2026 US transition:

```
'2026-03-07 13:30:00' AT TIME ZONE 'UTC' AT TIME ZONE 'America/New_York'  ->  2026-03-07 08:30  (EST, UTC-5)
'2026-03-09 13:30:00' AT TIME ZONE 'UTC' AT TIME ZONE 'America/New_York'  ->  2026-03-09 09:30  (EDT, UTC-4)
'2026-03-09 13:30:00' AT TIME ZONE 'UTC' AT TIME ZONE 'America/Phoenix'   ->  2026-03-09 06:30  (no shift)
```

The offset moves by an hour across the transition while `America/Phoenix` holds still — something a
hardcoded offset cannot do. The short 23-hour local day on 2026-03-08 is counted as an ordinary
calendar date per PRD Open Question #4: account 1's March days bucket 03-06 … 03-10 continuously,
with no gap and no duplicate.

### Fetch span — proving it does real work

Same anchor and window; the only change is the start date handed to `GetDailyCallCountsAsync`:

```
widened span     2026-05-31..2026-07-27 : 300 rows -> recent=175  baseline=311.75
narrowed to window 2026-06-28..2026-07-27 : 136 rows -> recent=175  baseline=74.75   <-- silently wrong
  Site O widened : baseline=21.25  pct=-57.65  Below
  Site O narrowed: baseline=4.75   pct=(none)  LowVolume
```

The recent total is unchanged, so nothing looks broken — the baseline quietly collapses to under a
quarter of its value and `Site O` degrades to `LowVolume`. That is the failure mode the techspec
named, reproduced on demand. `ActivityBaselineServiceTests` now pins the span at
`anchor − (windowDays − 1) − 28` for all three window lengths.

### Verification harness

The output above came from a throwaway console program that resolved `IActivityQueries` and
`IActivityBaselineService` from the real DI container — so the Task 4.7 registrations are exercised
too — with EF command logging switched on. It was deleted after the run, matching the seed-derivation
prototype from Task 3.0. Everything it proved is either recorded here or pinned by a unit test.

### Note on a Task 2.0 command that did not run

`docker compose up -d db` was already running with the migration applied and the seed loaded, but
the one-liner documented in `docker-compose.yml`,
`dotnet ef database update -p src/Relay.Infrastructure -s src/Relay.Api`, fails:
`Your startup project 'Relay.Api' doesn't reference Microsoft.EntityFrameworkCore.Design.` The
package is a `PrivateAssets="all"` reference in `Relay.Infrastructure`, so it never flows to
`Relay.Api`. `-s src/Relay.Infrastructure` works, because `DesignTimeRelayDbContextFactory` lets
Infrastructure stand in as the startup project. The comment in `docker-compose.yml` was corrected
here rather than left for Task 8.0 to copy into the README.

## Relevant Files

Created:
- `backend/src/Relay.Application/Baseline/IActivityQueries.cs` — the abstraction, declared by its consumer
- `backend/src/Relay.Application/Baseline/IActivityBaselineService.cs`
- `backend/src/Relay.Application/Baseline/ActivityBaselineService.cs` — fetch span, location filter, anchor
- `backend/src/Relay.Application/Baseline/BaselineQuery.cs`
- `backend/src/Relay.Application/Baseline/BaselineOutcome.cs` — closed union: Ready / AccountNotFound / UnknownLocations
- `backend/src/Relay.Infrastructure/Queries/ActivityQueries.cs` — the three `FromSql` reads
- `backend/src/Relay.Infrastructure/Queries/Rows/AccountAnchorRow.cs`
- `backend/src/Relay.Infrastructure/Queries/Rows/LocationHistoryRow.cs`
- `backend/src/Relay.Infrastructure/Queries/Rows/DailyCallCountRow.cs`
- `backend/src/Relay.Infrastructure/Configurations/AccountAnchorRowConfiguration.cs`
- `backend/src/Relay.Infrastructure/Configurations/LocationHistoryRowConfiguration.cs`
- `backend/src/Relay.Infrastructure/Configurations/DailyCallCountRowConfiguration.cs`
- `backend/tests/Relay.UnitTests/ActivityBaselineServiceTests.cs` — 11 tests over span, filter, anchor, outcomes

Modified:
- `backend/src/Relay.Infrastructure/ServiceCollectionExtensions.cs` — `IActivityQueries` registered `Scoped`
- `backend/src/Relay.Api/Program.cs` — `TimeProvider`, `IBaselineCalculator`, `IActivityBaselineService`
- `docker-compose.yml` — corrected the `dotnet ef` startup project in the documented one-liner

Unchanged, deliberately:
- `backend/src/Relay.Infrastructure/Migrations/` — the keyless row types are `ToView(null)`, so
  `dotnet ef migrations has-pending-model-changes` reports "No changes have been made to the model
  since the last migration". No migration was added or edited.
- `backend/src/Relay.Application/Relay.Application.csproj` — still no package and no project reference.
