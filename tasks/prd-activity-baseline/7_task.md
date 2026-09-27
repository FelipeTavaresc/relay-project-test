---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>frontend/activity-baseline</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server</dependencies>
</task_context>

# Task 7.0: URL-backed state, loading/empty/error states, frontend tests

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

Make the feature reload-safe and shareable, and make its three non-happy states real. The query string
is the store of record: window length and location filter are read from the route on init and written
back on change with `replaceUrl: true`, so a reload restores what was on screen and Back leaves the
dashboard (PRD FR21–FR23, Goal 4; techspec § Frontend).

Loading, empty, and error are part of the feature, not a follow-up (PRD Goal 6, FR17, FR18).

<requirements>
- `window` is validated against the literal set `[30, 60, 90]` and falls back to `30` on anything else. A malformed value never reaches the API (PRD FR22).
- `locations` is a comma-separated list intersected with the names the API returned; **unknown names are dropped, not forwarded** — note this differs deliberately from the backend, which rejects an unknown name with a 400 (Task 5.0).
- Writes to the URL use `replaceUrl: true` so filter changes don't stack history entries (PRD FR23).
- All three `ViewState` branches render: loading, error (naming what failed, with a working retry), and ready. Empty is the `@empty` branch of the results `@for`, not a second `@if` — an explicit empty state, not an error and not a blank table (PRD FR17).
- `aria-live="polite"` on the results region so async updates are announced.
- Fallbacks are **documented** — the chosen default is written down in the README (Task 8.0), per PRD FR22.
- No `fixture.detectChanges()` sprinkled hopefully — one deliberate call per state change.
- Deterministic tests: no real timers, no `new Date()` in a component.
</requirements>

## Subtasks

- [x] 7.1 Read window and locations from the route on init, through a validating parser that returns the documented defaults on bad input.
- [x] 7.2 Write state back to the query string on every change with `replaceUrl: true`.
- [x] 7.3 Intersect the URL's location list against the names present in the loaded response; drop the rest.
- [x] 7.4 Render the loading state, and make it reachable in the browser (throttle the network to see it).
- [x] 7.5 Render the error state with the failure described and a retry button that re-issues the request.
- [x] 7.6 Render the empty state via `@empty` — verify against account 20 (Quiet Harbor Spa, zero events), which exercises this path with real data.
- [x] 7.7 Add `aria-live="polite"` to the results region; check focus order and visible focus across the controls and table.
- [x] 7.8 Service tests with `HttpTestingController`: request shape (URL, query params) and the error path. Never hit a real backend.
- [x] 7.9 URL↔state round-trip tests, **including `window=13`** and an unknown location name.
- [x] 7.10 Component tests: each of loading / empty / error / ready renders, asserted **by role and text**, not by CSS class.
- [x] 7.11 Manual pass: apply filters, reload the page, confirm state is restored; press Back and confirm it leaves the dashboard.
- [x] 7.12 Run `npm run build` and `npm test`; record real output, failures included.

## Implementation Details

URL rules and validation: techspec § Frontend (the `[30,60,90]` literal set, the intersection
behaviour, `replaceUrl`). Persistence discipline and the "validate what comes out of the URL — it is
user input" rule: `angular.md` § URL and persistence. Test expectations: techspec § Testing Approach
(frontend paragraph) and `angular.md` § Tests.

There is a bootstrap ordering wrinkle worth planning for: the location filter can only be intersected
against names the API returned, which arrive *after* the first request. Send the first request with the
window plus the raw location list, then reconcile the filter once the response lands — or request
unfiltered and filter client-side for display only. Either is fine; pick one, and don't let an unknown
name cause a 400 on first load.

