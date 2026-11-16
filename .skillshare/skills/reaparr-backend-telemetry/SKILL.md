---
name: reaparr-backend-telemetry
description: Use for Reaparr backend telemetry instrumentation, ITelemetry contracts, PostHog analytics or person properties, error capture, trace and job correlation, Serilog export, telemetry noise reduction, and delivery verification. Not for frontend-only SDK or replay implementation.
---

# Reaparr Backend Telemetry

Load `reaparr-backend` first. Load the relevant backend unit/integration-test skill when changing tests. This skill owns telemetry-specific decisions; the backend skill owns general coding and tooling conventions.

## Working sequence

1. Read the affected `ITelemetry` contract, matching `Telemetry` partial, business caller, and regression tests. Trace the actual result type and persistence/job boundary before choosing an event or outcome. Current source takes precedence over this architectural guide; do not restore an old signature from examples here.
2. State the fact being recorded, its owner, counting unit, emission point, and suppression cases. Separate intent, accepted work, persisted outcome, and execution attempts. Reuse an existing event when its contract fits.
3. Keep the caller minimal: pass its existing DTO/entity/result into the facade. Implement explicit property projection and outcome classification inside `Telemetry`; migrate every affected caller/test in a clean cutover.
4. Verify the changed semantics at the native SDK seam and the relevant business boundary. Distinguish isolated tests, hosted execution, actual ingestion, and packaged/deployed evidence.

Completion means every changed emission has a defined meaning and owner, consumers are migrated, credential exclusions and correlation are preserved, and the report names exactly which verification ran. A local passing test is not ingestion or deployment proof.

## Architecture and source map

All paths below are repository-relative. Read the branch relevant to the task, not every file.

| Responsibility | Owning source |
| --- | --- |
| Business-facing contracts | `src/Application.Contracts/_Shared/Interfaces/ITelemetry*.cs` |
| Central identity, enrichment, admission, settings publication | `src/Application/Telemetry/Telemetry.cs` |
| Feature projections and outcome rules | `src/Application/Telemetry/Telemetry.Accounts.cs`, `Telemetry.Libraries.cs`, `Telemetry.Downloads.cs`, `Telemetry.Integrations.cs`, `Telemetry.Actions.cs` |
| Event/category enums and property/protocol constants | `src/Domain/Telemetry/` |
| Native SDK registration and automatic tracing/export | `src/AppHost/Startup/Startup.Telemetry.cs` |
| Request correlation and unexpected request failures | `src/AppHost/Middleware/TelemetryContextMiddleware.cs` |
| Identity-ready initialization | `src/AppHost/Boot.cs` |
| Quartz context serialization/restoration | `src/Application/_Shared/Extensions/Quartz.JobDataMap.Extensions.cs`, `Quartz.JobExecutionContext.Extensions.cs` |
| Serilog masking and native log export | `src/Logging/LogConfig.cs` |
| Facade projection/capture regression examples | `tests/UnitTests/Application.UnitTests/Telemetry/Telemetry.UnitTests.cs` |

There are three complementary paths:

- Intentional backend business facts go through `ITelemetry` into native PostHog analytics, person-property updates, or exception capture.
- Intentional operation spans start through `ITelemetry` using native .NET `Activity`. ASP.NET Core and HttpClient tracing remain automatic OpenTelemetry integrations.
- Normal `ILogger` calls stay Serilog calls; the native OpenTelemetry sink exports them to PostHog Logs. Do not route every log through the business facade.

PostHog's SDK/exporters own buffering, transport, batching, and delivery behavior. Keep the facade thin: no custom analytics framework, transport, queue, retries, identity system, or generic endpoint event registry. Feature flags, experiments, and surveys are outside this telemetry design.

## Small contracts, explicit payloads

Business callers inject `ITelemetry`, not build information, identity services, or native SDK clients solely to enrich telemetry. The facade owns shared enrichment.

