---
status: completed # Options: pending, in-progress, completed, excluded
---

<task_context>
<domain>infra/scaffolding</domain>
<type>implementation</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>none</dependencies>
</task_context>

# Task 1.0: Repository skeleton — two build roots

<critical>Read the prd.md and techspec.md files in this folder; if you do not read them your task will be invalidated</critical>

## Overview

Create the two independent build roots the whole feature sits on: `backend/` (a .NET 8 solution with
three source projects and one test project) and `frontend/` (an Angular workspace with Jest). Nothing
feature-specific is written here. The point is that each root builds and runs its own empty test suite
green **before** any feature code exists, so a failure later is never ambiguous between the code and
the scaffolding (techspec § Development Sequencing, step 1).

<requirements>
- `backend/` contains `Relay.sln`, `src/Relay.Api`, `src/Relay.Application`, `src/Relay.Infrastructure`, `tests/Relay.UnitTests` — the layout in techspec § Component Overview, verbatim.
- `backend/Directory.Build.props` sets `net8.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`.
- Project references enforce the one-way direction: `Api → Application`, `Api → Infrastructure` (composition only), `Infrastructure → Application`. `Relay.Application` references no other project and no EF Core package.
- `frontend/` is an Angular 17+ standalone workspace (no NgModules) with routing enabled.
- Frontend test runner is **Jest**, per techspec § Testing Approach. Replace the Karma/Jasmine default rather than keeping both.
- Each root has exactly one build command and one test command, runnable from that root, recorded in the task's completion notes for the README in Task 8.0.
- No feature code, no controllers, no components beyond what the generators produce.
</requirements>

## Subtasks

- [x] 1.1 Create `backend/` — solution, three source projects, test project, and the project references above.
- [x] 1.2 Add `backend/Directory.Build.props` with the target framework, nullable reference types, and warnings-as-errors.
- [x] 1.3 Add xUnit (plus the assertion library chosen for the repo — pick one and stay consistent) to `Relay.UnitTests`, with one trivial passing test so the runner is proven.
- [x] 1.4 Create `frontend/` as an Angular standalone workspace at the repository root level (sibling of `backend/`).
- [x] 1.5 Switch the frontend to Jest (`jest-preset-angular`), remove Karma/Jasmine config and dependencies, keep `npm test` as the one-line command.
- [x] 1.6 Add `.gitignore` entries for `bin/`, `obj/`, `node_modules/`, `dist/`, and coverage output.
- [x] 1.7 Run `dotnet build`, `dotnet test`, `npm run build`, `npm test` and record the real output.

## Implementation Details

Folder layout and the "two build roots, one repository" rule: techspec § System Architecture →
Component Overview. Dependency direction: same section, and `csharp-dotnet.md` § Project layout.
Angular target version and standalone/signals baseline: `angular.md` header and § Project layout.

Do not scaffold `src/app/features/activity-baseline/` here — Task 6.0 owns it.

## Success Criteria

- `dotnet build` from `backend/` completes with **zero warnings** (warnings-as-errors is on, so any warning is a failure).
- `dotnet test` from `backend/` runs and reports the trivial test passing.
- `npm run build` from `frontend/` completes clean.
- `npm test` from `frontend/` runs Jest and reports green (the generated `app.component.spec` or a trivial replacement).
- `Relay.Application.csproj` contains no `Microsoft.EntityFrameworkCore*` package reference and no reference to `Relay.Infrastructure`.
- The four commands are written down verbatim for Task 8.0 to copy into the README.

## Completion Notes

### The four commands, for the Task 8.0 README

| Root | Build | Test |
|---|---|---|
| `backend/` | `dotnet build` | `dotnet test` |
| `frontend/` | `npm run build` | `npm test` |

### Real output, 2026-09-26

