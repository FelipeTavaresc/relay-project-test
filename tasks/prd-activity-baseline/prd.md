# PRD — Activity Baseline ("Is this normal for us?")

**Ticket:** DASH-247 · **Status:** Decided · **Date:** 2026-09-26

## Overview

Relay's dashboard shows raw totals per location and nothing to interpret them against, so customers ask
their account manager *"is this number normal for us?"* and multi-location customers can't tell which
location needs attention.

This feature adds a **baseline comparison**: each location's recent call volume is compared against its
own history and expressed as a plain verdict — above typical, typical, below typical — ordered so the
location most needing attention comes first. An admin opening the dashboard Monday morning should answer
"is this normal?" and "where do I look first?" without exporting or calling anyone.

"Normal" is the **trailing same-weekday average**: each day in the window is compared against the
average of that location's prior 4 occurrences of the same weekday. The seed data justifies this —
weekday volume runs 78–97 events/day against 23–25 at weekends, so a like-for-like weekday comparison is
the only honest one. It is also explainable to a customer in one sentence, a property forecasting loses.

## Goals

1. An admin can state whether a location is above, at, or below its own typical level **within 10
   seconds of page load**, with no export and no training.
2. For a multi-location account, the largest deviation from baseline is **the first row**, with no
   sorting or filtering by the user.
3. Every figure **reconciles against the seed data**, verified by test — zero-activity days, duplicate
   events, low-volume locations, and DST boundaries included.
4. Window length and location filter **survive a page reload** and are shareable as a URL.
5. A verdict is only issued where the data supports one: **no location is labelled unusual on a swing
   that ordinary week-to-week variation explains.**
6. Loading, empty, and error states are each reachable and legible in the shipped UI.

## User Stories

- **Multi-location admin (15+ sites).** Reviewing Monday morning, I want locations ranked by deviation
  from their own normal, so I act on the one that changed instead of reading 15 rows of totals.
- **Single-site owner.** I want to know whether the last month was typical for me, so I can tell a
  genuine dip from ordinary noise.
- **Account manager.** Preparing for a customer call, I want the verdict the customer sees, so we
  discuss the same numbers.
- **Edge — low-volume location.** For a site averaging a handful of calls a week, I want to be told the
  volume is too low to judge, rather than shown a confident −40% caused by two missing calls.

## Key Features

### A. Baseline comparison (server-side aggregation)

1. The measured metric is **`call_received`** events — the highest-volume type (7,780 of 12,626 rows)
   and present at all 69 account-locations. Other event types are out of scope for this release.
2. Compute, per location, total calls in a user-selected **recent window** of 30, 60, or 90 days
   (default 30), ending at the **latest event date present for that account**. Anchoring to the data
   rather than to wall-clock time is required: the seed data ends 2026-07-27, so a wall-clock anchor
   would return zero for every location.
3. Compute a **baseline** over the same window by averaging, for each day in it, that location's call
   totals on the prior 4 occurrences of the same weekday.
4. Return per location: recent total, baseline total, absolute delta, percentage delta, and a
   **verdict** of `above` / `typical` / `below` / `low_volume` / `insufficient_history`.
5. A location is `above` or `below` only when its percentage delta exceeds a **volume-aware band** of
   `±max(10%, 100/√baseline)`; otherwise `typical`. The band widens where counts are small, because at a
   baseline of 4 calls a single call is 25% — a fixed ±10% band labels 67–84% of locations unusual and
   destroys the signal the feature exists to provide.
6. A location whose baseline is **below 10 calls** returns `low_volume` with no verdict and no
   percentage. Too little history to judge is a real answer; a confident percentage would be a lie.
7. With fewer than the 4 required prior occurrences of a weekday, return `insufficient_history` and omit
   the percentage rather than averaging a partial baseline.
8. A baseline of zero never divides by zero: report the absolute delta and flag the percentage as not
   applicable.
9. Days with no calls count as **zero**, not missing — absence of activity is a signal, not a gap.
10. Duplicate and out-of-order source events do not change totals; ordering is not assumed. The seed
    data contains 12 exact-duplicate `(account, location, event_type, occurred_at)` rows, which must not
    be silently deduplicated or double-counted without a documented decision.
11. Day boundaries follow the **account's IANA timezone** (`accounts.timezone`) — the only timezone the
    schema carries; `activity_events.location` is a label with no zone of its own. The window spans the
    8 March 2026 US DST transition, and `America/Phoenix` (accounts 7, 15) and `UTC` (account 18) do not
    shift, so the conversion must be real, not a fixed offset.
12. Aggregation happens in the database or a dedicated service layer — never in the browser, never in a
    controller.

### B. Ranked location view

