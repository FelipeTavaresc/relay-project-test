# C# / .NET Standards

Backend rules for `/create-techspec`, `/create-tasks` and `/execute-task`.
Stack facts and the scope boundary live in [`project-standards.md`](./project-standards.md) —
this file is *how* to write the C#. Where the two overlap, `project-standards.md` wins.

Target: **.NET 8+**, nullable reference types on, warnings as errors in the solution's
`Directory.Build.props`.

## Design principles, concretely

SOLID is only useful when it changes a decision. These are the versions that do:

- **Single responsibility** — a class has one reason to change. A type named `…Manager`,
  `…Helper` or `…Utils` is almost always two types that haven't been separated yet.
- **Open/closed** — extend by adding a type, not by adding a `switch` arm to an existing one.
  A `switch` over a closed set that lives in exactly one place is fine; the same `switch`
  duplicated in three places is the smell.
- **Liskov** — a derived type must not tighten preconditions. No `throw new NotSupportedException()`
  in an override; if a member doesn't apply, the abstraction is wrong.
- **Interface segregation** — interfaces are defined by the *consumer's* need. An interface with
  one implementation and one caller earns its keep only if it exists to be faked in a test or
  to break a dependency direction; otherwise inject the concrete type.
- **Dependency inversion** — the aggregation/domain layer defines the abstractions it needs; EF Core,
  HTTP and the clock are details that plug into them.

Do not add an abstraction on speculation. Two real call sites, or a test that cannot be written
without it, is the bar for introducing an interface, a generic, or a base class.

## Project layout

Keep the seam between HTTP, business logic, and persistence visible — separate projects are the
clearest way, separate folders the acceptable minimum:

```
src/
  Relay.Api/            controllers, DTOs, DI wiring, Program.cs
  Relay.Application/    aggregation services, domain records, abstractions
  Relay.Infrastructure/ EF Core DbContext, configurations, migrations, queries
tests/
  Relay.UnitTests/
  Relay.IntegrationTests/
```

Dependency direction is one-way: `Api → Application → Infrastructure` for composition only —
`Application` must not reference `Infrastructure` types. If it needs one, it declares the interface.

## Naming and shape

- `PascalCase` for types, methods, properties, constants, and local `const`; `camelCase` for
  locals and parameters; `_camelCase` for private fields. No Hungarian notation, no `m_`.
- Async methods end in `Async`. Every `Task`-returning method takes a `CancellationToken` and
  passes it down — no exceptions for EF Core calls or `HttpClient`.
- Interfaces are `I`-prefixed and named for the role (`IActivityBaselineCalculator`), not the
  implementation (`IActivityBaselineCalculatorImpl`).
- No abbreviations that aren't domain vocabulary. `avg` and `acct` are not domain vocabulary.
- One public type per file, file named after the type.

## Immutability and types

- Records for anything that carries data and doesn't behave: DTOs, query results, value objects.
- `required` + `init` over constructors-with-ten-parameters; primary constructors for small services.
- `IReadOnlyList<T>` / `IReadOnlyDictionary<K,V>` on every public surface that returns a collection.
  Never return `List<T>` or an array from a public API, and never return `null` for "no rows" —
  return an empty collection.
- Prefer `DateOnly`/`TimeOnly` for calendar values and `DateTimeOffset` for instants. `DateTime`
  without a kind is a bug waiting for a timezone boundary.
- `decimal` for money and rates that are compared or displayed; `double` only for statistics where
  the loss is understood and documented.

## Methods and control flow

- Guard clauses first, happy path last, no `else` after a `return`. Nesting deeper than three
  levels means a method is hiding inside this one.
- A method does one thing at one level of abstraction. If you need a comment to mark sections
  inside it, those sections are methods.
- Comments explain *why*, never *what*. Delete commented-out code rather than shipping it.
- No magic numbers or strings: named constants, or `static readonly` where a `const` won't do.
- Pattern matching and switch expressions over chained `if`/`as`/`is` casting.

## Async and concurrency

