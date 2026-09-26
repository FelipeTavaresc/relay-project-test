---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>application/baseline</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>none</dependencies>
</task_context>

# Task 3.0: BaselineCalculator domain service and unit tests

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

All of the baseline math: zero-fill, the trailing same-weekday average, the volume-aware band, the
verdict, the outlier flag, and the ranking. It is a pure function over a list of daily counts — no
database, no clock, no I/O — which is why it can be written and proven before any SQL exists, and why
every PRD edge case is a plain unit test here (techspec § Development Sequencing, step 3).

This is the part most likely to be subtly wrong, and it is where correctness for the whole feature
lives. Treat the tests as the deliverable, not as a follow-up.

<requirements>
- `IBaselineCalculator` / `BaselineCalculator` in `Relay.Application`, synchronous, with no injected dependencies and no `DateTime.UtcNow`.
- Domain records exactly as in techspec § Data Models: `AccountContext`, `LocationHistory`, `DailyLocationCount`, `Verdict`, `LocationBaseline`, `OutlierDay`, `BaselineReport`.
- `decimal` for baselines, deltas, percentages and band; `DateOnly` for calendar values (`csharp-dotnet.md` § Immutability and types).
- Algorithm exactly as specified in techspec § Baseline algorithm, including the verdict precedence order: `InsufficientHistory` → `LowVolume` (baseline < 10) → zero baseline (null percentage, absolute delta only) → band comparison.
- Band is `max(10m, 100m / sqrt(baselineTotal))`; `Above`/`Below` only when `|pct| > band`, else `Typical` (PRD FR5).
- Outlier flag when one source day supplies more than **25%** of a location's baseline — a named constant with its derivation in a comment, not a magic number (techspec § Known Risks).
- Ranking: `|PercentDelta|` descending, `LowVolume` and `InsufficientHistory` last (PRD FR14).
- The thresholds (4 prior weekdays, 10-call floor, 10% band floor, 25% outlier share) are named server-side constants — PRD § Out of Scope forbids a configuration UI for them.
- `IReadOnlyList<T>` on every public surface; empty collections, never null.
- Input rows are unordered and may contain duplicates; output must not depend on input order (PRD FR10).
</requirements>

## Subtasks

- [x] 3.1 Write the domain records and the `Verdict` enum in `Relay.Application/Baseline/`.
- [x] 3.2 Implement zero-fill across the window and the trailing-4 same-weekday lookback (`d−7`, `d−14`, `d−21`, `d−28`).
- [x] 3.3 Implement the insufficient-history rule: if any of the 4 prior dates precedes the location's `FirstLocalDate`, that occurrence never existed → `InsufficientHistory` for the location.
- [x] 3.4 Implement band, verdict precedence, and the null-percentage cases.
- [x] 3.5 Implement per-source-day contribution tracking and the 25% outlier flag.
- [x] 3.6 Implement the ranking, including the tie behaviour and the low-volume/insufficient-history tail.
- [x] 3.7 Implement the account-level summary totals on `BaselineReport`.
- [x] 3.8 Unit tests — the full list in Implementation Details below. Table-driven where the cases share a shape.
- [x] 3.9 Commit the reconciliation fixture (account 6, 30-day window) and assert the seed-derived figures.
- [x] 3.10 Run `dotnet test` and record the real output, failures included.

## Implementation Details

Algorithm: techspec § Baseline algorithm. Records: techspec § Data Models. Verdict semantics and the
reasoning behind the volume-aware band: PRD FR4–FR9. Ranking: PRD FR14.

**Required test cases** (techspec § Testing Approach → Unit Tests). These are not a suggestion:

