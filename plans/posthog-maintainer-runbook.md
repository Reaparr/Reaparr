# PostHog setup plan and maintainer runbook (issue 678)

## Status and scope

**The SDK cutover and scoped logger/test alignment are implemented and verified locally; live SDK delivery and release packaging remain unverified.** Updated 2026-11-04. The implementation uses the official core PostHog .NET SDK `2.18.1`, its normal singleton lifecycle, the current telemetry identity, and the retained Serilog OTLP path. Frontend diagnostics remain Phase 2/future. Earlier local verification entries below are historical records from earlier worktree states, not proof of current source; the current scoped verification is recorded separately.

The first milestone is **a reusable backend event path while retaining the existing Serilog OTLP log export, with a diagnostic proof of concept as its backend acceptance target**. Frontend diagnostics remain Phase 2/future. The diagnostic is the first consumer of the event interface, not a separate diagnostic-only transport. Broad product-event instrumentation and the remainder of issue 678 stay outside this milestone.

**Approved implementation deviations — correct, not outstanding fixes (2026-11-04):** ordinary endpoint authentication without an explicit administrator-role check; identity resolution through `IUserService.GetTelemetryId` without adapter UUID validation or a safe lookup-failure `503` guarantee; immediate configured-build OTLP export following the global log threshold; and existing masking controlled by `IsUnmasked`. These choices supersede the earlier stricter requirements. Only logger lifecycle/existing-sink preservation and stale regression coverage required correction.

Earlier repository inspection found no PostHog dependency in `src/AppHost/AppHost.csproj` or `src/AppHost/ClientApp/package.json`, no PostHog module in the inspected Nuxt configuration, and an empty `src/AppHost/Telemetry/GetContext/` directory. That dependency observation is superseded by the selected core `PostHog` `2.18.1` package; application registration, Docker/desktop build forwarding, packaged artifacts, and live delivery remain unverified. The previous runbook's claims of retained integration and completed source-map tooling must not be treated as implementation evidence.

## Agreed behavior

- Use the maintainer's configured PostHog project and its build-time `TelemetryHost` ingestion origin; supported regions and domains are selected by configuration, not a fixed US host.
- Keep project-level IP storage enabled (`anonymize_ips: false`), as selected during live setup on 2026-11-01. This does not add end-user IP forwarding or remove event GeoIP suppression.
- Supply `TelemetryHost` and `TelemetryProjectToken` at build time for distributed Docker and desktop releases. Changing either requires a new build.
- Runtime environment variables, command-line configuration, settings files, and generic configuration binding must not replace the embedded connection configuration. Runtime environment values with matching names must have no effect.
- Only public ingestion configuration is supplied to these builds. **No personal API, source-map upload, secret feature-evaluation, or MCP credentials** are supplied to the builds or embedded in outputs.
- Configured releases enable the diagnostic event path and standard operational log export with existing masking behavior, without an application-level telemetry setting or mutation API.
- Ordinary local development and automated tests do not silently transmit to the real project. Live local verification requires an explicitly configured build. A published `dev` release is distinct from a local run.
- Event capture is initially exercised only by deliberate diagnostics. Automatic startup/pageview/click/product events, replay, surveys, and feature flags remain out of scope. The existing Serilog operational export follows the global logger threshold on its standard OTLP sink and is unchanged by the event SDK selection.
- Put the authenticated **Send test event** action on the frontend debug page. Leave existing settings pages unchanged.
- Use a lean injectable `ITelemetry` interface backed by the official core PostHog .NET SDK `2.18.1` for events. Do not install a browser event SDK. Register the core client through its normal singleton services registration and let the SDK own its default queue, batching, retries, and disposal flush. Do not add custom event transport, fallback, queue, retry, flush, or lifecycle machinery. Separately retain the standard Serilog OTLP sink for logs.
- Event `distinct_id` is the current telemetry ID returned by `IUserService.GetTelemetryId` (the existing login account UUID), supplied by trusted server-side context. No generated installation identity. Event properties exclude usernames, emails, tokens, Plex IDs, media, paths, URLs, and free text. Logs follow the broader, explicitly approved masked-operational-log policy below; the event allowlist is not a claim about log contents.
- The SDK's normal queue/batching/retry/disposal lifecycle is part of the event path. A successful `TrackAsync` means the SDK accepted the event for sending, not that PostHog accepted, ingested, or delivered it. Logs use their existing standard in-memory batching and shutdown behavior.
- PostHog failure must not prevent startup, login, or ordinary application work. Diagnostic failures are visible; remote logging remains best effort and cannot throw into the application. SDK-owned asynchronous upstream failures do not become a synchronous `503` promise for a future batch send; `503` remains valid for unavailable configuration or SDK queue-admission failure, with no safe lookup-failure `503` guarantee.

### Credential and privacy boundaries

