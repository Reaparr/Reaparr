# PostHog setup plan and maintainer runbook (issue 678)

## Status and scope

**Implementation handoff updated; implementation not started.** Updated 2026-11-01 following annotation review to specify a reusable injectable event interface, advanced-settings ownership, and Serilog log export. Event/SDK research was performed on 2026-10-31; PostHog Logs and Serilog OTLP documentation were checked on 2026-11-01. Application implementation, remote configuration, release packaging, and live delivery remain unverified.

The first milestone is **a reusable backend event path plus Serilog log export, verified by a diagnostic proof of concept, then the frontend diagnostic page**. The diagnostic is the first consumer of the event interface, not a separate diagnostic-only transport. Broad product-event instrumentation and the remainder of issue 678 stay outside this milestone.

Repository inspection found no PostHog dependency in `src/AppHost/AppHost.csproj` or `src/AppHost/ClientApp/package.json`, no PostHog module in the inspected Nuxt configuration, and an empty `src/AppHost/Telemetry/GetContext/` directory. The inspected Docker and desktop build paths do not yet forward PostHog build inputs. The previous runbook's claims of retained integration and completed source-map tooling must not be treated as implementation evidence.

## Agreed behavior

- Use the maintainer's **PostHog Cloud US** project, with ingestion at `https://us.i.posthog.com`.
- Supply the ingestion host and project ingestion token **at build time** for distributed Docker and desktop releases. Changing either requires a new build.
- Runtime environment variables, command-line configuration, settings files, and generic configuration binding must not replace the embedded connection configuration. Runtime environment values with matching names must have no effect.
- Only public ingestion configuration is supplied to these builds. **No personal API, source-map upload, secret feature-evaluation, or MCP credentials** are supplied to the builds or embedded in outputs.
- Configured releases default to enabled, with persisted **instance-wide administrator opt-out** at `advancedSettings.telemetryDisabled`, covering backend events, frontend-origin events, and admission of new logs to remote export.
- Ordinary local development and automated tests do not silently transmit to the real project. Live local verification requires an explicitly configured build. A published `dev` release is distinct from a local run.
- Event capture is initially exercised only by deliberate diagnostics. Automatic startup/pageview/click/product events, replay, surveys, and feature flags remain out of scope. **Automatic Serilog Information/Warning/Error/Fatal export is now in scope.**
- Put the administrator-only **Send test event** action on the frontend debug page. Put the instance opt-out control on **Settings → Advanced**, not on the debug page.
- Use a lean injectable `ITelemetry` interface backed by the single-event Capture API for events. No PostHog .NET/browser event SDK, event batching, event offline storage, or custom event retries. Separately use the standard batched Serilog OTLP sink for logs.
- Event `distinct_id` is the existing login account UUID (`AppUser.Id`), supplied by trusted server-side context. No generated installation identity. Event properties exclude usernames, emails, tokens, Plex IDs, media, paths, URLs, and free text. Logs follow the broader, explicitly approved masked-operational-log policy below; the event allowlist is not a claim about log contents.
- Event opt-out prevents new dispatch and retains no unsent event work. **For logs, opt-out stops new admission; logs accepted before opt-out may drain or retry afterward**, including during shutdown. This exception was explicitly selected; do not claim all network activity stops instantly.
- PostHog failure must not prevent startup, login, or ordinary application work. Diagnostic failures are visible; remote logging remains best effort and cannot throw into the application. Neither event nor log submission proves remote delivery.

### Credential and privacy boundaries

