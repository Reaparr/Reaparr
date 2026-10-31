---
name: reaparr-backend
description: Use when doing any Reaparr backend work under src excluding ClientApp, backend tests, backend configs, C# code, FastEndpoints, EF Core, Quartz jobs, SignalR, command handlers, unit tests, integration tests, backend architecture, or backend reviews. This skill must always load before any narrower backend skill.
---

# Reaparr Backend

## Purpose

This skill is mandatory for every Reaparr backend task. Always load `reaparr-backend` before reading, searching, editing, reviewing, debugging, planning, or testing backend code. Do not skip it because a narrower backend skill appears to match; load this skill first, then load the narrower skill.

It gives the shared backend map, mandatory tool rules, verification gates, logging rules, and routing to narrower skills.

Backend work includes code, tests, configuration, plans, reviews, debugging, and refactors under:
- `src/` except `src/AppHost/ClientApp/`
- `tests/UnitTests/`
- `tests/IntegrationTests/`
- backend project files, DI modules, migrations, settings, job config, endpoint contracts, and generated backend API surfaces

Do not use this skill for frontend-only work under `src/AppHost/ClientApp/`; use the Reaparr frontend skills instead.

## Vertical-slice structure

Application endpoints are organized by operation, not grouped into product-level aggregate files.

- Put each endpoint operation in its own descriptively named folder under the relevant feature/product area.
- Keep the endpoint, its request/response contracts, and its validator in that operation's endpoint file unless the existing slice uses separate files.
- A C# endpoint file must declare only one FastEndpoints endpoint class.
- Never create aggregate files such as `SonarrIntegrationEndpoints.cs`, `RadarrIntegrationEndpoints.cs`, `ManagementEndpoints.cs`, or any other `*Endpoints.cs` file containing multiple endpoint operations.
- When adding related detail, setup, and delete operations, create sibling slices such as `GetSonarrIntegration/`, `SetupSonarrIntegration/`, and `DeleteSonarrIntegration/` rather than grouping them by product in one file.
- Before creating files, inspect the nearest existing endpoint siblings through native repository tools and match their folder/file organization.

Generic minimal-file or DRY guidance must not override this structure. In Reaparr, the operation is the vertical slice.

## Required Tooling

Native repository tooling is mandatory for backend work. Use these tools directly:
- `read`, `edit`, and `write` for file operations;
- codebase-memory for structural/text discovery and impact analysis, and `glob` for file-name discovery;
- `lsp` for symbol inspection, references, refactors, and diagnostics;
- short `bash` commands for native project tooling and factual checks;
- `dotnet-test-mcp` or the repository's native test command for backend tests.

For application start, restart, and stop instructions, see **Running Reaparr** in the project root `AGENTS.md`.

### Uncommitted-change reviews

When asked to review, audit, or correct uncommitted backend changes:

- Use native repository tools to enumerate and inspect changed files, surrounding code, symbols, and usages.
- Apply corrections with native file-editing tools.
- Use native diagnostics and test tooling to validate the corrected change set.
- Preserve unrelated user changes and review every staged and unstaged backend change before claiming the audit is complete.

### Native tooling fast path

After identifying the indexed project and repository tool route, reuse them for the task:

| Action | Native tool |
| --- | --- |
| Read file/range | `read` |
| Create or overwrite file | `write` |
| Surgical edit | `edit` |
| Find paths | codebase-memory search; `glob` for file names |
| Symbol-aware lookup/refactor | `lsp` |
| Short project command | `bash` |
| Backend tests | `dotnet-test-mcp` or the repository's native test command |

Use repository-native file/LSP tools without requiring an external IDE. Codebase-memory remains the discovery/impact route; backend tests use `dotnet-test-mcp` when available.

### Tool failures

Correct malformed parameters before retrying. Narrow oversized requests. For a permission denial, do not retry the underlying operation through another tool; request the specific missing permission. For unavailable required indexing or test tooling, report the exact blocker rather than changing runners. An unchanged test failure is evidence to diagnose, not a reason to rerun.

## Secondary Skill Routing

Load this skill first, then load narrower skills when the task matches. When editing or creating backend unit tests under `tests/UnitTests/`, `reaparr-backend-unit-tests` is mandatory, even if the production code being tested lives in a tooling project such as `tools/Build`.

