# PostHog frontend research

**Research checked 2026-10-31; decisions updated 2026-11-01.** Sources below are PostHog documentation and `posthog-js`/`@posthog/nuxt` first-party source. Annotation review selected the direct Capture API relay without SDK queues and the existing authenticated Reaparr login account UUID (`AppUser.Id`) as `distinct_id`. SDK configuration details are supporting research for deferred future work, not an open choice for this milestone.

**Scope update from subsequent annotation review:** backend classes use one lean injectable `ITelemetry` event interface; the frontend diagnostic is a proof consumer of that shared event path. Opt-out is `advancedSettings.telemetryDisabled`, controlled on Settings → Advanced. The debug page contains test actions/status and a link only. Separately, automatic masked Information+ Serilog logs are now included; their pre-opt-out batches may drain. The strict unqueued browser/event guidance below does not prohibit those approved backend logs.

## Short verdict

**Reaparr should not install `@posthog/nuxt` or initialize `posthog-js` in the browser for this scope.** The official module and browser SDK are designed to initialize a browser project client (the project token is therefore public), and the browser SDK adds automatic capture/session behavior and owns asynchronous request/retry queues. Even a carefully configured SDK has no public queue-discard API; the documented/current behavior permits events accepted before opt-out to finish batching or retrying ([opt-out issue #5020](https://github.com/PostHog/posthog-js/issues/5020), closed as working as intended; [request queue source](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/request-queue.ts)).

Selected implementation: use a small Reaparr-owned diagnostic sender instead:

1. An administrator-only debug-page action explicitly creates one of the allowlisted diagnostic events. No page-load, route-change, error, log, replay, survey, flag, or remote-config path calls it.
2. The browser sends a minimal, same-origin request to a Reaparr endpoint. It sends no PostHog token, URL/referrer, DOM/input text, exception object/stack, account identity, Plex data, or browser/session properties.
3. Reaparr validates the fixed diagnostic request, checks instance-wide opt-out and release policy, resolves the login account UUID (`AppUser.Id`) from the authenticated account as `distinct_id`, and forwards a single event to the Capture API using the project token embedded in the backend at build time. The browser cannot supply or override the UUID.
4. The sender has no durable/retry queue. Opt-out gates new calls and discards app-owned unsent work; the ingress does not forward after opt-out. A request already dispatched cannot be recalled reliably. Report diagnostic failures to the administrator without affecting normal application operations.

This is still an official PostHog ingestion path: the [Capture and batch API](https://posthog.com/docs/api/capture) is a public POST API authenticated by a **project token**. It is not an official Nuxt integration, and should be treated as a deliberately narrow application-owned adapter.

## Browser SDK facts and the strict configuration envelope

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
| Campaign/referrer state | `save_campaign_params: true`, `save_referrer: true` in the source | `save_campaign_params: false`, `save_referrer: false`; direct Capture API is safer because it adds no SDK enrichment |
| Requesting | `request_batching: true`; the queue's default flush interval is 3 seconds | `request_batching: false` can reduce waiting, but does **not** provide a strict opt-out cancellation boundary |
| Filtering | no `before_send` by default; `property_denylist: []` | use `before_send` only as a last defense, and an allowlist before the SDK call; `sanitize_properties` is deprecated in the source |

The SDK's default `capture_pageview` also changes when a `defaults` date snapshot is selected (for example, `defaults: '2025-05-24'` makes it `history_change`). Do not rely on legacy defaults or `defaults: 'latest'` for a privacy boundary; set every relevant option explicitly and pin any SDK version if the repository ever adopts it. The docs list further date snapshots through 2026 and their changing defaults ([configuration reference](https://posthog.com/docs/libraries/js/config#configuration-builder)).

A browser SDK also automatically adds session context: PostHog documents that the JavaScript library adds `$session_id` and `$window_id`, and captures session-entry properties ([sessions](https://posthog.com/docs/data/sessions)). There is no documented switch that makes a normal SDK `capture()` payload contain *no* session properties. A direct, server-forwarded Capture API body can omit them entirely.

## Identity, storage, and cookieless modes

- `persistence: 'memory'` stores SDK state only in page memory; the [persistence docs](https://posthog.com/docs/libraries/js/persistence) say it lasts for the page view. `disable_persistence: true` disables browser persistence. Neither is needed for the selected relay: the backend resolves the existing login account UUID without browser identity storage.
- `cookieless_mode: 'always'` is a different PostHog feature. It requires enabling cookieless tracking in project settings, stores no PostHog cookie/local/session storage, and uses a privacy-preserving server hash. The [data-collection docs](https://posthog.com/docs/privacy/data-collection#cookieless-tracking) caution against identifying users in this mode and recommend `person_profiles: 'never'` if `identify()` must be neutralized.
- `cookieless_mode: 'on_reject'` is **not** compatible with Reaparr's strict opt-out requirement: the same docs say no events are captured until a decision, then rejected users are still counted with the server hash. Reaparr must stop capture/proxy instead of counting a rejected user.

**Selected Reaparr identity:** reuse the existing login account UUID (`AppUser.Id`), resolved by the backend from the authenticated account for both origins. Do not generate an installation ID or derive an identifier from username, email, Plex token, hostname, path, IP, or machine data. Do not accept a UUID from the browser. Keep `$process_person_profile: false`; do not call `identify`, alias, or person/group property APIs. This is account-linked pseudonymous data, not anonymous installation tracking. Identity follows the existing account lifecycle, including recreation and authentication-database copies.

The project should disable stored client IP data. The generic proxy guidance recommends forwarding `X-Forwarded-For` for geolocation ([proxy reference](https://posthog.com/docs/advanced/proxy/proxy-reference#requirements)); that is intentionally not appropriate for this account-UUID-only diagnostic payload. Do not forward the browser's IP/XFF, and configure the PostHog project to discard IP data ([IP controls](https://posthog.com/docs/privacy/data-collection#ip-data-capture)).

## Consent, opt-out, and queued work

PostHog documents `posthog.opt_out_capturing()` as a top-level switch that prevents all data from being captured and sent, including autocapture, manual events, and replay; `has_opted_out_capturing()` checks the state and the preference is persisted in localStorage or a cookie ([controlling data collection](https://posthog.com/docs/privacy/data-collection#asking-for-opt-out)). That guarantee concerns capture admission.

It is not a guarantee that already accepted browser requests disappear. The current first-party source has private `RequestQueue` and `RetryQueue` instances; `RequestQueue.unload()` intentionally sends retained work with `sendBeacon`, and neither queue exposes a public discard method ([request queue](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/request-queue.ts), [retry queue](https://github.com/PostHog/posthog-js/blob/main/packages/browser/src/retry-queue.ts)). PostHog issue [#5020](https://github.com/PostHog/posthog-js/issues/5020) records batch/retry delivery after opt-out for SDK versions 1.396.6 and 1.434.0 and was closed with the explanation that events accepted while opted in may still be delivered; it lists page reload or proxy-side dropping as workarounds. This is a version-sensitive behavior report, not a promise for every future release.

**Selected Reaparr behavior:** do not use SDK queues. Send one request per deliberate action with no persistence, batch timer, custom retry, or `sendBeacon` unload flush. Check current instance-wide opt-out before dispatch and retain no unsent events for later delivery. Backend enforcement is authoritative; browser cancellation cannot retract a dispatched upstream request. No app-owned queue/list or queue-clearing infrastructure is needed.

## Property sanitization and payload discipline

PostHog's JavaScript [usage docs](https://posthog.com/docs/libraries/js/usage#amending-or-sampling-events) describe `before_send` as a last-chance hook to amend, redact, or reject an event by returning `null`; it applies only to the Web SDK. The [configuration reference](https://posthog.com/docs/libraries/js/config) documents `property_denylist` as a list of names never sent with `capture` calls. These are useful defenses if an SDK is used, but a denylist is not an allowlist and `before_send` runs after the SDK has built/enriched its event. PostHog warns that modifying internal events can break features. `sanitize_properties` is deprecated in the first-party source; use `before_send` instead.

For Reaparr, enforce the allowlist **before** network I/O and again at the server boundary:

- allow only fixed diagnostic event names/origins and the agreed `environment`, `release`, `platform`, `runtime_mode`, and `runtime_identifier` labels;
- allow only small strings/enums/booleans/numbers with length and count limits; reject unknown keys and nested arbitrary objects;
- never accept or serialize URLs, query strings, referrers, DOM/input text, account names, Plex server/library/media data, tokens, file paths, raw error objects, exception messages/stacks, console text, or user-agent/IP values;
- do not send `$session_id`, `$window_id`, `$current_url`, UTM/referrer, `$set`, `$set_once`, person/group properties, or replay payloads;
- keep diagnostics deliberate and administrator-triggered. Do not wire the sanitizer to global error handlers or logs.

These are Reaparr controls, not PostHog guarantees. Project-level IP settings remain necessary because removing a property in the client is not equivalent to disabling IP capture.

## Proxy route and upstream payload

PostHog's standard ingestion API is documented at [Capture and batch API endpoints](https://posthog.com/docs/api/capture):

- single event: `POST https://us.i.posthog.com/i/v0/e/` (use the selected EU/self-hosted ingestion host when applicable);
- required fields: `api_key`, `event`, and `distinct_id`; `properties` and `timestamp` are optional;
- `distinct_id` is limited to 200 characters;
- for an anonymous event, include `"$process_person_profile": false` in `properties` and use a random distinct ID that is not associated with an identified event;
- `/batch/` accepts `{ "api_key": "...", "batch": [ ... ] }` and is optional, with a documented default body limit below 20 MB. It is unnecessary for two diagnostic events and would reintroduce queue/discard complexity.

**Selected Reaparr contract** (implementation pending; the [implementation handoff](posthog-maintainer-runbook.md#fixed-implementation-contracts) is authoritative):

```text
Browser -> POST /api/Debug/telemetry/diagnostic (administrator-authorized)
{
  "origin": 1
}

Reaparr -> PostHog Capture API (server only)
POST https://us.i.posthog.com/i/v0/e/
{
  "api_key": "<project ingestion token embedded at build time>",
  "event": "reaparr diagnostic",
  "distinct_id": "<authenticated Reaparr login account UUID: AppUser.Id>",
  "properties": {
    "$process_person_profile": false,
    "$geoip_disable": true,
    "environment": "stable",
    "release": "<backend-owned informational version>",
    "platform": "<backend-owned platform>",
    "runtime_mode": "<backend-owned mode>",
    "runtime_identifier": "<backend-owned runtime identifier>",
    "origin": "frontend"
  }
}
```

For the selected relay, the browser does not need the token in Nuxt public configuration or JavaScript. The project token is public by design and may be supplied through Docker build arguments or equivalent desktop build inputs, then embedded in the backend. Runtime environment variables cannot override it. Never supply privileged personal, source-map, or MCP keys to these builds. Backend ownership of ingestion configuration is the selected architecture, not a confidentiality guarantee or a requirement that PostHog imposes on public tokens.

The illustrative direct-API payload explicitly disables GeoIP enrichment as well as person-profile processing. The .NET SDK normally supplies `$geoip_disable` itself; a direct HTTP adapter must add it ([SDK event construction](https://github.com/PostHog/posthog-dotnet/blob/main/src/PostHog/Api/CapturedEvent.cs)). Project IP-discard settings remain a separate required control.

The UUID is pseudonymous account data even with person-profile processing disabled. Submission status must remain submitted/accepted rather than delivered; verify actual backend/frontend origin events in PostHog using the same login account UUID, release labels, and trigger time.

If a generic PostHog SDK reverse proxy is used in another future scope, the official mapping is event `/i/v0/e/*`, legacy event `/e/*`, replay `/s/*`, flags `/flags/*`, remote config `/array/*`, and assets `/static/*`; `rewriteRequestPath` requires `posthog-js` 1.416.0+ and the proxy must preserve query parameters ([configuration reverse-proxy paths](https://posthog.com/docs/libraries/js/config#customize-reverse-proxy-request-paths), [proxy reference](https://posthog.com/docs/advanced/proxy/proxy-reference)). Those routes are not required for the narrow same-origin Capture API adapter.

## Nuxt module versus direct SDK versus Capture API

| Option | What the first-party docs/source provide | Fit for this scope |
| --- | --- | --- |
| `@posthog/nuxt` | Official recommendation for Nuxt 4.x and 3.7+. It auto-initializes `posthog-js`, provides `usePostHog()`, feature-flag composables, a Nitro server client, and optional source-map processing ([Nuxt guide](https://posthog.com/docs/libraries/nuxt-js)). The module source adds a client plugin that calls `posthog.init(publicKey, { api_host: host, ...clientConfig })`, puts both key/host in `runtimeConfig.public`, and conditionally hooks `vue:error` to `captureException` when `capture_exceptions` is enabled ([module source](https://github.com/PostHog/posthog-js/blob/main/packages/nuxt/src/module.ts), [client plugin](https://github.com/PostHog/posthog-js/blob/main/packages/nuxt/src/runtime/vue-plugin.ts)). | **Reject for now.** It necessarily creates a browser project client/config, makes broad SDK defaults easy to miss, and brings server/source-map/error-tracking/module machinery deferred by the user contract. |
| Direct `posthog-js` with all disable flags | Smaller integration and explicit SDK settings are possible; `before_send`, memory persistence, and opt-out APIs are documented. | **Still reject for the strict contract.** It still ships a project token/config, automatically enriches events with session/browser state, owns batching/retries, and has no public unsent-queue discard. |
| Reaparr sender + PostHog Capture API | Uses the documented public ingestion endpoint and backend-embedded project token; no browser SDK, automatic capture, SDK storage, session props, replay, flags, remote config, or personal API key. | **Selected.** The backend resolves the authenticated login account UUID and enforces administrator authorization, opt-out, payload allowlisting, and failure isolation. |

## Version/documentation caveats

- The Nuxt guide currently covers Nuxt 4.x and Nuxt 3.7+; it links separate instructions for older Nuxt versions. Verify compatibility against the exact Nuxt and package versions before any future SDK decision.
- `before_send` was introduced in `posthog-js` 1.187.0; `tracing_headers` requires 1.380.0+; `rewriteRequestPath` requires 1.416.0+ ([configuration reference](https://posthog.com/docs/libraries/js/config)). These are not reasons to add the SDK now.
- The PostHog docs and `main` source currently publish date-based defaults through 2026. Pin exact package versions and review generated `PostHogConfig`/source before release; never treat an unpinned `main` branch or `defaults: 'latest'` as a stable privacy contract.
- The queue/opt-out behavior cited above is documented by the first-party source and issue tracker as observed at the cited versions; re-check it if a future release is considered. The Reaparr-owned sender remains preferable because its strict queue/opt-out behavior is explicit and testable in the application boundary rather than delegated to SDK internals.