- Async all the way down. Never `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — a deadlock
  or a swallowed `AggregateException` is the outcome, not a maybe.
- No `async void` outside event handlers. Return `Task`, and let exceptions propagate.
- Don't wrap synchronous work in `Task.Run` to look async.
- `IAsyncEnumerable<T>` for streaming a large result set; otherwise materialize once and return.

## Error handling

- Exceptions are for the exceptional. A missing account or an out-of-range window is an expected
  outcome — model it (a result type, a nullable return, a validation failure), don't throw.
- Never `catch (Exception)` to log-and-continue. Catch the specific type you can act on, or let it
  reach the boundary, where a `ProblemDetails`-shaped handler turns it into a response.
- Never swallow: no empty `catch`, no `catch { return null; }`.
- Validate at the edge and fail loudly: reject an out-of-range `days` parameter with `400` and a
  message naming the bound, rather than clamping it silently.

## EF Core and data access

- `AsNoTracking()` on every read-only query. Tracking is for writes.
- Project to the DTO/record in the query (`Select`) so the database returns only the columns used.
  No `ToListAsync()` followed by in-memory `Where`/`Sum`.
- Know where the query executes. Any `GroupBy`, date truncation, or aggregate must translate to SQL —
  verify with the generated SQL, not by hope. If a construct can't translate, push it into a SQL
  view or a raw `FromSql` query with parameters, and say so in a comment.
- Parameterize everything. String-concatenated SQL is never acceptable, `FromSql` interpolation
  included — use `FromSql($"…{value}")` (which parameterizes) and never `FromSqlRaw($"…{value}")`.
- Configure the model in `IEntityTypeConfiguration<T>` classes, not with attributes scattered on
  entities and not inline in `OnModelCreating`.
- Migrations are checked in and never edited after being applied. Index the columns the aggregation
  filters and groups by, and say why in the migration's name.
- `DbContext` is scoped. Don't capture it in a singleton, don't share it across parallel tasks.

## API surface

- Thin controllers: bind, validate, delegate, return. No LINQ over entities and no business rules
  in an action method.
- Typed results (`Results<Ok<T>, BadRequest<ProblemDetails>>` or `ActionResult<T>`) so the contract
  is visible in the signature and in OpenAPI.
- DTOs are separate types from entities, in both directions. An EF entity must never be serialized
  to a client.
- Status codes mean what they say: `400` for a malformed request, `404` for a missing resource,
  `422` for a well-formed request that fails a domain rule, `200` with an empty payload for a valid
  query that matched nothing — an empty range is not an error.

## Dependency injection and configuration

- Constructor injection only. No service locator, no `IServiceProvider` passed around, no static
  mutable state.
- Correct lifetimes: stateless services `Scoped` by default, `Singleton` only when genuinely
  immutable and thread-safe. Never inject a `Scoped` dependency into a `Singleton`.
- Inject `TimeProvider` (or a small `IClock`) rather than calling `DateTime.UtcNow` — untestable
  time is the most common reason aggregation tests can't pin a boundary.
- Bind configuration to options records with `IOptions<T>`; no `IConfiguration["Deeply:Nested:Key"]`
  reads in business code. No secrets in `appsettings.json`.

## Logging

- `ILogger<T>` with structured templates: `logger.LogWarning("Baseline window rejected for {AccountId}: {Days} days", accountId, days)`.
  Never interpolate into the message string — the properties are the point.
- Log at the boundary with enough context to reconstruct a bad request; don't log the same failure
  at three levels on its way up.
- Never log PII, connection strings, or full request bodies.

## Tests

- xUnit + `FluentAssertions` (or plain asserts, consistently). One behaviour per test, named
  `Method_Scenario_ExpectedOutcome`.
- Arrange-Act-Assert, no shared mutable fixture state between tests.
- Test the aggregation math and the boundaries directly, as units: empty range, single row,
  divide-by-zero, nulls, duplicate and out-of-order events, timezone edges, DST.
- One integration test per endpoint over a real (or in-memory-provider-free) database asserting the
  contract shape and the totals — the in-memory provider does not evaluate SQL translation, so it
  cannot prove the aggregation works. Prefer SQLite or a containerized instance.
- Mock only what you own and what crosses a boundary. Don't mock `DbContext`.
- Deterministic: no `DateTime.Now`, no `Random` without a fixed seed, no ordering assumptions on
  an unordered query.

## Checklist before calling a backend task done

- [ ] `dotnet build` clean — no warnings introduced
- [ ] `dotnet test` run, real output reported, failures included
- [ ] Generated SQL inspected for any new aggregation query
- [ ] Totals reconciled against the seed data by hand or by a test
- [ ] No EF entity crosses the API boundary; every collection return is read-only
- [ ] Migration added, applied to a clean database, and checked in
