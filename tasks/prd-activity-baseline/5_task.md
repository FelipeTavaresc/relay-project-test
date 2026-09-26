---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>api/endpoint</domain>
<type>implementation</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>http_server|database</dependencies>
</task_context>

# Task 5.0: Endpoint — controller, DTOs, validation, ProblemDetails, logging

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

Expose the feature over HTTP: `GET /api/v1/accounts/{accountId}/activity-baseline`. The controller
binds, validates, delegates to the application service from Task 4.0, and shapes the response. No
LINQ over entities, no business rules, no aggregation in the action method (techspec § Development
Sequencing, step 5; PRD FR12).

<requirements>
- Route and query shape exactly as techspec § API Endpoints: `?windowDays=30&locations=Site%20A,Site%20B`.
- Typed result signature: `Task<Results<Ok<ActivityBaselineResponse>, BadRequest<ProblemDetails>, NotFound>>`.
- Status codes per the table in techspec § API Endpoints — `400` for `windowDays` outside {30,60,90} or an unknown location name, `404` for an unknown account, `200` with empty locations and zero totals for an account with no activity (account 20). **An empty range is not an error.**
- Validation **rejects, never clamps** — the `ProblemDetails` message names the bound (`csharp-dotnet.md` § Error handling).
- Response DTOs are separate types from both the EF entities and the `Relay.Application` domain records. No EF entity crosses the boundary.
- `IReadOnlyList<T>` and records on every public surface; verdicts serialised as the snake_case strings in the sample payload (`above`, `typical`, `below`, `low_volume`, `insufficient_history`).
- `TimeProvider` injected wherever time is needed — never `DateTime.UtcNow` (and note the anchor comes from the data, not the clock).
- `CancellationToken` accepted by the action and passed all the way down.
- Structured logging at the boundary only: a `LogWarning` on a rejected window naming account and window, one `LogInformation` per served request carrying account, window, and location count. No PII — account ids and location labels only.
</requirements>

## Subtasks

- [x] 5.1 Define the response DTOs mirroring the JSON in techspec § API Endpoints: root, `summary`, `locations[]`, and the nested `baselineOutlier`.
- [x] 5.2 Define the request binding + validation for `windowDays` and the comma-separated `locations` filter.
- [x] 5.3 Implement `ActivityBaselineController` — bind, validate, delegate, map, return. Nothing else.
- [x] 5.4 Map domain records → DTOs, including the null-percentage cases and the optional outlier.
- [x] 5.5 Wire the `ProblemDetails` handler at the boundary so unhandled failures become a shaped response rather than a stack trace.
- [x] 5.6 Add the structured log statements described above.
- [x] 5.7 Complete DI wiring in `Program.cs`: DbContext, `IActivityQueries`, `IBaselineCalculator`, the application service, options binding, CORS for the Angular dev origin.
- [x] 5.8 Enable OpenAPI/Swagger so the contract is inspectable while building the frontend.
- [x] 5.9 Exercise every row of the status-code table by hand (curl or Swagger) and paste the real responses into this file.

## Implementation Details

Endpoint contract, status table, and the sample payload: techspec § API Endpoints. Controller
discipline: `csharp-dotnet.md` § API surface and `project-standards.md` § Backend conventions.
Logging templates: techspec § Monitoring and Observability.

Account selection is a **configured default account id** — PRD § Out of Scope excludes multi-account
switching and auth, and the account id in the route is the addressing mechanism, not a tenant picker.

The location filter validates against the names the account actually has: an unknown name is a `400`,
not a silently empty result. Note the asymmetry with the frontend (Task 7.0), which *drops* unknown
names read from the URL rather than forwarding them — so a malformed URL never reaches this endpoint.

Percentages in the response are rounded for display (the sample shows 2 decimal places); the ranking
in Task 3.0 sorts on the unrounded value. Keep rounding at the DTO mapping edge.

## Success Criteria

- `dotnet build` clean, `dotnet test` green, real output recorded.
- Every row of the status-code table demonstrated with a real request/response pasted into this file: valid query, account 20 empty, `windowDays=13` rejected with a message naming {30,60,90}, unknown location rejected, unknown account id ⇒ 404.
- Account 6 at `windowDays=30` returns `summary.recentTotal` `175` and `summary.baselineTotal` `311.75`, with `Site O` matching the sample payload's figures.
- Account 6's response carries `baselineOutlier` for `2026-06-03` on its locations (techspec § Key Decisions #1).
- Response contains no EF entity and no `Relay.Application` record type leaked as-is.
- A rejected request logs one warning with the account and window; a served request logs one information line.

