# Project Standards

Rules the `/create-techspec`, `/create-tasks` and `/execute-task` commands map against.
This file replaces the `@.claude/rules` reference used by the original Cursor-flavoured prompts.

This file is the **what and the boundary** — stack, data, scope, discipline. The *how* of writing
the code lives alongside it, and both apply:

- [`csharp-dotnet.md`](./csharp-dotnet.md) — C# / .NET conventions, SOLID applied, EF Core, testing
- [`angular.md`](./angular.md) — Angular / TypeScript conventions, signals, state, testing

Where a language file and this one disagree, this one wins.

## Stack (non-negotiable)

- **Backend:** .NET 8+ / C#
- **Frontend:** Angular SPA (TypeScript)
- **Database:** any relational DB with a **real schema and migrations** managed by the stack's
  standard tooling (EF Core migrations, or SQL scripts if raw SQL is the chosen approach)
- Do not swap any of these for an alternative, and do not introduce a second framework for a
  job the chosen one already does.

## Data

- Seed data is the source of truth. Do not invent or synthesize datasets to make a feature look better.
- Aggregation correctness outranks feature count. Verify totals against the seed data before
  declaring a task done.
- Handle the unglamorous cases explicitly: empty date ranges, single-row baselines, divide-by-zero,
  nulls, duplicate or out-of-order events, timezone boundaries.
- Aggregation belongs in the database or a dedicated service layer — not in a controller and not
  in the browser.

## Backend conventions

- Keep controllers thin: validate input, delegate, shape the response.
- Return explicit DTOs; never leak EF entities across the API boundary.
- Validate and clamp user-supplied ranges and identifiers; reject rather than silently coerce.
- Prefer `IReadOnlyList<T>` / records for response models.
- Log at the boundary with enough context to reconstruct a bad request.

## Frontend conventions

- One state-management approach for the whole app — pick it, state it in the README, stick to it.
- Any user-controlled input (filter, range, location selector, toggle) must **survive a page reload**.
- Loading, empty, and error states are part of the feature, not a follow-up.
- Function over form: clear and unstyled beats pretty and broken.

## Tests

- Meaningful tests on the parts that matter — aggregation math, boundary conditions, contract
  shape — not coverage farming.
- Tests must actually run from a documented one-line command.
- A task is not complete until the build and test commands have been run and their real output
  reported, failures included.

## Out of scope (do not build unless explicitly asked)

- Auth / login — assume a single trusted admin identity
- Alerting, notifications, ML, forecasting
- Production infra (Kubernetes, CI pipelines)
- Visual polish beyond legibility

## Working discipline

- Spec-driven: PRD → Tech Spec → tasks → implementation. Do not skip forward.
- The plan precedes the code. Record decisions, assumptions, and open questions as they happen
  rather than reconstructing them afterwards.
- Document any assumption made in place of a missing requirement, in the task file or the README.
- Never claim something is verified that was not run.
