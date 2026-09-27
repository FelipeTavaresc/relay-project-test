# AI Interaction Log — DASH-247

Raw working log, sessions 1–9 — planning through implementation. Appended to as I went; not
retrofitted to match the final code.

**This file is a merge of two logs kept separately while the work ran:** the root log (session 1) and
`tasks/prd-activity-baseline/AI_LOG.md` (sessions 2–9), which was kept next to the artefacts it
produced. No session's prose was rewritten. The merge changed three things only: the `### N.` step
numbering now runs as one sequence (sessions 6–9 shifted up by one to close a duplicate `26`), the
"overrode or corrected" tally continues to 27 instead of restarting at 1 in sessions 8 and 9, and each
session's trailing sections sit under it as `###` rather than floating at the top level.

| Session | Date | Subject |
|---|---|---|
| [1](#session-1--2026-09-26--prd-scoping) | 2026-09-26 | PRD scoping |
| [2](#session-2--2026-09-26--tech-spec) | 2026-09-26 | Tech Spec |
| [3](#session-3--2026-09-26--tech-spec-revision-repository-layout) | 2026-09-26 | Tech Spec revision — repository layout |
| [4](#session-4--2026-09-26--task-breakdown) | 2026-09-26 | Task breakdown — **superseded by session 5**, never written to disk |
| [5](#session-5--2026-09-26--task-breakdown-regenerated-after-a-context-clear) | 2026-09-26 | Task breakdown, regenerated after a context clear |
| [6](#session-6--2026-09-26--task-40-activityqueries-and-the-at-time-zone-aggregation) | 2026-09-26 | Task 4.0 — `ActivityQueries` and the `AT TIME ZONE` aggregation |
| [7](#session-7--2026-09-26--task-50-endpoint-dtos-validation-problemdetails-logging) | 2026-09-26 | Task 5.0 — endpoint, DTOs, validation, ProblemDetails, logging |
| [8](#session-8--2026-09-27--task-70-url-backed-state-loadingemptyerror-states-frontend-tests) | 2026-09-27 | Task 7.0 — URL-backed state, render states, frontend tests |
| [9](#session-9--2026-09-27--task-80-docker-compose-readme-reconciliation-deviations) | 2026-09-27 | Task 8.0 — Docker Compose, README, reconciliation, deviations |

**Tasks 1.0, 2.0, 3.0 and 6.0 have no session entry of their own.** They were executed — sessions 6
and 8 read their output and verified their checked boxes — but they were not logged at the time, and
nothing here reconstructs them after the fact. The gap is left visible rather than filled in.

## Tools and division of labour (the honest line)

Written in session 1 and left as written. Every session below carries its own *Tools this session*
note, and sessions 6–9 are code — so the last sentence here ("no implementation code has been
written yet") is true of session 1 and of nothing after it.


**Claude Code (Opus 5)** for all of it so far: authoring the PRD, the tech spec and the task breakdown,
and — more usefully — running the seed-data analysis that overturned two of my own scoping decisions.
I wrote none of the PRD, spec or task prose by hand; I directed it, rejected one round of its questions,
overrode two of its recommendations in session 2, and made it prove its numbers against `seed.sql`
instead of accepting its illustrative examples. No other model used at any point. Verification was
agent-run but human-checked: I read the awk output, not just the summary. **No implementation code has
been written yet** — everything logged so far is planning, and nothing in these logs should be read as
a claim that something was built or tested.

Custom agent context committed in `.claude/`: `rules/project-standards.md`, `rules/csharp-dotnet.md`,
`rules/angular.md`, plus the `create-prd` / `create-techspec` / `create-tasks` / `execute-task` skills
and templates. These pre-date this ticket and are the reason the agent's output already knew about
"no aggregation in the browser", `IReadOnlyList<T>`, and URL-backed state without my saying so.

---

## Session 1 — 2026-09-26 — PRD scoping

### 1. Invocation — accepted

**Prompt:** `/create-prd @plan.md`

The skill forces clarify → plan → draft. The agent read `plan.md` and `prompt-base.md`, noticed the
repo held only those docs (no starter data yet), and asked four scoping questions before writing
anything. Correct behaviour; I let it run.

### 2. First clarifying round — accepted as offered

It asked about baseline definition, primary view, which input persists, and extra exclusions, and it
recommended one option per question with a rendered preview of each. My picks:

| Question | Chose | Over |
|---|---|---|
| Baseline | Trailing same-weekday average (prior 4) | Prior-period, mean±σ, both |
| Primary view | Ranked location table | Summary + drilldown, trend chart |
| Persisted input | Window length **and** location filter | (multi-select) |
| Extra exclusions | All four offered | — |

I took its recommendations because the reasoning was right, not because they were first: weekday
seasonality dominates call volume, and a ranked table answers both complaints in the ticket with one
endpoint. It drafted a 1,475-word PRD, then trimmed to 1,243 when I'd set a ~1,000 cap — and told me it
had stopped at 1,243 rather than cutting requirements to hit the number. I'd rather it report the
overage than quietly gut the FR list, so: accepted.

**⚠️ What I should have caught here and didn't:** the PRD it produced specified windows of "7, 14, or 30
days" and a `typical` band of "±10%". Neither number came from the data — the data wasn't in the repo
yet. It also generated a preview table showing `42 calls vs baseline 31`. Those numbers were invented
and, as it turned out, ~10× the real per-location weekly volume. Plausible-looking illustrative numbers
are the most dangerous output an agent produces, because they anchor the spec. They anchored mine.

### 3. I answered the open questions in the file; it verified instead of accepting — **redirected, and it was right to push back**

I dropped `schema.sql` and `seed.sql` into the PRD folder and typed answers inline under the Open
Questions section, including `Check in the seed seed.sql` for the date-coverage question.

Rather than take my answers at face value, it parsed all 12,626 events and checked. What it found:

```bash
grep "INSERT INTO activity_events" seed.sql | sed -E "s/.*VALUES \(([0-9]+), ([0-9]+), '([^']*)', '([^']*)', '([^ ]+) ([^']+)', ([^,]+), (.*)\);/\1\t\2\t\3\t\4\t\5\t\6\t\7\t\8/" > events.tsv
# 12626 rows parsed, 0 unparsed
```

- 20 accounts, **19 with events** — account 20 (Quiet Harbor Spa) has zero, so the empty state is
  reachable with real data rather than needing a fake.
- Account 6 has **15 locations** — the multi-location case the ticket cares about.
- 69 account-location pairs, `Site A`…`Site O`, all with the full 2026-02-01 → 2026-07-27 span.
- 12 exact-duplicate `(account, location, event_type, occurred_at)` rows; 313 calls with `NULL`
  duration; 398 `NULL` outcomes; no negative or zero durations.
- Weekday/weekend split: 78–97 events on weekdays vs 23–25 at weekends. My same-weekday baseline choice
  from step 2 holds up.

Then it wrote and ran the actual baseline aggregation in awk before any C# existed:

```
window=7 d  n=69  low_volume(base<10)=69/69  | flagged at ±10%: 84% of locations
window=14d  n=69  low_volume(base<10)=49/69  | flagged at ±10%: 77%
window=30d  n=69  low_volume(base<10)=0/69   | flagged at ±10%: 67%  (±25%: 36%)
```

**This killed the spec I had just approved.** At a 7-day window every single location has a baseline
under 10 calls — typical counts are 1–7 — so one extra call moves the delta 25–50%. And ±10% flags
61–84% of locations in *every* configuration, meaning "above/below typical" would be noise. The feature
is supposed to answer "is this normal?"; as specified, it would have answered "nothing is normal."

Two further things it caught that I hadn't asked about:

- **The data ends 2026-07-27; today is 2026-09-26.** A window anchored to wall-clock time returns zero
  for every location. The anchor has to be the latest event date. I'd have shipped an empty dashboard
  and debugged the SQL.
- **My "use location-local" answer is not implementable as written.** `activity_events.location` is a
  bare `VARCHAR` label; the only timezone in the schema is `accounts.timezone`. It said so plainly
  instead of quietly implementing account-local and calling it location-local. Worth noting my *own*
  earlier assumption in the draft PRD was "UTC, since the seed data likely carries no per-location
  timezone" — half right: there's no per-location zone, but there *is* an account one, and the window
  crosses the 8 March 2026 DST transition with `America/Phoenix` and `UTC` accounts that don't shift.

### 4. Second question round — **rejected**

It came back with four questions (window options, band strategy, event type, sort direction). Good
questions, but it had bundled a real finding with a decision request, and I wanted the finding explained
before picking from a menu. I rejected the tool call and asked it to tell me what each question was
actually about. It re-explained the arithmetic — ~0.6 calls/day/location, so a 7-day window holds ~4
calls and its baseline is ~4 — which is what made the decision obvious. Worth the extra round trip.

### 5. Decisions — accepted its recommendation on all four

| Decision | Chose | Why |
|---|---|---|
| Window | **30 / 60 / 90, default 30** | 7 and 14 day windows are unusable on this data, so don't offer them |
| Band | **±max(10%, 100/√baseline)** | Counting noise scales with √n; a fixed band over- or under-flags depending on volume |
| Metric | **`call_received`** | 7,780 rows, present at all 69 locations, best volume for a stable baseline |
| Sort | **Absolute deviation** | Ticket asks "is this normal", not "what's broken" — a spike counts |

The volume-aware band is the one I'd defend hardest: it's the `mean±σ` idea I *rejected* in step 2,
reintroduced where it belongs. It doesn't change what the baseline is (still the same-weekday average) —
it only sets how far from it counts as unusual. Rejecting it as a baseline definition and accepting it
as a threshold isn't a contradiction; I just didn't see the distinction until the numbers forced it.

It also flagged, unprompted, that `low_volume` and `insufficient_history` are now **unreachable through
the UI** — at a 30-day window the smallest baseline is 11.25 calls, and all 69 locations have full
history. It recommended keeping both states with direct unit tests rather than widening the window
options to manufacture a demo path. Accepted; that's in the PRD's Open Questions rather than buried.

---

### Specific moments I overrode or corrected the agent — session 1

1. **Invented example numbers (step 2).** Its `42 calls vs baseline 31` preview was fabricated and ~10×
   the real volume. It anchored my ±10% band and my 7-day window. Both were wrong, and I approved them.
   Lesson recorded: don't let an agent's illustrative numbers into a spec before the data is in the repo.
2. **Rejected the second question round (step 4)** rather than answering a menu I didn't yet understand.
   The re-explanation changed nothing about the options and everything about my confidence in picking.
3. **Held it to the word cap honestly.** It reported 1,243 words against a ~1,000 target instead of
   deleting requirements to comply. I'd rather see the overage flagged than discover the FR list was
   silently trimmed.
4. **Made it prove date coverage rather than assert it.** My inline answer was "check the seed" — it
   could have written "≥5 weeks, confirmed" and I'd likely have believed it. It ran the query and
   reported per-location first/last dates.

Where it was right and I was wrong: the 7-day window, the ±10% band, the wall-clock anchor, and
"location-local". Four for four on things I'd approved or asserted.

### Friction worth noting

- A `bash` heredoc write of the PRD failed on quoting (`unexpected EOF while looking for matching '`)
  before it fell back to a direct file write. Cost one round trip, no harm.
- No Python on this machine, so the entire seed analysis ran in `gawk`. Slower to write, but the awk
  scripts are in the log above and are re-runnable, which a throwaway pandas session wouldn't have been.

### Still to verify (not yet done — do not read this as done)

- [ ] Generated SQL for the baseline query actually translates (no client-side `GroupBy` fallback)
- [ ] Totals reconciled against the awk prototype above, per location, for account 6
- [ ] DST-day counts on 2026-03-08 for a `America/Chicago` account vs `America/Phoenix`
- [ ] Duplicate-row policy decided and tested (FR10)
- [ ] `low_volume` / `insufficient_history` covered by unit tests, since the UI can't reach them

---

## Session 2 — 2026-09-26 — Tech Spec

### Tools this session

Claude Code (Opus 5) again, for all of it: seed analysis, the spec draft, and — new this session —
two documentation lookups it ran on its own initiative to check claims it would otherwise have
asserted from memory. I authored none of the spec prose; I made four scoping calls and overrode its
recommendation on two of them. No other model.

### 6. Invocation — accepted

**Prompt:** `/create-techspec @tasks\prd-activity-baseline\prd.md` (plus the AI-log requirement).

It read the PRD, the three rule files, and the template, then — before drafting anything — re-ran the
aggregation against `seed.sql`. I didn't ask it to. Given what session 1 taught me about letting
invented numbers into a spec, that was the right instinct.

### 7. It found a planted anomaly that invalidates part of session 1's analysis — **accepted, and it was right**

Running the PRD's own algorithm at account-local dates, it produced:

```
account 6 @ 30d, data as-is    → 15 of 15 locations "below typical"  (−26% to −58%)
all 69 locations @ 30d         → 10 above, 17 below, 42 typical
```

Fourteen of those seventeen "below" are account 6. The cause:

```
=== daily totals ALL accounts, top 3 days ===
882 2026-06-03
110 2026-05-22
109 2026-02-12
```

**`2026-06-03` carries 882 events — 485 `call_received` for account 6 alone, across all 15 of its
locations**, against ~5–15/day typical for that account. Next-busiest day in the entire dataset is 110.
At a 30-day window that day sits outside the window but inside the baseline lookback (it feeds
2026-07-01's prior-Wednesday average), so it inflates every baseline and pushes every location "below".

Account 6 is the PRD's designated multi-location reference case (FR20). The demo case renders as a wall
of 15 identical "below" verdicts, which is exactly the failure PRD Goal 5 forbids.

It also quietly undermined a session-1 claim I'd put in the PRD. Open Question #1 asserts `low_volume`
is unreachable because "the smallest baseline is 11.25 calls". True — but only *because* the spike
inflates account 6's baselines. With 06-03 removed, five of account 6's locations fall below the
10-call floor. The claim in the PRD is correct for the data as-is and wrong about *why*. I've left the
PRD alone and let the techspec carry the correction, since the shipped behaviour doesn't change.

### 8. It tested the fixes before recommending one — **accepted; this is the part I'd have got wrong**

My instinct on seeing one outlier day wreck a mean was "use a median". It prototyped that instead of
agreeing:

| Baseline | 30d flagged | 60d | 90d |
|---|---|---|---|
| **mean of 4 (PRD)** | **39%** | **10%** | **3%** |
| median of 4 | 29% | 68% | 80% |
| trimmed mean (drop max) | 49% | 90% | 99% |

Both robust estimators are dramatically worse — 80% and 99% of locations flagged at a 90-day window.
The reason is obvious once stated and wasn't obvious to me beforehand: per-location volume is ~0.5
calls/day, so lookback quadruples look like `{0,0,1,2}`, and a median systematically underestimates a
sparse count process. It explicitly declined to propose changing the estimator and said the PRD's
mean-of-4 stands. An agent that had just agreed with me would have shipped a worse feature.

What it proposed instead was additive: keep the arithmetic, flag the cause. Then it calibrated the
threshold empirically rather than picking a round number:

```
window=30d  share>0.25: 15 locations   ← exactly account 6's 15, all naming 2026-06-03
            highest share anywhere else: 0.200 (acct 11), 0.188 (acct 10), 0.188 (acct 18)
window=60d  share>0.25: 15   highest elsewhere: 0.140
window=90d  share>0.25: 15   highest elsewhere: 0.092
```

Zero false positives at all three windows with a clean gap. Accepted.

### 9. It corrected its own assumption about SQL Server — **noted; caught by its own check, not by me**

Drafting the DB decision it was about to claim SQL Server's `AT TIME ZONE` accepts IANA names inside a
Linux container. It searched instead of asserting, and the answer is the opposite: SQL Server takes
**Windows** zone names even on Linux, mapping the host's IANA zone through a fixed table. The seed
stores IANA strings, so SQL Server needs a mapping table joined into every aggregation query.

I want to be precise about crediting this: it stated the wrong thing as a working assumption in its
own reasoning, then verified and corrected before it reached me. That's the behaviour I want, but it's
worth recording that the first instinct was wrong, and that the only reason I know is that it checked.

It did the same thing unprompted on the frontend: rather than specifying Angular's `httpResource` /
`rxResource` from memory, it looked up their status and found the Resource API is **still experimental
as of Angular 20.3**, then specced the stable `toSignal` + `switchMap` path that `angular.md` already
prescribes. Two lookups, two claims that would otherwise have been asserted confidently.

### 10. Four decisions — I took its recommendation on two and **overrode it on two**

| Decision | Its recommendation | My call |
|---|---|---|
| Outlier day | Flag on the row, keep mean-of-4 | **Accepted** |
| Database + timezone | Postgres, `AT TIME ZONE` in SQL | **Accepted** |
| Where the baseline math lives | Split: counts in SQL, verdicts in C# | **Overrode → all in C#** |
| Integration tests | Testcontainers Postgres | **Overrode → unit tests only** |

The first two were straightforward — the Postgres one especially, since it had just proved the SQL
Server path costs a mapping table for zero behavioural gain, and "the team runs SQL Server" isn't worth
that against a 4–6 hour budget.

The two overrides are mine and I'll own them rather than dress them up:

- **All-in-C# over the split.** I want the aggregation logic in one readable, dependency-free place I
  can reason about, and I'd rather a reviewer read one C# file than a C# file plus a CTE.
- **Unit tests only.** Budget. Testcontainers means a container pull inside `dotnet test` and a fixture
  to write, and I'd rather spend the remaining hours on the feature working end to end.

I don't think the second one is *right*. It's a trade I'm making with my eyes open, which is different.

### 11. It flagged a contradiction in my own answers instead of quietly resolving it — **accepted**

My picks for decisions 2 and 3 conflict. "Postgres, convert in SQL" puts the timezone `GROUP BY` in the
database; "all in C#" — as the option was written — pulls raw rows and buckets them in memory, which
would throw that `GROUP BY` away. I didn't notice. It did, said so directly rather than picking one and
moving on, and wrote the reconciliation into the spec as a named decision (§ Key Decisions #4) with a
line saying it should be corrected if I meant the stricter reading.

It also refused to let my unit-tests-only call pass silently: `project-standards.md` and
`csharp-dotnet.md` both mandate an integration test per endpoint *specifically* because the in-memory
provider can't evaluate SQL translation. Rather than dropping the requirement, it recorded it under
Standards Compliance as a **named deviation** with the compliant alternative spelled out and an
estimate ("roughly one test file"). That's the right handling — it did what I asked and made the cost
legible instead of arguing or complying silently.

---

### Specific moments I overrode or corrected the agent — session 2

5. **The 2026-06-03 spike (step 7).** I didn't catch it, it did, and it invalidates the reasoning
   behind one of my own PRD claims. This is the second time in two sessions that the thing that broke
   the spec was in the data rather than in the logic. Lesson updated: I keep reviewing the algorithm
   when I should be reviewing the distribution.
6. **My median instinct was wrong (step 8).** I'd have swapped mean for median on sight of a single
   outlier and shipped a feature flagging 80% of locations at a 90-day window. It tested first. The
   general lesson: an outlier is not automatically an argument for a robust estimator — at ~0.5
   events/day the mean is the right estimator and the outlier is a *presentation* problem.
7. **Overrode it on integration tests (step 10)**, against both its recommendation and the project's
   own written rules. Recorded here so it isn't discovered later as an oversight. It isn't one.
8. **Overrode it on the aggregation split (step 10)** for readability, accepting that this is the
   weaker demonstration of "real aggregation" that the brief explicitly asks to see.

Where it was right and I was wrong this session: the spike, the median instinct, and the contradiction
between my own two answers. Where it was wrong: it nearly asserted the SQL Server timezone behaviour
backwards — caught by its own check, not by mine.

### Friction worth noting

- A `bash` heredoc append to this file truncated mid-write (`warning: here-document ... delimited by
  end-of-file`), leaving a half-finished sentence in the log. Second heredoc failure in two sessions —
  same tool, same cause. Fixed by truncating the partial line and appending from a written file
  instead. Noting it because the failure mode is silent-ish: the file *looked* written.

### Still to verify (unchanged items carried forward, plus new)

- [ ] Generated SQL for the daily-count query inspected — **now the only check on it, since I declined
      the integration test**
- [ ] Account 6 @ 30d reconciled against the spec's committed fixture (Site O: 9 / 21.25 / −57.65%;
      account totals 175 / 311.75)
- [ ] DST-day counts on 2026-03-08 for `America/Chicago` vs `America/Phoenix`
- [ ] Duplicate-row policy tested (FR10) — 12 duplicate rows, 9 of them `call_received`
- [ ] `low_volume` / `insufficient_history` covered by unit tests, since the UI can't reach them
- [ ] Outlier flag fires on exactly account 6's 15 locations and nothing else, at all three windows
- [ ] Fetch span is window + 28 days, not window — asserted by a test on the first window day

---

## Session 3 — 2026-09-26 — Tech Spec revision: repository layout

### Tools this session

Claude Code (Opus 5), one short exchange. No analysis re-run, no lookups — a structural edit to
`techspec.md` only. No code exists yet, so nothing had to move on disk.

### 12. Invocation — accepted

**Prompt:** "Adjust the techspec to organize the project as backend and frontend folder." Plus a
follow-up mid-turn asking for this interaction to be logged here.

My reason is delivery legibility, not architecture: a reviewer opening the repo should see the two
halves of the submission immediately, and the original spec's `src/` + `web/` split reads as one .NET
repo with a web folder bolted on. Nothing about the design changes.

### 13. It treated the change as a prefix rather than a redesign — **accepted**

The spec's paths came from `.claude/rules`, which prescribes `src/Relay.*` + `tests/Relay.*`
(`csharp-dotnet.md`) and `src/app/{core,features,shared}` (`angular.md`). It kept both layouts intact
and moved them under the new roots — `backend/src/Relay.Api/`, `frontend/src/app/features/…` — instead
of flattening to `backend/Relay.Api/`, which is what I'd have typed by hand and would have quietly put
the spec at odds with its own rule files. It said so explicitly in the new paragraph rather than
leaving me to notice the rules still matched.

Concretely changed: the Component Overview is now a repo tree covering both roots and compose build
contexts; `§ Frontend` and every row of Relevant Files carry the new prefixes; the frontend test line
names the root its one-line command runs from.

### 14. It refused to duplicate the seed file — **accepted, and I'd have got this wrong**

My mental model of "backend/ and frontend/" included a `db/` folder at the root holding `schema.sql`
and `seed.sql`, which are currently in `tasks/prd-activity-baseline/`. It pointed out that copying them
gives two versions of the stated source of truth and specced the compose init mount pointing at the
task folder instead — one copy, nothing to drift. Unconventional placement for a shipped repo, and the
right call for this one.

### 15. It added a scaffolding step to the build order — **accepted**

New step 1: both roots scaffolded and building, with test commands green on an empty suite, before any
feature code. Previously the build order opened at the database and left the project skeleton implicit.
Cheap, and it removes the "is this my code or my folder structure" ambiguity from the first real failure.
Everything downstream renumbered; the ordering rationale is unchanged.

---

### Specific moments I overrode or corrected the agent — session 3

9. **Nothing overridden this session.** One correction in my favour (the seed-duplication catch in
   step 14) and one convention I'd have broken without noticing (step 13). Recording a session where I
   changed nothing, so the log doesn't read as if I only write down disagreements.

### Still to verify — added this session

- [ ] `docker-compose.yml` build contexts actually resolve from `./backend` and `./frontend`, and the
      `seed.sql` init mount from `tasks/prd-activity-baseline/` works on a clean volume
- [ ] README carries one build and one test command **per root**, as the spec now claims

---

## Session 4 — 2026-09-26 — Task breakdown

> **Superseded by session 5.** The 10-task plan below was never written to disk — the approval gate
> held and the session ended there. What actually shipped is the 8-task breakdown from session 5.
> Read this entry as the record of a plan, not of the files in this folder.

### Tools this session

Claude Code (Opus 5), planning only — no code, no analysis re-run, no lookups. It authored the task
breakdown; I set the approval gate and made two structural calls on it. Nothing was generated to disk
in this session beyond this log entry: the skill's approval gate held and I had not yet approved at the
time of writing. No other model.

### 16. Invocation — accepted

**Prompt:** `/create-tasks @tasks\prd-activity-baseline\prd.md @tasks\prd-activity-baseline\techspec.md`
Plus a follow-up mid-turn asking for this interaction to be logged here.

It read `prd.md`, `techspec.md`, both templates (`tasks-template.md`, `task-template.md`), `schema.sql`,
and listed the repo root. That last one mattered: it confirmed from the listing that no `backend/` or
`frontend/` exists yet — `.claude/`, `AI_LOG.md`, `ai-log/`, `plan.md`, the two prompt files and
`tasks/` — rather than assuming the session-3 layout had already been scaffolded. Greenfield confirmed
from the filesystem, not from the spec's claim about it.

It then stopped and presented a 10-task list for approval **before writing any file**, which is what the
skill's `<critical>` gate demands. Worth noting because the gate is the only thing standing between "a
plan I agreed to" and "ten files I now have to read to find out what I agreed to."

### 17. It expanded the spec's 7-step build order into 10 tasks — **accepted, with one exception below**

The techspec's `§ Development Sequencing` lists seven steps. The mapping it proposed:

| Spec step | Became |
|---|---|
| 1 Repository skeleton | 1.0 |
| 2 Database + migrations | 2.0 |
| 3 `BaselineCalculator` + unit tests | 3.0 (also absorbs the Application contracts — records, `IActivityQueries`, `IBaselineCalculator`) |
| 4 `ActivityQueries` + `AT TIME ZONE` | 4.0 |
| 5 Controller, DTOs, validation | 5.0 |
| 6 Angular feature → URL persistence → states | **6.0 / 7.0 / 8.0** |
| 7 Docker Compose + README | 10.0 |
| — | **9.0 Reconciliation & verification** (new) |

Two of those are its judgement, not the spec's:

**The Angular step split three ways.** Step 6 in the spec is one line carrying a service, four
components, four render states, a11y, and URL round-trip tests. As a single task file that is
unreviewable and, more to the point, un-abandonable — you cannot stop halfway and know where you are.
Split into data layer (6.0) / UI + states (7.0) / URL persistence (8.0), each with its own tests.
Accepted; the smart-dumb boundary in `angular.md` already implies that seam.

**It put the Angular contract on the critical path *off* the backend.** 6.0 depends only on 1.0, because
the DTO shapes are frozen in `§ API Endpoints` and the frontend can be written against them before the
API answers. That is right, and it is the only real parallelism in a 4–6 hour build.

### 18. It added a task that isn't in the spec's build order, and said so — **accepted**

New 9.0: commit the seed-derived fixture (account 6 @ 30d — `Site O` 9 / 21.25 / −57.65%; totals
175 / 311.75) as a test, run the one-off script against live endpoint output, record the hand-inspected
SQL. Its stated reason, verbatim:

> Task 9.0 isn't in the spec's build order — I split it out because PRD Goal 3 and the
> omitted-integration-test mitigation both land there, and burying it inside 4.0/5.0 is how it gets
> skipped.

That is exactly the failure mode. I declined the integration test in session 2 and accepted a
hand-verification mitigation in its place; a mitigation that exists only as a bullet inside someone
else's task is a mitigation that doesn't exist. It offered to fold it back in for a tidier 9-task list.
I'd rather have the ugly tenth task.

### 19. It re-surfaced the contradiction it flagged in session 2 instead of quietly resolving it — **accepted**

Techspec Key Decision #4 ("baseline math in C#, day bucketing in SQL") carries the words *"Flagged for
correction if the intent was the stricter reading."* It did not treat my silence as agreement. Verbatim:

> **Techspec Key Decision #4 is flagged "for correction"** — baseline math in C#, day bucketing in SQL.
> I've written the tasks to the spec as-is (SQL buckets the local day). Say the word if you want the
> stricter all-in-C# reading instead; it changes 4.0.

Second time the same open item has been handed back rather than decided for me. The spec's reasoning
still holds — Postgres `AT TIME ZONE` is the whole reason the database was chosen, and pulling 7,780 raw
rows to bucket dates in C# would throw that away — so the tasks proceed as written. Recording that the
decision was made by me here, not inherited by default.

### 20. Phasing — accepted as offered

Four phases: foundation (1.0–2.0), correctness core (3.0–5.0), UI (6.0–8.0), verify & ship (9.0–10.0).
Ten main tasks sits on the boundary where the skill says to suggest phasing; it suggested it unprompted.
The phase boundaries are also the sane stopping points if the 4–6 hour cap bites — phase 3 half-done
with phase 2 green is a defensible submission, the reverse is not.

---

### Specific moments I overrode or corrected the agent — session 4

10. **Nothing overridden.** Two of its calls went beyond the spec (the three-way Angular split, the new
    9.0) and I took both. One decision it declined to make for me (Key Decision #4) I made explicitly
    rather than letting the spec's default stand by silence.
11. **The gate did the work, not me.** My only real contribution this session was invoking a skill that
    forces a plan-before-files approval step. Sessions 1 and 2 are a record of me approving things I
    hadn't checked; a breakdown of ten files would have been the easiest place yet to do that again.

### Still to verify — added this session

- [ ] The ten task files, once generated, each name the PRD FRs they satisfy — every FR1–FR23 covered by
      exactly one task, nothing orphaned
- [ ] 6.0 really can be built and its tests run green before 5.0 exists (the claimed parallelism), or the
      dependency graph is wrong
- [ ] The 4–6 hour cap against ten tasks — if phases 1–2 alone consume it, 7.0/8.0 get cut, not rushed

---

## Session 5 — 2026-09-26 — Task breakdown, regenerated after a context clear

### Tools this session

Claude Code (Opus 5) — planning and file generation, no code, no analysis re-run, no lookups. It
authored `tasks.md` and all eight task files; I approved the plan and changed nothing in what it
produced. The one thing I contributed beyond approval was asking, afterwards, whether this session had
been logged — which is what surfaced everything below. No other model.

### 21. Invocation — accepted

**Prompt:** `/clear`, then
`/create-tasks @tasks\prd-activity-baseline\techspec.md @tasks\prd-activity-baseline\prd.md`
(techspec first this time; session 4 had the PRD first). Plus a follow-up asking for this entry.

The `/clear` is the material fact. This run had no memory of session 4's plan and re-derived the
breakdown from the two documents alone. Same reads as before — `prd.md`, `techspec.md`, both templates,
a root listing, `schema.sql`, the head of `seed.sql`. The listing confirmed the folder still held only
prd / techspec / schema / seed / this log, which is independent proof that session 4's 10-task plan
really was never written to disk.

The gate held again: an 8-task list presented, nothing on disk until I said *"approved, generate the
files."*

### 22. Same skill, same two inputs, a materially different plan — **this is the finding**

| | Session 4 | Session 5 (shipped) |
|---|---|---|
| Task count | 10 | **8** |
| Angular | split three ways — data / UI+states / URL | split two ways — components / URL+states |
| Reconciliation | **its own task, 9.0** | fixture inside 3.0, end-to-end inside 8.0 |
| Docker + README | 10.0 | 8.0 |
| Phases | four | five |
| Key Decision #4 | handed back to me verbatim | never mentioned |

Neither plan is wrong. The difference that matters is where verification lives. Session 4 pulled
reconciliation out into its own task *specifically* so it wouldn't get buried, and its stated reason
(step 18) was that a mitigation existing only as a bullet inside someone else's task is a mitigation
that doesn't exist. Session 5 put it back inside 3.0 and 8.0 — and asked me about the placement
explicitly before writing.

I approved it. Without re-reading this log first, so I approved a decision I had already made the other
way an hour earlier, and I didn't notice until I went looking. That is the honest version. A cleared
context doesn't carry my past decisions; only the documents do, and my decisions from session 4 live in
this log, which the agent had no reason to read.

### 23. It found two contradictions in the techspec that three prior sessions missed — **accepted, both**

Unprompted, while writing 2.0. Verbatim:

> the seed can't be mounted into the Postgres init directory as written (techspec § Development
> Sequencing step 2): initdb runs before migrations exist, so the inserts would fail against missing
> tables.

> § Data Models says `occurred_at` stays `DateTimeOffset` at the EF layer, § Known Risks requires the
> column be `timestamp without time zone`. Npgsql maps `DateTimeOffset` to `timestamptz`, so both
> can't hold.

The first is a consequence of *session 3's own decision* (step 14 — mount the seed from the task
folder rather than copying it). That call was right and I still think so, but neither session 3 nor the
techspec noticed that the init directory runs before the schema exists. It sat in the spec as a step
that cannot execute.

The second I'd have resolved backwards if I'd had to pick under time pressure. Its reasoning: the
column type wins because it is the one that changes query results, and the CLR type is off the hot path
anyway since the aggregation is raw SQL. Obvious once said.

Both are now written into `2_task.md` as decisions with the alternatives and a requirement to record
the choice — not silently resolved, which is the handling I want and the same pattern as session 2
step 11.

### 24. It did **not** re-raise Key Decision #4 — my catch, not its

Session 4 handed that open item back to me rather than deciding it (step 19). This run wrote 4.0 to the
spec as-is — SQL buckets the local day — and never mentioned that the techspec marks the decision
*"Flagged for correction if the intent was the stricter reading."*

No harm done: I decided it in session 4 and the task files match that decision. But it reached the task
files by inheritance, not because anyone restated it. The marker is still sitting in
techspec § Key Decisions #4 reading as unresolved, and it has now passed through a full planning run
unexamined. Either I resolve it in the techspec or I accept that the next cleared context inherits it
silently too.

### 25. Two judgment calls put to me before writing — both **approved as offered**

1. Reconciliation placement (fixture in 3.0, live check in 8.0), with "give it its own task" named as
   the alternative.
2. Keeping the 6.0/7.0 split the techspec's sequencing implies, with the cost stated plainly: 6.0
   completes in a state that is not yet reload-safe.

I took both as offered. Given the comparison in step 22, the first is the one I'd revisit before 3.0
starts.

### 26. What shipped

`tasks.md` plus `1_task.md` … `8_task.md`, frontmatter intact. Three things it wrote in that I didn't
ask for and am keeping:

- **The fetch-span test is marked mandatory in 3.0**, with the reasoning attached — techspec
  § Known Risks names a narrowed span as the likely implementation bug, and it fails *silently*.
- **A deliberate asymmetry named in both 5_task.md and 7_task.md**: the backend rejects an unknown
  location with a 400, the frontend drops unknown names read from the URL. Both are specified, in
  different sections; each task now points at the other so neither gets "fixed" into agreement.
- **4.0's manual verification subtasks are written as mandatory rather than optional**, on the grounds
  that they are the whole mitigation for the integration tests I declined in session 2.

---

### Specific moments I overrode or corrected the agent — session 5

12. **Nothing overridden in the output.** Third session running. What I corrected this time was the
    *record*, not the work: this session was absent from the log, and session 4's entry described ten
    task files that don't exist.
13. **I re-approved a decision I'd previously made the other way (step 22)**, and only found out by
    asking a question I nearly didn't ask. The lesson isn't about the agent — a cleared context behaved
    exactly as it should. It's that my own decisions live in a file nobody reads at invocation time.
14. **Where it was right and I wasn't looking (step 23):** two contradictions in a spec I'd already
    signed off, one of them created by a session-3 decision I called correct at the time. Third session
    in a row where the problem was in something I'd already approved rather than in what it just wrote.
15. **Where it fell short (step 24):** it let Key Decision #4 pass without re-raising it. Milder than
    session 2's SQL Server near-miss, and it had no way to know the question had ever been asked — but
    the effect is a flagged decision quietly hardening into eight task files.

### Still to verify — added this session

- [ ] FR coverage across **eight** task files, not ten — every FR1–FR23 in exactly one task, nothing
      orphaned (carried forward from session 4, count corrected)
- [ ] Reconciliation survives being split across 3.0 and 8.0 — session 4's objection stands until 8.0
      is actually done
- [ ] The schema-vs-seed ordering decision in `2_task.md` is made and written down **before** 2.0
      starts, not discovered during it
- [ ] Key Decision #4: resolve the "flagged for correction" marker in the techspec, or accept it as
      inherited and say so there
- [ ] 6.0 really can be built and its tests run green before 5.0 exists — still unproven, still the
      only parallelism in the plan

### Friction worth noting

- No heredoc this session — the log was appended with the file-editing tool instead, after two
  truncated `bash` heredoc writes in sessions 1 and 2. Third time would have been on me.

---

## Session 6 — 2026-09-26 — Task 4.0: ActivityQueries and the AT TIME ZONE aggregation

### Tools this session

Claude Code (Opus 5). Code, real database, real queries. Context7 was named in the `/execute-task`
template but is not configured in this repo, so the agent fell back to `WebFetch` against the
Microsoft EF Core docs — which turned out to matter (see 28). Postgres 16 in Docker, already carrying
the Task 2.0 migration and the seed. No other model.

### 27. Invocation — accepted

**Prompt:** `/clear`, then `/execute-task c:\Dev\take-home\qualitara\tasks\prd-activity-baseline\4_task.md`

Another cleared context. It read `4_task.md`, `tasks.md`, `prd.md`, `techspec.md`, then all of the
existing backend code before writing anything. Dependencies 2.0 and 3.0 were checked as `[x]` and
verified present rather than assumed.

### 28. It checked the docs and the docs changed the design — **not overridden, worth recording**

`4_task.md` requires parameterised `FromSql` and forbids `FromSqlRaw`. The techspec writes the query
as a `const string` with `{0}`/`{1}` placeholders — which is *`FromSqlRaw` with separate parameter
arguments*, not `FromSql`. A `const string` cannot be passed to `FromSql` at all; it takes a
`FormattableString`. The two halves of the spec are not compatible as written.

It resolved this the way the task file says rather than the way the techspec's code block reads:
inline interpolated `FromSql`, no const. Correct call, and it said so instead of quietly picking one.

The docs also settled something the spec never addressed: `FromSql` can only start from a `DbSet`, so
the three non-entity result shapes need to be in the EF model. It used keyless entity types with
`ToView(null)`, then — unprompted — verified with
`dotnet ef migrations has-pending-model-changes` that this adds no migration operations. *"No changes
have been made to the model since the last migration."* That check is the difference between the claim
and the fact, and I didn't ask for it.

It also noticed EF Core 8's `Database.SqlQuery<T>` now supports unmapped types and would have avoided
the keyless-type machinery entirely — and rejected it, because the task file says `FromSql`
specifically. I agree with deferring to the task file, but I want the alternative on the record: if
`SqlQuery<T>` had been used, `RelayDbContext` would carry no query-shape types at all.

### 29. Its own test caught its own wrong expectation

It wrote a test asserting that 05:30 UTC on 27 September is still 26 September in Chicago. It isn't —
Chicago is UTC−5 in September, so that is 00:30 the same day. The test failed, and the failure was in
the *test*, not the service. It moved the clock to 02:30 UTC and it passed.

Worth logging precisely because it is the unglamorous case: a timezone assumption made carelessly in a
test about timezones. The test suite caught it in the same minute it was written. Had the assertion
been written the other way round — expecting the wrong date and getting it — nothing would have
caught it.

### 30. Where I'd have accepted less than it gave

The three mandatory verification subtasks (4.8–4.10) could each have been satisfied with a sentence.
What went into `4_task.md` instead: the logged SQL with the actual `@p0` parameter lists, an
`EXPLAIN (ANALYZE)` plan showing `Rows Removed by Join Filter: 752` — i.e. the non-sargable predicate
the techspec predicted, *observed* rather than restated — and a UTC-vs-local bucket table for one
named seed row (event 9395) where exactly one event moves across the day boundary.

The reconciliation deserves its own note. It derived account 6's fifteen rows **twice**: once in pure
SQL in psql (`generate_series` zero-fill plus self-join, owing nothing to the C#), and once through
the real service. They agree row for row, and both agree with the committed fixture. That is a
genuinely independent check rather than the code agreeing with itself, and it wasn't asked for.

The fetch-span demonstration is the one I'd have skipped and been wrong to: narrowing the span to the
window leaves `recent=175` completely unchanged and collapses the baseline from `311.75` to `74.75`.
Nothing looks broken. That is the whole point of the risk and now it is on paper with numbers.

### 31. It fixed a Task 2.0 defect I'd have shipped into the README

The one-liner documented in `docker-compose.yml` —
`dotnet ef database update -p src/Relay.Infrastructure -s src/Relay.Api` — does not run.
`EFCore.Design` is `PrivateAssets="all"` in Infrastructure, so it never reaches `Relay.Api`:
*"Your startup project 'Relay.Api' doesn't reference Microsoft.EntityFrameworkCore.Design."*

Task 8.0 copies that block into the README. It corrected the comment and flagged it rather than
treating it as out of scope. I'd rather it had asked, but it told me plainly, the change is one line,
and the alternative was a documented command that fails for whoever runs it first.

### 32. Scope it added that I did not ask for — and am keeping

`ActivityBaselineServiceTests`, 11 cases. Not in the subtask list. It covers the fetch span at all
three window lengths, the per-account anchor, the location filter, and the three outcomes. Given that
4.0 is the part with *no* integration coverage by decision, adding unit coverage for the composition
layer is the right instinct. Test count 47 → 58, all green.

### Specific moments I overrode or corrected the agent — session 6

16. **Nothing overridden.** Fourth session in a row. The corrections that mattered were the agent's
    own: a spec inconsistency it surfaced instead of silently resolving (28), a wrong test expectation
    its own suite caught (29), and a Task 2.0 command that never worked (31).
17. **Where it was right and the spec wasn't (28):** the techspec's code block and the task file's
    requirement genuinely contradict each other on `FromSql` vs `FromSqlRaw`. Fourth session running
    where the problem was in something I'd already approved.
18. **What I'd still call unproven:** the `AT TIME ZONE` GROUP BY has now been hand-verified three
    ways, but it still has no automated test. Every figure in `4_task.md` § Verification record is a
    point-in-time capture. If the seed or the migration changes, nothing fails — the record just
    silently stops being true. The Testcontainers fixture named in techspec § Standards Compliance is
    still the fix, and 4.0 raises its value rather than lowering it.

### Still to verify — added this session

- [ ] The verification record in `4_task.md` is a snapshot, not a test. Re-run it if schema or seed
      moves, or replace it with the Testcontainers fixture.
- [ ] `BaselineOutcome.UnknownLocations` assumes Task 5.0 turns it into a 400 naming the offenders —
      check 5.0 actually does, rather than flattening it to a bare 400.
- [ ] The empty-account window (account 20) falls back to *today in the account's zone* via
      `TimeProvider`. Nothing in the PRD or techspec specifies what `windowStart`/`windowEnd` should
      be when there is no data; this is an assumption, documented here and in the code. Confirm the
      response DTO in 5.0 presents it sensibly rather than implying data was examined.

### Friction worth noting

- `bash` heredoc truncation again, twice, exactly as sessions 1, 2 and 5 recorded. The agent switched
  to the file-writing tool after the first failure each time. Fourth session with the same friction —
  at this point it is a property of the environment, not an accident.
- No Python on this machine (`python` resolves to the Microsoft Store stub). Caught on first use, cost
  one round trip, worked around with `sed` and the file tools.

---

## Session 7 — 2026-09-26 — Task 5.0: Endpoint, DTOs, validation, ProblemDetails, logging

### Tools this session

Claude Code (Opus 5). Real database (the same Postgres 16 container from session 6, still carrying
the migration and the seed), the API actually run and curled. Context7 is still named in the
`/execute-task` template and still not configured here, so the agent fell back to `WebSearch` and
`WebFetch` against the Microsoft docs. No other model.

### 33. Invocation — accepted

**Prompt:** `/clear`, then `/execute-task c:\Dev\take-home\qualitara\tasks\prd-activity-baseline\5_task.md`

Cleared context again. It read `5_task.md`, `tasks.md`, the techspec's § API Endpoints / § Monitoring
/ § Testing Approach / § Standards Compliance, the PRD's FR list, then every existing file in
`Relay.Application` and the whole of `Program.cs`, `ServiceCollectionExtensions` and the csprojs
before writing anything.

### 34. It read the docs for something I would have let it guess at

Two lookups, both load-bearing:

- whether an MVC controller action can return `Results<Ok<T>, BadRequest<ProblemDetails>, NotFound>`
  at all (the techspec prescribes that signature, but typed results are a minimal-API idiom). Answer:
  yes, `IResult` returns are supported in controllers, but the union's own endpoint metadata is *not*
  read by ApiExplorer — so Swagger needs explicit `[ProducesResponseType]`. It added them.
- whether `app.UseExceptionHandler()` with no arguments works when `AddProblemDetails()` is
  registered. It does. Without that check the parameterless call is a startup crash in older
  versions, and the agent said as much before looking.

### 35. It flagged the techspec's own sample payload as wrong — **not overridden**

The success criteria say `Site O` must match the sample payload's figures. It does on recent (`9`),
baseline (`21.25`), percent (`-57.65`) and band (`21.69`) — but the sample's
`baselineOutlier.sharePercent` is `41.2` and the real value is `31.76`. The agent did not quietly
match the spec or quietly ignore it: it pointed out that `31.76` is what the algorithm produces, that
Task 4.0's reconciliation record already says `31.76`, and that the sample predates the calculator.
Recorded as decision 3 in `5_task.md`. My call: the sample is illustrative, the code wins, and the
disagreement stays visible rather than being patched out of the techspec after the fact.

### 36. It put arithmetic in the domain layer rather than the mapper — agreed

`summary.absoluteDelta` and `summary.percentDelta` are in the sample payload but were not on
`BaselineReport`. The obvious move is to subtract and divide in the DTO mapper, which is three lines.
It instead added them as computed properties on `BaselineReport`, with the zero-baseline guard beside
the same guard for rows, on the grounds that PRD FR12 keeps arithmetic out of the controller's layer.
That is the right reading of a rule that most people would have treated as being about aggregation
only. It touched a file from an already-committed task to do it, and said so.

### 37. Scope it added that I did not ask for — and am keeping

`BaselineResponseMapperTests`, nine cases: the account 6 payload, the summary line, all five
snake_case verdict strings, the two verdicts where both percentages must be absent, the account-20
empty report, and rounding at the boundary. Not in the subtask list — 5.9 asks only for hand
verification. It also added a `Relay.Api` project reference to the test project to make that possible.
Test count 63 → 72. The snake_case strings are the frontend's contract, so having them pinned by a
test rather than by a curl transcript is worth the reference.

### 38. The hand check caught two things no test would have

Subtask 5.9 is hand verification, and it earned its place twice — both in the *documented* contract
rather than the behaviour:

- the `200` response was published in `swagger.json` as `text/plain`, because an `IResult`-returning
  action gives ApiExplorer no content type. Fixed with `[Produces]`.
- the query parameters were published as `WindowDays` and `Locations`, not `windowDays` and
  `locations`, because the binding source name defaults to the property name. Binding is
  case-insensitive, so every curl passed the whole time; only the contract the frontend reads was
  wrong. Fixed with `[FromQuery(Name = ...)]`.

Both were found by looking at the generated OpenAPI document, which is exactly the class of drift the
"inspect it by hand" subtasks exist for.

### 39. It answered both open items session 6 left for 5.0

- `BaselineOutcome.UnknownLocations` becomes a `400` that names the offender
  (`"Account 6 has no location named Site Z."`), not a flattened bare 400. ✔
- account 20's window, which falls back to *today in the account's zone* because the account has no
  anchor in its data, is presented as `windowStart`/`windowEnd` alongside zero totals and
  `percentDelta: null`. It is honest — nothing implies data was examined — but it is still an
  assumption with no requirement behind it, and it is now visible in a real response body
  (`2026-08-28 .. 2026-09-26`) rather than only in a code comment.

### 40. Things it removed from `Program.cs` — accepted, but they are removals

`UseHttpsRedirection` and `UseAuthorization`, both scaffolded by `dotnet new webapi`. The reasoning
given: compose serves plain HTTP so a redirect breaks the documented URLs, and there is no
authorization layer to run. I agree on both, and I want it on the record that the scaffold was edited
rather than left alone, because "the template put it there" is the usual reason such lines survive.

It also left Swagger ungated by environment, deliberately. Fine for a throwaway dataset with no auth;
it would not be fine in anything real, and the comment in `Program.cs` says so.

### Specific moments I overrode or corrected the agent — session 7

19. **Nothing overridden.** Fifth session running. Again the corrections that mattered were its own:
    a sample payload in the techspec that disagrees with the algorithm (35), and two OpenAPI contract
    defects it found by inspecting the generated document rather than trusting that its curls passing
    meant the contract was right (38).
20. **Where I would have accepted something worse:** the summary deltas (36). Three lines in the
    mapper would have passed every criterion in the task file and quietly put domain arithmetic in
    the API project.
21. **What I'd still call unproven:** everything about this endpoint's *wiring* is verified by a
    transcript, not a test. The status codes, the ProblemDetails shapes, the CORS header and the
    `AT TIME ZONE` GROUP BY underneath all have zero automated coverage. Only the mapping does, as of
    this session. The Testcontainers fixture named in techspec § Standards Compliance now covers two
    tasks' worth of manual verification and is still the single highest-value addition.

### Still to verify — added this session

- [ ] The `400` bodies from the controller are served as `application/json`, not
      `application/problem+json`, because `TypedResults.BadRequest<ProblemDetails>` does not set the
      problem content type. The prescribed signature was kept over the header (decision 4 in
      `5_task.md`). If a client ever content-negotiates on it, that is the thing that breaks.
- [ ] The shaped `500` was demonstrated in `Production` only. In `Development` the framework's own
      developer exception page sits ahead of `UseExceptionHandler`, so the local experience is an HTML
      stack trace. Confirm Task 8.0 runs the compose `api` service in an environment where the shaped
      response is what the SPA actually sees.
- [ ] `Frontend:AllowedOrigins` defaults to `http://localhost:4200` in `appsettings.json`. Task 8.0
      must override it if the compose `web` service is served from any other origin, or the browser
      will block every request with no server-side error to look at.

### Friction worth noting

- `bash` heredoc truncation for the third time in three code sessions, on the largest block of the
  session (the verification record). Switched to the file-writing tool immediately. At this point the
  agent should be defaulting to the file tool for anything over ~50 lines instead of trying the
  heredoc first.
- Windows file locking: `dotnet build` failed twice with MSB3027 because the API was still running in
  the background holding `Relay.Api.exe`. Resolved with `taskkill`. Worth knowing for Task 8.0, where
  the same collision will happen between a running container and a local build.

---

## Session 8 — 2026-09-27 — Task 7.0: URL-backed state, loading/empty/error states, frontend tests

### Tools this session

Claude Code (Opus 5). Real Postgres 16 container (still seeded from session 6), the API actually run
(`ASPNETCORE_ENVIRONMENT=Development`) and curled, `ng serve` on 4200. Context7 still not configured;
the agent used `WebFetch` against angular.dev and one `WebSearch`. **Playwright**, added mid-session
at my instruction — installed into the session scratchpad, not into `frontend/package.json`.

### 41. Invocation — accepted

**Prompt:** `/clear`, then `/execute-task c:\Dev\take-home\qualitara\tasks\prd-activity-baseline\7_task.md`

It read `7_task.md`, `tasks.md`, the whole PRD, the techspec's § Frontend / § Testing Approach /
§ API Endpoints, then every file task 6.0 produced plus the jest config and tsconfigs before writing.

### 42. It refused both options the task file offered — **not overridden**

`7_task.md` said the bootstrap wrinkle could be solved either way: "send the first request with the
window plus the raw location list, then reconcile" or "request unfiltered and filter client-side for
display only. Either is fine; pick one." The agent took neither and said why, then proved both halves
against the running API rather than asserting them:

- raw list first → `?locations=Site O,Nowhere` → `400 "Account 6 has no location named Nowhere."`,
  which the same task file forbids two lines later.
- client-side display filtering → the endpoint computes `summary` over the *filtered* subset
  (`?locations=Site O` returns a summary of `9 / 21.25`, not the account's `175 / 311.75`), so the
  summary line would contradict the table.

It shipped a third option: first request carries the window only, names come back, the URL's list is
intersected against them, filtered request follows. I accept it. The argument I did not anticipate and
that settles it: that discovery request is needed *anyway*, because a filtered response only contains
the locations asked for, so a filtered deep link would otherwise never learn the account's other
location names and could never offer them as checkboxes.

This is the first time in eight sessions the agent has declined a binary the task file handed it and
been right on both branches.

### 43. The build caught a bug the test run could not

`npm test` was green — 55/55 — while `npm run build` failed with four TS7022/TS7024 errors: the
signal graph is circular by nature (`params` → request → response → known names → `filter` →
`params`), and TypeScript cannot infer a type around a circle, so it silently degraded the whole
chain to `any`. Jest passed because `jest-preset-angular` transpiles without full type checking.

Fixed with one explicit annotation (`private readonly view: Signal<PageView>`). Worth recording as a
standing fact about this repo: **a green `npm test` is not evidence that the frontend type-checks.**
`angular.md` bans `any`; only the build enforces it.

### 44. It designed against a feedback loop I would not have seen until it hung

The same circularity is a live hazard, not just a typing one: each response produces a new
`knownLocations` array, which recomputes the filter, which recomputes the request params, which
fires another request, for ever. It guarded it twice, unprompted — `merge()` returns the *previous*
array when a response adds no new name, and the `params` computed carries a value-based `equal` — and
wrote a test ("stops requesting once the filtered response has settled") whose only job is to fail if
that circle ever reopens.

### 45. Playwright — my redirection, mid-turn

**Prompt:** `Try do the tests using playwright`

It had reached the four manual subtasks (7.4, 7.6, 7.7, 7.11), found no browser automation available,
and was about to hand me a script to run by hand. I told it to use Playwright instead. It installed
Playwright and Chromium **into the scratchpad rather than `frontend/package.json`**, unprompted,
reasoning that the techspec calls for no e2e suite and a verification harness is not a deliverable.
That is the call I would have made.

12/12 checks passed against the real API: loading state with the response delayed 1s; error state
naming the ProblemDetails detail and recovering on retry; account 20's empty state; tab order
(30/60/90 → Site A → Site B → Site C) and a real `3px solid rgb(11,87,208)` focus ring; reload
restoring URL, rows, radio and the unticked location; Back leaving the app with `history.length=2`
after two filter changes; `?window=13&locations=Nowhere` issuing exactly one request, `?windowDays=30`.

### 46. Its first browser run reported a failure that was its own harness

First pass came back 11/12 with "reload restores the same view from the URL" failing. It did not
touch the component. It wrote a diagnostic script, found the table had **0 rows before** the reload,
and identified the cause as its own wait condition: `networkidle` is not a settle condition for this
page, because the gap between the discovery request and the filtered request looks idle. Second
defect in the same check — the failure message was a hardcoded string, so it could not say which of
three sub-conditions had failed. It fixed both and re-ran to 12/12.

The instinct I wanted and got: a failing check is not automatically a failing product. Both defects
are recorded in `7_task.md` rather than quietly fixed.

### Specific moments I overrode or corrected the agent — session 8

22. **Playwright.** It was going to hand me a manual script; I told it to drive the browser instead.
    It did, and kept the dependency out of the repo on its own.
23. Nothing else. No rejected output this session, which is itself worth noting after seven sessions
    where there always was something.

### Still to verify — added this session

- [ ] The account-20 fixture's `windowStart`/`windowEnd` are **2026-08-28 → 2026-09-26**, not the
      seed's July dates: with no events there is no latest local event date, so the API falls back to
      the injected clock. That is correct behaviour and the fixture says so, but it means the fixture
      is date-sensitive in a way the account-6 one is not. If a future session re-captures it, the
      dates will move.
- [ ] `locations` is sent as a comma-separated value containing spaces (`Site B,Site C`). It
      round-trips correctly through `HttpParams` and through a real browser, but any location name
      containing a literal comma would break the encoding. No seed name does; nothing enforces it.
- [ ] The two-request bootstrap on a filtered deep link is visible in the network tab. Task 8.0's
      README should say so, or it reads as a bug to anyone watching.

### Friction worth noting

- The `<caption>` is clipped at a 380px viewport, because a `<caption>` is as wide as its table and
  the table carries `min-width: 40rem`. Seen in the screenshot, left alone deliberately — visual
  polish beyond legibility is out of scope — but it was *seen*, which is the point of looking.
- `dotnet run` failed to start until `ASPNETCORE_ENVIRONMENT=Development` was set: the connection
  string lives in `appsettings.Development.json` and the base file deliberately carries an empty one.
  `--no-launch-profile` skips the launchSettings that would have set it. Task 8.0 will hit this.
- Third consecutive session where the agent reached for a Python one-liner to patch a file on a
  machine with no Python installed, then fell back to the edit tool. It should stop trying.

---

## Session 9 — 2026-09-27 — Task 8.0: Docker Compose, README, reconciliation, deviations

**Prompt:** `/execute-task c:\Dev\take-home\qualitara\tasks\prd-activity-baseline\8_task.md`

Nothing else. One slash command, and the last task in the list.

### What I let it decide, and what I checked

The task said three services. It came back saying three cannot satisfy the success criterion, because
Task 2.0's ordering decision (migrations own the schema, seed afterwards) puts two host commands
between `docker compose up` and a dashboard with data — and it proposed two one-shot services,
`migrate` and `seed`, chained with `service_completed_successfully`.

I accepted that. It is the first time this session that the agent argued *against* the literal task
text and was right to: three services would have meant a README saying "now run these two commands",
which is exactly the "no manual steps" the criterion rules out. It also considered
`context.Database.Migrate()` on API startup and rejected it on its own — schema authority in the
request path, and it doesn't solve the seed anyway. That reasoning is in `8_task.md`.

What I did check, because the argument was convenient: that the seed guard actually guards. It reads
`count(*) FROM activity_events` and skips when non-zero. Proven by running `docker compose up -d`
twice and reading the second `seed` log — `already holds 12626 rows — nothing to load.` A seed that
inserts explicit primary keys would otherwise fail the second `up`, and "it's idempotent" is the kind
of claim I've had to take back before.

### The thing this task was for

`docker compose up` failed on `npm ci`, and the first diagnosis was wrong:

> "the container's npm is older than the host's — `node:22-alpine` ships npm 10, the host has npm 11"

It switched the image to `node:24-alpine` and the build failed identically. Only then did it run
`npm ci --dry-run` **on the host** — which failed the same way. The committed `package-lock.json` had
been out of sync with `package.json` since Task 1.0, and nothing had ever run `npm ci` to find out,
because `npm install` repairs the tree in place and says nothing.

So the README's documented install command had never worked from a clean checkout, through seven
tasks, including a session that reported `npm test` green fifty-five times. The tests were green. The
install path was broken. Those are different claims and I had been reading them as one.

This is the second genuine defect found by *running the documented command as written* rather than by
reading code (the first was Task 4.0's narrowed fetch span). Subtask 8.10 exists for exactly this and
it paid for itself.

The fix needed two passes, and the agent did not pretend otherwise: `npm install --package-lock-only`
on Windows fixed the host-side entries and the container still failed on `@emnapi/core@1.11.3` —
a Linux-only optional dependency that npm on Windows never resolves and so cannot write. It
regenerated the lock inside `node:24-alpine`. I asked for the diff shape before accepting it: 72
`win32` references before and after, no Angular version changes. Then host `npm ci` → 777 packages,
build clean, 55/55; container build clean.

### Specific moments I overrode or corrected the agent — session 9

24. **The wrong first diagnosis stayed in the record.** It wanted to describe the npm failure as an
    image-version problem, which is what it had believed for two builds. I made it write both: the
    theory, that it was wrong, and what the cause actually was. `8_task.md` now says "That was not the
    cause" in its own words.
25. **It wrote a comment inside a `RUN` line continuation** in `frontend/Dockerfile`. It works — the
    parser strips comment lines before joining — but it reads as a mistake to anyone reviewing the
    file, and a reviewer pausing on a line that's fine is a cost. Moved above the instruction.
26. **I would not take "the dashboard works" from a `curl`.** It had 200s from nginx, the right API
    base URL inside the served bundle, and CORS headers, and offered that as 8.4 complete. A bundle
    that loads is not a table that renders. Playwright (browsers still cached from Task 7, dependency
    still not in `package.json`) against the containers: 15 rows, `Site O` first at −57.65%, deep link
    restoring 90 days and one location through the nginx fallback, `?window=13` falling back to 30, no
    console errors.
27. **Third session in a row it reached for Python**, on a machine that still has no Python. It also
    lost a large heredoc to shell quoting and fell back to the file-write tool. Cost a minute each time.

### Still to verify — added this session

- [ ] The lockfile was regenerated by npm **11.19** (in the container); the host runs npm **11.6**.
      `npm ci` works on both, but the next `npm install` on the host may churn the file again. Nothing
      pins the npm version — `package.json` has no `engines` field, and adding one wasn't in scope.
- [ ] The `web` image bakes `API_BASE_URL` at build time, so a deployment to any other origin needs a
      rebuild, not a restart. Fine here; it is a real constraint and only the Dockerfile comment and
      the README say so.
- [ ] `migrate` is an SDK image — roughly 1 GB, local-only. Nobody should mistake it for a shipping
      artefact. It is the default-target-adjacent stage in `backend/Dockerfile`, so `docker build
      ./backend` with no `--target` still gives the slim `api` image. Worth not breaking.
- [ ] `reconcile.mjs` hardcodes account 6, the 30-day window, and the figures. That is the point — it
      is a fixture, not a tool — but if the seed is ever replaced, it fails loudly rather than
      adapting, and the failure will look like a bug in the API.

### Friction worth noting

- The success criterion "no manual steps beyond what the README lists" is self-defeating as written:
  any number of manual steps satisfies it if the README lists them. The agent read past the letter to
  the intent and built the zero-step version. I'd rather it did that than lawyer the sentence, but it
  is worth noting that it *chose* which reading to honour.
- `CLAUDE.md` still opened with "the repository contains specs only — no application code has been
  written". False for seven tasks. The agent corrected it as part of this task and flagged that it
  had done so, which is right — but it had been reading that paragraph at the start of every session
  and never once mentioned it contradicted the files in front of it.
- Five containers, and the only one with a healthcheck is `db`. `api` and `web` have none, so
  `docker compose ps` says "Up" for an API that may still be starting. Not required by the task, not
  added. Noting it rather than pretending "Up" means ready.