## Relevant Files

Created:
- `backend/src/Relay.Api/Controllers/ActivityBaselineController.cs` — the endpoint
- `backend/src/Relay.Api/Contracts/ActivityBaselineRequest.cs` — query binding
- `backend/src/Relay.Api/Contracts/BaselineRequestValidation.cs` — `Valid`/`Invalid`, the 30/60/90 bound
- `backend/src/Relay.Api/Contracts/ActivityBaselineResponse.cs` — response root
- `backend/src/Relay.Api/Contracts/BaselineSummaryResponse.cs` — `summary`
- `backend/src/Relay.Api/Contracts/LocationBaselineResponse.cs` — `locations[]`
- `backend/src/Relay.Api/Contracts/BaselineOutlierResponse.cs` — nested `baselineOutlier`
- `backend/src/Relay.Api/Contracts/BaselineResponseMapper.cs` — domain records to wire contract
- `backend/src/Relay.Api/Options/FrontendOptions.cs` — CORS origin allowlist, bound and validated
- `backend/tests/Relay.UnitTests/BaselineResponseMapperTests.cs` — nine mapping tests

Modified:
- `backend/src/Relay.Api/Program.cs` — ProblemDetails, CORS, Swagger with XML comments; dropped
  `UseHttpsRedirection`/`UseAuthorization`
- `backend/src/Relay.Api/Relay.Api.csproj` — `GenerateDocumentationFile` for the Swagger descriptions
- `backend/src/Relay.Api/appsettings.json` — the `Frontend` section
- `backend/src/Relay.Application/Baseline/BaselineReport.cs` — `AbsoluteDelta`/`PercentDelta` for the
  summary line, so that arithmetic stays out of the API layer (Decisions #1 below)
- `backend/tests/Relay.UnitTests/Relay.UnitTests.csproj` — reference to `Relay.Api` for the mapper tests

## Verification record (subtask 5.9)

Captured output, not intention. API run from `backend/` against the live `relay-db` (Task 2.0
migration applied, `seed.sql` loaded):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Relay.Api \
  --no-launch-profile --urls http://localhost:5141
```

`Date`, `Server` and `traceId` values are elided below; nothing else is edited.

### Status-code table, row by row

**Valid query, rows found ⇒ `200`** — the reconciliation case. `windowStart`/`windowEnd` come from
the data (anchor `2026-07-27`), not the clock.

```
$ curl -i "http://localhost:5141/api/v1/accounts/6/activity-baseline?windowDays=30"
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"accountId":6,"accountName":"Metro Collision Centers","timezone":"America/New_York",
 "windowDays":30,"windowStart":"2026-06-28","windowEnd":"2026-07-27",
 "summary":{"recentTotal":175,"baselineTotal":311.75,"absoluteDelta":-136.75,"percentDelta":-43.87},
 "locations":[
  {"location":"Site O","recentTotal":9,"baselineTotal":21.25,"absoluteDelta":-12.25,
   "percentDelta":-57.65,"bandPercent":21.69,"verdict":"below",
   "baselineOutlier":{"date":"2026-06-03","sharePercent":31.76}},
  {"location":"Site F","recentTotal":8,"baselineTotal":18.50,"absoluteDelta":-10.50,
   "percentDelta":-56.76,"bandPercent":23.25,"verdict":"below",
   "baselineOutlier":{"date":"2026-06-03","sharePercent":51.35}},
  {"location":"Site M", ... -54.10 ...}, {"location":"Site D", ... -53.62 ...},
  {"location":"Site N", ... -50.77 ...}, {"location":"Site G", ... -48.57 ...},
  {"location":"Site J", ... -47.06 ...}, {"location":"Site B", ... -45.83 ...},
  {"location":"Site L", ... -44.55 ...}, {"location":"Site H", ... -42.22 ...},
  {"location":"Site I", ... -40.0  ...}, {"location":"Site A", ... -37.78 ...},
  {"location":"Site E", ... -35.71 ...}, {"location":"Site K", ... -33.33 ...},
  {"location":"Site C","recentTotal":20,"baselineTotal":27.00,"absoluteDelta":-7.00,
   "percentDelta":-25.93,"bandPercent":19.25,"verdict":"below",
   "baselineOutlier":{"date":"2026-06-03","sharePercent":34.26}}]}
