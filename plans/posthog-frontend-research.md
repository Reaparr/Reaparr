# PostHog frontend research

**Research checked 2026-10-31; decisions updated 2026-11-03.** Sources below are PostHog documentation and `posthog-js`/`@posthog/nuxt` first-party source. The selected event implementation is the existing Reaparr `ITelemetry` path in `Reaparr.Application.Telemetry`, backed by the official core PostHog .NET SDK `2.18.1` and the current telemetry ID returned by `IUserService.GetTelemetryId` as `distinct_id`. Browser SDK configuration details and the earlier direct Capture API proposal are supporting/superseded research, not open choices for this milestone.

**Current scope:** backend classes use one lean injectable `ITelemetry` event interface; frontend diagnostics remain a Phase 2/future proof consumer of that shared backend SDK path. There is no application telemetry setting, toggle, or mutation API, and existing settings pages remain unchanged. Standard Serilog operational logs follow the global logger threshold, use existing `IsUnmasked`-controlled masking, attach immediately on configured builds without settings admission, preserve file/Seq when unconfigured, and retain normal batching/shutdown behavior. SDK-owned event queueing is backend behavior; the browser owns no event transport.

## Short verdict

**Reaparr should not install `@posthog/nuxt` or initialize `posthog-js` in the browser for this scope.** The official module and browser SDK are designed to initialize a browser project client (the project token is therefore public), and they add automatic capture/session behavior and browser-side asynchronous request/retry queues. Those browser facts do not reject the selected core .NET SDK; they explain why the browser remains a thin backend diagnostic caller. The documented/current behavior permits browser events accepted before opt-out to finish batching or retrying ([opt-out issue #5020](https://github.com/PostHog/posthog-js/issues/5020), closed as working as intended; [request queue source](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/request-queue.ts)).

Selected implementation:

1. An authenticated debug-page action explicitly requests one of the allowlisted diagnostic origins; it has no explicit admin-role or principal-name requirement. No page-load, route-change, error, log, replay, survey, flag, or remote-config path calls it.
2. The browser sends a minimal, same-origin request to a Reaparr endpoint. It sends no PostHog token, URL/referrer, DOM/input text, exception object/stack, account identity, Plex data, or browser/session properties.
3. Reaparr validates the fixed diagnostic request and ordinary authentication, resolves the current telemetry ID through `IUserService.GetTelemetryId`, and calls the backend `ITelemetry` implementation. `Telemetry` invokes the singleton core SDK with the project token embedded in the backend at build time. The browser cannot supply or override the telemetry ID; the adapter adds no UUID validation or safe lookup-failure `503` guarantee.
4. The browser has no durable/retry queue and never sends direct ingestion traffic. The backend SDK owns its documented normal queue, batching, retries, background lifecycle, and disposal flush. A diagnostic succeeds only when the SDK accepts it for sending; later upstream failures remain SDK-owned. Report SDK admission failures to the authenticated caller without affecting normal application operations; no settings mutation precedes submission.

This remains an official PostHog SDK-backed ingestion path. The [Capture and batch API](https://posthog.com/docs/api/capture) documents the upstream public POST API and project-token authentication used internally by the SDK; the browser does not call it. The Reaparr endpoint is a deliberately narrow application-owned diagnostic boundary, not a general proxy.

## Browser SDK facts (deferred reference; not selected)

### Deferred browser SDK configuration facts
This section records vendor defaults only; none of these browser settings, advanced flags, toggles, or remote-configuration APIs are selected for Reaparr.

The browser SDK defaults are broad. The current source's [`defaultConfig`](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/posthog-core.ts) and the [JavaScript configuration reference](https://posthog.com/docs/libraries/js/config) document these relevant defaults:

| Behavior | SDK default | If an SDK is ever used, the explicit setting needed for this scope |
| --- | --- | --- |
| DOM autocapture/rageclick | `autocapture: true`; `rageclick: true` | `autocapture: false`, `rageclick: false` |
| Page events | `capture_pageview: true`; `capture_pageleave: 'if_capture_pageview'` | `capture_pageview: false`, `capture_pageleave: false` |
| Exceptions/performance | `capture_exceptions: undefined` and `capture_performance: undefined`, which can fall back to project remote configuration | `capture_exceptions: false`, `capture_performance: false` |
| Session replay/surveys | `disable_session_recording: false`; `disable_surveys: false` | `disable_session_recording: true`, `disable_surveys: true` |
| External bundles | `disable_external_dependency_loading: false` | `disable_external_dependency_loading: true`; the first-party [config type source](https://github.com/PostHog/posthog-js/blob/main/packages/types/src/posthog-config.ts) says this blocks external scripts such as replay/survey/site-app bundles |
| Flags/remote config | `advanced_disable_flags: false` | `advanced_disable_flags: true`; this disables `/flags`/`/decide` and remote configuration. The [config reference](https://posthog.com/docs/libraries/js/config#disable-flags-endpoint) warns that flags and other remotely configured features then do not load |
| Browser storage | `persistence: 'localStorage+cookie'` | `persistence: 'memory'` or `disable_persistence: true` (neither gives cross-page SDK identity) |
| Person processing | `person_profiles: 'identified_only'` | `person_profiles: 'never'`; no `identify`, `alias`, `group`, or person-property calls |
| Campaign/referrer state | `save_campaign_params: true`, `save_referrer: true` in the source | If a browser SDK were ever used, disable both; the selected backend SDK relay adds no browser SDK enrichment |
| Requesting | `request_batching: true`; the queue's default flush interval is 3 seconds | `request_batching: false` can reduce waiting but does not remove the SDK's asynchronous request/retry lifecycle |
| Filtering | no `before_send` by default; `property_denylist: []` | use `before_send` only as a last defense, and an allowlist before the SDK call; `sanitize_properties` is deprecated in the source |

The SDK's default `capture_pageview` also changes when a `defaults` date snapshot is selected (for example, `defaults: '2025-05-24'` makes it `history_change`). Do not rely on legacy defaults or `defaults: 'latest'` for a privacy boundary; set every relevant option explicitly and pin any SDK version if the repository ever adopts it. The docs list further date snapshots through 2026 and their changing defaults ([configuration reference](https://posthog.com/docs/libraries/js/config#configuration-builder)).

A browser SDK also automatically adds session context: PostHog documents that the JavaScript library adds `$session_id` and `$window_id`, and captures session-entry properties ([sessions](https://posthog.com/docs/data/sessions)). There is no documented switch that makes a normal SDK `capture()` payload contain *no* session properties. This is deferred browser-SDK research only; the selected browser action calls Reaparr and the backend core SDK owns the event payload/lifecycle.

## Identity, storage, and cookieless modes

- `persistence: 'memory'` stores SDK state only in page memory; the [persistence docs](https://posthog.com/docs/libraries/js/persistence) say it lasts for the page view. `disable_persistence: true` disables browser persistence. Neither is needed for the selected backend SDK path: the backend resolves the current telemetry ID through `IUserService.GetTelemetryId` without browser identity storage.
- `cookieless_mode: 'always'` is a different PostHog feature. It requires enabling cookieless tracking in project settings, stores no PostHog cookie/local/session storage, and uses a privacy-preserving server hash. The [data-collection docs](https://posthog.com/docs/privacy/data-collection#cookieless-tracking) caution against identifying users in this mode and recommend `person_profiles: 'never'` if `identify()` must be neutralized.
- `cookieless_mode: 'on_reject'` is not selected: the docs describe server-hash counting after rejection, whereas this diagnostic uses the current telemetry ID from `IUserService.GetTelemetryId` and no browser SDK.

**Selected Reaparr identity:** always reuse the current telemetry ID returned by `IUserService.GetTelemetryId` (the existing login account UUID) for both origins independently of authentication mode. Do not generate an installation ID or derive an identifier from username, email, Plex token, hostname, path, IP, or machine data. Do not accept a UUID from the browser, add adapter-side UUID validation, or promise a safe lookup-failure `503`. Keep `$process_person_profile: false`; do not call `identify`, alias, or person/group property APIs. This is account-linked pseudonymous data, not anonymous installation tracking. Identity follows the existing account lifecycle, including recreation and authentication-database copies.

Project IP storage remains enabled (`anonymize_ips: false`) by explicit maintainer choice. Keep event `$geoip_disable: true` and do not forward the browser's IP/XFF or other end-user IP headers. Retaining ingestion-source IPs does not authorize additional client-IP collection ([IP controls](https://posthog.com/docs/privacy/data-collection#ip-data-capture)).

## SDK consent and queued work (reference only)

PostHog documents `posthog.opt_out_capturing()` as a top-level switch that prevents all data from being captured and sent, including autocapture, manual events, and replay; `has_opted_out_capturing()` checks the state and the preference is persisted in localStorage or a cookie ([controlling data collection](https://posthog.com/docs/privacy/data-collection#asking-for-opt-out)). That guarantee concerns capture admission.

It is not a guarantee that already accepted browser requests disappear. The current first-party source has private `RequestQueue` and `RetryQueue` instances; `RequestQueue.unload()` intentionally sends retained work with `sendBeacon`, and neither queue exposes a public discard method ([request queue](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/request-queue.ts), [retry queue](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/retry-queue.ts)). PostHog issue [#5020](https://github.com/PostHog/posthog-js/issues/5020) records batch/retry delivery after opt-out for SDK versions 1.396.6 and 1.434.0 and was closed with the explanation that events accepted while opted in may still be delivered; it lists page reload or proxy-side dropping as workarounds. This is a version-sensitive behavior report, not a promise for every future release.

**Selected Reaparr behavior:** do not use a browser PostHog SDK or browser-side SDK queues. The debug action sends only the fixed same-origin request to Reaparr; it does not persist, batch, retry, or `sendBeacon` events. The backend core SDK owns its normal queue, batching, retries, background lifecycle, and disposal flush. Ordinary authentication and payload validation are authoritative; browser cancellation cannot retract an event already accepted by the SDK. No app-owned browser queue/list, queue-clearing infrastructure, or telemetry settings control is needed.

## Browser SDK property hooks (deferred reference only)

PostHog's JavaScript [usage docs](https://posthog.com/docs/libraries/js/usage#amending-or-sampling-events) describe `before_send` as a last-chance hook to amend, redact, or reject an event by returning `null`; it applies only to the Web SDK. The [configuration reference](https://posthog.com/docs/libraries/js/config) documents `property_denylist` as a list of names never sent with `capture` calls. These are historical defenses if a browser SDK is ever considered, not Reaparr configuration: a denylist is not an allowlist and `before_send` runs after the SDK has built/enriched its event. PostHog warns that modifying internal events can break features. `sanitize_properties` is deprecated in the first-party source.

For Reaparr, enforce the diagnostic request schema at the backend endpoint and the event allowlist in `Telemetry` **before** calling the SDK:

- allow only the fixed diagnostic event name/origins and the agreed `environment`, `release`, `platform`, `runtime_mode`, and `runtime_identifier` labels;
- allow only small strings/enums/booleans/numbers with length and count limits; reject unknown keys and nested arbitrary objects;
- never accept or serialize URLs, query strings, referrers, DOM/input text, account names, Plex server/library/media data, tokens, file paths, raw error objects, exception messages/stacks, console text, or user-agent/IP values;
- do not send `$session_id`, `$window_id`, `$current_url`, UTM/referrer, `$set`, `$set_once`, person/group properties, or replay payloads from the browser; SDK-owned metadata such as `$lib`, `$lib_version`, the event UUID, and `$is_server` remains expected on backend events;
- keep diagnostics deliberate and authenticated-user-triggered. Do not wire the sanitizer to global error handlers or logs.

These are Reaparr controls, not PostHog guarantees. Project-level IP settings remain necessary because removing a property in the client is not equivalent to disabling IP capture.

## Upstream API facts and selected backend contract

PostHog's standard ingestion API is documented at [Capture and batch API endpoints](https://posthog.com/docs/api/capture):

- single event vendor example: `POST https://us.i.posthog.com/i/v0/e/` (Reaparr uses the configured `TelemetryHost` origin and does not hardcode this example; use the selected regional/self-hosted ingestion host when applicable);
- required fields: `api_key`, `event`, and `distinct_id`; `properties` and `timestamp` are optional;
- `distinct_id` is limited to 200 characters;
- `/batch/` accepts `{ "api_key": "...", "batch": [ ... ] }` and is an optional vendor endpoint.

These are upstream API facts used internally by the selected core SDK. **Superseded direct-API research:** an earlier revision included an illustrative browser-to-backend-to-Capture JSON body and a one-request/no-queue relay. That body and manual transport are retained only as historical research; do not hand-build JSON, call the ingestion URL from Reaparr, or implement one-attempt/no-SDK-retry behavior.

**Selected Reaparr contract** (implementation pending; the [implementation handoff](posthog-maintainer-runbook.md#fixed-implementation-contracts) is authoritative):

```text
Browser -> POST /api/Debug/telemetry/diagnostic (ordinary authenticated access)
{
  "origin": 1
}

Reaparr endpoint -> ITelemetry -> Reaparr.Application.Telemetry
Telemetry -> singleton PostHog 2.18.1 IPostHogClient.Capture(
  distinctId: current telemetry ID from IUserService.GetTelemetryId,
  eventName: "reaparr diagnostic",
  properties: allowlisted backend-owned labels,
  groups: null,
  flags: null)
```

The browser does not need `TelemetryHost` or `TelemetryProjectToken` in Nuxt public configuration or JavaScript. These public build inputs may be supplied through the `TELEMETRY_HOST`/`TELEMETRY_PROJECT_TOKEN` Docker build arguments or equivalent desktop build inputs, then embedded in the backend SDK configuration. Runtime environment variables cannot override them. Never supply privileged personal, source-map, or MCP keys to these builds. Backend ownership of ingestion configuration is the selected architecture, not a confidentiality guarantee or a requirement that PostHog imposes on public tokens.

The core .NET SDK supplies documented SDK metadata such as `$lib`, `$lib_version`, an event UUID, `$is_server`, and `$geoip_disable`; these fields are expected and are not an exact raw API-schema contract. The implementation keeps `$process_person_profile: false`, the current telemetry ID from `IUserService.GetTelemetryId`, and approved allowlisted labels while forwarding no browser IP/XFF, headers, URL, or context.

The telemetry ID is pseudonymous account data even with person-profile processing disabled. API success status must remain **Diagnostic event accepted for sending** when SDK queue admission succeeds, not HTTP accepted, ingested, or delivered; verify actual backend/frontend origin events in PostHog using the same telemetry ID, release labels, and trigger time.

If a generic PostHog SDK reverse proxy is used in another future scope, the official mapping is event `/i/v0/e/*`, legacy event `/e/*`, replay `/s/*`, flags `/flags/*`, remote config `/array/*`, and assets `/static/*`; `rewriteRequestPath` requires `posthog-js` 1.416.0+ and the proxy must preserve query parameters ([configuration reverse-proxy paths](https://posthog.com/docs/libraries/js/config#customize-reverse-proxy-request-paths), [proxy reference](https://posthog.com/docs/advanced/proxy/proxy-reference)). Those routes are not required for the selected narrow same-origin diagnostic endpoint.

## Browser integrations versus Reaparr backend core SDK

| Option | What the first-party docs/source provide | Fit for this scope |
| --- | --- | --- |
| `@posthog/nuxt` | Official recommendation for Nuxt 4.x and 3.7+. It auto-initializes `posthog-js`, provides `usePostHog()`, feature-flag composables, a Nitro server client, and optional source-map processing ([Nuxt guide](https://posthog.com/docs/libraries/nuxt-js)). The module source adds a client plugin that calls `posthog.init(publicKey, { api_host: host, ...clientConfig })`, puts both key/host in `runtimeConfig.public`, and conditionally hooks `vue:error` to `captureException` when `capture_exceptions` is enabled ([module source](https://github.com/PostHog/posthog-js/blob/main/packages/nuxt/src/module.ts), [client plugin](https://github.com/PostHog/posthog-js/blob/main/packages/nuxt/src/runtime/vue-plugin.ts)). | **Reject for now.** It necessarily creates a browser project client/config, makes broad SDK defaults easy to miss, and brings server/source-map/error-tracking/module machinery deferred by the user contract. |
| Direct `posthog-js` with all disable flags | Smaller integration and explicit SDK settings are possible; `before_send`, memory persistence, and opt-out APIs are documented. | **Still reject for the strict contract.** It still ships a project token/config, automatically enriches events with session/browser state, owns batching/retries, and has no public unsent-queue discard. |
| Reaparr backend `ITelemetry` + core `PostHog` `2.18.1` SDK | Browser sends only the authenticated diagnostic request; the backend singleton SDK uses the embedded project token, normal queue/batching/retry/disposal lifecycle, telemetry ID from `IUserService.GetTelemetryId`, allowlisted properties, and no browser SDK. | **Selected.** The backend requires ordinary authentication without an explicit admin-role or principal-name check and enforces payload policy; success means SDK accepted for sending, not HTTP acceptance, ingestion, or delivery. |

## Version/documentation caveats

- The Nuxt guide currently covers Nuxt 4.x and Nuxt 3.7+; it links separate instructions for older Nuxt versions. Verify compatibility against the exact Nuxt and package versions before any future SDK decision.
- `before_send` was introduced in `posthog-js` 1.187.0; `tracing_headers` requires 1.380.0+; `rewriteRequestPath` requires 1.416.0+ ([configuration reference](https://posthog.com/docs/libraries/js/config)). These browser-SDK details are not reasons to add a browser SDK now.
- The PostHog docs and `main` source currently publish date-based defaults through 2026. Pin exact package versions and review generated `PostHogConfig`/source before release; never treat an unpinned `main` branch or `defaults: 'latest'` as a stable privacy contract.
- The selected backend package is the pinned core `PostHog` `2.18.1` SDK. Its queue, batching, retry, and disposal behavior are part of the implementation; do not replace them with direct HTTP, a browser SDK, custom queues, or custom retries.