13. List one row per location: name, recent total, baseline, delta, percentage delta, verdict.
14. Default order is **largest absolute percentage deviation first**, treating a spike and a drop as
    equally worth surfacing. `low_volume` and `insufficient_history` rows sort last.
15. Each row states its verdict in **text, not colour alone**.
16. Show the account-level recent total and baseline as a summary line.
17. A valid query matching no activity renders an explicit empty state — not an error, not a blank table.
    Account 20 (Quiet Harbor Spa) has zero events and exercises this path with real data.
18. A failed request renders what failed and offers a retry.

### C. User-controlled input that survives reload

19. The user selects the **recent window length** (30 / 60 / 90 days).
20. The user filters the view to **one or a subset of locations**. Account 6 has 15 locations and is the
    reference case.
21. Both selections are written to the URL query string and restored on load; the URL reproduces the
    same view for anyone who opens it.
22. Values read from the URL are validated; malformed or out-of-range values fall back to a documented
    default and never reach the API.
23. Filter changes replace the history entry rather than stacking it, so Back leaves the dashboard.

## User Experience

One view. A summary line answers "is the account normal?"; the table beneath answers "which location
isn't?". Window and location are real labelled controls; changing either updates the URL, and reloading
restores what was on screen.

**Accessibility:** semantic `<table>` with scoped headers, every control tied to a `<label>`, full
keyboard reach with visible focus, `aria-live` on the results region so async updates are announced, and
legibility at a narrow viewport. Verdict is never conveyed by colour or icon alone.

## High-Level Technical Constraints

- **Fixed stack:** .NET 8+ backend, Angular SPA, a relational database with a real schema and migrations
  from standard tooling, loaded from the provided `schema.sql` / `seed.sql`.
- **Seed data is the source of truth** — no synthesized rows to make the feature look better.
- At least one endpoint performs real aggregation, not a pass-through query.
- One frontend state-management approach, named in the README; tests run from a one-line documented
  command.
- Single trusted admin identity; no authentication layer.
- Effort capped at 4–6 focused hours; aggregation correctness outranks feature count.
- Dataset is small (12,626 events), so no latency target beyond a view that loads without a stall.

## Out of Scope

Per the ticket and brief: alerting/notifications, ML/forecasting, auth/login, production infra
(Kubernetes, CI), visual polish beyond legibility. Additionally excluded by decision:

- **Multi-account switching** — one hardcoded account identity, no tenant picker.
- **CSV or data export** — the view must stand alone, since avoiding the export is the point.
- **A baseline configuration UI** — window count, band formula, and low-volume floor are documented
  server-side constants.
- **Charts and time-series visualisation** — numbers, deltas, and verdicts only.
- **`lead_created` and `appointment_set` metrics**, outcome/duration analysis, and per-weekday drilldown.

Deferred: per-metric breakdown, per-location trend history, an account-level "typical range" band.

## Open Questions

1. **Two specified states are unreachable from this seed data.** Every one of the 69 locations has the
   full ~25 weeks of history, and at a 30-day window the smallest baseline is 11.25 calls — so
   `insufficient_history` (FR7) and `low_volume` (FR6) never fire through the UI. They remain correct
   behaviour for real accounts with a newly opened site, and will be covered by unit tests directly
   rather than by a browser walkthrough. Flagging this rather than widening the window options to
   manufacture a demo path.
2. **Duplicate-row policy (FR10).** Whether the 12 exact duplicates are a data-quality artefact to
   deduplicate or genuine concurrent events to count. *Working assumption: count them as-is and document
   the choice, since nothing in the schema marks them as erroneous.*
3. **Low-volume floor of 10 calls.** Chosen so no 30-day row is suppressed while the band still protects
   smaller accounts; it is a documented constant, not a derived threshold.
4. **DST-day handling.** On the 8 March 2026 transition a local day is 23 hours. *Working assumption:
   count events by local calendar date, accepting the short day, rather than normalising to 24 hours.*

### Resolved during scoping

| # | Question | Decision |
|---|---|---|
| 1 | Event type scope | Single type: `call_received` |
| 2 | Location representation | `VARCHAR` column on `activity_events`, scoped per account (69 pairs) |
| 3 | Date coverage | Full ~25 weeks for all 69 locations; 2026-02-01 → 2026-07-27 |
| 4 | Neutral band | Volume-aware `±max(10%, 100/√baseline)`, replacing the initial fixed ±10% |
| 5 | Timezone | Account IANA timezone (`accounts.timezone`); no per-location zone exists |
| 6 | Default sort | Largest absolute deviation, spikes and drops ranked equally |
| — | Window options | 30 / 60 / 90 days, default 30 — 7 and 14 day windows dropped as too low-volume |