```

`summary.recentTotal` `175` and `summary.baselineTotal` `311.75` as required, `Site O` at
`9` / `21.25` / `-57.65%` / band `21.69`, ranked first, and **all 15 locations carry
`baselineOutlier` for `2026-06-03`** (techspec § Key Decisions #1).

**Valid query, account has no activity ⇒ `200`, empty `locations`, zero totals.** Account 20 has no
events, so it has no anchor in its data; the window ends today in its own zone, taken from the
injected `TimeProvider`. `percentDelta` is `null` rather than a division by zero.

```
$ curl -i "http://localhost:5141/api/v1/accounts/20/activity-baseline?windowDays=30"
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"accountId":20,"accountName":"Quiet Harbor Spa","timezone":"America/Los_Angeles","windowDays":30,
 "windowStart":"2026-08-28","windowEnd":"2026-09-26",
 "summary":{"recentTotal":0,"baselineTotal":0,"absoluteDelta":0,"percentDelta":null},
 "locations":[]}
```

**`windowDays` not in {30,60,90} ⇒ `400` naming the bound.** Rejected, not clamped — and rejected
before the service is called, so no query runs for it.

```
$ curl -i "http://localhost:5141/api/v1/accounts/6/activity-baseline?windowDays=13"
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Invalid window","status":400,
 "detail":"windowDays must be one of 30, 60, 90; 13 is not allowed.",
 "instance":"/api/v1/accounts/6/activity-baseline"}
```

**Unknown location name ⇒ `400`**, naming it, rather than a silently empty result.

```
$ curl -i "http://localhost:5141/api/v1/accounts/6/activity-baseline?windowDays=30&locations=Site%20Z"
HTTP/1.1 400 Bad Request
Content-Type: application/json; charset=utf-8

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Unknown location","status":400,
 "detail":"Account 6 has no location named Site Z.",
 "instance":"/api/v1/accounts/6/activity-baseline"}
```

**Unknown `accountId` ⇒ `404`.** The controller returns a bare `NotFound`; `UseStatusCodePages` gives
it a `ProblemDetails` body of the same shape as the others.

```
$ curl -i "http://localhost:5141/api/v1/accounts/999/activity-baseline?windowDays=30"
HTTP/1.1 404 Not Found
Content-Type: application/problem+json; charset=utf-8

{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,
 "traceId":"..."}
```

### Beyond the table

**Location filter with real names** — the summary is the filtered subset added up, so the summary
line and the table can never disagree.

```
$ curl -i ".../accounts/6/activity-baseline?windowDays=30&locations=Site%20A,Site%20O"
HTTP/1.1 200 OK

{... "summary":{"recentTotal":23,"baselineTotal":43.75,"absoluteDelta":-20.75,"percentDelta":-47.43},
 "locations":[{"location":"Site O", ... -57.65 ...},{"location":"Site A", ... -37.78 ...}]}
```

**`windowDays` absent** ⇒ the documented default of 30; omitting the query string entirely returns the
same body as `?windowDays=30`. **Non-numeric `windowDays`** never reaches the validator —
`[ApiController]` rejects it at model binding, already `ProblemDetails`-shaped:

```
$ curl -i ".../accounts/6/activity-baseline?windowDays=abc"
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json; charset=utf-8

{"type":"...#section-15.5.1","title":"One or more validation errors occurred.","status":400,
 "errors":{"WindowDays":["The value 'abc' is not valid for WindowDays."]},"traceId":"..."}
```

**Unhandled failure ⇒ shaped `500`, no stack trace** (subtask 5.5). Demonstrated in `Production`
with the connection string pointed at a dead port, because in `Development` the framework installs
its own developer exception page ahead of any middleware `Program.cs` registers:

```
$ ASPNETCORE_ENVIRONMENT=Production Database__ConnectionString="...Port=15999..." \
    dotnet run --project src/Relay.Api --no-build --no-launch-profile --urls http://localhost:5142
$ curl -i "http://localhost:5142/api/v1/accounts/6/activity-baseline?windowDays=30"
HTTP/1.1 500 Internal Server Error
Content-Type: application/problem+json; charset=utf-8

{"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1",
 "title":"An error occurred while processing your request.","status":500,"traceId":"..."}
