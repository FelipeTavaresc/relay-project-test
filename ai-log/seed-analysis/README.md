# seed-analysis

Derives the account 6 reconciliation fixture from `tasks/prd-activity-baseline/seed.sql`, so the
figures in techspec § Testing Approach have a reproducible provenance rather than being asserted
from memory.

```bash
dotnet run --project ai-log/seed-analysis -- tasks/prd-activity-baseline/seed.sql
```

It prints the account's timezone and anchor date, the window and fetch span, the per-location daily
counts inside that span, each location's first/last local date, and an independent replication of
the baseline algorithm. The two CSVs under `backend/tests/Relay.UnitTests/Fixtures/` are that
output; `BaselineCalculatorTests` asserts against them.

Timezone conversion uses `TimeZoneInfo` with the IANA id, i.e. real tz rules — **not** the hardcoded
DST cutover an earlier `gawk` prototype approximated. It therefore agrees with the
`AT TIME ZONE` bucketing Task 4.0 performs in Postgres, DST-spanning windows included.

Two cross-checks hold against figures stated independently in the specs:

- 2026-06-03 sums to **485** `call_received` events across account 6's 15 locations
  (techspec § Key Decisions #1).
- Account 6 at 30 days totals **175** recent / **311.75** baseline, with `Site O` at
  **9** / **21.25** / **−57.65%** (techspec § Testing Approach → Reconciliation).

One figure in the specs does **not** reconcile: the sample response in techspec § API Endpoints shows
`Site O` with `"sharePercent": 41.2`. The derived share is **31.76%** (`27 / 4 ÷ 21.25`); 41.67% is
`Site B`'s. The sample is illustrative and the reconciliation figures it is bracketed by all match,
so the derivation stands and the test asserts 31.76.