Account 20 is the empty-state fixture with real data (PRD FR17). `low_volume` and
`insufficient_history` are **not** reachable through this UI on this seed data (PRD Open Question #1)
— their coverage lives in Task 3.0's unit tests. Do not widen the window options to manufacture a demo
path for them.

## Success Criteria

- `npm run build` clean; `npm test` run with real output reported.
- Reloading the page with filters applied restores the same view from the URL.
- `?window=13` loads at 30 days without an API call carrying `13`; an unknown location in the URL is dropped, not sent.
- Back from a filtered view leaves the dashboard rather than stepping through filter changes.
- Loading, empty (account 20), and error states each exercised in the browser, not only in tests.
- Tests query by role and text; a CSS class change would not break them.
- Results region announces updates; every control is reachable and operable by keyboard.

## Decisions taken during implementation

1. **Bootstrap ordering: the first request is deliberately unfiltered.** Neither option the task
   offered survives contact with Task 5.0. Sending the raw location list first 400s on an unknown
   name — confirmed live: `?locations=Site O,Nowhere` returns
   `400 "Account 6 has no location named Nowhere."` Filtering client-side for display instead would
   desync the summary line, because the endpoint computes `summary` over the *filtered* subset —
   also confirmed live: filtering account 6 to `Site O` returns a summary of `9 / 21.25`, not the
   account's `175 / 311.75`. So the first request carries the window only, the returned names are
   intersected with the URL's list, and the filtered request follows. One extra round trip on a
   filtered deep link, never a 400.

   This request is needed regardless: a filtered response contains only the locations asked for, so
   a filtered deep link would otherwise never learn the account's other location names and could
   never offer them as checkboxes.

2. **An entirely unrecognised location list falls back to "all locations"**, not to an empty table.
   The URL is *not* rewritten to drop the unknown name — FR22 requires a documented fallback, not a
   truthful address bar, and rewriting would mean a navigation during load.

3. **No URL write on init.** The query string is written when the user first touches a control, not
   on load, so a bare `/` stays bare. `replaceUrl` on every write means one history entry either way.

4. **`baseline-url-params.ts` exports both halves of the codec.** `angular.md`'s one-public-symbol
   rule targets components and services; splitting an inverse pair across two files invites drift.

## Verification

`npm run build` clean, `npm test` 55/55 (7 suites). Both re-run after the final change.

The browser pass (7.4, 7.6, 7.7, 7.11) was driven with Playwright rather than clicked by hand —
installed in the session scratchpad, not added to `frontend/package.json`, since the techspec calls
for no e2e suite. Against the real API and the seeded database, 12/12:

| Check | Observed |
|---|---|
| 7.4 loading (response delayed 1s) | `aria-busy=true`, "Loading the report…", no table |
| 7.5 error + retry | alert names the ProblemDetails detail; retry restored 15 rows |
| 7.6 empty (account 20) | table present, 0 rows, no alert, summary names Quiet Harbor Spa |
| 7.7 focus order | 30/60/90 days → Site A → Site B → Site C; focus ring `3px solid rgb(11,87,208)` |
| 7.11 reload | URL identical, same 14 rows, 90 days still checked, unticked Site A still absent |
| 7.11 Back | `history.length=2` after two filter changes; Back left the app |
| FR22 end to end | `?window=13&locations=Nowhere` issued exactly one request: `?windowDays=30` |
| narrow viewport (380px) | table scrolls in its own container; the page does not |

**Two defects the browser pass caught, both in the harness rather than the app** — worth recording
because the first run reported a false failure: `networkidle` is not a settle condition here (the gap
between the discovery and filtered requests looks idle, so the table was sampled while empty), and a
hardcoded detail string hid which sub-condition had failed.

One cosmetic issue seen and deliberately left: at 380px the `<caption>` is clipped by the table's
`min-width`, since a caption is as wide as its table. Content is reachable by scrolling; visual
polish beyond legibility is out of scope.

## Relevant Files
- `frontend/src/app/features/activity-baseline/activity-baseline-page.component.ts` — reads/writes the URL, intersects the filter
- `frontend/src/app/features/activity-baseline/baseline-url-params.ts` — **new**, the validating codec and the documented fallback
- `frontend/src/app/features/activity-baseline/baseline-url-params.spec.ts` — **new**, parser tests including `window=13`
- `frontend/src/app/features/activity-baseline/activity-baseline.service.spec.ts` — **new**, `HttpTestingController` contract and error path
- `frontend/src/app/features/activity-baseline/activity-baseline-page.component.spec.ts` — rewritten on `RouterTestingHarness` + `SpyLocation`
- `frontend/src/app/features/activity-baseline/testing/account-20.response.ts` — **new**, account 20's live response as the empty-state fixture
- `frontend/src/app/features/activity-baseline/baseline-controls.component.ts` — empty-locations copy now covers account 20