| Task | Load next |
| --- | --- |
| Command records, handlers, validators, command dispatch, `ICommandExecutor` | `reaparr-command-handler-patterns` |
| Backend unit tests under `tests/UnitTests/` | `reaparr-backend-unit-tests` |
| Backend integration tests under `tests/IntegrationTests/` | `reaparr-backend-integration-tests` |
| Broad .NET or C# implementation choices | `dotnet`, `modern-csharp`, or `dotnet-best-practices` |
| ASP.NET Core hosting, middleware, auth, API composition | `aspnet-core` or `dotnet-backend` |
| FastEndpoints endpoint design | `fast-endpoints` |
| EF Core modeling, migrations, query behavior, persistence | `entity-framework-core`, `ef-core`, or `optimizing-ef-core-queries` |
| SignalR hubs, clients, realtime delivery | `signalr` |
| TUnit mechanics or test runner behavior | `tunit` |
| Data access performance | `database-performance` |
| Retry, timeout, circuit breaker, fault tolerance | `resilience-patterns` |

Prefer Reaparr-specific skills over generic skills when both apply.

## Backend Architecture Map

| Area | Role |
| --- | --- |
| `src/AppHost` | Application host, startup/configuration, Autofac composition, FastEndpoints registration, SignalR setup, generated API surface, frontend hosting boundary |
| `src/Application` | Primary use cases, command handlers, orchestration, Plex download flows, API-facing application behavior |
| `src/Application.Contracts` | Cross-project commands, DTO contracts, request/response types consumed outside implementation assemblies |
| `src/Domain` | Domain entities, shared abstractions, FastEndpoints command executor abstractions, domain services and shared primitives |
| `src/Data` | EF Core contexts, persistence configuration, migrations/seeding, database-specific behavior |
| `src/BackgroundJobs` | Quartz jobs and background workflows; jobs dispatch commands and must not throw out of `Execute` |
| `src/BackgroundJobs.Contracts` | Contracts for background job commands and cross-project scheduling requests |
| `src/PlexApi` and contracts | Plex API clients, DTOs, and translation around Plex server/library/media operations |
| `src/External` | External-service integrations and adapters |
| `src/FileSystem` | File and path abstractions, filesystem-side media/download behavior |
| `src/Settings` | Strongly typed settings and configuration models |
| `src/Logging` | Serilog/logging support |
| `src/PublicApi` | Public API-facing contracts and integration boundaries |
| `tests/BaseTests` | Shared unit/integration test infrastructure, seed data, fake Plex data, mock filesystem/http helpers |

## Core Backend Patterns

### Comments

- Preserve existing comments that are still valid, including during refactoring and simplification. Do not remove them merely to shorten the code or diff.
- Maintain comments alongside their code: move them with the relevant logic and update them when behavior changes. Remove a comment only when it no longer applies.
- Add useful comments that explain intent, invariants, ordering, cancellation semantics, or other non-obvious behavior.
- Before finishing, inspect removed comments in the diff and restore any that remain applicable.

### Validate at boundaries; trust internal contracts

- Validate command/request inputs in their validators. Validate external response data at the ingestion boundary before passing it into application workflows.
- After validation, handlers and internal helpers consume the established contract. Do not repeat null, range, identity, collection, or scope checks already guaranteed by that contract.
- Normalize external representations once at the owning boundary. Internal callers use the normalized representation without repeating normalization or adding fallback values for states the contract excludes.
- Incoming mapped entities have clean generated database IDs. Assign persisted IDs when matching existing rows and assign relationships when writing them; do not reset IDs or repair reused entity graphs defensively.
- For independent batch records, discard invalid records at ingestion when the operation permits partial acceptance, then process the valid records. Invalid command context still fails validation.

### Plex API authority

