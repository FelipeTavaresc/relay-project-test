### 🏢 Product background

**Relay** is a fictional B2B SaaS that helps service businesses track inbound customer activity — calls, leads, appointments — across their locations. Customers range from single-site shops to groups with 15+ locations. Each customer gets a **reporting dashboard**: today it shows raw totals per location and not much else.

Two things the team keeps hearing:

- Account managers say customers ask *"is this number normal for us?"* and can't answer it from the dashboard.
- The support team says customers with multiple locations struggle to spot which location needs attention.

The team works spec-driven and AI-first: engineers get tickets like the one below, build a plan, and implement with AI agents by default.

### 🎫 The ticket

> **DASH-247 — Help customers understand whether their recent activity is normal**

Customers looking at their dashboard can't tell whether this week's numbers are good, bad, or typical for them. Product wants the dashboard to help a customer answer *"is this normal for us?"* at a glance, without exporting data or calling their account manager.

**Notes from product:** probably involves comparing against some baseline, multi-location customers matter, a customer admin should be able to look at this Monday morning and act on it.

**Out of scope per product:** alerting/notifications, ML/forecasting.
> 

The ticket is deliberately light — that's realistic, not an oversight. Deciding what it actually means (what "normal" is, what window, what view) is part of the exercise. Your interpretation and scoping decisions are **evaluated criteria**, not just tolerated variance.


### ✅ What to build

Your interpretation of the ticket is yours to make and defend, but every submission must contain this skeleton:

- **Backend:** at least one API endpoint that performs **real aggregation over the seed data** (not pass-through queries), serving your dashboard feature.
- **Frontend:** an SPA view that consumes it, with at least one piece of **user-controlled input** (filter, date range, location selector, toggle — your call) whose **state survives a page reload**.
- **Tests that run**, with a one-line note in the README on how to run them.
- **A relational database with a real schema and migrations** (your stack's standard tooling), loaded from the provided seed data.
- Backend and frontend should be in different folders;
- Create a docker files and docker compose to start the application with the needed requirements


**Stack (required):** build this in **.NET 8+ (C#)** on the backend and **Angular** on the frontend. This is the stack the team you'd be joining works in, and the exercise is evaluated in it — **submissions in another stack can't be evaluated for this role.** The database is flexible: any relational DB is fine (SQL Server is what the team runs, so it's a nice touch — but don't burn your time budget on DB setup). Tell us in the README about the notable choices you made within the stack (ORM vs. raw SQL, state management approach, etc.) and why.

### 🌱 Seed data

Schema + seed dataset (two tables: `accounts` and `activity_events`) are provided in a starter repository:

**https://github.com/Qualitara/relay-analytics-starter**

**Copy its contents into a fresh repository of your own** (please don't fork — forks are publicly linked to the source repo), and **give your repo a name of your own choosing** rather than keeping `relay-analytics-starter`. A distinct name keeps your submission from sitting alongside everyone else's in search results.

### 🚫 Explicitly out of scope

Skip these so they don't eat your budget. We won't penalize their absence:

- **Auth / login** — assume a single trusted customer admin; a hardcoded identity is fine.
- **Alerting / notifications and ML / forecasting** — per the ticket.
- **Production infra** — no Kubernetes or CI required. Local run is fine.
- **Visual polish** — function over form. Unstyled-but-clear beats pretty-but-broken.


### Important

**An AI interaction log** with as much raw detail as possible: actual prompts and model responses, where you **accepted, rejected, or redirected** the AI and why, and a short reflection naming **specific moments** where you caught the AI being wrong or chose to override it. Please also include one honest line on **which tools/models you used and for what** (authoring vs. reviewing vs. not at all)

**AI interaction log** (`AI_LOG.md`) — as described above, plus any committed agent-context artifacts.
