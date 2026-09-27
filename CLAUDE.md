# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

A take-home exercise for ticket **DASH-247** ("Help customers understand whether their recent activity
is normal") for *Relay*, a fictional B2B SaaS tracking inbound customer activity per location. The
brief, the scoping decisions, and the AI interaction log are themselves graded deliverables — see
`prompt-base.md` for what is being evaluated.

**Tasks 1.0–8.0 are complete.** `backend/` and `frontend/` both exist and build; `docker compose up`
brings up the whole stack (`db` → `migrate` → `seed` → `api` → `web`) and serves a seeded dashboard on
`http://localhost:4200`. See `README.md` for the run and test commands, the decisions, the assumptions
and the documented deviations. Everything below still describes the design — but it is now code you
can read, so read it rather than trusting this summary, and do not describe any change as built or
tested until you have run it.

## Spec-driven workflow (do not skip forward)

PRD → Tech Spec → tasks → implementation, driven by four skills in `.claude/commands/`:
`/create-prd`, `/create-techspec`, `/create-tasks`, `/execute-task`. Artifacts live in
`tasks/prd-<feature>/`; the only feature is `tasks/prd-activity-baseline/`.

```
tasks/prd-activity-baseline/
  prd.md          requirements, FR1–FR23, open questions      ← read before any task
  techspec.md     architecture, algorithm, decisions          ← read before any task
  tasks.md        checkbox summary + dependency phases
  1_task.md … 8_task.md   one file per task, frontmatter `status:`
  schema.sql / seed.sql   the provided dataset — source of truth, single copy
```

`/execute-task` picks the first task whose `status: pending` and whose dependencies are `completed`,
flips it to `in-progress`, implements, runs build+tests, ticks the boxes in both the task file and
`tasks.md`, sets `status: completed`, and commits (Conventional Commits, single lowercase line, **no
`Co-Authored-By` trailer** — this repo's commands override the default attribution).

Task order and parallelism (from `tasks.md`): 1.0 → (2.0 ∥ 3.0 ∥ 6.0) → 4.0 → 5.0 → 7.0 → 8.0.

## Commands

Two independent build roots. Each owns its toolchain and is run from its own directory.

```bash
# backend/
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~BaselineCalculatorTests.Ranks"   # single test

# frontend/  (Jest, not Karma — Task 1.0 replaces the Angular default)
npm run build
npm test
npm test -- -t "restores window from the URL"                             # single test

# repository root
docker compose up          # db → migrate → seed → api (./backend) → web (./frontend)
node scripts/reconcile.mjs  # live endpoint vs the seed-derived figures (needs the stack up)
```

A task is not complete until build and test have actually been run and their **real output reported,
failures included**. Never claim verification that did not happen.

## Architecture

```
backend/                     .NET 8 — Relay.sln, Directory.Build.props (warnings-as-errors)
  src/Relay.Api/             controller · DTOs · validation · ProblemDetails · DI
  src/Relay.Application/     BaselineCalculator (pure math) · records · IActivityQueries
  src/Relay.Infrastructure/  RelayDbContext · migrations · ActivityQueries (FromSql)
  tests/Relay.UnitTests/
frontend/                    Angular 17+ standalone workspace
  src/app/core/              ApiConfig, HTTP error interceptor
  src/app/features/activity-baseline/   the only feature
```

Dependency direction is one-way: `Api → Application`, with `Infrastructure` implementing the
`IActivityQueries` interface that `Application` declares. **`Relay.Application` references no EF Core
type and no other project.** Nothing crosses the backend/frontend boundary except the HTTP contract in
techspec § API Endpoints — no generated client, no TypeScript emitted from the C# DTOs.

### The split that matters: SQL buckets days, C# does the math

- **Postgres** does timezone-correct day bucketing:
  `(occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone)::date`, grouped by location and
  local date. Not expressible in LINQ, so it is a parameterised `FromSql` (never `FromSqlRaw`).
- **`BaselineCalculator`** — a pure, synchronous function over a list of daily counts, no database, no
  clock, no I/O — does zero-fill, the trailing-4 same-weekday average, the band, the verdict, the
  outlier flag, and the ranking. This is where correctness lives and where every edge case is tested.

### Algorithm invariants that are easy to get wrong

- **The fetch span is wider than the window.** The baseline for the window's *first* day looks back 28
  days, so the query spans `anchor − (windowDays − 1) − 28 … anchor`. Fetching only the window is the
  most likely bug here and it fails silently — early days just get a baseline of zero.
- **Anchor to the latest local event date *per account*, not wall clock.** The seed ends 2026-07-27;
  a `DateTime.UtcNow` anchor returns zero everywhere. Inject `TimeProvider`, never call `UtcNow`.
- Verdict order: `InsufficientHistory` → `baseline < 10` ⇒ `LowVolume` → `baseline == 0` ⇒ absolute
  delta only, percentage `null` → else band `max(10m, 100m / sqrt(baseline))`, `Above`/`Below` when
  `|pct| > band`, else `Typical`. Ranking is `|PercentDelta|` desc, low-volume/insufficient last.
- The 12 exact-duplicate seed rows are **counted as-is**, by decision (PRD Open Question #2); a unit
  test pins that so it cannot drift.
- `2026-06-03` is a planted anomaly (882 events, 485 of them account 6's `call_received`). It inflates
  the baseline of all 15 of account 6's locations. It is **flagged, not smoothed** — median and trimmed
  mean were prototyped against the seed and both performed worse (see techspec § Key Decisions #1).
  A `baselineOutlier` is emitted when one source day supplies >25% of a location's baseline.

### Why Postgres, not SQL Server

SQL Server's `AT TIME ZONE` takes **Windows** zone names even on Linux; the seed stores IANA strings,
which Postgres accepts directly. This is not academic — 176 `call_received` events fall before 08:00
UTC and so land on the previous local day in US zones.

### Frontend state

Signals in the container, RxJS only at the HTTP edge (`toSignal` over a `switchMap`). The Resource API
(`rxResource`/`httpResource`) is deliberately not used — still experimental. View state is a
discriminated union (`loading | error | ready`) so a stale table can never render under an error. The
**query string is the store of record**: window is validated against `[30,60,90]` (fallback 30),
locations are intersected with what the API returned, writes use `replaceUrl: true`.

## Standards

`.claude/rules/` is authoritative and detailed; read the relevant file before writing code.
`project-standards.md` (the what and the boundary) wins where it overlaps the other two.

- [`project-standards.md`](.claude/rules/project-standards.md) — stack, data discipline, out-of-scope
- [`csharp-dotnet.md`](.claude/rules/csharp-dotnet.md) — SOLID applied, EF Core, async, testing
- [`angular.md`](.claude/rules/angular.md) — signals, smart/dumb split, URL state, testing

Out of scope unless explicitly asked: auth/login (single hardcoded admin), alerting, ML/forecasting,
production infra, visual polish, multi-account switching, CSV export, charts, a baseline config UI.

## Reconciliation fixture

Account 6 at 30 days must produce: account totals `175` recent / `311.75` baseline; `Site O` recent
`9`, baseline `21.25`, `−57.65%`. These are committed as a test fixture — checked by a test, not by
eye. The `gawk` prototypes that derived them are in `ai-log/seed-analysis/` (re-runnable, but they
approximate DST with a hardcoded cutover; where they disagree with the real `AT TIME ZONE` query on a
DST-spanning window, **the prototype is wrong**).

## Known deviation, already decided

**No integration tests.** `project-standards.md` and `csharp-dotnet.md` both require one per endpoint
over a real database. This build ships unit tests only. The `AT TIME ZONE` GROUP BY therefore has no
automated coverage — mitigated by hand-inspecting the generated SQL and by the reconciliation fixture.
The compliant alternative (a Testcontainers Postgres fixture applying migrations and loading
`seed.sql`) is named in techspec § Standards Compliance and is the single highest-value addition if
budget allows. Recorded in README § Deviations, alongside `scripts/reconcile.mjs`, which mitigates
it without replacing it — it needs a running stack and it is not part of `dotnet test`.

## AI log is a deliverable

`AI_LOG.md` at the repository root is the single log — sessions 1–9, planning through Task 8.0, merged
from the two files the sessions were originally written into. It records actual prompts, where the
human accepted/rejected/redirected the agent, and where the agent was caught being wrong. It is graded
on being raw and honest — append as you go, do not retrofit it to match the final code, do not
sanitize it.
