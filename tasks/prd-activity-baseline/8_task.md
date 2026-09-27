---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>infra/delivery</domain>
<type>documentation</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>database|http_server</dependencies>
</task_context>

# Task 8.0: Docker Compose, README, reconciliation and documented deviations

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

Assemble the three services into one `docker compose up`, write the README that makes the build
runnable and the decisions legible, and close the loop on reconciliation end to end (techspec
§ Development Sequencing, step 7).

The README is a deliverable, not a courtesy: `project-standards.md` requires the state-management
approach named, the test command documented as a one-liner, and every assumption made in place of a
missing requirement written down. The omitted integration tests must be recorded here as an explicit
deviation, not left implicit.

<requirements>
- `docker-compose.yml` runs three services: `db` (Postgres, from Task 2.0), `api` (build context `./backend`), `web` (build context `./frontend`).
- A Dockerfile per root: `backend/Dockerfile`, `frontend/Dockerfile`.
- The seed is loaded from `tasks/prd-activity-baseline/seed.sql` — still one copy, no duplicate.
- README documents **one build command and one test command per root**, and they must actually work from a clean checkout.
- README names the frontend state-management approach: signals in services/container, RxJS only at the HTTP edge.
- README records the documented defaults for URL-derived values (window falls back to 30; unknown locations dropped).
- README records the server-side constants and their derivation: 4 prior weekdays, the 10-call low-volume floor, the `max(10%, 100/√baseline)` band, the **25% outlier share threshold**.
- README records the **deviation: no integration tests**, with the compliant alternative named (a Testcontainers Postgres fixture applying migrations, loading `seed.sql`, asserting endpoint totals) — techspec § Standards Compliance.
- README records the reconciliation figures and how to reproduce them.
- No secrets in `appsettings.json` or compose; connection details come from environment variables.
</requirements>

## Subtasks

- [x] 8.1 Write `backend/Dockerfile` (multi-stage build, non-root where practical).
- [x] 8.2 Write `frontend/Dockerfile` (build, then serve the static bundle).
- [x] 8.3 Extend `docker-compose.yml` with `api` and `web`, service dependencies, healthchecks, and the API base URL passed to the web build.
- [x] 8.4 Verify a cold start: `docker compose down -v` then `docker compose up` reaches a working dashboard with seeded data.
- [x] 8.5 Write the one-off reconciliation check — a documented script or command comparing live endpoint output against the seed-derived figures (account 6, 30 days: `175` / `311.75`; `Site O` `9` / `21.25` / `−57.65%`).
- [x] 8.6 Write the README: what it is, how to run it, the two build commands, the two test commands, the state-management choice.
- [x] 8.7 Write the decisions section: Postgres-over-SQL-Server on the IANA timezone fact, the data-anchored window, baseline math in C# with day bucketing in SQL, the outlier flag instead of a robust estimator.
- [x] 8.8 Write the assumptions and open-questions section: duplicates counted as-is, DST short day accepted, the 10-call floor, and the two states unreachable from this seed data.
- [x] 8.9 Write the deviations section: integration tests omitted, plus any deviation recorded in Tasks 2.0–7.0 (schema/seed ordering, the `DateTimeOffset` vs `timestamp` resolution).
- [x] 8.10 Run every documented command from a clean checkout exactly as written and record the real output.

## Implementation Details

Compose layout and build contexts: techspec § Component Overview. Decisions to write up: techspec
§ Key Decisions #1–#4 (with the estimator comparison table from #1 — the 30d/60d/90d flagged
percentages are the evidence that mean-of-4 stands). Deviation wording: techspec § Standards
Compliance. Reconciliation figures: techspec § Testing Approach.

