# Relay — Activity Baseline (DASH-247)

> This README is grown task by task. Task 2.0 added the database section below; Task 8.0 adds the
> full run/test instructions for the API and the SPA, and the consolidated decisions list.

## Database

PostgreSQL 16 in Docker. **Postgres is load-bearing, not a preference:** `AT TIME ZONE` accepts the
seed's IANA zone names (`America/Chicago`, `America/Phoenix`, `UTC`) directly, which is what makes the
timezone-correct day bucketing in the aggregation query possible. SQL Server's `AT TIME ZONE` takes
Windows zone names even on Linux and would need an IANA→Windows mapping table joined into every
query. See techspec § Key Decisions #2.

### Schema/seed ordering — decided in Task 2.0

**Migrations own the schema.** The techspec described the seed as "mounted into the db container's
init directory", but taken literally that runs `seed.sql` through `initdb` *before* any table exists —
EF Core migrations create the schema and have not run at that point, so every INSERT would fail.

So `seed.sql` is mounted read-only at `/seed/seed.sql` and is deliberately **not** in
`/docker-entrypoint-initdb.d`. The order is: empty container → migrations → seed. One source of
schema truth (the EF migration), and one copy of the seed (the file under `tasks/prd-activity-baseline/`,
mounted rather than duplicated).

### First run, from scratch

```bash
docker compose up -d db                                    # repository root; wait for healthy
cd backend
dotnet ef database update -p src/Relay.Infrastructure -s src/Relay.Infrastructure
cd ..
docker compose exec -T db psql -v ON_ERROR_STOP=1 -U relay -d relay -f /seed/seed.sql
```

`Relay.Infrastructure` is both the migrations project and the startup project for `dotnet ef`: it
carries `DesignTimeRelayDbContextFactory`, so the tooling needs no environment setup, and
`Microsoft.EntityFrameworkCore.Design` stays out of `Relay.Api`.

The seed is **not idempotent** — it inserts explicit primary keys. To reload, reset first:
`docker compose down -v && docker compose up -d db`, then repeat the steps above.

> Running the `psql` line from **Git Bash on Windows** requires `MSYS_NO_PATHCONV=1` in front of it,
> or MSYS rewrites `/seed/seed.sql` into a Windows path. PowerShell and Linux shells need nothing.

### Connection string

Bound to `DatabaseOptions` through `IOptions<T>`, never hardcoded. `appsettings.json` ships it empty
so no credentials live in the base settings file; `appsettings.Development.json` supplies the local
compose credentials, and Docker Compose will override with `Database__ConnectionString`. Startup
fails fast (`ValidateOnStart`) when neither provides one. `dotnet ef` reads `RELAY_DB_CONNECTION`,
falling back to the same local default.

### Column types

`occurred_at` and `created_at` are pinned to `timestamp without time zone` holding UTC, as the seed
states. This is not cosmetic: with `timestamptz` the first half of
`occurred_at AT TIME ZONE 'UTC' AT TIME ZONE accounts.timezone` becomes a no-op and the day bucketing
silently changes meaning. Because Npgsql maps `DateTimeOffset` to `timestamptz` unconditionally, the
CLR property is `DateTime`, deviating from the techspec's § Data Models note in favour of its
§ Known Risks entry — the column type is the one that changes query results.
