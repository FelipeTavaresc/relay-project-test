# Angular / TypeScript Standards

Frontend rules for `/create-techspec`, `/create-tasks` and `/execute-task`.
Stack facts and the scope boundary live in [`project-standards.md`](./project-standards.md) —
this file is *how* to write the Angular. Where the two overlap, `project-standards.md` wins.

Target: **Angular 17+** with standalone components, signals, and the built-in control flow.
No NgModules for new code, no legacy `*ngIf`/`*ngFor` in new templates.

## Design principles, concretely

- **A component renders; a service decides.** HTTP calls, aggregation, date math, and persistence
  belong in services. A component that formats and binds is doing its job.
- **Smart/dumb split** — container components fetch state and pass it down; presentational
  components take `input()`s, emit `output()`s, and own no state they didn't receive. A
  presentational component must be renderable from a single object literal in a test.
- **Single responsibility** — one component, one reason to change. When a template exceeds ~75 lines
  or a class holds two unrelated groups of fields, it is two components.
- **Dependency inversion** — components depend on a service's public surface, never on `HttpClient`
  directly. Exactly one layer knows the API's URLs and response shapes.
- **Open/closed** — extend with a new component or a new injected strategy, not by adding a
  `@Input() mode` flag whose value the template branches on three times.
- **No speculative abstraction.** A shared component or a generic type needs two real consumers.

## Project layout

```
src/app/
  core/          app-wide singletons: api clients, interceptors, persistence, guards
  features/<f>/  one folder per feature: container, child components, feature service, models
  shared/        genuinely reusable dumb components, pipes, directives
```

- Feature folders don't import from each other. Shared behaviour moves to `shared/` or `core/`.
- One public symbol per file; filenames kebab-case and suffixed by role
  (`activity-baseline.service.ts`, `location-table.component.ts`).
- Lazy-load feature routes with `loadComponent`; keep the initial bundle to the shell.

## TypeScript rules

- `strict: true`, plus `noUncheckedIndexedAccess` and `noImplicitOverride`. Don't relax them.
- **`any` is banned.** Use `unknown` and narrow. A cast (`as`) is an assertion you owe the reader a
  reason for — prefer a type guard.
- Type every API response with an explicit interface or type alias that mirrors the backend DTO.
  Never bind a template to an untyped `Object`.
- No non-null assertions (`!`) to silence the compiler — handle the `undefined` case.
- `readonly` on fields that aren't reassigned; `const` by default, `let` only when mutated.
- Discriminated unions over boolean pairs: `{ status: 'loading' } | { status: 'error', message } |
  { status: 'ready', data }` beats `isLoading` + `hasError` + possibly-stale `data`.

## State management

- **Pick one approach for the whole app and state it in the README** (`project-standards.md`).
  Default: **signals** in services for app/feature state, RxJS only at the async edges
  (HTTP, debounced input, event streams). Don't mix a second store library in alongside.
- Signals: `signal()` for owned state, `computed()` for anything derived — never a field that must
  be recalculated by hand, and never derived state duplicated into a second `signal`.
- `effect()` is for synchronizing with the outside world (persistence, the URL, the document title).
  Never use one to write to another signal — that's a `computed`.
- State a component doesn't own is exposed as a readonly signal (`asReadonly()`) or a `computed`;
  mutation goes through a method on the service.
- `@if` / `@for` / `@switch` in templates, `@for` always with `track`. Use `@empty` for the empty
  case rather than a second `@if`.
- `ChangeDetectionStrategy.OnPush` on every component. With signals and `OnPush`, zone-triggered
  re-renders stop being the thing that hides a bug.

## Data fetching

- One service per backend resource, returning typed observables (or signals via `toSignal`/resource
  APIs) — no `HttpClient` in a component, no URL strings outside that service.
- Handle the three states for every request: **loading, empty, error**. They are part of the
  feature, not a follow-up. An error state says what failed and offers a retry.
- Unsubscribe or don't subscribe: prefer the `async` pipe / `toSignal`; where a manual subscription
  is unavoidable, use `takeUntilDestroyed()`. A leaked subscription is a defect.
- `switchMap` for request-per-input-change (cancels the stale one), `debounceTime` +
  `distinctUntilChanged` on anything driven by typing. Never `mergeMap` for a search box.
- Never `subscribe()` inside a `subscribe()`.
- No client-side aggregation. Totals, baselines, and comparisons come from the API
  (`project-standards.md`); the frontend formats and sorts what it was given.

## URL and persistence

- Every user-controlled input — filter, date range, location selector, toggle — **must survive a
  page reload**. The query string is the store of record: it makes state shareable and the
  back button meaningful. Use `localStorage` only for preferences with no place in a URL.
- Read state from the route on init, write it back on change with `replaceUrl: true` so filter
  changes don't fill the history stack.
- Validate what comes out of the URL or storage — it is user input. Reject and fall back to a
  documented default rather than passing a malformed range to the API.

## Templates and forms

- No logic in templates: no method calls in a binding that do work (they run on every check), no
  arithmetic, no chained optional-chaining gymnastics. Move it to a `computed` or a pure pipe.
- Reactive forms, typed (`FormGroup<{ from: FormControl<string> }>`). Template-driven forms only for
  a single trivially bound control.
- Pure pipes for formatting; never mutate an input inside a pipe or a getter.
- Semantic HTML and real labels: `<button>` for actions, `<a>` for navigation, every input
  associated with a `<label>`, tables as `<table>` with `<th scope>`. Keyboard reachable, visible
  focus, `aria-live` on regions that update asynchronously.
- Function over form: clear and unstyled beats pretty and broken. Styles are component-scoped; no
  global overrides, no `::ng-deep` in new code, no `!important`.

## Tests

- Karma/Jasmine or Jest — whichever the repo starts with, consistently. Must run from the
  one-line command documented in the README.
- Test the parts that matter: the service's request/response contract and error handling, the
  URL↔state round trip (including a malformed value), and that each component renders its
  loading / empty / error / ready states.
- `HttpTestingController` for service tests — never hit a real backend from a unit test.
- Assert against rendered output and roles, not internal fields; query by text or role rather than
  by CSS class, so a style change doesn't break a behaviour test.
- No `fixture.detectChanges()` sprinkled hopefully — one deliberate call per state change.
- Deterministic: no real timers (`fakeAsync`/`tick`), no `new Date()` in a component (inject a clock
  or take the date as input).

## Checklist before calling a frontend task done

- [ ] `npm run build` clean — no new warnings, no `any` introduced
- [ ] `npm test` run, real output reported, failures included
- [ ] Reload the page with filters applied: state restored from the URL
- [ ] Loading, empty, and error states each exercised in the browser
- [ ] No `HttpClient`, business rule, or aggregation left in a component
- [ ] Keyboard-navigable, and readable at a narrow viewport