The README's decisions section should be short and specific. The three that carry weight: **why
Postgres** (SQL Server's `AT TIME ZONE` takes Windows zone names, the seed stores IANA), **why the
anchor is the latest event date** (seed ends 2026-07-27, today is later, a wall-clock anchor returns
zero everywhere), and **why a flag rather than a robust estimator** for `2026-06-03` (both prototyped
alternatives were worse at this per-location volume).

Techspec § Key Decisions #4 flags one point for correction if the original intent was the stricter
reading — "all baseline math in C#" taken literally would have discarded the SQL day bucketing.
Repeat that flag in the README rather than leaving it only in the techspec.

## Success Criteria

- `docker compose down -v && docker compose up` from a clean checkout produces a working dashboard with seeded data, with no manual steps beyond what the README lists.
- Every command in the README was run as written, and its real output is recorded in this file.
- The live endpoint's account 6 / 30-day figures match `175` / `311.75` and `Site O` `9` / `21.25` / `−57.65%`.
- README names the state-management approach, both test commands, the constants with their derivations, the assumptions, and the integration-test deviation with its compliant alternative.
- No secrets committed; no second copy of `seed.sql`.
- `dotnet build`, `dotnet test`, `npm run build`, `npm test` all reported with real output, failures included.

## Implementation record

### What was built

**Five compose services, not three.** The task asked for `db` / `api` / `web`. Two one-shot services
were added because the success criterion — "a working dashboard with seeded data, with no manual steps"
— cannot be met with three: Task 2.0 decided that migrations own the schema and the seed loads
afterwards, and both of those steps were host commands requiring the .NET SDK and `psql`.

- `migrate` — the SDK-based `migrator` target of `backend/Dockerfile`, running
  `dotnet ef database update -p src/Relay.Infrastructure -s src/Relay.Infrastructure`. Exits 0.
- `seed` — the `postgres:16` image running `scripts/load-seed.sh` against the same mounted
  `seed.sql`, gated on `depends_on: migrate: service_completed_successfully`.

`api` waits on `seed: service_completed_successfully`, so the dashboard can never come up against a
half-loaded database. The seed script checks `count(*) FROM activity_events` first, which makes a
repeat `docker compose up` a no-op instead of a duplicate-key failure — the seed inserts explicit
primary keys and is not idempotent.

The API base URL reaches the SPA as a **build arg** (`API_BASE_URL`, default `http://localhost:5141`),
`sed`-substituted into `src/environments/environment.ts` before `npm run build`, because Angular
compiles that file into the bundle. Two `grep -q` assertions follow the substitution so a reshaped
`environment.ts` fails the image build instead of silently shipping the default. `ACCOUNT_ID` is
passed the same way. Verified in the served bundle, not just at build time (below).

Ports were chosen so both run paths use the same addresses: `api` on `5141:8080` (the `dotnet run`
profile's port, and the frontend's default base URL) and `web` on `4200:80` (the `ng serve` port, which
is already the CORS allowlist entry in `appsettings.json`).

### 8.4 / 8.10 — cold start, real output

`docker compose down -v --remove-orphans && docker compose up -d --build`, from an empty volume:

```
 Container relay-db  Healthy
 Container relay-migrate  Started
 Container relay-migrate  Exited
 Container relay-seed  Started
 Container relay-seed  Exited
 Container relay-api  Started
 Container relay-web  Started
```

```
SERVICE   STATUS                      PORTS
api       Up 13 seconds               0.0.0.0:5141->8080/tcp
db        Up 42 seconds (healthy)     0.0.0.0:5432->5432/tcp
migrate   Exited (0) 29 seconds ago
seed      Exited (0) 14 seconds ago
web       Up 13 seconds               0.0.0.0:4200->80/tcp
```

`docker compose logs migrate` / `logs seed`:

```
Build started...
Build succeeded.
Applying migration '20260926215056_InitialSchemaWithBaselineAggregationIndex'.
Done.

seed: activity_events is empty — loading /seed/seed.sql
seed: 12626 events, 7780 call_received, latest 2026-07-27 22:20:34
```

12,626 / 7,780 / `2026-07-27 22:20:34` are the same figures Task 2.0 recorded against a hand-loaded
database, so the containerised load is not a different load.

A second `docker compose up -d` re-ran `seed`, which skipped as designed:

```
seed: activity_events already holds 12626 rows — nothing to load.
```

### Serving and wiring, verified through the containers

```
--- web root:                200 text/html
--- SPA fallback for a deep path (/some/deep/link):  200
--- api base url inside the served bundle:  http://localhost:5141
--- CORS actual request from the web origin:
HTTP/1.1 200 OK
Access-Control-Allow-Origin: http://localhost:4200
--- 400 path (windowDays=13):
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Invalid window","status":400,
 "detail":"windowDays must be one of 30, 60, 90; 13 is not allowed.",
 "instance":"/api/v1/accounts/6/activity-baseline"}
--- 404 path (account 999):  404
--- empty account 20:
{"accountId":20,"accountName":"Quiet Harbor Spa","timezone":"America/Los_Angeles","windowDays":30,
 "windowStart":"2026-08-28","windowEnd":"2026-09-26",
 "summary":{"recentTotal":0,"baselineTotal":0,"absoluteDelta":0,"percentDelta":null},"locations":[]}
```

The deep-path `200` is the `try_files $uri $uri/ /index.html` line in `frontend/nginx.conf` — without
it a reloaded deep link would 404 in nginx before the app booted, which is exactly the reload FR21
requires to work.

### Dashboard render against the containerised stack

Driven with Playwright from the session scratchpad (the browsers were already cached from Task 7; the
dependency is still not in `frontend/package.json`):

```
title      : Activity baseline — Relay
rows       : 15
first row  : Site O | One day (3 Jun 2026) supplies 31.76% of this typical figure. |
             9  21.25  -12.25  -57.65%  Below typical | Normal variation here is up to ±21.69%.
deep link  : rows=1 90-days-checked=true url=http://localhost:4200/?window=90&locations=Site%20O
window=13  : 30-days-checked=true rows=15
no console errors, no failed requests
```

A full-page screenshot confirms the summary line reads `175` / `311.75` / `-136.75 (-43.87%)` over
"Metro Collision Centers — Last 30 days — 28 Jun 2026 to 27 Jul 2026, America/New_York", with all 15
rows ranked `Site O` (−57.65%) to `Site C` (−25.93%).

### 8.5 — the reconciliation check

`scripts/reconcile.mjs`, run as `node scripts/reconcile.mjs` (no dependencies, global `fetch`). Against
the compose stack:

```
GET http://localhost:5141/api/v1/accounts/6/activity-baseline?windowDays=30

ok   windowStart                           2026-06-28
ok   windowEnd                             2026-07-27
ok   locations returned                    15
ok   summary.recentTotal                   175
ok   summary.baselineTotal                 311.75
ok   locations[0].location (ranked first)  Site O
ok   locations[0].recentTotal              9
ok   locations[0].baselineTotal            21.25
ok   locations[0].percentDelta             -57.65
ok   locations[0].verdict                  below
ok   locations[0].baselineOutlier.date     2026-06-03
ok   rows flagged on 2026-06-03            15

All 12 figures reconcile against the seed-derived values.
```

It exits non-zero on a mismatch and names the figure that moved. Two checks go beyond the figures the
task listed, because both encode contract decisions rather than arithmetic: `Site O` is asserted at
**index 0** (FR14 ranks the largest deviation first), and the `2026-06-03` flag is asserted on **all
15** rows (if it fired on a subset, the 25% threshold or the attribution would have drifted).

Re-run against the API started the development way (`dotnet run --project src/Relay.Api`, compose
`api`/`web` stopped to free the port) — also 12/12, `Hosting environment: Development`, so both
documented run paths produce the same numbers.

### 8.10 — the four documented commands

```
$ cd backend && dotnet build
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)

$ cd backend && dotnet test
Aprovado!  - Com falha: 0, Aprovado: 72, Ignorado: 0, Total: 72, Duração: 70 ms - Relay.UnitTests.dll (net8.0)

$ cd frontend && npm run build
Application bundle generation complete. [2.115 seconds]
Output location: C:\Dev\take-home\qualitara\frontend\dist\frontend

$ cd frontend && npm test
Test Suites: 7 passed, 7 total
Tests:       55 passed, 55 total
```

Both frontend commands were re-run **after** the lockfile fix below, not before.

## The defect this task found: `npm ci` did not work at all

`docker compose up` failed on `[web build] RUN npm ci`:

```
npm error code EUSAGE
npm error `npm ci` can only install packages when your package.json and package-lock.json ... are in sync.
npm error Invalid: lock file's @emnapi/wasi-threads@1.2.1 does not satisfy @emnapi/wasi-threads@1.2.3
npm error Missing: @emnapi/core@1.10.0 from lock file
npm error Missing: @emnapi/runtime@1.10.0 from lock file
npm error Missing: @emnapi/wasi-threads@1.2.1 from lock file
```

The first read was "the container's npm is older than the host's" — `node:22-alpine` ships npm 10, the
host has npm 11 — so the build image was moved to `node:24-alpine` (npm 11.19). **That was not the
cause.** It failed the same way, and `npm ci --dry-run` on the host failed identically: the committed
`package-lock.json` had been out of sync with `package.json` since Task 1.0, and nothing had run
`npm ci` to notice. `npm install` had always been used, which repairs the tree in place and never
complains.

So the README's own documented command did not work from a clean checkout — the thing subtask 8.10
exists to catch.

Fixed in two steps, because one was not enough:

1. `npm install --package-lock-only` on the host added the missing `@emnapi/*` entries — 37 added
   lines, one version bump, **no Angular version changes**. The container build still failed, now
   missing `@emnapi/core@1.11.3` and `@emnapi/runtime@1.11.3`: those hang off a *Linux-only* optional
   dependency, which npm on Windows never resolves and so cannot write.
2. Regenerated the lockfile inside `node:24-alpine` (`npm install --package-lock-only --ignore-scripts`
   over a copy of `package.json` + `package-lock.json`), which produced a lock carrying both platforms'
   trees — 72 `win32` references before and after, so nothing Windows-side was lost.

Verified on both sides afterwards, not assumed: host `npm ci` → `added 777 packages`, `npm run build`
clean, `npm test` 55/55; container `npm ci` → clean, image built. The `web` image is kept on
`node:24-alpine` anyway, since the lockfile is npm 11's and npm 10 rejects it — recorded in the README
as a version requirement rather than left as a trap.

## Decisions taken during implementation

1. **Two extra compose services rather than migrations on API startup.** `context.Database.Migrate()`
   in `Program.cs` would have been fewer moving parts, but it puts schema authority in the request
   path, contradicts Task 2.0's recorded decision, and still leaves the seed unsolved. One-shot
   containers encode the *documented order* — empty container → migrations → seed — and each step's
   output stays legible in `docker compose logs <service>`.
2. **The migrator is a separate, SDK-based image target.** `dotnet ef` needs the SDK and the project
   sources, and the API image must not carry either. It is the one image here that is not slim, and it
   is local-only.
3. **`seed` runs the `postgres:16` image with the script mounted**, rather than an image of its own:
   `psql` is the client the seed file was written for, and a Dockerfile whose only job is to install
   `psql` would be a third build context to maintain.
4. **API base URL is a build arg, not runtime configuration.** A runtime `config.json` fetched on boot
   would avoid rebuilding for a new origin, but it adds a request before the app can start and a second
   configuration path to keep honest. The bundle is rebuilt per deployment anyway.
5. **nginx keeps the base image's user model** (root master, `nginx` workers). Running the master
   unprivileged would mean relocating the port, the pidfile and the cache dirs for a container that
   serves four static files. Noted in the Dockerfile rather than done. The `api` container *is*
   non-root (`USER $APP_UID`, 1654).
6. **The reconciliation check is a Node script, not a shell pipeline.** `jq` is not present on this
   machine and Python is not installed; Node is already a hard dependency of the frontend root, and a
   script that can name which figure moved is worth more than a `curl | grep` that exits 1.
7. **`CLAUDE.md`'s "repository contains specs only — no application code" paragraph was corrected.**
   It was written before Task 1.0 and had been false for seven tasks; leaving it would mislead the next
   session about what exists and what has been run.

## Deliberately not done

- **No integration test was added.** It remains the accepted deviation (techspec § Standards
  Compliance), now recorded in the README with the Testcontainers alternative named. `reconcile.mjs`
  mitigates it; it does not replace it — it needs a running stack and it is not in `dotnet test`.
- **No `frontend/README.md` cleanup.** It is still the Angular CLI default. Out of this task's scope,
  and the root README is the documented entry point.
- **Playwright still not a repo dependency** (Task 7.0's decision, unchanged).
- **The `<caption>` clipping at a 380px viewport** noted in Task 7.0's log was left as-is — visual
  polish beyond legibility is out of scope.

## Relevant Files

Created:
- `docker-compose.yml` — rewritten: `db` · `migrate` · `seed` · `api` · `web`
- `backend/Dockerfile` — `restore` → `publish` → `migrator` → `api` (default target)
- `backend/.dockerignore`
- `frontend/Dockerfile` — `build` (Node 24, API base URL baked in) → `web` (nginx)
- `frontend/.dockerignore`
- `frontend/nginx.conf` — static serving plus the SPA fallback
- `scripts/load-seed.sh` — the guarded, one-shot seed load
- `scripts/reconcile.mjs` — live-endpoint reconciliation against the seed-derived figures

Modified:
- `README.md` — run and test commands, state management, constants, decisions, assumptions, deviations
- `frontend/package-lock.json` — repaired so `npm ci` works on Windows and in the Linux image
- `CLAUDE.md` — the stale "specs only, no application code" paragraph
- `tasks/prd-activity-baseline/tasks.md`, `8_task.md`

Unchanged, deliberately:
- `tasks/prd-activity-baseline/seed.sql` — mounted read-only into `db` and `seed`, never copied