- A fresh Plex API response that completed successfully and passed boundary validation is always authoritative. Treat it as the source of truth exactly as received, including an empty collection: if Plex reports that everything is gone, reconcile persistence accordingly and remove stale records. Never second-guess a valid response by comparing it with stored data, issuing a confirmatory request, or retaining stale records because the change seems surprising.
- A failed, cancelled, malformed, or explicitly incomplete retrieval is not an authoritative snapshot. Never convert it to an empty inventory or use it for destructive reconciliation; preserve existing data and follow the normal failure handling.
- Keep checks that implement actual behavior: create versus update, timestamp changes, missing stored records, authorization, ownership, concurrency conflicts, and cancellation. These are not duplicate input validation.
- Add defensive handling only for a reachable failure demonstrated by the contract, current callers, or observed behavior. Hypothetical misuse is not a requirement.
- When an internal invariant is violated, fix its producer or boundary validation. Do not conceal the violation with silent defaults, skipped work, or graph repair.

### Endpoints

- Match existing FastEndpoints inheritance: `Endpoint<TRequest, TResponse>` or `EndpointWithoutRequest<TResponse>`, as appropriate for the operation.
- Reaparr has no project-local `BaseEndpoint<,>` requirement; do not introduce a wrapper just to satisfy guidance.
- Endpoint behavior should usually dispatch through `ICommandExecutor` rather than embedding orchestration logic.

### Dependency Injection

- Autofac modules use `*Module : Module`.
- Register modules through the existing AppHost composition path.
- Prefer existing services and abstractions before introducing new ones.

### Enums and mapping

- Use enums for finite domain values, states, modes, and categories. Always prefer an existing enum over magic string literals; translate external strings at the integration boundary.
- Keep mappers simple and single-purpose: map source values to target values with direct, predictable assignments.
- Keep filtering, validation, querying, enrichment, deduplication, logging, side effects, and business rules outside mappers.

### Persistence

- EF Core access goes through project contexts/factories and existing repository/service patterns.
- Always prioritize EF Core APIs and LINQ over handwritten SQL for queries and updates. Use manual SQL only when EF Core/LINQ cannot express the required operation or a demonstrated performance requirement justifies it; explain the necessity before introducing it.
- When manual SQL is necessary, interpolate property-based identifiers with `nameof(Entity.Property)` rather than hardcoding property names. Respect explicit database column mappings and quote identifiers appropriately; `nameof` is not a substitute for a differently mapped column name. Keep runtime values parameterized through the existing EF Core SQL APIs, never concatenated or embedded into SQL text. Identifier interpolation must remain separate from value parameterization.
- Avoid application-side joins and unbounded queries.
- Rely on Reaparr's configured no-tracking default for read queries; do not add `.AsNoTracking()` explicitly.
- Keep query behavior deterministic and testable.
- Use normal database-operation semantics by default. Add an explicit transaction only when a specified business invariant requires several writes to succeed or fail together; scope it to those writes. Multiple writes alone do not establish an atomicity requirement.
- Introduce retries only for a demonstrated transient failure with defined replay semantics. Do not add snapshots, ID resets, collection clearing, or tracker clearing merely to make an otherwise single-pass operation replayable.

### EF Core migrations

- Generate EF Core migration files and `*ModelSnapshot.cs` files through MCP tooling with `dotnet-mcp:dotnet_ef` and `action: MigrationsAdd`; do not hand-author schema migrations or snapshots.
- A generated migration's `Up` or `Down` method may be manually extended with reviewed `migrationBuilder.Sql(...)` statements only for deterministic data backfills or transformations that EF Core cannot generate. Keep schema operations, designer files, and model snapshots generated-only.
- If a migration needs to be removed or regenerated, use `dotnet-mcp:dotnet_ef` with the appropriate migration action instead of deleting or rewriting files by hand.
- After migration generation or an allowed SQL backfill edit, inspect the migration and run native diagnostics on every changed model, configuration, and migration file.
- Verify data backfills against both an upgrade from the preceding schema and creation of a fresh database.

### Background Jobs

- Quartz jobs use `IJob` and should be marked `[DisallowConcurrentExecution]` when concurrent execution is unsafe.
- Pass parameters through `JobDataMap`.
- Dispatch real work through `ICommandExecutor`.
- Jobs must catch, log, and swallow exceptions; do not let exceptions escape `Execute`.

### Realtime

- After MCP migration generation, inspect generated files and run native diagnostics on the changed model/configuration files before claiming completion.
- Broadcast through `IHubContext<THub, TClientInterface>.Clients.All` or the narrowest appropriate client target.

