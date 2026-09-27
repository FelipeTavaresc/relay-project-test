#!/usr/bin/env bash
# Loads tasks/prd-activity-baseline/seed.sql into the compose `db` service, once.
#
# Run by the one-shot `seed` service in docker-compose.yml, after `migrate` has created the schema
# (Task 2.0: migrations own the schema, the seed is data only). Runs in the postgres image, so psql
# is the client and no database tooling is needed on the host.
#
# The seed is NOT idempotent — it inserts explicit primary keys — so this script is the guard:
# it loads only when activity_events is empty, which makes `docker compose up` repeatable.
# To force a reload: docker compose down -v && docker compose up
set -euo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGUSER:?PGUSER is required}"
: "${PGDATABASE:?PGDATABASE is required}"

SEED_FILE=${SEED_FILE:-/seed/seed.sql}

existing=$(psql --no-psqlrc --tuples-only --no-align \
    --command "SELECT count(*) FROM activity_events")

if [ "$existing" != "0" ]; then
    echo "seed: activity_events already holds ${existing} rows — nothing to load."
    exit 0
fi

echo "seed: activity_events is empty — loading ${SEED_FILE}"
# ON_ERROR_STOP so a failed INSERT fails the container rather than leaving a half-loaded database
# that the API would then serve wrong numbers from.
psql --no-psqlrc --set ON_ERROR_STOP=1 --quiet --file "$SEED_FILE"

psql --no-psqlrc --tuples-only --no-align --command "
    SELECT 'seed: ' || count(*) || ' events, '
        || count(*) FILTER (WHERE event_type = 'call_received') || ' call_received, latest '
        || max(occurred_at)
    FROM activity_events"