PostHog documents the project token as public: it supports ingestion and public client operations, not private-data access. Anyone obtaining a Docker image or desktop executable can recover embedded values. Keeping the token out of browser configuration is an architectural choice, not a confidentiality guarantee. Personal API keys are different and must never ship ([token guidance](https://posthog.com/docs/api), [Nuxt credential guidance](https://posthog.com/docs/libraries/nuxt-js)).

The login account UUID is a pseudonymous account identifier, not an anonymous installation ID. Reuse the existing persisted `AppUser.Id` from `src/Identity.Contracts/Models/AppUser.cs`; do not use a Plex account ID, username, email, security stamp, or browser-supplied ID. Both diagnostic paths use the same authenticated account UUID. Keep `$process_person_profile: false` and do not call `identify`, alias, or person-property APIs; suppressing a PostHog person profile does not make an account-linked identifier anonymous.

Identity follows the existing account lifecycle: recreating the account can change its UUID, and copying the authentication database can copy it. The current `CreateDefaultAppUserCommand` deletes/recreates the account when `ResetCredentials` is enabled. Do not promise immutable per-installation uniqueness or add a separate identity lifecycle for telemetry.

No implementation can retract a request already dispatched. The direct event path has no queue or retries. The Serilog path intentionally uses its normal in-memory batch/retry behavior: opt-out filters new log admission, not previously accepted batches. No disk spool, custom retry policy, queue-purge lifecycle, or replay of disabled-period logs is required.

## Maintainer prerequisites

1. Create/select one restricted-access Reaparr project in PostHog Cloud US. Record organization/project IDs, region, retention, spending limits, reviewer, and verification date in the team's private operations record. Do not put actual credential values in this plan.
2. Obtain the project's ingestion token; it authenticates both events and OTLP logs. No personal API key is needed ([Capture API](https://posthog.com/docs/api/capture), [PostHog .NET Logs](https://posthog.com/docs/logs/installation/dotnet)). Enable/confirm Logs availability and its retention/spending controls in the selected project.
3. Configure host/token as release-build inputs in repository Actions configuration and explicitly forward them through dev/stable callers to reusable Docker and desktop build jobs. Build-time environment variables may transport values into the compiler; runtime environment variables may not configure the installed application.
4. Disable stored IP capture in **Project settings → General → IP data capture configuration**. Existing projects do not automatically inherit later organization-default changes. Review retention, deletion handling, and product spending controls for the chosen project ([data collection controls](https://posthog.com/docs/privacy/data-collection)).
5. Review the opt-out/opt-in legal requirements for the distribution and jurisdictions before publishing. The selected policy is enabled-by-default explicit product-event diagnostics **plus automatic masked Information+ operational logs**, with instance-wide opt-out and permitted drain of pre-opt-out log batches. This document does not establish legal consent compliance.
6. Verify events through the PostHog UI using maintainer account access. No MCP or management credential is needed in Reaparr, the debug page, or the release builds.

## Fixed implementation contracts

These are the implementation choices for the agreed milestone, not additional product features. Existing repository conventions take precedence for mechanical naming/placement; the behavior and boundaries below are fixed.

### Build-time ingestion configuration

| Boundary | Host | Project token |
| --- | --- | --- |
| Repository Actions variables | `POSTHOG_HOST` | `POSTHOG_PROJECT_TOKEN` |
| Reusable workflow string inputs | `posthog_host` | `posthog_project_token` |
| Docker build arguments | `POSTHOG_HOST` | `POSTHOG_PROJECT_TOKEN` |
| Desktop build-tool options | `--posthog-host` | `--posthog-project-token` |
| MSBuild properties | `ReaparrPostHogHost` | `ReaparrPostHogProjectToken` |
| Entry-assembly metadata keys | `ReaparrPostHogHost` | `ReaparrPostHogProjectToken` |
| `IAppBuildInfo` read-only properties | `PostHogHost` | `PostHogProjectToken` |

- Stamp metadata only into the entry application assembly, using the existing assembly-metadata mechanism and build-system escaping, not generated source string interpolation. Read it once through `AppBuildInfo`; update `DesignTimeAppBuildInfo` and test implementations with empty values.
- Both values absent means unavailable for ordinary local/test builds. A partial pair or invalid supplied value is a build error. Release callers and reusable jobs with publishing enabled must reject a missing pair before packaging/pushing. Do not introduce an extra runtime enable/configuration variable.
- The chosen ingestion origin is `https://us.i.posthog.com` (an optional trailing slash may be normalized at build time). Derive `/i/v0/e/` for events and `/i/v1/logs` for logs; no third build credential or independently configurable log host. Reject another origin, credentials, query, fragment, or host path. Accept the public ingestion token, never a personal/project-secret key; do not require a management API call to validate it.
- Explicitly forward build inputs through both dev/stable callers, reusable jobs, Docker publish, and desktop publish commands. Desktop frontend preparation needs no PostHog credentials because its assets call only Reaparr.
- Do not copy build inputs into final-image `ENV`, startup scripts, writable settings, Nuxt public configuration, or desktop runtime options. Do not log complete token values. The public token remains recoverable from the release assembly.
- Host/token changes must invalidate the application publish layer/artifact. Test conflicting `POSTHOG_*`, `PostHog__*`, and MSBuild-property-named runtime environment values against built artifacts; none may change the selected destination or token.

### Advanced settings and instance opt-out

- The current Advanced page (`src/AppHost/ClientApp/src/pages/settings/advanced.vue`) has no corresponding `AdvancedSettings` model. Add a minimal sibling `AdvancedSettingsModule`, `IAdvancedSettings`, and `AdvancedSettingsDTO`, with one non-required boolean `TelemetryDisabled`, default `false`. Expose it as `advancedSettings.telemetryDisabled`; do not put it in `DebugSettings`.
- Wire `AdvancedSettings` into `IUserSettings`, `UserSettings`, `SettingsModelDTO`, both mapper directions, reset/change notifications, settings-store defaults, and affected fixtures. Use existing `UserSettings`/`ConfigManager` persistence, not a new store or database migration. Old saved JSON without the module/property must load with the default without resetting unrelated settings.
- Keep existing settings GET. Ordinary whole-settings PUT must preserve the current server-side telemetry opt-out rather than apply a stale snapshot; unrelated autosave must not silently re-enable events or logs.
- Mutate only this field through administrator-authorized `PUT /api/Settings/advanced/telemetry`, with required `{ "disabled": true }` or `{ "disabled": false }`, returning the normal settings Result envelope. Put the separate operation in `UserSettings/SetTelemetrySettings/`; update `AdvancedSettings.TelemetryDisabled` using existing notification/persistence. Reject missing/null/additional inputs and unauthorized callers before mutation.
- Add a `TelemetrySection` to Settings → Advanced with the opt-out toggle and an explanation that already-accepted logs may finish uploading. The debug page shows current status and links to Advanced; it does not duplicate the toggle.
- Wait for the dedicated settings mutation response before showing a completed change. Refresh settings on entering either page; no polling or new SignalR topic. Backend event dispatch and log admission consult the live instance setting, so stale pages cannot bypass it.
- Events check opt-out immediately before dispatch and retain no queue. Logs check opt-out before being passed to the batched OTLP sink; previously accepted batches may drain. Re-enabling resumes admission without creating or replaying logs/events from the disabled interval.

### Lean injectable application interface

Add one provider-neutral interface in the existing Domain contract boundary (`src/Domain/Telemetry/ITelemetry.cs`, namespace `Reaparr.Domain`), so application classes can depend on it without referencing PostHog or an HTTP client:

```csharp
public interface ITelemetry
{
    Task<Result> TrackAsync(
        string eventName,
        string accountUuid,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken);
}
```

- Implement once as `PostHogTelemetry` in Application and register it against `ITelemetry` in the existing Autofac composition. Inject `ITelemetry` into use cases that own an event. The diagnostic command handler is the first consumer and calls this exact implementation; do not add a second diagnostic sender.
- Callers supply the existing account UUID explicitly from trusted application context. The implementation does not depend on `HttpContext`, use a global "current user", or silently select the first account. Future background callers must establish their account context; wiring unrelated jobs is not part of this proof of concept.
- `parameters` contains event-specific scalar values only. The implementation owns destination/token, `distinct_id`, privacy switches, and release/runtime labels; caller attempts to override reserved fields are rejected. Maintain explicit event/property schemas in the implementation, initially only `reaparr diagnostic` with the finite `origin` parameter. Add future events at their owning use cases and extend their schemas/tests, not via arbitrary browser dictionaries.
- `TrackAsync` checks current opt-out/configuration, validates the event-specific boundary once, adds common metadata, makes one HTTP attempt, and returns the existing Result success/failure/cancellation semantics. Expected disabled/unconfigured states send nothing. Consumers handle the Result as best effort; application correctness must not depend on telemetry.
- Keep this to one interface and one implementation. No builder, ambient context, scoped property bag, event bus, plugin registry, provider hierarchy, extra public configuration interface, or method per event. Do not route Serilog logs through `TrackAsync`; their sink owns the OTLP path.

### Diagnostic API and identity

- Add `POST /api/Debug/telemetry/diagnostic`, under `ApiRoutes.DebugController`, in the owning Application vertical slice `Telemetry/SendDiagnostic/`. Keep endpoint/request/validator together and command/handler together according to existing slice conventions.
- Request body: `{ "origin": 0 }` for `Backend`, `{ "origin": 1 }` for `Frontend`, backed by a shared finite `TelemetryDiagnosticOrigin` enum. The field is required: missing, null, or undefined values produce `400` rather than defaulting to `Backend`. No UUID, event name, properties dictionary, URL, host, or token is an input. Reject additional fields rather than forwarding them.
- Require authentication and `DefaultUserAppCredentials.DefaultAdminRole` explicitly. Resolve the current principal's account name through the existing `IUserService.FindByNameAsync` and take that persisted account's `AppUser.Id`; the name is used only for internal lookup and is never transmitted. Never substitute the first account, a username as distinct ID, a Plex ID, or a caller-supplied identifier.
- The development bypass currently injects `dev-user-id` (`DisableAuthenticationMiddleware`), not a login account UUID. Reject synthetic/unresolvable identity with no outbound request. For local live verification, sign in using the real account; do not modify global authentication behavior to make telemetry work.
- Return the existing FluentResults HTTP envelope, not an upstream PostHog response. `200` means **submitted/accepted by ingestion**, never confirmed delivered; use the fixed success message `Diagnostic event submitted`. Validation uses `400`, unauthenticated requests `401`, non-admin/unresolvable account `403`, disabled telemetry `409`, and unavailable configuration/upstream failure/timeout `503`, with safe fixed messages and no upstream body or credential leakage.
- Use one diagnostic endpoint for both origins. Resolve account/authorization in that slice, then call injected `ITelemetry.TrackAsync("reaparr diagnostic", account.Id, originParameters, ct)`. Backend acceptance invokes it with `Backend`; the frontend action invokes each origin once. This proves the reusable event path without instrumenting unrelated business flows.

### Outbound event and transport

Send `POST https://us.i.posthog.com/i/v0/e/`, JSON:

```json
{
  "api_key": "<embedded project ingestion token>",
  "event": "reaparr diagnostic",
  "distinct_id": "<authenticated persisted AppUser.Id>",
  "properties": {
    "$process_person_profile": false,
    "$geoip_disable": true,
    "origin": "backend",
    "environment": "stable",
    "release": "<exact informational version>",
    "runtime_mode": "<docker or desktop>",
    "platform": "<existing CurrentOS enum name>",
    "runtime_identifier": "<existing runtime identifier>"
  }
}
```

- Map the finite diagnostic origin to `backend` / `frontend`; derive common labels from build metadata (`IsDevRelease` selects `dev` / `stable`). The first event schema is fixed as shown; later application events extend the implementation's explicit schemas. Do not attach exception data, person updates, or browser metadata to events.
- Register a named `PostHog` event client in `HttpClientModule`, following the named GitHub client pattern: fixed 10-second timeout, redirects disabled, **no `DefaultHttpClientRetryHandler`**. Register through existing `Startup.Services`; only `PostHogTelemetry` owns this transport.
- Use the caller token for event network I/O and the existing Result boundary for failure/cancellation, with no second attempt. No global capture hooks or background event service. The separate Serilog sink may batch/retry logs as explicitly approved below.
- Construct a fresh outbound message with JSON content only; never clone browser/HTTP request headers or cookies. Treat an unexpected redirect, non-success status, transport failure, or timeout as a failed submission. Do not follow a redirect with the embedded token.

### Serilog operational log exporter

**Selected policy:** automatically export all masked operational Serilog events at **Information and above**, subject to the existing global logger threshold. Include rendered messages, structured properties, and exception details after independent remote masking. Local-only Debug/Verbose do not become remote logs, and changing PostHog configuration must not alter local sink levels.

- Add the real `Serilog.Sinks.OpenTelemetry` package in `src/Logging/Logging.csproj`, with a reviewed pinned version/lockfile. It maps Serilog events directly to OTLP and does not require the PostHog event SDK or a second Microsoft `ILogger`/OpenTelemetry logging provider ([Serilog sink](https://github.com/serilog/serilog-sinks-opentelemetry)).
- Configure HTTP/Protobuf with the explicit **logs endpoint** `https://us.i.posthog.com/i/v1/logs`, `Authorization: Bearer <embedded project token>`, and traces disabled. Set the sink's `LogsEndpoint` explicitly so base-endpoint suffix handling cannot change `/i/v1/logs`. Use **`ignoreEnvironment: true`**; `OTEL_EXPORTER_OTLP_*`, `OTEL_RESOURCE_ATTRIBUTES`, and `OTEL_SERVICE_NAME` must not override embedded settings or approved resource labels ([PostHog Logs](https://posthog.com/docs/logs/installation/dotnet), [sink configuration](https://github.com/serilog/serilog-sinks-opentelemetry/blob/dev/README.md)).
- Preserve existing `Serilog.ILogger`/`Log.*` calls. Extend the existing `LogConfig` / `SignalRLogConfig` path with one remote branch; retain console, file, Seq, log buffer, and SignalR behavior. Do not add a parallel provider that exports the same event twice.
- Keep remote admission closed until persisted settings have loaded successfully. `Program` initializes logging before `SetupConfigFile`, and `AttachSignalR` rebuilds the logger: the remote branch must have a single shared export owner across those stages, not duplicate queues or exporters. Do not replay buffered bootstrap logs. Ordinary unconfigured/dev-test builds never activate remote export.
- Gate the remote branch at admission on valid build configuration, loaded settings, and `!AdvancedSettings.TelemetryDisabled`. Use the standard bounded, in-memory sink batching/retry behavior; pin and test the selected package defaults. No disk spool or app-owned queue/retry loop. Previously admitted logs may drain after opt-out; shutdown may flush them. No purge/recreate lifecycle is needed under the chosen policy.
- Apply remote masking **regardless of `IAppRuntimeInfo.IsUnmasked`**. The current `LogConfig` skips local masking in that mode, which must not enable unmasked remote export. Mask only the remote copy; do not change local debugging behavior.
- Remote masking covers rendered text, structured values, and exception messages/stack strings; property-name filtering alone is insufficient. Reuse existing email/URL/sensitive-property masking where applicable, and explicitly redact credentials/tokens, authorization/cookies, account names, Plex identifiers/media values, and filesystem paths in the exported representation. Test nested values and exception strings. If a record cannot be sanitized through the selected representation, drop it remotely rather than sending raw content.
- Do not pass the original `Exception` to the remote sink after merely masking structured properties: that sink serializes `Exception.Message`/`ToString()` independently. Prepare a remote-only log representation with sanitized text/properties and sanitized `exception.type`, `exception.message`, and `exception.stacktrace` attributes, without the original exception object. Preserve timestamp/severity and leave the local event unchanged.
- **Privacy limit explicitly accepted:** this is broad best-effort masked operational logging, not the narrow event-property allowlist and not a guarantee that all personal/media data can be recognized in free text. Messages and exceptions may still contain residual sensitive context. Record that broader collection in the maintainer privacy review.
- Resource labels are `service.name=reaparr`, service version, environment, runtime mode, platform, and runtime identifier from backend build metadata. Do not automatically attach hostnames, filesystem paths, IPs, raw request headers, or person/session enrichment. Process logs without a user context remain process logs; do not manufacture an account UUID for them.
- Exclude the exporter's own HTTP/transport/failure diagnostics from its remote branch to avoid recursive export. Sink failures remain isolated and visible through local diagnostics, not thrown into application call sites or re-emitted as another remote error.
- Set a bounded export timeout and disable redirect forwarding of the embedded token through the sink's supported HTTP handler hook. No external collector, OTLP runtime configuration, new privileged credential, traces, or metrics are required.
- Verification must observe an actual Serilog Information log and an exception-bearing Error log in **PostHog Logs**, separately from the diagnostic events view. Test masking with unmasked local mode enabled, single-export behavior across startup/SignalR attachment, admission stopping on opt-out, and the permitted drain of pre-opt-out batches.

### Frontend ownership

- New `src/AppHost/ClientApp/src/pages/debug-pages/telemetry.vue` contains only administrator diagnostic actions/status and a link to Advanced. Add the opt-out `TelemetrySection` to the existing Advanced page. Use Quasar controls, existing translations, and stable `data-cy` selectors.
- Extend `settingsStore` with the new `advancedSettings` defaults/mapping, the dedicated opt-out mutation, and the diagnostic action using the generated client. Ordinary autosave preserves server opt-out; no new telemetry store/framework.
- The debug **Send test event** action requests backend then frontend-origin diagnostics, attempting the second even if the first fails and displaying each result independently. Disable submission while a settings mutation/test is pending or telemetry is disabled; do not automatically retry.
- No automatic product event on mount, navigation, reload, login, toggle, or re-enable. Those operations may produce approved Serilog operational logs. No browser PostHog dependency, direct ingestion traffic, or token/UUID configuration is needed.
- Regenerate the TypeScript API from the running backend using `bun run generate-api-ts` from `src/AppHost/ClientApp`; update affected fixtures/defaults to the generated contract rather than hand-editing generated files.

## Phase 1 — Backend setup and verification

### Build and configuration

- Extend `Directory.Build.props`, `IAppBuildInfo`, `AppBuildInfo`, and `DesignTimeAppBuildInfo` using the fixed metadata contract above; update affected implementations/test fixtures together.
- Wire the existing release paths: `.github/workflows/dev-release.yml`, `.github/workflows/stable-release.yml`, `.github/workflows/docker-build-multiarch.yml`, `.github/workflows/desktop-build.yml`, `docker/Dockerfile`, and `tools/Build/Workflows/DesktopPublishWorkflow.cs`.
- Supply the same selected host/token to every Docker architecture and desktop runtime. Keep the public ingestion configuration separate from ordinary application secrets and from any deferred tooling credentials.
- Make publishing fail on missing or invalid required ingestion configuration; keep ordinary unconfigured development/test builds usable with telemetry unavailable. Never silently fall back to a different project or region.
- Compile/embed the selected values; do not leave a runtime `POSTHOG_*`, `PostHog__*`, or equivalent options-binding path. Do not embed credentials in writable `appsettings` and call them immutable.
- Preserve exact informational release metadata across backend and frontend. Use `environment` (`dev` / `stable`), `release`, `runtime_mode`, `platform`, and `runtime_identifier` for the diagnostic labels already agreed in the runbook.

### Application behavior

- Implement `advancedSettings.telemetryDisabled`, the small advanced-settings module, and its administrator mutation API in the backend phase. Add the Advanced-page control in the frontend phase; reuse the existing account UUID.
- Implement/register `ITelemetry` and `PostHogTelemetry`. Inject `ITelemetry` into the diagnostic command handler as the first caller; keep the administrator endpoint thin, enforce authorization server-side, and preserve Result semantics. The same interface is available to future system-wide use cases without endpoint or HTTP coupling.
- Validate event schemas and parameters in the implementation; own all common/identity/connection fields there. Do not expose a generic capture endpoint or wire unrelated business-event call sites in this milestone.
- Add the Serilog OTLP remote branch with independent masking and the selected Information+ policy. Do not add PostHog request-context middleware, automatic product-event/exception capture, feature management, replay, or a duplicate logging provider.
- Events return submitted/failed, not delivered. Logs are best-effort batched exports; only observing the event feed and Logs UI proves each remote path.

### Backend acceptance checkpoint

- Targeted tests cover enabled/disabled behavior, reuse of the authenticated account UUID on both paths, rejection of caller-supplied identities, prohibited property exclusion, endpoint authorization, unavailable upstream behavior, and attempted runtime configuration overrides.
- Exercise the real backend diagnostic path with an explicitly configured build; observe its event in the selected PostHog project's activity feed with the correct ID and labels.
- Confirm startup/login/unrelated operations produce no automatic product events. Verify their eligible operational logs export only after persisted opt-out has loaded, and confirm Information/exception-bearing Error records in PostHog Logs with masking applied.
- Finish backend delivery verification before implementing frontend capture. The initial backend checkpoint can invoke the administrator diagnostic API directly; the final user-facing trigger belongs only on the frontend debug page.

## Phase 2 — Frontend setup and verification

- Reuse `debug-pages/` for proof actions and the existing Advanced page for opt-out. Do not put the toggle in DebugSettings or duplicate it on the diagnostic page.
- Make the debug-page action exercise distinguishable backend and frontend diagnostics sharing the authenticated login account UUID resolved by the backend. Show the result of each path independently so one success cannot hide the other failure.
- Route browser diagnostics through the Reaparr backend. The backend must check current instance opt-out and administrator authorization for every submission, even when an already-open tab has stale state.
- Use the selected browser → Reaparr → PostHog Capture API path. Frontend-origin delivery proves the diagnostic relay, not initialization of `posthog-js`; neither the browser SDK nor the .NET SDK is part of this milestone.
- Do not expose a general-purpose forwarding proxy. Allow only the diagnostic operation/payload required by this milestone, to the embedded destination; do not forward Reaparr cookies, session authorization, original client IP headers, browser URLs, or arbitrary browser headers to PostHog.
- Do not load a browser PostHog SDK, remote configuration, or ancillary assets. The browser sends only the fixed diagnostic request through the authenticated Reaparr API; the backend supplies account identity and the allowlisted payload.
- Use existing settings refresh and the dedicated advanced opt-out mutation, with server policy authoritative for stale pages. Disabled-period events/logs are not retained; re-enable does not replay them. Already-admitted pre-opt-out logs may still drain.

### Frontend acceptance checkpoint

- Targeted tests cover administrator-only access, disabled state, stale-tab rejection, independent backend/frontend statuses, correct origins, and prohibited property exclusion.
- In a real browser, activate the debug-page test action and inspect network requests. Observe both origin events in the selected PostHog project with the same login account UUID and correct release/runtime labels.
- Navigate/reload/log in without clicking the diagnostic: no automatic product event or browser PostHog asset/config request. Backend operational logs from that work are explicitly in scope.
- Disable instance telemetry and attempt both event paths from a stale tab: no new event dispatch or remote-log admission. Previously admitted log batches may still upload. Logs/events created while disabled must not appear after re-enable/restart.
- Simulate unavailable PostHog: show diagnostic failure without breaking the debug page or normal application work.

## Selected implementation and supporting rationale

The Capture API approach and fixed contracts above are selected. SDK findings explain why they were chosen; they are not unresolved implementation alternatives. Packaged-artifact verification is required evidence for the build-time configuration contract.

### 1. Use the supported Capture API for this milestone

The official single-event API accepts a project token, `event`, `distinct_id`, and optional properties at the regional ingestion host (`POST /i/v0/e/`). This can satisfy manual diagnostic delivery without automatically loading a browser SDK or maintaining SDK background queues ([API reference](https://posthog.com/docs/api/capture)).

**Selected event approach:** one provider-neutral `ITelemetry` implemented by `PostHogTelemetry`, with a typed diagnostic relay as its first caller. Each event uses one Capture API request, no event queue/retry framework. Serilog is a separate standard batched OTLP log path, not a PostHog event SDK.

For the event path, the SDK queue concern remains concrete: browser events accepted before opt-out can still be delivered from pending batches/retries ([maintainer clarification](https://github.com/PostHog/posthog-js/issues/5020#issuecomment-5798941874)), and the .NET public client lacks queue discard and flushes on disposal ([client contract](https://github.com/PostHog/posthog-dotnet/blob/main/src/PostHog/IPostHogClient.cs), [implementation](https://github.com/PostHog/posthog-dotnet/blob/main/src/PostHog/PostHogClient.cs)). Direct unqueued events retain the stronger event policy. Logs intentionally use a different, explicitly approved policy allowing pre-opt-out batches to drain.

Benefit: the proof validates reusable common enrichment/policy/transport for later application events without scattering business instrumentation now. The Serilog branch provides system-wide operational logs immediately. Broad product-event capture, replay, and browser error tracking remain deferred.

### 2. Prevent person-profile creation and GeoIP enrichment explicitly

For Capture API events, use `$process_person_profile: false` with the existing login account UUID as `distinct_id`. Do not call `identify`, alias, or person-property APIs. This suppresses person-profile processing; it does not make the account-linked identifier anonymous. The API otherwise treats captures as identified by default, and an ID previously associated with a PostHog person can remain identified ([capture profile processing](https://posthog.com/docs/api/capture#anonymous-event-capture)).

For direct Capture API submissions, set `$geoip_disable: true` alongside `$process_person_profile: false`, and discard IP capture in project settings. The .NET SDK already sets the GeoIP-disable property; a direct API adapter must do so explicitly. Do not forward end-user IP headers. These controls avoid unnecessary enrichment but do not hide the server's network source address from the remote service ([server-side property guidance](https://posthog.com/docs/product-analytics/identity-resolution), [SDK event construction](https://github.com/PostHog/posthog-dotnet/blob/main/src/PostHog/Api/CapturedEvent.cs), [privacy controls](https://posthog.com/docs/privacy/data-collection)).

### 3. Use a diagnostic relay, not a general-purpose SDK proxy

PostHog's general proxy supports asset/config hosts, ingestion versions, compression, and SDK-specific headers; its reference also recommends forwarding IPs for geolocation and large recording payloads. Those are requirements for a full SDK proxy, not appropriate defaults for this narrow diagnostic operation ([proxy reference](https://posthog.com/docs/advanced/proxy/proxy-reference)).

**Selected boundary:** the diagnostic relay calls injected `ITelemetry`; it is not an arbitrary PostHog SDK proxy. System-wide events use the same interface directly from owning classes. Operational logs use the separately configured Serilog sink.

### 4. Verify immutable configuration in packaged artifacts

Test conflicting runtime environment values against the built Docker and desktop outputs, not only mocked options. Verify that rebuilding with changed host/token changes the embedded configuration and invalidates the relevant build cache. Validate property handling and escaping at the build boundary instead of interpolating token text directly into source.

A fixed token cannot be revoked or retargeted in already distributed binaries without breaking their delivery or distributing an update. Record that operational consequence rather than adding an unapproved remote-configuration escape hatch.

### 5. Keep verification status honest and attributable

Use the fixed `reaparr diagnostic` event and authoritative `backend` / `frontend` origin values defined above. The UI says submitted/accepted, not delivered. During live verification, match login account UUID, origin, release, and trigger time to avoid mistaking an older event for the current run. Do not add ingestion read access or a privileged polling API to automate confirmation.

## Deferred work and explicit exclusions

- Broad automatic product-event instrumentation, dedicated exception-event/error tracking, feedback, bug reports, dashboards, AI/Self-driving, surveys, session replay, feature flags, traces, and metrics. Serilog masked Information+ logs, including exception details, are explicitly included.
- PostHog MCP setup and credentials.
- Source-map injection/upload, PostHog CLI credentials, hidden-map release changes, and .NET symbol upload. No release in this milestone should require a privileged PostHog key. Existing unrelated release tooling is not authorized for removal or refactoring.
- Remote project changes are manual maintainer prerequisites, not something this plan or an application build performs.
- No custom retries/offline stores, remote configuration, compatibility shims, telemetry builder/registry/context framework, or extra provider hierarchy. The single `ITelemetry` interface/implementation and standard Serilog sink batching/retries are explicitly in scope.

## Ordered implementation work packages

Complete all implementation and test edits for each package before running its checks. Backend-first is a real acceptance gate, not merely file-edit order.

| Order | Implementation owners | Required result and verification |
| --- | --- | --- |
| 1. Immutable build configuration | `Directory.Build.props`; `src/Environment/Interfaces/IAppBuildInfo.cs`; `AppBuildInfo.cs`; `DesignTimeAppBuildInfo.cs`; release/reusable workflows; `docker/Dockerfile`; `tools/Build/Arguments/DesktopCommandSettings.cs`; `tools/Build/Workflows/DesktopPublishWorkflow.cs` | Forward, validate, stamp, and read the two fixed values. Extend existing `Environment.UnitTests` and `Build.UnitTests` responsibilities for absent/partial/invalid inputs, metadata values, publish inputs, and runtime-override immunity. Keep unconfigured local/test builds usable. |
| 2. Reusable events, advanced settings, and Serilog | New Domain `ITelemetry`; Application `PostHogTelemetry`; `AdvancedSettingsModule`/interface/DTO and settings wrappers/mappers; settings update preservation; `Telemetry/SendDiagnostic/` and `UserSettings/SetTelemetrySettings/`; shared origin enum; HTTP registration; `Logging.csproj`, `LogConfig`, `SignalRLogConfig`, and `Program` activation | Implement the lean interface, advanced opt-out and authorization, account UUID event identity, one-attempt event transport, and single masked Information+ OTLP log branch. Extend Domain/Application/Settings/Logging and startup tests according to ownership. Ordinary tests use isolated transports. |
| 3. Backend acceptance gate | Configured backend artifact + maintainer project access | Targeted checks and production-file Code Health reviews; observe an authenticated backend-origin event plus real Serilog Information/Error records in PostHog Logs. Verify masking and opt-out admission/drain policy. Missing live access blocks this gate, not isolated implementation work. |
| 4. Advanced settings and frontend diagnostic | Advanced page + new `TelemetrySection`; diagnostic debug page; `settingsStore`; generated API; navigation/translations/defaults/fixtures | Advanced controls instance opt-out; debug page exercises both event origins independently and links to Advanced. Add store/component/browser tests; no browser SDK or duplicated opt-out control. |
| 5. End-to-end and packaging | Real browser; Docker and desktop artifacts | Verify events and Logs separately, masking, no automatic product events, no disabled-period event/log admission, permitted pre-opt-out log drain, non-fatal export failure, no duplicate sinks, immutable runtime configuration including hostile `OTEL_*` values, and precise platform coverage. |

New backend folders retain their owning project's root namespace and `.csproj.DotSettings` exclusions. Keep endpoint operations separate. Check references for shared contracts and settings wrappers. Add only the Serilog OTLP dependency needed for logs; no PostHog event SDK or compatibility aliases.

### Required regression matrix

- **Build:** no pair locally → unavailable; partial/invalid pair → build failure; publishing without a pair → publish gate failure; configured artifacts → embedded values survive conflicting runtime environment settings; changed build inputs → changed embedded output.
- **Settings:** legacy JSON without `advancedSettings` loads without resetting other values; explicit disabled state survives serialization/restart; stale whole-settings PUT cannot re-enable it; only the administrator mutation changes it; unauthorized changes return `403` without side effects.
- **Identity/auth:** anonymous → `401`; non-admin/synthetic/unresolved account → `403`; both allowed origins use the real persisted `AppUser.Id`; caller-supplied identity/properties/host/token and undefined origins are rejected with no upstream request.
- **Event interface/transport:** diagnostic uses the injected production `ITelemetry`; common metadata and identity cannot be overridden; unsupported event/parameter schemas fail without sending; successes have the exact allowlist; disabled/unconfigured events never send; failure/timeout/redirect/cancellation causes no retry/replay or credential leakage.
- **Logs:** Information+ eligible, Debug/Verbose excluded; independent remote masking also in local unmasked mode, nested properties, and exception strings; sink failures do not break local logs/app work; settings load gates startup admission; SignalR logger replacement does not duplicate export; runtime `OTEL_*` values cannot change endpoints/headers/resources. Opt-out blocks new admission but allows pre-opt-out drain; disabled-period logs never queue or replay.
- **Frontend/live:** Advanced mutation completes before diagnostic submission; debug invokes both origins once even on partial failure; show per-origin submitted/failed/disabled status. Observe backend/frontend events by UUID/release/time and separate Serilog records in PostHog Logs. No product events on mount/navigation/toggle; ordinary backend logs are expected when enabled.

### External prerequisites and stop conditions

- No real token/private operations record is needed for isolated implementation/tests. Live acceptance needs maintainer-provided US ingestion configuration, event feed and Logs access, project IP-discard/retention/spending settings, and the approved broad masked-log privacy review.
- Release workflow validation requires repository Actions-variable administration; publication itself is not authorized by this plan. Never publish, push, commit, or change remote project settings merely to complete a verification checkbox.
- Packaged-runtime verification requires the relevant Docker/desktop runtime; only claim the platforms actually exercised. No host-package installation is authorized.
- SDK-versus-API and event identity are settled. Logs use the Serilog OTLP sink with broad masked Information+ content and permitted pre-opt-out drain, as explicitly selected. No privileged credential is required. Missing live access blocks acceptance, not permission to fabricate delivery or silently narrow the log scope.

## Verification and delivery record

Implementation verification must use repository-approved targeted backend/frontend tests, production-file Code Health reviews, a real browser for the debug control, and packaged-runtime smoke checks for immutable Docker/desktop configuration. Ordinary tests must use isolated transports and must not send to the production project. Run related checks only after their implementation and test edits are complete.

Record: artifact/version/runtime, build configuration source (not token values), advanced opt-out state, observed event and Logs timestamps, event UUID/origin labels, log masking/exception inspection, pre-opt-out drain versus disabled-period admission, duplicate-export check, runtime/OTEL override result, outage result, reviewer/date, and exact platform coverage.

For this document revision: repository spot checks and official documentation research were performed. No application tests, package builds, remote project changes, or live ingestion checks were executed; no implementation or delivery success is claimed.

## Companion research

- [Backend SDK and transport findings](posthog-backend-research.md)
- [Frontend SDK and proxy findings](posthog-frontend-research.md)
