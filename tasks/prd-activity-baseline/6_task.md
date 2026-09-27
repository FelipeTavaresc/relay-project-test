---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>frontend/activity-baseline</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>high</complexity>
<dependencies>http_server</dependencies>
</task_context>

# Task 6.0: Angular feature — service, container, presentational components

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

The single lazy-loaded feature: one container that owns state and fetches, three presentational
children, and one service that is the only file in the app that knows the API's URL and DTO shapes
(techspec § Frontend; § Development Sequencing, step 6).

This task can start before Task 5.0 lands — the contract in techspec § API Endpoints is complete
enough to build against. Task 7.0 then adds URL persistence and the loading/empty/error states.

<requirements>
- File layout exactly as techspec § Frontend: `activity-baseline-page.component.ts` (container), `baseline-controls`, `baseline-summary`, `baseline-table`, `activity-baseline.service.ts`, `activity-baseline.models.ts`.
- **State management is signals in the container**, RxJS only at the HTTP edge — `toSignal` over a `switchMap` from a params signal. The Resource API (`rxResource` / `httpResource`) is deliberately **not** used: still experimental as of Angular 20.3.
- View state is the discriminated union in techspec § Frontend: `{status:'loading'} | {status:'error',message} | {status:'ready',data}`. A stale table must not be able to render under an error.
- `ChangeDetectionStrategy.OnPush` on **every** component.
- `@if` / `@for` (always with `track`) / `@switch` — no legacy `*ngIf`/`*ngFor`.
- No `HttpClient` in any component; no URL string outside the service; no client-side aggregation — the frontend formats and sorts what it was given (`angular.md` § Data fetching, PRD FR12).
- Response types are explicit interfaces mirroring the backend DTOs. `any` is banned; no non-null assertions to silence the compiler.
- Presentational children take `input()`s and emit `output()`s, own no state they didn't receive, and must be renderable from a single object literal in a test.
- Semantic HTML: real `<table>` with `<th scope>`, every control tied to a `<label>`, `<button>` for actions, keyboard reachable with visible focus.
- Verdict is rendered as **text**, never colour or icon alone (PRD FR15).
- Route is lazy-loaded with `loadComponent`; the initial bundle stays the shell.
</requirements>

## Subtasks

- [x] 6.1 Add `core/` — the `ApiConfig` provider (base URL from the environment) and the HTTP error interceptor.
- [x] 6.2 Write `activity-baseline.models.ts` — typed interfaces for the response, summary, location row, outlier, the verdict union, and the `ViewState` discriminated union.
- [x] 6.3 Write `activity-baseline.service.ts` — the single owner of the endpoint URL, returning a typed observable.
- [x] 6.4 Write the container: params signal (window + locations) → `switchMap` → `toSignal`, exposing `ViewState`.
- [x] 6.5 Write `baseline-summary.component.ts` — the account-level recent total and baseline line (PRD FR16).
- [x] 6.6 Write `baseline-table.component.ts` — one row per location with name, recent total, baseline, delta, percentage delta, verdict as text; `@for` with `track`; `@empty` for the empty case (PRD FR13).
- [x] 6.7 Render the outlier note: where a row carries `baselineOutlier`, show a line of text naming the date, so the account 6 "everything is below typical" reading is explainable (techspec § Key Decisions #1).
- [x] 6.8 Write `baseline-controls.component.ts` — window radio group (30/60/90) and location checkboxes, as a **typed reactive form**, emitting changes upward. It stores nothing itself.
- [x] 6.9 Wire the lazy route with `loadComponent` and make the feature the app's landing view.
- [x] 6.10 Add pure pipes for percentage/number formatting — no method calls doing work in a binding, no arithmetic in a template.
- [x] 6.11 Run `npm run build` and `npm test`; record real output.

## Implementation Details

Component list, state approach, and the explicit rejection of the Resource API: techspec § Frontend.
Angular conventions: `angular.md` § Design principles, § State management, § Data fetching,
§ Templates and forms. Contract to code against: techspec § API Endpoints.

The ranking arrives pre-sorted from the API — do **not** re-sort in the table (PRD Goal 2, FR14).
Likewise totals and deltas are given, never recomputed client-side.

`aria-live` on the results region, URL persistence, and the three view states are Task 7.0's scope;
build the `ViewState` union here so 7.0 has somewhere to put them.

State-management choice must be stated in the README — Task 8.0 writes it, this task decides it
(signals in the container, RxJS at the HTTP edge only).

## Success Criteria

- `npm run build` clean — no new warnings, no `any` introduced.
- `npm test` run and real output reported, failures included.
- The feature renders account 6's 15 locations against the running API (or a stubbed response if Task 5.0 is not yet merged), largest deviation first, verdicts legible as text.
- No `HttpClient` import outside `activity-baseline.service.ts`; no endpoint URL string anywhere else.
- Every component declares `ChangeDetectionStrategy.OnPush`; no `*ngIf`/`*ngFor` in any new template.
- Each presentational component can be rendered in a test from a single object literal input.
- Table is keyboard navigable with visible focus and readable at a narrow viewport.

## Relevant Files

Added — core:
- `frontend/src/environments/environment.ts` — API base URL and the single configured account id
- `frontend/src/app/core/api-config.ts` — `ApiConfig` interface, `API_CONFIG` token, `provideApiConfig()`
- `frontend/src/app/core/api-error.ts` — `ApiError`, the only failure type anything downstream sees
- `frontend/src/app/core/http-error.interceptor.ts` — unwraps `HttpErrorResponse` / `ProblemDetails`

Added — feature:
- `frontend/src/app/features/activity-baseline/activity-baseline.models.ts`
- `frontend/src/app/features/activity-baseline/activity-baseline.service.ts`
- `frontend/src/app/features/activity-baseline/activity-baseline-page.component.ts` (+ `.spec.ts`)
- `frontend/src/app/features/activity-baseline/baseline-controls.component.ts` (+ `.spec.ts`)
- `frontend/src/app/features/activity-baseline/baseline-summary.component.ts` (+ `.spec.ts`)
- `frontend/src/app/features/activity-baseline/baseline-table.component.ts` (+ `.spec.ts`)
- `frontend/src/app/features/activity-baseline/pipes/verdict-label.pipe.ts`,
  `percent-delta.pipe.ts`, `signed-number.pipe.ts`, `local-date.pipe.ts`
- `frontend/src/app/features/activity-baseline/testing/account-6.response.ts` — the live payload, captured verbatim

Modified:
- `frontend/src/app/app.routes.ts` — lazy `loadComponent` landing route
- `frontend/src/app/app.config.ts` — `provideHttpClient(withInterceptors(...))`, `provideApiConfig()`
- `frontend/src/app/app.ts`, `app.html`, `app.css`, `app.spec.ts` — Angular placeholder replaced by the shell
- `frontend/src/styles.css` — colour tokens and visible focus only
- `frontend/tsconfig.app.json` — excludes `src/**/testing/**` so fixtures cannot reach the bundle

## Notes for Task 7.0

- `ViewState` is in place and all three arms render, but `aria-live` on the results region and the
  URL↔state round trip are still 7.0's. The container's `params` signal is the single write point.
- The location checkbox list is accumulated across responses (`accumulate` in the container),
  because a filtered response carries only the locations asked for. 7.0's "intersect the URL's
  locations with what the API returned" should intersect against `knownLocations()`, not against
  the current response.
- No browser walkthrough was done in this task: the render is covered by a component test over the
  verbatim live payload instead. The manual loading/empty/error pass belongs with 7.0, where the
  reload-with-filters check becomes possible.