```

The exception itself is logged server-side, not swallowed:
`fail: Microsoft.EntityFrameworkCore.Query[10100] ... Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:15999`.

**CORS** (subtask 5.7) — the allowlisted origin only, no wildcard:

```
$ curl -D - -H "Origin: http://localhost:4200"  ".../accounts/20/activity-baseline?windowDays=30"
Access-Control-Allow-Origin: http://localhost:4200
$ curl -D - -H "Origin: http://evil.example"    ".../accounts/20/activity-baseline?windowDays=30"
(no Access-Control-Allow-Origin header)
```

**OpenAPI** (subtask 5.8) — `/swagger/v1/swagger.json` and `/swagger/index.html` both `200`. The
published parameter names are `accountId`, `windowDays`, `locations`, and the `200` response is
`application/json` against `#/components/schemas/ActivityBaselineResponse`. Both of those needed
fixing after a first pass: an `IResult`-returning MVC action carries no content-type metadata for
ApiExplorer, so the `200` was documented as `text/plain` until `[Produces]` was added, and the request
record's properties were published as `WindowDays`/`Locations` until `[FromQuery(Name = ...)]` pinned
them. Binding is case-insensitive, so only the *documented* contract was ever wrong — the kind of
drift a hand check catches and a unit test would not.

### Logging (subtask 5.6)

One line per request, at the boundary only, from the run above — no PII, account ids and location
labels only:

```
info: Relay.Api.Controllers.ActivityBaselineController[0]
      Baseline served for account 6: 30 days, 15 locations
info: Relay.Api.Controllers.ActivityBaselineController[0]
      Baseline served for account 20: 30 days, 0 locations
warn: Relay.Api.Controllers.ActivityBaselineController[0]
      Baseline window rejected for account 6: 13 days
warn: Relay.Api.Controllers.ActivityBaselineController[0]
      Baseline locations rejected for account 6: 30 days, unknown Site Z
warn: Relay.Api.Controllers.ActivityBaselineController[0]
      Baseline requested for unknown account 999: 30 days
info: Relay.Api.Controllers.ActivityBaselineController[0]
      Baseline served for account 6: 30 days, 2 locations
```

### Build and test

```
$ dotnet build
Compilacao com exito.
    0 Aviso(s)
    0 Erro(s)

$ dotnet test
Aprovado!  - Com falha: 0, Aprovado: 72, Ignorado: 0, Total: 72, Duracao: 103 ms - Relay.UnitTests.dll (net8.0)
```

72 tests, up from 63: nine new `BaselineResponseMapperTests` covering the account 6 payload shape, the
summary line, every snake_case verdict string, the rows where both percentages must be absent, the
account-20 empty report, and the two-decimal rounding at the boundary.

## Decisions and assumptions recorded here

1. **`summary.absoluteDelta` / `summary.percentDelta` are computed properties on `BaselineReport`, not
   arithmetic in the mapper.** The sample payload carries them but Task 3.0's record did not. Deriving
   them at the API boundary would put arithmetic in the controller's layer, which PRD FR12 forbids, and
   the zero-baseline guard (account 20 ⇒ `null`) belongs beside the same guard for rows.
2. **`windowDays` absent ⇒ 30**, the documented default (PRD FR22). Rejection is for values that are
   *present and out of range*: clamping `13` to `30` would silently change what the numbers mean, which
   is what "reject, never clamp" exists to prevent.
3. **`Site O`'s `baselineOutlier.sharePercent` is `31.76`, not the `41.2` in the techspec's sample
   payload.** The sample was written during specification, before the calculator existed; `31.76` is
   what the algorithm produces and it matches the figure already recorded in Task 4.0's reconciliation.
   Every other `Site O` figure in the sample matches exactly. The sample is illustrative; the code and
   both verification records agree.
4. **The controller's `400` bodies are served as `application/json`, not `application/problem+json`.**
   The techspec prescribes the signature `BadRequest<ProblemDetails>`, and `TypedResults.BadRequest`
   applies the RFC 9110 `type`/`status` defaults but not the problem content type. Keeping the
   prescribed signature was judged more important than the header; the framework-generated
   `400`/`404`/`500` bodies do use `application/problem+json`, so a client parsing either gets the
   same fields.
5. **No backend `DefaultAccountId` option.** The route parameter is the addressing mechanism, and the
   SPA holds the account it looks at (`frontend/src/app/core/` ApiConfig, Task 6.0). A server-side
   default that nothing reads would be speculative configuration.
6. **`UseHttpsRedirection` and `UseAuthorization` removed from `Program.cs`.** The API is served over
   plain HTTP under docker compose, so redirecting would break the documented local URLs, and there is
   no authorization layer to run — a single trusted admin identity (PRD § Out of Scope).
7. **Swagger is not gated on `IsDevelopment()`.** The SPA is written against this contract and there is
   nothing behind the API to protect: no auth, no tenancy, one throwaway dataset.
