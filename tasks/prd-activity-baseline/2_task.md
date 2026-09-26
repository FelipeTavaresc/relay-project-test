---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>infra/database</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
</task_context>

# Task 2.0: Database, EF Core model, migrations, seed load

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

Stand up PostgreSQL in Docker, model `accounts` and `activity_events` in EF Core from `schema.sql`,
produce a checked-in migration, and get the 12,626 seed rows into the database. Every later task needs
real rows, which is why this comes before the queries (techspec § Development Sequencing, step 2).

PostgreSQL is not a preference here — it is load-bearing. `AT TIME ZONE` accepts the seed's IANA
strings directly, which is the entire reason the aggregation query in Task 4.0 works (techspec
§ Key Decisions #2). Do not substitute another engine.

<requirements>
- Postgres runs in Docker and is reachable from the API with a connection string bound through options, not hardcoded.
- `RelayDbContext` in `Relay.Infrastructure` with `Account` and `ActivityEvent` entities mapped to the tables and columns in `tasks/prd-activity-baseline/schema.sql` (snake_case names preserved).
- Model configuration lives in `IEntityTypeConfiguration<T>` classes — not attributes on entities, not inline in `OnModelCreating` (`csharp-dotnet.md` § EF Core).
- `occurred_at` and `created_at` are pinned to `timestamp without time zone` (UTC, as the seed states). This is the risk called out in techspec § Known Risks: `timestamptz` changes `AT TIME ZONE` semantics and would silently break the day bucketing.
- An index on `(account_id, event_type, occurred_at)`, with the migration named so the reason is visible.
- The seed is loaded from `tasks/prd-activity-baseline/seed.sql` — **one copy, no duplicate file to drift**.
- Migration is checked in and applied to a clean database at least once.
</requirements>

## Subtasks

- [x] 2.1 Add the Postgres service to `docker-compose.yml` (named volume, healthcheck, fixed port). Task 8.0 adds the `api` and `web` services to the same file.
- [x] 2.2 Add Npgsql EF Core packages to `Relay.Infrastructure` and the connection string as an options record bound with `IOptions<T>`.
- [x] 2.3 Write the `Account` and `ActivityEvent` entities and their `IEntityTypeConfiguration<T>` classes, mapping every column in `schema.sql` including the nullable `duration_seconds` and `outcome`.
- [x] 2.4 Create the initial migration including the aggregation index; name it so the index's purpose is legible.
- [x] 2.5 Decide and implement the schema-vs-seed ordering (see Implementation Details), then load `seed.sql`.
- [x] 2.6 Apply the migration to a clean database and load the seed end to end from scratch.
- [x] 2.7 Verify the load with direct SQL: 12,626 total events, 7,780 `call_received`, 69 distinct `(account_id, location)` pairs, latest `occurred_at` on 2026-07-27, account 20 present with zero events. Record the real output.

## Implementation Details

Schema source: `tasks/prd-activity-baseline/schema.sql`. Column types and the timezone rationale:
techspec § Key Decisions #2 and § Known Risks. EF Core rules: `csharp-dotnet.md` § EF Core and data access.

**Ordering decision you must make and record.** The techspec says the seed is "mounted into the db
container's init directory". Taken literally that runs `seed.sql` before any table exists, because
migrations own the schema and have not run yet — the inserts would fail. Resolve it one of two ways
and write the choice into this file and the README:

- *Migrations own the schema* (recommended): the container starts empty, migrations are applied
  (`dotnet ef database update`, or at API startup for local dev), then `seed.sql` is loaded by a
  documented one-line command or a one-shot compose service. One source of schema truth.
- *initdb owns the schema*: mount `schema.sql` then `seed.sql` into the init directory and treat the
  EF migration as a checked-in artifact verified separately against a clean database. Two sources of
  truth — only take this if the first is genuinely blocked.

**CLR type note.** Techspec § Data Models says `occurred_at` "stays `DateTimeOffset` at the EF layer",
while § Known Risks requires the column be `timestamp without time zone`. Npgsql maps
`DateTimeOffset` to `timestamptz`, so the two cannot both hold. The column type wins — it is the one
that changes query results. Map the property as `DateTime` and note the deviation here. The
aggregation is raw SQL (Task 4.0), so the CLR type is not on the hot path either way.

## Success Criteria

- `docker compose up db` starts Postgres and it passes its healthcheck.
- The migration applies to a clean database with no manual SQL.
- Seed loaded, with the row counts in subtask 2.7 verified by real query output pasted into this file — 12,626 events, 7,780 `call_received`, 69 account-location pairs.
- `\d activity_events` (or equivalent) shows `occurred_at` as `timestamp without time zone` and the `(account_id, event_type, occurred_at)` index present.
- No second copy of `seed.sql` exists anywhere in the repository.
- `dotnet build` still clean; the schema/seed ordering decision is written down.

## Outcome

### Decision — migrations own the schema (subtask 2.5)

Took the recommended option. `seed.sql` is mounted read-only at `/seed/seed.sql` and deliberately
**not** placed in `/docker-entrypoint-initdb.d`: initdb runs before EF Core migrations exist, so the
INSERTs would hit tables that have not been created. Order is empty container → `dotnet ef database
update` → seed load. One source of schema truth, one copy of the seed. Recorded in `README.md`
§ Database and in the `docker-compose.yml` header comment.

The seed is **not idempotent** (explicit primary keys). Reloading requires `docker compose down -v`
first; that is documented in the README rather than worked around, since the alternative would mean
editing the provided seed.

### Deviation — `occurred_at` is `DateTime`, not `DateTimeOffset` (accepted, as the task directed)

Confirmed against the Npgsql docs rather than assumed: since Npgsql 6.0, `DateTime` with
`Kind=Utc` maps to `timestamptz` and `Kind=Unspecified` to `timestamp`, while `DateTimeOffset` maps
to `timestamptz` **unconditionally**. So techspec § Data Models ("stays `DateTimeOffset` at the EF
layer") and § Known Risks ("pin `timestamp without time zone`") cannot both hold. The column type
wins; the property is `DateTime` and the column type is pinned explicitly in
`ActivityEventConfiguration` rather than left to Npgsql's Kind-based inference.

### Deviation — `Relay.Infrastructure` is its own startup project for `dotnet ef`

`dotnet ef -s src/Relay.Api` fails: `Microsoft.EntityFrameworkCore.Design` is referenced with
`PrivateAssets=all` in Infrastructure and so does not flow to the API. Rather than add a design-time
package to `Relay.Api`, `DesignTimeRelayDbContextFactory` lives in Infrastructure and the documented
command is `-p src/Relay.Infrastructure -s src/Relay.Infrastructure`. Keeps EF design-time tooling
out of the API project entirely.

### Tooling note

`dotnet ef` on this machine is the 9.0.6 global tool driving a `net8.0` project with EF Core 8.0.11
packages. It generated and applied the migration without complaint.

### 2.7 — verification against a clean database, real output

Sequence run from scratch: `docker compose down -v` → `docker compose up -d db` (healthy) →
`\dt` returns "Did not find any relations" → `dotnet ef database update` →
`Applying migration '20260926215056_InitialSchemaWithBaselineAggregationIndex'. Done.` →
seed load, 18.3 s.

```
 total_events | call_received | account_location_pairs | accounts | latest_occurred_at
--------------+---------------+------------------------+----------+---------------------
        12626 |          7780 |                     69 |       20 | 2026-07-27 22:20:34
(1 row)

 id |       name       |      timezone       | events
----+------------------+---------------------+--------
 20 | Quiet Harbor Spa | America/Los_Angeles |      0
(1 row)
```

All five assertions hold: 12,626 events, 7,780 `call_received`, 69 `(account_id, location)` pairs,
latest `occurred_at` on 2026-07-27, account 20 present with zero events (the FR17 empty-state path).

`\d activity_events` — `occurred_at` is `timestamp without time zone` and the aggregation index is
present:

```
      Column      |            Type             | Collation | Nullable | Default
------------------+-----------------------------+-----------+----------+---------
 id               | integer                     |           | not null |
 account_id       | integer                     |           | not null |
 location         | character varying(80)       |           | not null |
 event_type       | character varying(40)       |           | not null |
 occurred_at      | timestamp without time zone |           | not null |
 duration_seconds | integer                     |           |          |
 outcome          | character varying(40)       |           |          |
Indexes:
    "PK_activity_events" PRIMARY KEY, btree (id)
    "ix_activity_events_account_id_event_type_occurred_at" btree (account_id, event_type, occurred_at)
Foreign-key constraints:
    "FK_activity_events_accounts_account_id" FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE RESTRICT
```

### Extra checks — the container's tzdata, and seed facts the specs depend on

`AT TIME ZONE` was verified to resolve real IANA data before anything was built on it. This is why
the image is `postgres:16` and not `-alpine`:

```
     ny_dst_day      |       phoenix       |      utc_zone       |       chicago
---------------------+---------------------+---------------------+---------------------
 2026-03-08 03:30:00 | 2026-01-31 20:00:00 | 2026-02-01 03:00:00 | 2026-01-31 21:00:00
(1 row)
```

New York on the DST transition day is UTC−4 (post-cutover), Phoenix is UTC−7 with no shift, `UTC` is
a no-op, and the 03:00 UTC instants land on the **previous local day** in US zones — exactly the
behaviour FR11 requires.

Three seed facts the downstream tasks depend on, confirmed present after the load:

```
 duplicate_extra_rows | calls_before_0800_utc | events_on_2026_06_03
----------------------+-----------------------+----------------------
                   12 |                   176 |                  882
(1 row)
```

12 exact-duplicate rows (PRD FR10 / Open Question #2 — counted as-is), 176 `call_received` before
08:00 UTC (techspec § Key Decisions #2 — these shift a local day), and the 882-event `2026-06-03`
anomaly (§ Key Decisions #1). The load is faithful to the provided seed.

### Build and test

```
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)

Aprovado!  – Com falha: 0, Aprovado: 1, Ignorado: 0, Total: 1, Duração: 10 ms - Relay.UnitTests.dll (net8.0)
```

The single test is Task 1.0's scaffolding test. This task adds no unit tests: it is schema and
infrastructure, and its correctness is established by the real SQL output above. The aggregation math
gets its tests in Task 3.0.

## Relevant Files

Created:
- `docker-compose.yml` — `db` service, named volume, healthcheck, seed mounted read-only
- `README.md` — database section, the schema/seed ordering decision, first-run commands
- `backend/src/Relay.Infrastructure/RelayDbContext.cs`
- `backend/src/Relay.Infrastructure/Entities/Account.cs`
- `backend/src/Relay.Infrastructure/Entities/ActivityEvent.cs`
- `backend/src/Relay.Infrastructure/Configurations/AccountConfiguration.cs`
- `backend/src/Relay.Infrastructure/Configurations/ActivityEventConfiguration.cs`
- `backend/src/Relay.Infrastructure/Options/DatabaseOptions.cs`
- `backend/src/Relay.Infrastructure/ServiceCollectionExtensions.cs` — `AddRelayPersistence`
- `backend/src/Relay.Infrastructure/DesignTimeRelayDbContextFactory.cs`
- `backend/src/Relay.Infrastructure/Migrations/20260926215056_InitialSchemaWithBaselineAggregationIndex.cs`
  (+ `.Designer.cs`, `RelayDbContextModelSnapshot.cs`)

Modified:
- `backend/src/Relay.Infrastructure/Relay.Infrastructure.csproj` — Npgsql, EF Design, Options packages
- `backend/src/Relay.Api/Program.cs` — `AddRelayPersistence(builder.Configuration)`
- `backend/src/Relay.Api/appsettings.json` — empty `Database:ConnectionString`
- `backend/src/Relay.Api/appsettings.Development.json` — local compose connection string

Read-only inputs, unchanged:
- `tasks/prd-activity-baseline/schema.sql`, `tasks/prd-activity-baseline/seed.sql`