PostHog documents the project token as public: it supports ingestion and public client operations, not private-data access. Anyone obtaining a Docker image or desktop executable can recover embedded values. Keeping the token out of browser configuration is an architectural choice, not a confidentiality guarantee. Personal API keys are different and must never ship ([token guidance](https://posthog.com/docs/api), [Nuxt credential guidance](https://posthog.com/docs/libraries/nuxt-js)).

The telemetry ID is the current account UUID returned by `IUserService.GetTelemetryId`, not an anonymous installation ID. Do not use a Plex account ID, username, email, security stamp, or browser-supplied ID. Both diagnostic paths use the same server-resolved telemetry ID. Keep `$process_person_profile: false` and do not call `identify`, alias, or person-property APIs; suppressing a PostHog person profile does not make an account-linked identifier anonymous.

Identity follows the existing account lifecycle: recreating the account can change its UUID, and copying the authentication database can copy it. The current `CreateDefaultAppUserCommand` deletes/recreates the account when `ResetCredentials` is enabled. Do not promise immutable per-installation uniqueness or add a separate identity lifecycle for telemetry.

No implementation can retract an event after the SDK accepts it for sending. The official SDK owns its normal in-memory queue, batching, retry, and disposal-flush lifecycle. Reaparr must not add a second queue, custom retry policy, fallback transport, per-event `FlushAsync`, or queue-purge lifecycle. The Serilog path remains separate and retains its existing standard OTLP batching/retry behavior and shutdown flush.

## Maintainer prerequisites

1. Create/select one restricted-access Reaparr project in the configured PostHog deployment. Record organization/project IDs, ingestion origin, region, retention, spending limits, reviewer, and verification date in the team's private operations record. Do not put actual credential values in this plan.
2. Obtain the project's ingestion token; it authenticates both events and OTLP logs. No personal API key is needed ([Capture API](https://posthog.com/docs/api/capture), [PostHog .NET Logs](https://posthog.com/docs/logs/installation/dotnet)). Enable/confirm Logs availability and its retention/spending controls in the selected project.
3. Configure `TelemetryHost` and `TelemetryProjectToken` as release-build inputs in repository Actions configuration and explicitly forward them through dev/stable callers to reusable Docker and desktop build jobs. Build-time environment variables may transport values into the compiler; runtime environment variables may not configure the installed application.
4. Keep stored IP capture enabled in **Project settings → General → IP data capture configuration** (`anonymize_ips: false`). This retains ingestion-source IPs; Reaparr still does not forward end-user IP headers. Review retention, deletion handling, and product spending controls for the chosen project ([data collection controls](https://posthog.com/docs/privacy/data-collection)).
5. Review applicable privacy and consent requirements for the distribution and jurisdictions before publishing. The selected policy is configured-build explicit product-event diagnostics **plus automatic operational logs at the global logger threshold, with existing `IsUnmasked`-controlled masking**, without an application-level telemetry toggle. This document does not establish legal consent compliance.
6. Verify events through the PostHog UI using maintainer account access. No MCP or management credential is needed in Reaparr, the debug page, or the release builds.

## Fixed implementation contracts

These are the implementation choices for the agreed milestone, not additional product features. Existing repository conventions take precedence for mechanical naming/placement; the behavior and boundaries below are fixed.

### Build-time ingestion configuration

| Boundary | Host | Project token |
| --- | --- | --- |
| Repository Actions variables | `TELEMETRY_HOST` | `TELEMETRY_PROJECT_TOKEN` |
| Reusable workflow string inputs | `telemetry_host` | `telemetry_project_token` |
| Docker build arguments | `TELEMETRY_HOST` | `TELEMETRY_PROJECT_TOKEN` |
| Desktop build-tool options | `--telemetry-host` | `--telemetry-project-token` |
| MSBuild properties | `ReaparrTelemetryHost` | `ReaparrTelemetryProjectToken` |
| Entry-assembly metadata keys | `ReaparrTelemetryHost` | `ReaparrTelemetryProjectToken` |
| `IAppBuildInfo` read-only properties | `TelemetryHost` | `TelemetryProjectToken` |

- Stamp metadata only into the entry application assembly, using the existing assembly-metadata mechanism and build-system escaping, not generated source string interpolation. Read it once through `AppBuildInfo`; update `DesignTimeAppBuildInfo` and test implementations with empty values.
- Both values absent means unavailable for ordinary local/test builds. A partial pair or invalid supplied value is a build error. Release callers and reusable jobs with publishing enabled must reject a missing pair before packaging/pushing. Do not introduce an extra runtime enable/configuration variable.
- The configured ingestion origin is an absolute HTTPS origin with an optional port and optional single trailing slash; accept alternate PostHog regions/domains and self-hosted origins. Normalize only that optional trailing slash from the configured value at the build boundary. Let the PostHog SDK select its event endpoints; use `/i/v1/logs` for the standard Serilog OTLP sink. There is no third build credential or independently configurable log host. Reject credentials, query, fragment, or non-root path. Accept the public ingestion token, never a personal/project-secret key; do not require a management API call to validate it.
- Explicitly forward build inputs through both dev/stable callers, reusable jobs, Docker publish, and desktop publish commands. Desktop frontend preparation needs no telemetry credentials because its assets call only Reaparr.
- Do not copy build inputs into final-image `ENV`, startup scripts, writable settings, Nuxt public configuration, or desktop runtime options. Do not log complete token values. The public token remains recoverable from the release assembly.
- Host/token changes must invalidate the application publish layer/artifact. Test conflicting `TELEMETRY_*`, `Telemetry__*`, and `ReaparrTelemetry*` runtime environment values against built artifacts; none may change the selected destination or token.

### Availability

- Telemetry is available only when the immutable build configuration is present. Do not add a telemetry settings module, DTO, toggle, or mutation endpoint.
- Keep the existing application settings and Advanced page unchanged. On a configured build, attach standard remote logging immediately without a settings-loaded admission gate or bootstrap replay. Preserve the file and Seq sinks when telemetry is unconfigured; an unconfigured build omits only remote OTLP export.

### Lean injectable application interface

Add one provider-neutral interface in the existing Domain contract boundary (`src/Domain/Telemetry/ITelemetry.cs`, namespace `Reaparr.Domain`), so application classes can depend on it without referencing PostHog or an event transport:

```csharp
public interface ITelemetry
{
    Task<Result> TrackAsync(
        string eventName,
        IReadOnlyDictionary<string, object?> parameters);
}
```

- Implement once as `Telemetry` in `src/Application/Telemetry/Telemetry.cs` (namespace `Reaparr.Application`) and register it against `ITelemetry` in the existing Autofac composition. Register the official core PostHog `2.18.1` client through its documented normal singleton services registration and inject `IPostHogClient` into `Telemetry`. Inject `ITelemetry` into use cases that own an event. The diagnostic endpoint is the first consumer; do not add a forwarding-only command handler or a second diagnostic sender.
- Every event uses the current telemetry ID returned by `IUserService.GetTelemetryId`, unconditionally. Callers cannot supply an identity. Do not branch on authentication mode, inspect a principal's name, cache an installation identity, or generate a UUID. Do not add adapter-side UUID validation or promise a safe lookup-failure `503`. Future owning use cases use this same interface and identity rule.
- `parameters` contains event-specific scalar values only. The implementation owns the SDK destination/token, `distinct_id`, privacy switches, and release/runtime labels; caller attempts to override reserved fields are rejected. Maintain explicit event/property schemas in the implementation, initially only `reaparr diagnostic` with the finite `origin` parameter. Add future events at their owning use cases and extend their schemas/tests, not via arbitrary browser dictionaries. SDK-owned metadata such as `$lib`, `$lib_version`, the event UUID, and `$is_server` is expected and is not an exact raw API-schema contract.
- `TrackAsync` checks build configuration, validates the event-specific boundary once, resolves the telemetry ID through `IUserService.GetTelemetryId`, adds common metadata, calls `IPostHogClient.Capture` with the SDK's normal lifecycle, and returns the existing Result semantics. A `true` capture result means the SDK accepted the event for sending; a `false` result is queue-admission failure. Unconfigured builds perform no account lookup or capture. SDK-owned asynchronous upstream failures are not synchronous `TrackAsync` failures, and Reaparr must not call `FlushAsync` per event or add custom retry/fallback behavior. Consumers handle the Result as best effort; application correctness must not depend on telemetry.
- Keep this to one interface and one implementation. No builder, ambient context, scoped property bag, event bus, plugin registry, provider hierarchy, extra public configuration interface, or method per event. Do not route Serilog logs through `TrackAsync`; the existing standard OTLP sink owns the Logs path.

### Diagnostic API and identity

- Add `POST /api/Debug/telemetry/diagnostic`, under `ApiRoutes.DebugController`, in the owning Application vertical slice `Telemetry/SendDiagnostic/`. Keep endpoint/request/validator together. The endpoint calls injected `ITelemetry` directly; `Telemetry` owns identity and SDK submission, so no forwarding-only diagnostic command is needed.
- Request body: `{ "origin": 0 }` for `Backend`, `{ "origin": 1 }` for `Frontend`, backed by a shared finite `TelemetryDiagnosticOrigin` enum. The field is required: missing, null, or undefined values produce `400` rather than defaulting to `Backend`. No UUID, event name, properties dictionary, URL, host, or token is an input. Reject additional fields rather than forwarding them.
- Require ordinary authenticated access. Do not add an explicit `DefaultUserAppCredentials.DefaultAdminRole` requirement or inspect the principal's name; authorization is independent of event identity, and the implementation uses the current telemetry ID from `IUserService.GetTelemetryId`.
- This telemetry-ID rule applies equally with normal authentication, the existing development authentication bypass, and future background callers. Never send `dev-user-id`, a username, a Plex ID, a caller-supplied identifier, or a generated installation identity. Preserve the maintainer's authentication-bypass setting rather than changing global authentication behavior for telemetry.
- Return the existing FluentResults HTTP envelope, not an upstream PostHog response. `200` means **the SDK accepted the event for sending**, never confirmed HTTP acceptance, ingestion, or delivery; use the exact success message `Diagnostic event accepted for sending`. Validation uses `400`, unauthenticated requests `401`, and unavailable configuration or SDK queue-admission failure `503`, with safe fixed messages and no upstream body or credential leakage. Do not promise a safe lookup-failure `503`; SDK-owned asynchronous upstream failures must not be represented as a synchronous `503` promise for a future batch send.
- Use one diagnostic endpoint for both origins. Require ordinary authentication and the fixed request schema, then call injected `ITelemetry.TrackAsync("reaparr diagnostic", originParameters)`. Backend acceptance invokes it with `Backend`; the frontend action invokes each origin once. Both use the current telemetry ID returned by `IUserService.GetTelemetryId`.

### Outbound event and SDK lifecycle

- Use the official core `PostHog` package `2.18.1` through its documented normal singleton services registration. Inject the singleton `IPostHogClient` into `Telemetry`; do not register a separate named event `HttpClient`, custom handler, direct HTTP adapter, or browser event client.
- Map the finite diagnostic origin to `backend` / `frontend` and derive common labels from build metadata (`IsDevRelease` selects `dev` / `stable`). The first event schema is explicit and finite; later application events extend the implementation's schemas. Do not attach exception data, person updates, browser metadata, client IPs, request headers, cookies, or arbitrary caller properties to events.
- Call the non-obsolete SDK `Capture` overload with the implementation-owned telemetry ID returned by `IUserService.GetTelemetryId`, event name, allowlisted properties, `groups: null`, and `flags: null`. Do not enable feature evaluation, request-context middleware, advanced SDK settings, or any PostHog settings/toggle/API. The SDK may add documented metadata such as `$lib`, `$lib_version`, an event UUID, `$is_server`, and its normal transport fields; those SDK-owned fields are expected and are not an exact raw API-schema contract.
- The SDK's `Capture` result reports queue admission only. A successful result means **accepted for sending** and returns the fixed diagnostic message above; it does not synchronously report HTTP acceptance, ingestion, or delivery. Let the SDK own its default queue, batching, retries, background lifecycle, and disposal flush. Do not call `FlushAsync` per event, add custom retry/fallback behavior, or promise a synchronous upstream failure status for future batch sends.
- The browser calls only the authenticated Reaparr diagnostic endpoint. It never sends a direct PostHog ingestion request, token, UUID, URL/referrer, or browser headers. The backend supplies the identity and allowlisted event through `Telemetry`.
- **Superseded design note:** an earlier revision specified hand-built JSON to `POST /i/v0/e/`, a named HTTP client, one-attempt semantics, and no SDK queue/retry state. That direct Capture API design is retained only as historical vendor research; it is not an implementation instruction.

### Serilog operational log exporter

**Selected policy:** configure the standard Serilog OTLP sink directly in `LogConfig` whenever immutable build configuration is present. Export severity follows the global logger threshold; there is no separate Information-only floor. Use the existing sensitive-data masking behavior controlled by `IsUnmasked`.

- PostHog Logs uses OpenTelemetry/OTLP rather than the analytics event SDK ([PostHog .NET Logs](https://posthog.com/docs/logs/installation/dotnet)). Use the existing `Serilog.Sinks.OpenTelemetry` package directly; no custom sink, wrapper, sanitizer, transport, or lifecycle classes.
- Preserve console, file, Seq, log buffer, SignalR, and local sink behavior. Configured builds attach remote OTLP logging immediately without settings-loaded admission or bootstrap replay; unconfigured builds preserve file and Seq sinks while omitting only remote OTLP. Serilog owns batching, failure isolation, disposal, and shutdown flush. SignalR must attach to the existing logger without rebuilding or replaying it.
- Apply the existing sensitive-data enricher according to `IsUnmasked`: use masking when `IsUnmasked` is false and retain unmasked values when it is true. Keep resource labels (`service.name=reaparr`, version, environment, runtime mode, platform, runtime identifier), the configured `TelemetryHost`, and embedded authorization.
- **Explicitly approved simplification:** the maintainer selected the standard sink plus existing `IsUnmasked`-controlled masking instead of full custom redaction. Even with masking enabled, exception text and some free-text credentials, paths, and other sensitive values may be exported unmasked. No fail-closed or complete-sanitization guarantee is made.
- Any live verification of operational records in **PostHog Logs** at the effective global logger threshold remains separate from event SDK acceptance and must not be represented as proof of event delivery.

### Frontend ownership

- New `src/AppHost/ClientApp/src/pages/debug-pages/telemetry.vue` contains authenticated diagnostic actions/status only. Use Quasar controls, existing translations, and stable `data-cy` selectors; do not modify the Advanced page.
- Add the diagnostic action using the generated client and existing store conventions; no telemetry settings defaults/mapping, settings mutation, or new telemetry store/framework.
- The debug **Send test event** action requests backend then frontend-origin diagnostics, attempting the second even if the first fails and displaying each result independently. Disable submission while a test is pending; do not automatically retry.
- No automatic product event on mount, navigation, reload, or login. Those operations may produce approved Serilog operational logs. No browser PostHog dependency, direct ingestion traffic, or token/UUID configuration is needed.
- Regenerate the TypeScript API from the running backend using `bun run generate-api-ts` from `src/AppHost/ClientApp`; update affected fixtures/defaults to the generated contract rather than hand-editing generated files.

## Phase 1 — Backend setup and verification

### Build and configuration

- Keep solution-wide `Directory.Build.props` free of telemetry-specific logic. Declare the .NET SDK's `AssemblyMetadata` items and build-input validation target directly in `src/AppHost/AppHost.csproj`, without a separate `.targets` import. Keep `IAppBuildInfo`, `AppBuildInfo`, and `DesignTimeAppBuildInfo` on the fixed metadata contract above.
- Wire the existing release paths: `.github/workflows/dev-release.yml`, `.github/workflows/stable-release.yml`, `.github/workflows/docker-build-multiarch.yml`, `.github/workflows/desktop-build.yml`, `docker/Dockerfile`, and `tools/Build/Workflows/DesktopPublishWorkflow.cs`.
- Supply the same configured `TelemetryHost`/`TelemetryProjectToken` pair to every Docker architecture and desktop runtime. Keep the public ingestion configuration separate from ordinary application secrets and from any deferred tooling credentials.
- Make publishing fail on missing or invalid required ingestion configuration; keep ordinary unconfigured development/test builds usable with telemetry unavailable. Never silently fall back to a different project or region.
- Compile/embed the selected values; do not leave a runtime `TELEMETRY_*`, `Telemetry__*`, or equivalent options-binding path. Do not embed credentials in writable `appsettings` and call them immutable.
- Preserve exact informational release metadata across backend and frontend. Use `environment` (`dev` / `stable`), `release`, `runtime_mode`, `platform`, and `runtime_identifier` for the diagnostic labels already agreed in the runbook.

### Application behavior

- Implement/register `ITelemetry` and `Telemetry`. Inject `ITelemetry` into the thin authenticated diagnostic endpoint as the first caller; require ordinary authentication without an explicit admin-role or principal-name check and preserve Result semantics. The implementation always resolves the telemetry ID through `IUserService.GetTelemetryId`, injects the singleton `IPostHogClient`, and keeps the same interface available to future system-wide use cases without endpoint, principal, or HTTP coupling.
- Validate event schemas and parameters in the implementation; own all common/identity/connection fields there. Do not expose a generic capture endpoint or wire unrelated business-event call sites in this milestone. SDK-owned metadata is expected and must not be treated as an exact raw payload contract.
- Keep the standard Serilog OTLP sink and existing masking package directly in `LogConfig`. Do not add request-context middleware, automatic product events, custom logging wrappers, or a duplicate logging provider.
- Events report SDK acceptance for sending or SDK queue-admission failure, not HTTP acceptance, ingestion, or delivery. SDK asynchronous upstream failures remain SDK-owned. Logs are best-effort standard batched exports; only observing the event feed and Logs UI proves later remote activity.

### Backend acceptance checkpoint

- Targeted tests cover configured/unconfigured behavior, unconditional telemetry-ID reuse through `IUserService.GetTelemetryId` across both origins, rejection of caller-supplied identities, prohibited property exclusion, ordinary authentication, SDK queue-admission failure, asynchronous SDK-owned upstream failure isolation, and attempted runtime configuration overrides. Adapter-side UUID validation and a safe lookup-failure `503` are not required contracts.
- Exercise the real backend diagnostic path with an explicitly configured build; observe its event in the selected PostHog project's activity feed with the correct ID and labels.
- Confirm startup/login/unrelated operations produce no automatic product events. On a configured build, verify operational logs attach immediately without a settings-loaded admission gate; effective severity follows the global logger threshold and masking follows `IsUnmasked`. Confirm file/Seq behavior remains available when telemetry is unconfigured.
- Finish backend SDK verification before implementing the frontend debug page. The initial backend checkpoint can invoke the authenticated diagnostic API directly; the final user-facing trigger belongs only on the frontend debug page.

## Phase 2 — Frontend setup and verification

- Reuse `debug-pages/` for proof actions; leave existing application settings unchanged.
- Make the debug-page action exercise distinguishable backend and frontend diagnostics sharing the current telemetry ID resolved by the backend. Show the result of each path independently so one success cannot hide the other failure.
- Route browser diagnostics through the Reaparr backend. The backend must require ordinary authentication and enforce the fixed request schema on every submission; do not add an explicit admin-role or principal-name check.
- Use the selected browser → Reaparr → backend `ITelemetry` path. The backend's core PostHog SDK owns event submission and normal queue/batch/retry/disposal behavior; frontend-origin activity does not initialize `posthog-js` or send direct ingestion traffic.
- Do not expose a general-purpose forwarding proxy. Allow only the diagnostic operation/payload required by this milestone; do not forward Reaparr cookies, session authorization, original client IP headers, browser URLs, or arbitrary browser headers to the backend SDK or PostHog.
- Do not load a browser PostHog SDK, remote configuration, or ancillary assets. The browser sends only the fixed diagnostic request through the authenticated Reaparr API; the backend supplies account identity and the allowlisted payload.

### Frontend acceptance checkpoint

- Targeted tests cover authenticated access, independent backend/frontend statuses, correct origins, unavailable configuration/transport, and prohibited property exclusion.
- In a real browser, activate the debug-page test action and inspect network requests. Observe both origin events in the selected PostHog project with the same backend-resolved telemetry ID and correct release/runtime labels.
- Navigate/reload/log in without clicking the diagnostic: no automatic product event or browser PostHog asset/config request. Backend operational logs from that work are explicitly in scope.
- Simulate unavailable configuration or SDK queue-admission failure: show diagnostic failure without breaking the debug page or normal application work.
- Simulate unavailable PostHog after SDK queue admission: retain the accepted-for-sending result. Later upstream failures remain SDK-owned and must not break the debug page or normal application work; do not add synchronous delivery checks or per-event flushing.

## Selected implementation and supporting rationale

The official core PostHog SDK approach and fixed contracts above are selected. SDK findings explain the normal lifecycle and acknowledgement boundary; they are not unresolved implementation alternatives. Packaged-artifact verification remains required evidence for the build-time configuration contract.

### 1. Use the supported core .NET SDK for this milestone

The official SDK owns the event wire format and ingestion endpoint, including its batched Capture API requests ([.NET SDK](https://posthog.com/docs/libraries/dotnet), [Capture API](https://posthog.com/docs/api/capture)). Reaparr must not hand-build those HTTP requests or JSON bodies.

**Selected event approach:** one provider-neutral `ITelemetry` implemented by `Reaparr.Application.Telemetry`, with the typed diagnostic endpoint as its first caller. Register official core `PostHog` `2.18.1` services as a normal singleton and inject `IPostHogClient` into `Telemetry`. Call the SDK's non-obsolete `Capture` overload with `flags: null`; do not add a browser SDK, direct ingestion adapter, or feature-evaluation path.

The SDK owns the documented asynchronous queue, batching, retries, background lifecycle, and disposal flush ([client contract](https://github.com/PostHog/posthog-dotnet/blob/761bd01094f420027e1450390948a72519507116/src/PostHog/IPostHogClient.cs), [implementation](https://github.com/PostHog/posthog-dotnet/blob/761bd01094f420027e1450390948a72519507116/src/PostHog/PostHogClient.cs)). `Capture` is non-blocking and its boolean reports queue admission, not remote ingestion. Reaparr adds no custom event queue, retry, fallback, per-event flush, or lifecycle. Operational logs remain on the existing standard Serilog OTLP batching/shutdown path.

This SDK path validates reusable common enrichment/policy for later application events without scattering business instrumentation now. Broad product-event capture, replay, and browser error tracking remain deferred.

**Superseded design note:** an earlier revision selected one direct Capture API request per diagnostic and explicitly rejected the .NET SDK's queue/retry lifecycle. That manual transport, one-attempt, and no-queue/no-SDK-retry requirement is outdated research and must not be implemented.

### 2. Prevent person-profile creation and client-context enrichment

For SDK events, keep `$process_person_profile: false` with the current telemetry ID returned by `IUserService.GetTelemetryId` as `distinct_id`. Do not call `identify`, alias, or person-property APIs. This suppresses person-profile processing; it does not make the account-linked identifier anonymous. The API otherwise treats captures as identified by default, and an ID previously associated with a PostHog person can remain identified ([capture profile processing](https://posthog.com/docs/api/capture#anonymous-event-capture)).

Keep the selected SDK privacy behavior for GeoIP (`$geoip_disable: true` where represented by the SDK/event properties) alongside `$process_person_profile: false`. Project-level IP storage remains enabled (`anonymize_ips: false`) by explicit maintainer choice; IP storage and GeoIP enrichment are separate controls. Do not forward end-user IP headers or add client-IP properties. PostHog can observe the backend's network source address; do not describe this policy as IP-free collection ([server-side property guidance](https://posthog.com/docs/product-analytics/identity-resolution), [SDK event construction](https://github.com/PostHog/posthog-dotnet/blob/761bd01094f420027e1450390948a72519507116/src/PostHog/Api/CapturedEvent.cs), [privacy controls](https://posthog.com/docs/privacy/data-collection)).

The SDK may add documented metadata (`$lib`, `$lib_version`, event UUID, `$is_server`, and other transport fields). Those fields are expected and do not turn the event contract into an exact raw JSON schema.

### 3. Use a diagnostic endpoint, not a general-purpose SDK proxy

PostHog's general proxy supports asset/config hosts, ingestion versions, compression, and SDK-specific headers; its reference recommends forwarding IPs for geolocation and large recording payloads. Those capabilities are not appropriate defaults for this narrow diagnostic operation ([proxy reference](https://posthog.com/docs/advanced/proxy/proxy-reference)).

**Selected boundary:** the diagnostic endpoint calls injected `ITelemetry`, which calls the official core SDK. It is not an arbitrary PostHog proxy. System-wide events use the same interface directly from owning classes. Operational logs use the existing standard Serilog sink.

### 4. Verify immutable configuration in packaged artifacts

Test conflicting runtime environment values against the built Docker and desktop outputs, not only mocked options. Verify that rebuilding with changed host/token changes the embedded configuration and invalidates the relevant build cache. Validate property handling and escaping at the build boundary instead of interpolating token text directly into source.

A fixed token cannot be revoked or retargeted in already distributed binaries without breaking their delivery or distributing an update. Record that operational consequence rather than adding an unapproved remote-configuration escape hatch.

### 5. Keep verification status honest and attributable

Use the fixed `reaparr diagnostic` event and authoritative `backend` / `frontend` origin values defined above. The UI says **Diagnostic event accepted for sending**, not HTTP accepted, ingested, or delivered. During live verification, match the telemetry ID returned by `IUserService.GetTelemetryId`, origin, release, and trigger time to avoid mistaking an older event for the current run. Do not add ingestion read access or a privileged polling API to automate confirmation.

## Deferred work and explicit exclusions

- Broad automatic product-event instrumentation, dedicated exception-event/error tracking, feedback, bug reports, dashboards, AI/Self-driving, surveys, session replay, feature flags, traces, and metrics. Serilog operational logs at the global logger threshold, with existing `IsUnmasked`-controlled masking and exception details, are explicitly included.
- PostHog MCP setup and credentials.
- Source-map injection/upload, PostHog CLI credentials, hidden-map release changes, and .NET symbol upload. No release in this milestone should require a privileged PostHog key. Existing unrelated release tooling is not authorized for removal or refactoring.
- Remote project changes are manual maintainer prerequisites, not something this plan or an application build performs.
- Do not add application-owned event retries/offline stores, custom event queues, per-event flushes, fallback transports, remote configuration, compatibility shims, telemetry builder/registry/context frameworks, or extra provider hierarchies. Use the official SDK's normal queue/batching/retry/disposal lifecycle. The single `ITelemetry` interface/implementation and existing standard Serilog sink remain the selected boundaries.

## Ordered implementation work packages

Complete all implementation and test edits for each package before running its checks. Backend-first is a real acceptance gate, not merely file-edit order.

| Order | Implementation owners | Required result and verification |
| --- | --- | --- |
| 1. Immutable build configuration | `src/AppHost/AppHost.csproj`; `src/Environment/Interfaces/IAppBuildInfo.cs`; `AppBuildInfo.cs`; `DesignTimeAppBuildInfo.cs`; release/reusable workflows; `docker/Dockerfile`; `tools/Build/Arguments/DesktopCommandSettings.cs`; `tools/Build/Workflows/DesktopPublishWorkflow.cs` | Forward, validate, stamp, and read the two fixed values using SDK-generated metadata, without solution-wide telemetry properties or manual attribute encoding. Extend existing `Environment.UnitTests` and `Build.UnitTests` responsibilities for absent/partial/invalid inputs, metadata values, publish inputs, project isolation, and runtime-override immunity. Keep unconfigured local/test builds usable. |
| 2. Reusable events and existing Serilog | Domain `ITelemetry`; Application `Telemetry` in `src/Application/Telemetry/Telemetry.cs`; `Telemetry/SendDiagnostic/`; shared origin enum; official core SDK singleton registration | Implement the lean interface, ordinary authenticated diagnostic access without an explicit admin-role/name check, current telemetry-ID identity via `IUserService.GetTelemetryId`, SDK queue-admission result semantics, and no-browser-SDK diagnostic path. Retain standard Serilog OTLP behavior, immediate configured-build attachment, global-threshold severity, `IsUnmasked` masking, SignalR attachment without logger rebuild, and file/Seq behavior when unconfigured. |
| 3. Backend acceptance gate | Configured backend artifact + maintainer project access | Targeted checks and production-file Code Health reviews; observe a backend-origin event plus operational records in PostHog Logs at the effective global logger threshold. Verify `IsUnmasked` masking, immediate configured-build attachment, unconfigured file/Seq behavior, SignalR attachment without logger rebuild, and shutdown flush. Missing live access blocks this gate, not isolated implementation work. |
| 4. Frontend diagnostic | Diagnostic debug page; existing store conventions; generated API; navigation/translations/fixtures | Debug page exercises both event origins independently. Add store/component/browser tests; no browser SDK or telemetry settings control. |
| 5. End-to-end and packaging | Real browser; Docker and desktop artifacts | Verify events and Logs separately, global-threshold severity, `IsUnmasked` masking, no automatic product events, no unconfigured-build event/OTLP dispatch while preserving file/Seq, immediate configured-build logging, non-fatal export failure, no duplicate sinks, immutable runtime configuration including hostile `OTEL_*` values, and precise platform coverage. |

New backend folders retain their owning project's root namespace and `.csproj.DotSettings` exclusions. Keep endpoint operations separate. Use the pinned core `PostHog` `2.18.1` package and retain the existing Serilog OTLP dependency/configuration; add no browser SDK or compatibility aliases.

### Required regression matrix

- **Build:** no pair locally → unavailable; partial/invalid pair → build failure; publishing without a pair → publish gate failure; configured artifacts → embedded values survive conflicting runtime environment settings; changed build inputs → changed embedded output.
- **Settings:** preserve existing settings modules and persistence behavior; telemetry introduces no settings module, DTO, or mutation API.
- **Identity/auth:** ordinary unauthenticated requests → `401`; no explicit admin-role/name check; every event unconditionally uses the current telemetry ID from `IUserService.GetTelemetryId` without caller identity or authentication-mode branches; do not add adapter-side UUID validation or promise a safe lookup-failure `503`; caller-supplied identity/properties/host/token and undefined origins are rejected with no SDK capture.
- **Event interface/SDK:** diagnostic uses the injected production `ITelemetry`; common metadata and identity cannot be overridden; unsupported event/parameter schemas fail without capture; SDK-owned metadata is expected; `true` means accepted for sending and `false` means queue-admission failure; unconfigured builds never capture; no custom event transport, retry, fallback, or per-event flush; asynchronous upstream failures remain SDK-owned.
- **Logs:** eligible records follow the global logger threshold; existing `IsUnmasked` behavior controls masking; standard batching, failure isolation, and root logger disposal flush remain. Configured builds attach OTLP immediately without settings admission or bootstrap replay, unconfigured builds preserve file/Seq sinks, SignalR attaches without rebuilding the logger, and runtime `OTEL_*` values cannot change embedded configuration.
- **Frontend/live:** debug invokes both origins once even on partial failure; show per-origin accepted-for-sending/failed status using the exact success message. Observe backend/frontend events by UUID/release/time and separate Serilog records in PostHog Logs. No product events on mount/navigation; ordinary backend logs are expected in configured builds.

### External prerequisites and stop conditions

- No real token/private operations record is needed for isolated implementation/tests. Live acceptance needs maintainer-provided `TelemetryHost`/`TelemetryProjectToken` configuration, event feed and Logs access, confirmation that project IP storage remains enabled, retention/spending settings, and the approved broad masked-log privacy review.
- Release workflow validation requires repository Actions-variable administration; publication itself is not authorized by this plan. Never publish, push, commit, or change remote project settings merely to complete a verification checkbox.
- Packaged-runtime verification requires the relevant Docker/desktop runtime; only claim the platforms actually exercised. No host-package installation is authorized.
- SDK selection and event identity are settled. Logs use the existing Serilog OTLP sink with global-threshold severity, `IsUnmasked`-controlled masking, immediate configured-build attachment, preserved file/Seq behavior when unconfigured, and standard batch/shutdown behavior. No privileged credential is required. Missing live access blocks acceptance, not permission to fabricate delivery or silently narrow the log scope.

## Verification and delivery record

Implementation verification must use repository-approved targeted backend/frontend tests, production-file Code Health reviews, a real browser for the debug control, and packaged-runtime smoke checks for immutable Docker/desktop configuration. Ordinary tests must use isolated transports and must not send to the production project. Run related checks only after their implementation and test edits are complete.

Record: artifact/version/runtime, build configuration source (not token values), observed event and Logs timestamps, event UUID/origin labels, effective logger threshold and `IsUnmasked` masking behavior, configured-build immediate attachment, unconfigured file/Seq behavior, SignalR attachment without logger rebuild, shutdown-flush results, duplicate-export check, runtime/OTEL override result, outage result, reviewer/date, and exact platform coverage.

- SDK cutover: `ITelemetry` remains the application boundary; the official `IPostHogClient` owns event serialization, transport, batching, retries, and disposal flush. No custom event HTTP client or per-event flush remains.
- **Historical targeted backend tests (prior worktree state; not proof of current source):** **38 passed** — 19 telemetry-adapter cases, 15 diagnostic-endpoint cases, and 4 startup SDK-configuration cases. The real SDK exported a batch through an isolated test handler on service-provider disposal, preserving the fixed account UUID and embedded destination/token despite conflicting runtime configuration. Missing/partial embedded configuration caused no export.
- **Historical native diagnostics (prior worktree state; not proof of current source):** no errors in changed production/test files after correcting the standard options-builder registration. Production Code Health reviews returned scored results from 9.2 to 10.0; remaining findings concern adapter branch complexity and large startup methods. No metric-only helper extraction was added.
- **Historical runtime smoke (prior worktree state; not proof of current source):** restarted `Reaparr Docker Development`; `GET /api/Settings` returned `200`, and `POST /api/Debug/telemetry/diagnostic` returned the safe `503` response `Telemetry is unavailable` in the unconfigured local build.
- No live PostHog ingestion check, remote project mutation, release packaging, or packaged desktop/Docker acceptance was performed for this SDK cutover. Local SDK transport/disposal tests do not establish live delivery.

### Current scoped logger and regression alignment (2026-11-04)

- **27 targeted tests passed:** 2 logger lifecycle cases, 13 telemetry-adapter cases, and 12 diagnostic-endpoint cases.
- The logger cases exercised real file output and isolated local Seq HTTP ingestion without telemetry configuration, retained contextual loggers across SignalR attachment, and verified that only post-attachment events reach SignalR without replay.
- Adapter tests use `GetTelemetryId` and retain schema, enrichment, refreshed identity, unconfigured-build, and SDK queue-result coverage. Endpoint tests retain request validation and Result-envelope behavior without obsolete handler-level authorization expectations.
- Changed C# files had no reported native diagnostics; both changed production files received Code Health **10.0** with no findings.
- These checks did not exercise live PostHog ingestion, packaged Docker/desktop acceptance, or release publication.


### Historical generic telemetry configuration record (prior worktree state; not proof of current source)

- **Historical count (not proof of current source):** **143 tests passed:** 110 build-tool tests, 19 telemetry-adapter cases, 4 SDK startup-configuration cases, and 10 remote-log sink cases. Artifact tests verify the generic metadata/build inputs and immutable runtime reads; configured alternate hosts, optional ports/trailing slashes, invalid URI boundaries, and out-of-range ports are covered. All four changed workflows passed `actionlint`, and changed C# files had no error diagnostics.
- **Historical runtime smoke (not proof of current source):** after restarting the backend, settings returned `200` and the unconfigured diagnostic returned the safe `503` response `Telemetry is unavailable`. No live ingestion, published desktop/Docker runtime acceptance, or remote Actions-variable writes were performed. The repository currently has no Actions variables; future configuration uses `TELEMETRY_HOST` and `TELEMETRY_PROJECT_TOKEN`.


## Companion research

- [Backend SDK and transport findings](posthog-backend-research.md)
- [Frontend SDK and proxy findings](posthog-frontend-research.md)