### Logging

- Use Serilog `ILogger` according to existing conventions.
- For command handlers and services, prefer assigning contextual loggers with `log.ForContext<T>()` when that is the local pattern.
- Every `_log` usage must include `.Here()` before the log-level call.

Good:

```csharp
_log.Here().Information("Refreshing Plex server access");
_log.Here().Error(ex, "Failed to queue library sync job");
```

Bad:

```csharp
_log.Information("Refreshing Plex server access");
_log.Error(ex, "Failed to queue library sync job");
```

## Result Handling

Reaparr uses `FluentResults` as the normal error boundary for commands, jobs, services, and other fallible backend operations. Preserve the distinction between success, cancellation, expected domain failures, and unexpected failures.

### Prefer `Result.Try` over `try/catch`

Use `Result.Try` before reaching for a manual `try/catch`. It is the standard project mechanism for converting thrown exceptions and cancelled operations into `Result` values.

Use the existing error boundary. Do not wrap every internal helper in another `Result.Try` when the caller already owns exception-to-Result conversion. Result handling is not a reason to add validation, fallback, retry, or transaction machinery.

```csharp
var result = await Result.Try(async Task () =>
{
    await DoWorkAsync(cancellationToken);
});
```

Use a manual `try/catch` only when `Result.Try` cannot express the required behavior, such as:
- translating a specific external exception into a specific HTTP or domain error;
- guaranteeing cleanup in `finally`;
- containing exceptions at framework boundaries such as Quartz listeners/jobs or `async void` lifetime callbacks;
- distinguishing caller cancellation from a timeout with an exception filter;
- performing bounded best-effort cleanup after the original operation was cancelled.

Never add a broad `catch (Exception)` merely to return `Result.Fail`; use `Result.Try`. If a framework boundary must catch broadly, log the exception and ensure it cannot escape that boundary.

### Handle cancelled and failed results together

Always use `IsCancelledOrFailed` with `LogIfFailed()` when a cancelled or failed result must stop the current operation:

```csharp
if (insertResult.IsCancelledOrFailed)
    return insertResult.LogIfFailed();
```

`LogIfFailed()` logs cancellation as a warning and other failures as errors, while preserving the original result and its errors. Use the same combined guard for generic and merged results; do not expand it into separate `IsCancelled`/`LogWarning()` and `IsFailed`/`LogError()` guards.

When converting a generic result to the enclosing method's non-generic `Result` return type, append `.ToResult()` without a type parameter:

```csharp
if (insertResult.IsCancelledOrFailed)
    return insertResult.LogIfFailed().ToResult();
```

Read `.Value` only after the guard establishes success. Propagate handled cancellation as a result rather than calling `cancellationToken.ThrowIfCancellationRequested()`. At framework callbacks that cannot return a `Result`, log with `LogIfFailed()` and exit cleanly.

### Result composition and propagation

- Return the original failed or cancelled result when signatures are compatible; this preserves error types and metadata.
- Use `ToResult()` only when converting `Result<T>` to non-generic `Result` is required.
- Use `Result.Merge(...)` for independent result-producing operations, then apply the same `IsCancelledOrFailed`/`LogIfFailed()` guard to the merged result.
- Never read `.Value` until success is established.
- Do not replace structured errors with only `error.Message`; retain `ExceptionalError`, HTTP/status metadata, and domain error types.
- Use existing `ResultExtensions` helpers for validation, not-found, conflict, timeout, and other expected failures so API metadata remains intact.
- For best-effort work that intentionally continues after failure or cancellation, call `result.LogIfFailed()` without introducing an early return.
- Avoid duplicate logging at every stack frame. Log at the boundary that handles or returns the result, and always log results consumed locally rather than propagated.
- When using `Result.Try`, inspect its returned result. Do not assume wrapping an operation is sufficient by itself.

### Cancellation-token use with Results

Pass the caller's token through async work for which cancellation is useful, including long-running EF Core queries, scheduler calls, command/event dispatch, SignalR calls, HTTP calls, file operations, and delays. Cancellation must be observable by cancellable work, but once represented as a cancelled `Result`, propagate it as a result rather than throwing.