- Pass existing objects rather than unpacking their fields into long signatures. For example, external download submission takes the existing integration identity, `TorrentMetadataDTO`, creation result, and operation-specific context.
- Pass the actual `Result<T>` when the payload determines outcome. Keep `ResultBase` when only result/error information matters. Avoid runtime generic-result casts and avoid manufacturing result copies solely for telemetry.
- A typed token-auth overload already identifies token authentication; callers need not repeat a method enum.
- Shared contracts must respect project dependencies. The existing `ValidatePlexTokenCommandResult` lives in `Application.Contracts`; referencing a DTO from a dependent project would create a cycle. Reuse shared contracts rather than inventing telemetry DTO hierarchies.
- Use enums for event names and finite states/actions/stages. Use `TelemetryProperty`, `TelemetryProfileProperty`, and `TelemetryProtocol` constants for wire keys. Convert enum values to strings at projection/export boundaries, not at callers.
- Passing an entity does not authorize whole-entity serialization. Select outgoing fields explicitly. Existing typed Sonarr/Radarr projections are preferable to adding a common abstraction merely to share assignments.
- Trust validated internal inputs. Do not add whitespace, range, null, or identity validation around simple telemetry calls. Semantic branches such as cancellation, no-op, partial completion, and credential exclusion are not input validation.
- Keep containment at existing SDK/framework/settings-observer boundaries. Telemetry failure must not change a business result; do not spread try/catch or `Result.Try` around every projection/call.

## Counting and outcome semantics

The event-specific contract wins over a generic success/failure helper. A successful result envelope may contain an unhealthy or incomplete outcome.

| Case | Required interpretation |
| --- | --- |
| Integration connection probe returns `Result.Ok` | Inspect `TestConnectionStatus`: `Success` -> `Succeeded`; `InvalidApiKey`/`UrlIsInvalid` -> `Rejected`; other unhealthy statuses -> `Failed`. Do not copy raw upstream error text into analytics. |
| Plex token authentication succeeds at the wrapper level | Inspect `IsValidated`; false is `Rejected`, not successful authentication. Preserve cancellation and typed authentication-error semantics. |
| Access refresh returns reports with failures/offline servers | Preserve partial work as `Partial`; distinguish no accounts/no work from disabled or missing entities. Use the typed report/response rather than caller-computed outcomes. |
| Plex server connection checks complete | Keep execution outcome separate from `TelemetryHealth`. Completed probes can correctly report `Succeeded` plus `Unhealthy`; do not conflate this contract with integration connection-test outcomes. |
| Integration deletion removes local state but remote cleanup fails | Preserve both facts: overall `Partial`, local removal, remote-cleanup outcome, and forced-deletion flag. |

Low-noise boundaries:

- `LibraryAccessChanged` consumes existing reconciliation reports after persistence. Include only `Granted`/`Revoked` rows; emit nothing for empty or Updated-only reports. Project final rows in one pass with actual server/library IDs, names, states, and grant/revoke counts. Keep suppression in the facade, not duplicated in callers; business reports/history remain unchanged.
- Library sync keeps queue admission and persisted terminal outcomes. Comparison keeps terminal outcomes. Separate `LibrarySyncStarted` and `LibraryComparisonStarted` analytics were removed; native job spans retain execution timing. Do not recreate start events just to time work.
- Aggregate server connection checks suppress constituent analytics and emit one aggregate result when enabled. Standalone checks retain their own event. Preserve existing periodic-check suppression.
- Keep logical download lifecycle facts distinct from per-file/attempt spans. Do not turn progress ticks, retries, or every child file into duplicate product-usage counts.
- Log a consumed/handled failure at its owning boundary. In particular, do not reintroduce the redundant DASH final-result log alongside existing terminal-failure logging.

Before reducing coverage further, name the question that remains answerable and the information lost. Measure deployed event/log/span volume before proposing sampling changes; low-noise analytics does not imply low total trace/log volume.

## Identity, configuration, and privacy