- empty input; single-row baseline; zero-activity days counted as zero, not missing (FR9)
- **`low_volume` and `insufficient_history`** — unreachable through the UI on this seed data (PRD Open
  Question #1), so these tests are their *only* coverage and are not optional
- baseline of zero ⇒ no division, absolute delta only, percentage null (FR8)
- band boundary: values immediately either side of `max(10, 100/√baseline)` — assert `Typical` at the
  boundary and `Above`/`Below` just past it
- out-of-order and duplicate input rows produce identical totals (FR10); duplicates are **counted
  as-is**, not deduplicated (PRD Open Question #2) — pin this choice with a test so it cannot drift
- ranking: ties, and `low_volume`/`insufficient_history` sorting last
- outlier flag fires above 25% share and not below it
- **the fetch-span trap**: assert the *first* day of the window has a non-zero baseline when prior
  weekday data exists. This is the highest-value single test in the task — techspec § Known Risks
  names a narrowed fetch span as the likely implementation bug, and it fails silently otherwise.

**Reconciliation fixture.** Account 6 at 30 days must produce `Site O` recent `9`, baseline `21.25`,
`−57.65%`, and account totals `175` / `311.75` (techspec § Testing Approach → Reconciliation). Commit
the daily counts as a fixture so these numbers are checked by a test, not by eye (PRD Goal 3). Task
8.0 checks the same figures end to end against the running endpoint.

## Success Criteria

- `dotnet test` green, with real output recorded in this file.
- Every bullet in the required-test list above has a named test; `Method_Scenario_ExpectedOutcome` naming.
- The reconciliation fixture test passes against the exact figures above.
- `BaselineCalculator` has no constructor dependencies, no `DateTime`/`Random` use, and no reference to EF Core or `Relay.Infrastructure`.
- Shuffling the input row order in a test changes nothing about the output.
- `dotnet build` clean — no new warnings.

## Build and Test Output

`dotnet build` from `backend/` — clean, no warnings:

```
  Relay.Application -> ...\src\Relay.Application\bin\Debug\net8.0\Relay.Application.dll
  Relay.Infrastructure -> ...\src\Relay.Infrastructure\bin\Debug\net8.0\Relay.Infrastructure.dll
  Relay.UnitTests -> ...\tests\Relay.UnitTests\bin\Debug\net8.0\Relay.UnitTests.dll
  Relay.Api -> ...\src\Relay.Api\bin\Debug\net8.0\Relay.Api.dll

Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

`dotnet test` from `backend/`:

```
Aprovado!  – Com falha:     0, Aprovado:    47, Ignorado:     0, Total:    47, Duração: 89 ms - Relay.UnitTests.dll (net8.0)
```

**The first run was red: 10 of 47 failed.** All ten were wrong arithmetic in the *tests*, not in the
calculator — the `EqualPriors(location, n)` helper gives four priors of `n` calls each, so the
baseline is `4n/4 = n`, and the theory rows had been written as though the baseline were the sum
(`InlineData(4, 20, "25.00", …)` expecting a baseline of 16). Recorded because a green-first-run note
would have been a lie, and because the failure mode is the same one the calculator is guarding
against: confusing a sum of occurrences with their mean.

```
Com falha! – Com falha:    10, Aprovado:    37, Ignorado:     0, Total:    47
  Evaluate_PercentDeltaAtOrBeyondTheBand_ReturnsTheExpectedVerdict  (8 theory rows)
      Expected: 25,00   Actual: 400,0
  Evaluate_Ranking_TreatsASpikeAndADropAsEquallyDeviant
      Expected: ["Site Down", "Site Up", "Site Mid"]
      Actual:   ["Site Down", "Site Mid", "Site Up"]
  Evaluate_Summary_SumsTheLocationColumns
      Expected: 56   Actual: 14
```

## Notes and Decisions

**Reconciliation is derived, not transcribed.** `CLAUDE.md` points at `gawk` prototypes in
`ai-log/seed-analysis/`; that directory did not exist in the repository. Rather than hand-assert the
figures, the fixture is derived from `seed.sql` by `ai-log/seed-analysis/`, now committed and
re-runnable, using `TimeZoneInfo` with the IANA id — real tz rules, not the hardcoded DST cutover the
prototypes approximated, so it agrees with Task 4.0's `AT TIME ZONE` bucketing. Two figures stated
independently in the specs fall out of it unprompted, which is what makes it trustworthy: 2026-06-03
sums to exactly **485** `call_received` events across account 6's 15 locations, and the account
totals are **175 / 311.75** with `Site O` at **9 / 21.25 / −57.65%** and a band of **21.69%**.

**One spec figure does not reconcile — flagged, not silently followed.** The sample response in
techspec § API Endpoints shows `Site O` with `"sharePercent": 41.2`. The derived share is **31.76%**
(`27 ÷ 4 ÷ 21.25`); **41.67%** is `Site B`'s. 2026-06-03 is a prior occurrence for exactly one window
day (`06-03 + 28 = 07-01`) for every location, so the share is always `count ÷ 4 ÷ baseline` — it
cannot be 41.2 for a location with 27 calls that day. The sample is illustrative and every
reconciliation figure bracketing it matches, so the derivation stands; the test asserts 31.76.

**The zero-baseline branch is unreachable behind the low-volume floor.** The required precedence is
`InsufficientHistory → baseline < 10 ⇒ LowVolume → baseline == 0 ⇒ null percentage`. With the floor
at 10 calls, a zero baseline always trips `LowVolume` first, so the third branch can never execute.
Both rules produce an identical row — absolute delta only, percentage and band null — so this is
invisible to a caller. The branch is kept as the division guard, because it is what makes the
percentage provably safe rather than safe by coincidence of a constant's value, and
`Evaluate_ZeroBaseline_ReportsAbsoluteDeltaOnlyWithoutDividing` pins the observable behaviour
including the `LowVolume` verdict a zero lands on.

**`BaselineRequest` added.** Implied by the `Evaluate(BaselineRequest)` signature in techspec § Main
Interfaces but absent from § Data Models. Carries the anchor, window length, the location row list,
and the daily counts over the fetch span.

**`InsufficientHistory` rows report no baseline and no delta** (`BaselineTotal` 0, `AbsoluteDelta` 0,
`RecentTotal` real). FR7 says to omit rather than average a partial baseline, so nothing is claimed
that the data cannot support. The account summary is the sum of the rows' columns, so the summary
line is always the visible column added up (FR16) — an assumption, since the PRD does not say how an
unjudgeable location should affect the account total. No seed location reaches this verdict.

**Ties break on location name** (ordinal), so the ranking is a total order and output never depends
on input order. `Evaluate_RowsInADifferentOrder_ProducesAnIdenticalReport` reverses the location list
and re-sorts all 300 fixture rows, then asserts the reports are element-wise equal.

**Duplicate `(location, date)` rows are summed, not overwritten.** Last-write-wins would also be
order-independent but would drop data; summing is what makes "counted as-is" (Open Question #2) and
order-independence (FR10) true at the same time.

## Relevant Files

- `backend/src/Relay.Application/Baseline/BaselineCalculator.cs` — the algorithm
- `backend/src/Relay.Application/Baseline/IBaselineCalculator.cs`
- `backend/src/Relay.Application/Baseline/BaselineThresholds.cs` — the four constants and their derivations
- `backend/src/Relay.Application/Baseline/Verdict.cs`
- `backend/src/Relay.Application/Baseline/AccountContext.cs`, `LocationHistory.cs`, `DailyLocationCount.cs`
- `backend/src/Relay.Application/Baseline/BaselineRequest.cs`, `BaselineReport.cs`, `LocationBaseline.cs`, `OutlierDay.cs`
- `backend/tests/Relay.UnitTests/BaselineCalculatorTests.cs` — 47 tests
- `backend/tests/Relay.UnitTests/Fixtures/Account6Fixture.cs`
- `backend/tests/Relay.UnitTests/Fixtures/account-6-daily-call-counts.csv` — 300 seed-derived rows
- `backend/tests/Relay.UnitTests/Fixtures/account-6-location-history.csv`
- `backend/tests/Relay.UnitTests/Relay.UnitTests.csproj` — fixtures registered as embedded resources
- `backend/tests/Relay.UnitTests/ScaffoldingTests.cs` — deleted; this suite replaces it, as its own comment said it should
- `ai-log/seed-analysis/` — the re-runnable derivation behind the fixture