- **Use cancellation where it can meaningfully stop work.** For very quick, bounded operations, prefer explicit `CancellationToken.None` at async calls that accept a token. This includes straightforward indexed key/scalar lookups (`FirstOrDefaultAsync(...)`, `SingleOrDefaultAsync(...)`, `FindAsync(...)`, `AnyAsync(...)`), small filtered database reads, and trivial local operations where interrupting midway provides no practical benefit. Use a tokenless overload only when no token parameter is available.
- **Keep trivial call chains token-free.** Do not add or thread `CancellationToken` parameters through internal helpers solely to forward them to these quick operations, and do not add cancellation checks around trivial synchronous work. Retain parameters required by framework/interface contracts, but use `CancellationToken.None` for the quick operation itself.
- **Judge actual cost, not method names.** `FirstOrDefaultAsync` can still execute an expensive query; `Where` only builds a query until execution. Consider filtering, indexing, data volume, and the complete operation. Continue propagating the caller's token for expensive queries, substantial projections, batch workflows, long-running I/O, and writes or transactions where cancellation is useful and safe. A sequence of quick calls can still form a long-running workflow that needs cancellation between units of work.

`CancellationToken.None` is intentional and must be respected. It may be used for work that must not be cancelled, including required state transitions or framework-owned operations whose completion is necessary for consistency. Do not mechanically replace `CancellationToken.None` with a caller token. Evaluate the operation's cancellation semantics first, and preserve `CancellationToken.None` where non-cancellability is deliberate.

After cancellation has been accepted, terminal-state persistence may need a separate cleanup token so an already-cancelled request or job token does not prevent recording `Cancelled`, `Paused`, or another durable state. Use `CancellationToken.None` when that persistence must complete and the operation is known to be short and bounded by its underlying infrastructure. For potentially blocking or externally dependent cleanup, prefer a separate short timeout token. Such cleanup must:
- be contained at the framework boundary so cleanup exceptions cannot escape;
- log cleanup timeout/cancellation as a warning;
- log other cleanup failures as errors;
- avoid an unbounded token only when the operation could block indefinitely.

## Code File and Folder Creation Rule

When backend work creates a new code file that requires a new folder, the folder creation is not complete until two things are true:

1. The new code file namespace uses only the owning project's root namespace.
2. The owning project's `.csproj.DotSettings` file has the new folder path registered in `NamespaceFoldersToSkip` with `True`.

Folders should never account for the namespace. Do not append folder names to the namespace of new backend code files.

Example:

If creating a new code file at:

```text
src/AppHost/Startup/HealthChecks/MyHealthCheck.cs
```

and `Startup/HealthChecks` is a new folder path for that project, the code file namespace should stay at the project root namespace:

```csharp
namespace Reaparr.AppHost;
```

Do not use a folder-derived namespace:

```csharp
namespace Reaparr.AppHost.Startup.HealthChecks;
```

Also update the owning project `.csproj.DotSettings` with an entry matching the existing encoding style, for example:

```xml
<s:Boolean x:Key="/Default/CodeInspection/NamespaceProvider/NamespaceFoldersToSkip/=startup_005Chealthchecks/@EntryIndexedValue">True</s:Boolean>
```

Rules:
- Applies to any backend code file creation that introduces a new folder.
- Applies to production backend projects and backend test projects.
- Use the owning project's `.csproj.DotSettings` file.
- Inspect existing entries in that file and copy the same casing and `_005C` path separator encoding style.
- Do not hardcode project paths from examples; determine the owning project from where the new file is being created.

## Backend Workflow

1. Load `reaparr-backend`; this is not optional for backend work.
2. Confirm the task is backend-scoped and load any narrower matching skills.
3. Use native search and symbol tools to find existing precedent.
4. Identify the owning project and, for tests, the matching test project.
5. Make the smallest maintainable change that fixes the root cause.
6. Preserve project boundaries; place shared contracts in `*.Contracts` projects only when cross-project consumption requires it.
7. Make test inputs honor the production boundary contract. Use real validators for invalid-input tests. Do not expand production behavior to repair invalid fixtures, reused persisted graphs, or hypothetical inputs outside that contract.
8. Keep behavior deterministic, especially in tests and background jobs.
9. Before finishing any backend code file creation, check whether a new folder was introduced. If yes, use the root project namespace in the code file and update the owning `.csproj.DotSettings` `NamespaceFoldersToSkip` entry.
10. Re-read changed files after edits to confirm the intended changes landed.
11. Use native diagnostics and language-server analysis to find errors that need fixing before claiming completion. Do not run a build as an error-discovery mechanism.