- Preserve the existing UUID returned by `IUserService.GetTelemetryId`. `Boot` initializes telemetry after the application account is ready; the facade caches the ID rather than querying per event. Do not substitute a Plex account ID, runtime ID, or new anonymous identity.
- Ingestion host/token are immutable build configuration. Preserve `HasValidTelemetry` eligibility and non-transmitting unconfigured builds across analytics, logs, and traces. Do not introduce runtime ingestion overrides.
- Current configuration belongs in native person properties; history and effective operation context belong on events/spans. Initialization publishes the current settings snapshot; the existing `SettingsUpdated` signal refreshes properties and emits `SettingsChanged`.
- Use existing DTOs/mutation owners. Snapshot values are actual non-secret values, including names, IDs, URLs, paths, and IPs where applicable—not non-default booleans, hashes, or anonymized substitutes. Preserve structured values and explicit false/zero/null values where the DTO contract includes them.
- Do not add a per-setting enum/default registry or a second settings comparison model. The existing publication path is signal-driven, not proof of a field-level diff or no-op suppression. Changing those semantics requires an explicit task.
- Inspect DTO projection and mutable-state timing when adding settings fields; publish committed state, not a reused mutable 'before' object. Credentials must be deliberately excluded from new analytics, profile, exception-context, and replay payloads.
- Preserve current Serilog/`IsUnmasked` behavior, including export. Operational logs may contain sensitive values under the maintainer's accepted policy; do not add a separate always-masked remote sink or silently rewrite native exception messages.
- Frontend/browser SDK and replay are a later boundary. Preserve backend session correlation; do not implement browser work incidentally. The agreed replay direction is every eligible session with inputs and credential-bearing UI masked.

## Correlation and native error ownership

`BeginRequest` attaches the cached identity and optional browser session header to native activity/log context. Central capture adds current W3C trace/span IDs, session context, build/runtime metadata, and native profile-processing properties.

Quartz helpers carry traceparent, tracestate, and session context in existing job metadata. `BeginJob` starts a consumer activity from that parent and enriches log context with job key/fire-instance identity. Unparented jobs must not inherit the scheduler's unrelated ambient activity; disposable scopes restore context. Retain existing task IDs, library IDs, refresh-run IDs, and operation identities rather than introducing a second correlation system.

Unexpected exceptions use native SDK exception capture through the facade. Expected rejection/cancellation is an outcome, not automatically an error-tracking exception. Request middleware preserves the original exception and excludes ordinary request-abort cancellation. Automatic HTTP instrumentation does not separately record exceptions where explicit error ownership is configured; preserve the ingestion-host exclusion to avoid tracing telemetry's own outbound traffic.

## Verification and investigation

For changes, use the smallest relevant checks after all related edits:

- Facade tests capture native SDK arguments: exact event, outcome, selected values, identity/context, credential absence, and zero emissions on suppressed paths. Include successful envelopes carrying unhealthy/incomplete payloads.
- Feature tests verify result/state transitions and emission timing, not just mock forwarding. Assert no duplicate constituent events, no completion before persistence, and preserved business results when SDK admission fails.
- Hosted tests exercise request/job/context boundaries when those change. Use the repository test runner and test skills; do not weaken assertions or change telemetry eligibility to force green.
- Native SDK acceptance is not remote receipt. Actual ingestion evidence must establish the expected event/profile/error/log/trace and correlation in PostHog. Use current official SDK/product docs for API changes and verify the installed SDK version; documentation for a newer version is not compatibility proof.
- Packaged Docker/desktop and deployed verification are separate gates. Keep them open until the authorized merge/deployment occurs; do not merge, deploy, or mutate remote services to close a local coding task.

Do not encode old test counts, one-session CodeScene skips, or manually verified diagnostic delivery as permanent proof. Preserve prior evidence without presenting it as verification of later edits. For an unconfigured diagnostic response, inspect build eligibility and readiness rather than adding fallback credentials or bypasses.

Official SDK reference: https://posthog.com/docs/libraries/dotnet