```
backend/ $ dotnet build
  Relay.Application -> backend\src\Relay.Application\bin\Debug\net8.0\Relay.Application.dll
  Relay.Infrastructure -> backend\src\Relay.Infrastructure\bin\Debug\net8.0\Relay.Infrastructure.dll
  Relay.UnitTests -> backend\tests\Relay.UnitTests\bin\Debug\net8.0\Relay.UnitTests.dll
  Relay.Api -> backend\src\Relay.Api\bin\Debug\net8.0\Relay.Api.dll
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)

backend/ $ dotnet test
Versão do VSTest 17.11.1 (x64)
Aprovado!  – Com falha: 0, Aprovado: 1, Ignorado: 0, Total: 1, Duração: 11 ms - Relay.UnitTests.dll (net8.0)

frontend/ $ npm run build
Initial total | 247.23 kB | 69.48 kB
Application bundle generation complete. [1.595 seconds]
Output location: frontend\dist\frontend

frontend/ $ npm test
Test Suites: 1 passed, 1 total
Tests:       2 passed, 2 total
Time:        1.342 s
```

(The .NET CLI on this machine is localised to pt-BR; output is reproduced verbatim rather than
translated. "Compilação com êxito / 0 Aviso(s)" = build succeeded, 0 warnings.)

### Decisions taken while scaffolding

1. **`backend/global.json` pins the SDK to `8.0.425`, `rollForward: latestFeature`.** The machine has
   SDKs 5.0 → 10.0.401 installed and `dotnet` picks the highest by default, which made the `webapi`
   template resolve from an installed 9.0 template package and emit `net9.0` — which the 8.0 SDK then
   refused to restore (`NETSDK1045`). Pinning fixes the target at the source instead of patching
   each generated `.csproj`, and makes the build reproducible for a reviewer.
2. **No fluent assertion library; xUnit's built-in `Assert`.** `csharp-dotnet.md` offers
   "xUnit + FluentAssertions (or plain asserts, consistently)". FluentAssertions 8.x is no longer
   free for commercial use and 7.x is frozen, so the plain-asserts branch of that rule is the
   repo-wide choice. Recorded in `Relay.UnitTests.csproj` so it does not get re-litigated in Task 3.0.
3. **Angular pinned to 20.3.37 (the v20 LTS), not 22.2.0.** `jest-preset-angular@17` supports
   `>=20 <23`, so either would work. 20.3 is what the tech spec was written against — its reason for
   avoiding `rxResource`/`httpResource` is "still experimental as of Angular 20.3", and that
   statement should stay true of the code that ships. Still comfortably inside the task's "17+".
4. **`jest.config.js`, not `jest.config.ts`.** The jest-preset-angular docs' TypeScript config fails
   on Node 24: native type-stripping loads it as ESM and the CJS preset exposes no named
   `createCjsPreset` export (`SyntaxError`, then a fallback demand for `ts-node`). A CommonJS
   `jest.config.js` sidesteps both; the reason is a comment in the file.
5. **Template sample code deleted** — `WeatherForecast.cs`, `WeatherForecastController.cs`,
   `Relay.Api.http`, and the two `Class1.cs` stubs. `Program.cs` is untouched generator output;
   Task 5.0 owns it.
6. **Two config tightenings beyond the generators**, both required by `angular.md` and cheaper to set
   now than to retrofit: `noUncheckedIndexedAccess: true` in `tsconfig.json`, and `angular.json`
   schematic defaults giving components/services/pipes their role suffix (Angular 20 drops it by
   default, but `angular.md` § Project layout requires `…​.component.ts` / `…​.service.ts`) plus
   `changeDetection: OnPush`.

### Carried forward

- `frontend/README.md` is still CLI boilerplate apart from a corrected test command. Task 8.0 owns
  the real README.
- No `Dockerfile` in either root yet — techspec § Component Overview lists one per root; Task 8.0.

## Relevant Files
- `backend/Relay.sln`, `backend/Directory.Build.props`, `backend/global.json`
- `backend/src/Relay.Api/` — `Relay.Api.csproj`, `Program.cs`, `appsettings*.json`, `Properties/launchSettings.json`
- `backend/src/Relay.Application/Relay.Application.csproj` — no project or package references, by design
- `backend/src/Relay.Infrastructure/Relay.Infrastructure.csproj`
- `backend/tests/Relay.UnitTests/` — `Relay.UnitTests.csproj`, `ScaffoldingTests.cs`
- `frontend/angular.json`, `frontend/package.json`, `frontend/jest.config.js`, `frontend/setup-jest.ts`
- `frontend/tsconfig.json`, `frontend/tsconfig.spec.json`, `frontend/README.md`
- `.gitignore`