## Run and Test Commands

Do not run `dotnet build` or project build commands solely to discover errors. Use `lsp` diagnostics and targeted native checks first. Build-related execution is allowed when it is part of running tests or the requested application workflow.

### Preferred test execution: dotnet-test-mcp

When `dotnet-test-mcp` is available, prefer it for backend test execution. Otherwise use the repository's native test command from the project configuration.

| Action | Tool |
| --- | --- |
| List test projects | `dotnet-test-mcp:list_test_projects` |
| List tests summary | `dotnet-test-mcp:list_tests_summary` |
| Run single test | `dotnet-test-mcp:run_single_test` |
| Run test class | `dotnet-test-mcp:run_all_tests_in_class` |
| Run test project | `dotnet-test-mcp:run_all_tests_for_project` |
| Run all tests | `dotnet-test-mcp:run_all_tests` |

Use direct calls to the documented test tool after one successful health check. If it is unavailable, use the repository's native test command and preserve the failure evidence.

All backend test execution should use the documented test route. Never use an external editor run configuration for test execution.

## Verification Gates

Never rely on a build alone to determine whether backend code has errors. Use native language-server diagnostics, project diagnostics, and targeted tests.

Required error-checking flow:
1. Run `lsp` diagnostics for each changed backend file when a language server is available.
2. If diagnostics are incomplete or fail, retry with narrower file ranges and repository-native checks.
3. Fix all relevant reported errors.
4. Only after diagnostics are clean, run tests when the change requires behavioral verification.

Build commands are not a substitute for diagnostics. Use them only when required by the requested workflow or as part of test execution.

Allowed build-related execution:
- Running backend unit tests.
- Running backend integration tests.
- Running a test project may compile as part of test execution; that is acceptable because the purpose is executing tests, not discovering compile errors.

Test routing:
- Command/handler or service changes: diagnostics first, then relevant unit tests when behavior changed.
- Unit test changes: diagnostics first, then the relevant unit test project, preferably filtered first and broader if risk warrants.
- Integration test changes or integration failures: diagnostics first, then targeted integration tests, then the full integration suite before claiming done.
- Cross-project or public contract changes: diagnostics across affected files and symbols first, then affected tests.
- Job, SignalR, EF Core, or endpoint changes: diagnostics first, then tests covering the runtime path when available; otherwise explain the missing behavioral verifier.

Do not claim success unless native diagnostics and required tests passed. If the language server is unavailable, state that explicitly and use the strongest available native checks.

## Common Mistakes

- Skipping this umbrella skill and loading only a narrow backend skill.
- Editing backend unit tests without also loading `reaparr-backend-unit-tests`.
- Testing filesystem behavior with real `File`, `Directory`, temp directories, or host filesystem state instead of `BaseUnitTest.SetupFileSystem(...)` and `MockFileSystem`.
- Treating this skill as optional for small changes.
- Using external editor integrations instead of native repository tools for file operations, or using shell commands instead of IDE run configurations for development-service lifecycle.
- Switching to broad filesystem commands after one native-tool hiccup instead of retrying with a narrower request.
- Running `dotnet build` or a project build to discover compile errors instead of using native diagnostics.
- Creating backend code files in new folders without keeping the namespace at the project root and updating the owning `.csproj.DotSettings` `NamespaceFoldersToSkip` entry.
- Creating new abstractions before checking existing Reaparr patterns.
- Putting shared command records in implementation projects when they belong in `*.Contracts`.
- Returning raw values or `null` from Result-based command handlers.
- Letting Quartz job exceptions escape.
- Weakening tests or assertions to force green.
- Using `--filter` or `--treenode-filter` shell arguments for test discovery — use the documented test tooling instead.
- Running frontend package managers for backend-only work.
- Blocking test execution on unavailable `dotnet-test-mcp`; use the repository's native test command when the MCP test route is unavailable.

