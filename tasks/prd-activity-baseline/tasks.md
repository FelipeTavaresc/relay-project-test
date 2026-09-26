# Implementation Tasks Summary for Activity Baseline ("Is this normal for us?")

## Tasks

- [x] 1.0 Repository skeleton — two build roots (M) — no dependencies
- [x] 2.0 Database, EF Core model, migrations, seed load (M) — depends on 1.0
- [x] 3.0 BaselineCalculator domain service and unit tests (L) — depends on 1.0
- [x] 4.0 ActivityQueries and the AT TIME ZONE aggregation (M) — depends on 2.0, 3.0
- [x] 5.0 Endpoint — controller, DTOs, validation, ProblemDetails, logging (M) — depends on 4.0
- [ ] 6.0 Angular feature — service, container, presentational components (L) — depends on 1.0
- [ ] 7.0 URL-backed state, loading/empty/error states, frontend tests (M) — depends on 6.0
- [ ] 8.0 Docker Compose, README, reconciliation and documented deviations (S) — depends on 5.0, 7.0

## Phases

- **Phase 1 — foundation:** 1.0
- **Phase 2 — parallel:** 2.0 ∥ 3.0 ∥ 6.0 (3.0 needs no database; 6.0 builds against the contract in techspec § API Endpoints before the API exists)
- **Phase 3 — backend wiring:** 4.0 → 5.0
- **Phase 4 — frontend finish:** 7.0
- **Phase 5 — assembly:** 8.0

## Size Notes
- S - Small
- M - Medium
- L - Large
