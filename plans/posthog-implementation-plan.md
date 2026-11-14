# Unified PostHog implementation plan

Related work: issue 678. Backend implementation was explicitly authorized after the planning review. The maintainer subsequently rejected the defensive/generic implementation and approved the minimal contract below through the grilling decisions. This file supersedes the old backend/frontend research and launch plans; do not recreate them.

**Scope:** backend phase 1 now; native browser collection/replay later. No commit, push, merge, deployment, remote project change or host-package installation is authorized. Preserve unrelated worktree changes. Backend changes are undergoing implementation verification; the ledger below distinguishes implementation, isolated evidence and real ingestion.

## 1. Approved decisions

- Cover meaningful use, selected configuration, effective operational settings and actual outcomes across Reaparr, not a small feature sample.
- All intentional backend business instrumentation goes through **business-specific `ITelemetry` methods**. Feature callers inject only `ITelemetry` for telemetry; they do not assemble property dictionaries or inject build information, identity/settings services or database access solely for telemetry.
- Native Serilog, automatic OpenTelemetry instrumentation/export and the future browser SDK remain independent native integrations. No custom analytics framework, endpoint registry, transport, queue, retries, identity system or event-builder hierarchy.
- Use enums for events and finite semantic values, reusing existing domain enums. **Enum names are the wire values**; no second enum-to-wire catalog. Native protocol/property keys remain named constants or existing property names. Preserve the established `reaparr diagnostic` event and backend/frontend origin labels.
- Trust internal contracts. Remove telemetry-specific caller validation, whitespace checks, configuration guards, `try/catch` and `Result.Try` wrappers. Preserve genuine request validation, business decisions, cancellation and existing error handling.
- Identity, build labels, configuration eligibility, SDK admission/failure containment and native scope enrichment live behind the facade. Resolve the existing account UUID through `IUserService.GetTelemetryId` at startup after default-user creation, before background business work; use that same UUID for this host lifetime. No replacement UUID or per-event identity query.
- **Person profiles contain settings-module configuration only.** The maintainer explicitly rejected database-backed account/server/connection/library/folder/integration profile inventories. Those features still have business events carrying their relevant non-secret values.
- Reuse `BaseSettingsModule.SetProperty` → `HasChanged` → `IUserSettings.SettingsUpdated`. The facade owns one subscription plus one startup snapshot. No before/after comparator, per-save hook, no-op detector, default model, per-setting enum, extra notification stream, reconciliation command or telemetry-specific database query.
- Send the full current non-secret settings snapshot as ordinary properties on `SettingsChanged` and as native person properties. Preserve actual false/zero/null/default/structured values, names, paths, URLs, IPs and IDs. The event means the existing notification fired after in-memory updates, not proof of a disk save or a particular changed field.
- Do not deliberately select passwords, tokens, API keys, cookies, authorization headers, raw requests/responses or downloader command lines for new business/profile payloads. **No generic sanitizer or SDK `BeforeSend` filtering.** The maintainer accepts original native backend exception messages, inner exceptions and source context, including possible sensitive values. Do not claim those native error payloads are always secret-free.
- Preserve existing Serilog/`IsUnmasked` behavior, including PostHog export. Operational logs may contain sensitive values by explicit maintainer choice; no separate remote masking policy.
- Keep immutable build-time ingestion configuration and non-transmitting unconfigured builds. Telemetry is best effort and cannot change business outcomes; native queue admission is not delivery.
- Frontend eligibility remains authenticated sessions, including authenticated setup, never anonymous login collection. Later replay records every eligible session with inputs and credential-bearing UI masked.
- Feature flags, experiments, surveys and AI/LLM telemetry are excluded.

### Accepted and deferred evidence

| Surface | Status |
| --- | --- |
| Rider launch | Fixed and accepted by the maintainer; do not reopen. |
| Existing backend and frontend diagnostic buttons | Delivery manually verified by the maintainer. The frontend button proxies the backend endpoint; this does not prove native browser collection/replay. |
| Revised backend implementation | Current source contains typed facade partials, stream-based profiles and native integrations. New verification is required; historical tests do not validate this replacement. |
| Real PostHog evidence for new feature/profile/error/trace flows | Separate authorized observation gate; local receivers and queue admission are not SaaS ingestion evidence. |
| Frontend SDK/replay | Later phase, not authorized by backend implementation. |
| Docker/desktop packaged/deployed verification | **OPEN until maintainer merge/deployment.** Do not merge, publish or deploy to close it. |

## 2. Minimum native integration and lifecycle

### Facade and ownership

`ITelemetry` lives in `src/Application.Contracts/_Shared/Interfaces/ITelemetry.cs` with Downloads, Libraries, Accounts, Integrations and Actions partials. Application contracts allow existing request/report types without moving business DTOs into Domain or creating telemetry payload DTOs. `src/Application/Telemetry/Telemetry.cs` and matching partials implement the single facade. Partials group related behavior; they are not independently registered services.

| Capability | Contract |
| --- | --- |
| Business facts | Synchronous `void` use-case methods such as `DownloadCreationProcessed`, `PlexAccountChanged`, `IntegrationSetupFinished` and `NotificationsCleared`; accept existing typed inputs/results, not caller dictionaries. Private `Capture` assembles labels/context and calls the native SDK. |
| `InitializeAsync()` | Boot calls once after successful `CreateDefaultAppUserCommand`, before queue setup/recovery/jobs. Resolve existing UUID through a short DI scope, publish native startup `$set`, subscribe to existing settings notifications. No database profile collection or Arr-migration dependency. |
| `SendDiagnostic(origin)` | Returns the existing `Result` admission contract: accepted-for-sending, unavailable or unconfigured. Retain public enum validation and unmapped-JSON rejection; do not expose arbitrary capture. |
| `CaptureException` / `CaptureErrors` | Original exception to native SDK. Result overload inspects actual exceptional errors, distinguishes expected cancellation/rejection and marks the current operation status; no exception-text copies on ordinary analytics. |
| `BeginRequest(sessionId)` | Synchronous native Activity/Serilog enrichment using the trusted startup UUID and supplied native session context. Caller receives an ordinary disposable. Never trust browser distinct ID as backend identity. |
| `BeginJob(operation, context)` | Synchronous scope owns native consumer Activity, LogContext and prior ambient-context restoration. Context is native `ActivityContext`, optional session and existing job/fire IDs supplied by the Quartz extension; no new context service or persistence. |
| Manual operation spans | Typed facade methods or `StartActivity(TelemetryActivity, ActivityContext)` return nullable native `Activity`; no listener/sampling means no span, not failed business work. |

Register the facade and `ActivitySource` once in `ApplicationModule`; the facade owns only its settings subscription. Native SDK/provider/logger disposal remains host-owned. No per-event flush or fire-and-forget business task. Keep failures contained at shared SDK admission and startup/settings-observer boundaries, not repeated at each business call. No defensive validation of trusted method arguments.

### Verified compatibility

| Product | Native choice and limit |
| --- | --- |
| Analytics/profiles/errors | **PostHog 2.18.1**. Native `Capture` overload accepts `personPropertiesToSet` and `personPropertiesToSetOnce`; pass the mutable snapshot to `$set`, not `$set_once`. Startup uses native `$set`; changed events carry both ordinary historical properties and native `$set`. Do not use network `IdentifyAsync` on business paths. `CaptureException` owns native exception envelopes/grouping. Its mechanism is SDK-defined (`handled: true` in this version); do not invent an unhandled argument. [R1–R4] |
| Logs | **Serilog.Sinks.OpenTelemetry 4.2.0**, the existing sink. Include native trace/span fields plus message-template attributes. `FromLogContext` carries person/session/job context. No second logger/OTel Logs provider. [R5] |
| Traces | **OpenTelemetry.Extensions.Hosting, Exporter.OpenTelemetryProtocol, Instrumentation.AspNetCore and Instrumentation.Http 1.20.1** in AppHost. One host provider, `AddSource("Reaparr")`, parent-based always-on sampling, standard W3C propagation and native HTTP/protobuf export. [R6–R8] |
| Automatic errors | Keep automatic `RecordException=false` and native URL-query redaction. ASP.NET instrumentation records error type/status; the facade owns original native exception capture once, rather than duplicating exception text in automatic span events. [R8] |
| Browser later | **posthog-js 1.446.0**, one client-only Nuxt plugin; no browser OTel or server-oriented Nuxt module. Recheck APIs only if deliberately selecting a different version. [R9] |

Keep analytics labels `environment`, `release`, `runtime_mode`, `platform`, `runtime_identifier`, `origin`, explicit `$process_person_profile=true` and `$geoip_disable=true`. Preserve existing Logs resource labels (`release_channel`, `runtime_platform`, `runtime_environment`, `application_id`, `operating_system`, `runtime_identifier`) and `service.name=reaparr`; process `application_id` is not person identity.

### Immutable ingestion configuration

| Boundary | Host / public project token |
| --- | --- |
| Actions variables and Docker arguments | `TELEMETRY_HOST` / `TELEMETRY_PROJECT_TOKEN` |
| Reusable workflows | `telemetry_host` / `telemetry_project_token` |
| Desktop build options | `--telemetry-host` / `--telemetry-project-token` |
| MSBuild and assembly metadata | `ReaparrTelemetryHost` / `ReaparrTelemetryProjectToken` |
| Facade/native host setup | `IAppBuildInfo.TelemetryHost` / `.TelemetryProjectToken` |

Preserve valid-pair-or-neither, build-time HTTPS-origin validation, invalid/partial build failures and configured publishing requirements. These are build/public-boundary contracts, not redundant business telemetry validation.

- SDK uses the embedded pair. Logs: `${host}/i/v1/logs`; traces: `${host}/i/v1/traces`; native HTTP/protobuf with `Authorization: Bearer <public ingestion token>`.
- Explicit trace exporter destination/protocol/headers override corresponding `OTEL_EXPORTER_OTLP*` ingestion settings. There is no blanket .NET OTLP `ignoreEnvironment`; do not promise unrelated batching/TLS/resource settings are ignored. Preserve Serilog `ignoreEnvironment:true`.
- Unconfigured builds disable SDK and omit trace provider/export and remote Logs sink. Runtime variables cannot activate or retarget ingestion. Exclude ingestion traffic from automatic client tracing.
- Native shutdown/disposal handles draining; no durable delivery guarantee on crash/updater exit. No management credentials in shipped artifacts. Preserve remote project policy; do not mutate retention, IP storage or spending configuration.

## 3. Whole-backend feature ownership and event semantics

Below, `A/` means `src/Application/`, `P/` means `src/PublicAPI/`. Event names are the matching `TelemetryEvent` enum members. Closed action/outcome/trigger/client values use enum names directly.

### Counting rules

1. Intent, admission and completion are different facts. Backend owners report what they actually accepted/did, not inferred UI clicks. Later frontend actions report confirmed user intention, not duplicate backend completion.
2. One deliberate endpoint-level create/control fact. Nested shared calls (restart→stop, server pause→child pause, shutdown auto-pause) do not create extra user requests.
3. Root downloads emit committed lifecycle changes. File attempts/retries/moves/cleanup use spans/logs/errors, not progress/chunk/product events. Retries keep existing logical task identity.
4. Preserve actual report units: creation `Total` includes hierarchy nodes, not just files or roots. Use existing per-family fields/counts; no telemetry-only media scan/counter.
5. Publish committed facts before cancellable notification/response writing. A later failure cannot erase the mutation. HTTP 200, `Result.Ok()` containing an unhealthy status and Quartz completion are not sufficient business-success evidence.
6. Expected auth/domain rejection/cancellation is an outcome, not a manufactured exception. Generic binding/validator failures stay technical HTTP observations. Capture an actual unexpected exception once at its terminal owner.
7. Existing IDs join operations: task/root key, media/server/library/account/folder IDs, integration GUID/type, torrent hash, refresh-run ID, library admission `(server, library, CreatedAt)`, ordered comparison pair, Quartz job/fire ID. No new run/identity scheme.
8. A successful mutation event does not imply a telemetry-computed before/after difference. Preserve existing no-op/domain decisions; do not add comparison queries/guards solely for analytics. Settings specifically follow existing notifications (§4).

### Downloads — every media family

| Owning files/use case | Fact and boundary |
| --- | --- |
| `A/PlexDownloads/Preview/GetDownloadPreviewEndpoint.cs`, `GetDownloadPreviewQuery.cs` | `DownloadPreviewFinished`: actual preview query result/selection, not creation. |
| `Create/CreateDownloadTasksEndpoint.cs`, `CreateDownloadTasksCommandHandler.cs` and existing generators | `DownloadCreationProcessed`: actual persisted report/rejection/no-op for movie, TV hierarchy, music hierarchy, photo album/image and other video. Public-client shared generation must not duplicate the UI request fact. |
| `Start/StartDownloadTaskEndpoint.cs`, `Pause/PauseDownloadTaskEndpoint.cs`, `Stop/StopDownloadTaskEndpoint.cs`, `Restart/RestartDownloadTaskEndpoint.cs` and commands | `DownloadControlProcessed`: selected keys, typed action, result and delete-files choice. Existing-job skip is not a new start; partial restart does not claim every child restarted. |
| `Delete/DeleteDownloadTaskEndpoint.cs`, key-based delete handler | `DownloadDeletionProcessed`: actual task deletion result; requested delete-files does not prove all destination files disappeared. |
| `ClearCompleted/ByServerId` and `ByDownloadTaskId` endpoints; `DeleteDownloadLogs/DeleteAllDownloadTaskLogsByDownloadTaskIdEndpoint.cs` | `CompletedDownloadsCleared` / `DownloadLogsCleared`: existing matched completed-task/deleted-log counts and scope. Zero is no-op; neither means transfer completion. |
| `_BackgroundJobs/DownloadJob/Notifications/DownloadTaskUpdateDispatcher.cs`, `src/Data.Contracts/Extensions/DbContext/DbContextExtensions.DownloadTaskStatus.cs` | `DownloadLifecycleChanged`: actual committed **root** transition. `DetermineDownloadStatus` returns existing `(DownloadTaskKey Key, DownloadStatus Status)` values from successful conditional writes, not a later status query. A directly changed root uses its committed new status. Never report a leaf status as parent status. Emit before cancellable notification. |
| `_BackgroundJobs/DownloadJob/DownloadJob.cs`, Direct/DASH clients, scheduled-limit job | Native file/attempt/effective-limit spans. Direct retry is not another logical root start. Preserve terminal callback context and actual selected client/segments/rate. |
| `_BackgroundJobs/MoveDownloadFileJob/MoveDownloadFileJob.cs`, `MoveDownloadFileFromFileTaskCommand.cs`, `CleanUpDownloadTaskFolders.cs` | Native movement/retention/cleanup spans with existing retry count/task key. Retry firing is an attempt; completion before cleanup failure stays completed. |
| `Recover/RecoverInterruptedDownloadsCommand.cs`, `AutoPause/AutoPauseActiveDownloadsCommand.cs` | Reuse the status dispatcher with recovery/system provenance. No extra lifecycle counter or status-routing redesign; existing two-pass pause counts are not distinct-file counts. |

`DownloadFinished` is not destination `Completed`; one completed child is not completed parent. Preserve typed paused/stopped/moving/error states. If deletion removes rows without a persisted `Deleted` transition, its deletion event is sufficient—do not invent that transition.

### Libraries, media and photos

| Owning files/use case | Fact and boundary |
| --- | --- |
| `A/PlexMedia/GetAll/GetAllMediaByTypeEndpoint.cs` | `MediaQueryFinished` for an actual non-empty search, with effective search/filter/sort/page/media family and existing result totals. Hydration/browse is not inferred intent. |
| `A/PlexLibraries/PhotoBrowse/SearchAssets/SearchPhotoAssetsQuery.cs` and endpoint | `PhotoSearchFinished`: actual search/rejection, item count, total count/bytes and pagination with correct units. |
| `RefreshLibraryMedia/RefreshLibraryMediaEndpoint.cs`, `A/PlexServers/SyncPlexServerMedia/SyncPlexServerMediaEndpoint.cs` | `LibraryRefreshRequested` / `ServerMediaSyncRequested`: actual request result, not completed sync. |
| `_BackgroundJobs/LibrarySync/Commands/QueueLibrarySyncJobCommand.cs` and queue claim/scheduling | `LibrarySyncQueued` only at actual new/reset admission, carrying existing admission identity/force flags. Already queued/processing/fresh or scheduling rollback is not another admission/start. |
| `_BackgroundJobs/LibrarySync/LibrarySyncJob.cs`, refresh command and listener | `LibrarySyncStarted` for real online processing; `LibrarySyncFinished` after persisted terminal outcome. Offline requeue is not completion. Covers Movie, TvShow, MusicArtist, PhotoAlbum, OtherVideos. |
| `CancelLibrarySync` endpoint and `CancelLibrarySyncJobCommand.cs` | `LibrarySyncCancellationProcessed`: actual cancellation request result. Already-terminal/newer/absent admission and completion-winning races are not cancelled work; no duplicate worker terminal event. |
| `_BackgroundJobs/LibraryComparison/PlexLibraryComparisonJob.cs`, scheduling/invalidation; `Comparison/Movie`, `TvShow`, `Music`, `State` handlers | `LibraryComparisonStarted` / `LibraryComparisonFinished` for ordered supported pairs. Suppression/coalescing is not execution. Native handler/state spans; no fake photo/other-video comparison or telemetry-only counting query. |
| `RefreshLibraryAccess/RefreshLibraryAccessCommandHandler.cs`, `CRUD/AddOrUpdatePlexLibrariesCommandHandler.cs` | `LibraryAccessReconciled` at actual reconciliation with refresh-run/account/server/library identities and existing grants/revocations/partial reports. No DB profile refresh. |
| `A/PlexMedia/_BackgroundJobs/MetadataSync/MetadataSyncJob.cs` and processors | `MetadataSyncFinished` with actual processed counts; offline skipped. Registered but not automatically scheduled—do not create a recurring job to generate telemetry. |
| `_BackgroundJobs/CheckPlexLibrariesForUpdates` | Scheduled discovery feeds the same real admissions with scheduled provenance, not manual-refresh usage. |

### Accounts, servers, connections and application actions

| Owning files/use case | Fact and boundary |
| --- | --- |
| `A/PlexAccounts/Create`, `Update`, `Delete` endpoints | `PlexAccountChanged` with safe actual account values and action. Later inspection scheduling failure is separate; account deletion does not create a new Reaparr identity. |
| `GenerateToken`, `ValidateCredentials`, `ValidatePlexToken` endpoints | `PlexAuthenticationProcessed` with typed method/result, never token/password/code/raw request. |
| Account/server `RefreshAccess` endpoints and account refresh job/command | `PlexAccessRefreshRequested` versus per-account `PlexAccessRefreshFinished`, existing reports and manual/scheduled provenance. Server fan-out request and account outcomes are different units. |
| `A/PlexServers/SetServerOwned`, `SetServerEnabled`, `SetServerAlias`, `Delete` | `PlexServerChanged` with saved override/effective-owned, enabled, alias/deletion values. Later cache/comparison failure does not undo a saved value. Settings-module alias updates follow their existing notification behavior independently. |
| `InspectPlexServer` endpoint, queue command and job | `ServerInspectionRequested`, `ServerInspectionStarted`, `ServerInspectionFinished`. Existing/disabled skips are not new work. Inspection/sync admission is not finished sync/comparison. |
| `ServerPause`, `ServerResume` endpoints/commands | `ServerDownloadControlProcessed`: actual pause flag/request result. Ignored child results cannot prove all children paused; waking a queue is not completed resume. |
| `A/PlexServerConnection/Create`, `Update`, `Delete`, `SetPreferredConnection` | `PlexConnectionChanged` with normalized address/protocol/port/URL/preferred values and IDs, including existing GET mutations. No configuration inventories/profile queries. |
| Connection `Validate`, `CheckConnectionStatus`, `CheckAllConnectionsByServer`; server `RefreshPlexServerConnections` | `PlexConnectionCheckFinished`: execution result **and actual health status**. Automatic health work remains operational. Connection refresh actually performs account-scoped access refresh; report that real scope. |
| `A/PlexLibraries/SetLibraryEnabled`, `SetDefaultDestination` | `PlexLibraryChanged`: saved enabled/destination values, even when later queue/cancel/cache work fails. Do not mistake GET mutation for hydration. |
| `A/FolderPaths/GetDirectory`, `Create`, `Update`, `Delete` | `FolderDirectoryListed` for real listing result/path; `FolderPathChanged` for record operations, not physical directory creation/media deletion. |
| `A/Authentication/UserLogin`, `UserLogOut`, `UpdateCredentials` | `AuthenticationProcessed` / `CredentialsUpdateProcessed` with actual login/locked-out/rejected or partial username/password-operation result; never credential values. |
| `A/Notifications/Hide`, `ClearAll` | `NotificationVisibilityProcessed` and `NotificationsCleared`. Matched rows do not establish changed visibility; clear count is actual deletion, including zero. |
| `A/Update/DownloadUpdate`, `ApplyUpdate` | `UpdateDownloadFinished`, `UpdateApplyAccepted`, `UpdateLauncherFinished` after real launcher invocation. Preserve `OnCompleted`→`Task.Run` native parent. Old process cannot prove installation/restart success. |

### Sonarr/Radarr and compatibility API

| Owning files/use case | Fact and boundary |
| --- | --- |
| `A/Integrations/{Sonarr,Radarr}/Create*Integration`, `Update*Integration`, `Delete*Integration` | `IntegrationChanged` with existing GUID/provider and safe name/URL/category/folder/configuration fields. Local save is not remote provisioning. Forced local removal and remote cleanup failure remain distinguishable. |
| Provider `Setup*Integration` command | `IntegrationSetupFinished` with actual result and existing typed setup stage, including persisted remote IDs before later failure. No duplicate endpoint completion/profile refresh. Generic enum constraint may pass the existing SignalR stage without adding a Contracts→SignalR dependency or mirrored enum. |
| Provider `TestConnection` endpoint | `IntegrationConnectionTestFinished`: actual upstream health/status, not envelope success. Startup retests are technical, not manual adoption. |
| Legacy `ClearConfiguration` endpoints/migration | No custom comparison/profile hook or `LegacyIntegrationConfigurationCleared` event. Preserve existing legacy operation and settings notifications; safe settings DTO snapshot excludes legacy credential settings. Do not claim a DB integration/remote resource was deleted. |
| `P/Indexer/Torznab/TorznabEndpoint.cs` and existing search/feed commands | `ExternalIndexerQueryFinished`: search versus RSS, actual result/count/category/query/media/integration context. HTTP-200 XML error/cancel is not success; RSS is automated feed use. Caps/unknown are technical only. |
| `P/DownloadClient/Torrents/DownloadTorrent` | `TorrentMetadataGenerated` after real torrent metadata generation, not Plex transfer or import. |
| `AddTorrent` | `ExternalDownloadSubmissionProcessed`: accepted after task creation **and hash association**. HTTP-200 `Fails.` remains failure/rejection. Preserve task report/hash/stage when creation committed before association failed; no duplicate UI creation event. |
| `DeleteTorrent` | `ExternalDownloadRemovalProcessed`: real matches/deletions/partial/prerequisite outcomes and delete-files choice, not inferred from HTTP-200 `Ok.`. |

### Explicit exclusions

- Ordinary list/detail/settings/auth-status/credential hydration; username availability; sync status; snapshots/log/notification/background-job polling; automatic update checks: native traces/logs, later pageviews/actions, no per-poll product event.
- Media overview/detail, comparison-detail reads, photo album/assets/members/folders/source-roots/inspection reads, library access timeline and metadata/filter options: frontend views/actions, not backend comparison/inspection executions.
- Image bytes/thumbnails/static assets/Swagger/hub/SignalR delivery: technical only.
- qBittorrent info/files/properties/version/preferences/categories; Torznab caps/unknown: protocol/technical only. Category validation is not category creation.
- `ResetDatabaseEndpoint` currently only returns OK; `GetTorrentFileEndpoint` returns placeholder bytes. No fake reset/export success event and no stub implementation bundled into telemetry.
- Media-overview cache coordination, comparison derived-state application, database optimization, schedule-limit recalculation, automatic connectivity, startup/migration and internal notification production: native system spans/logs/errors, not manual adoption. `AllJobListener` remains technical lifecycle reporting, not an endpoint/job business-event registry.

## 4. Settings profiles and effective configuration

### Existing notification ownership

`UserSettings` already merges module `HasChanged` observables and throttles updates by 500 ms. `ConfigManager` independently persists from that stream. `BaseSettingsModule.SetProperty` supplies its existing equality behavior; reference-valued replacements or startup source emissions may still notify. **Trust those semantics.** Do not add another timer, diff, no-op test, per-GET/per-save hook or persistence ordering protocol.

The facade publishes startup `$set` after account creation and subscribes once. Each subsequent notification creates one detached snapshot, uses it as all ordinary `SettingsChanged` properties and native `$set` properties, then lets native SDK admission return. No telemetry runs inside the root settings PUT gate. Existing scheduling, persistence and response error behavior stays owned by that endpoint.

Use `IUserSettings.ToDTO()` as the existing safe contract. Serialize to `JsonElement` with a private copy of standard camel-case/string-enum options retaining nulls; this detaches aliased collections before native asynchronous batching. Flatten **one module/property level** into `settings.<camelModule>.<camelProperty>`, preserving compound dictionaries/lists as structured values. No default comparison, recursive leaf registry, hashes, non-default booleans or duplicate changed-field/count metadata.

| Included source | Current values |
| --- | --- |
| General/Confirmation DTOs | Existing setup/account/visibility/poster/disclaimer/Discord choices and confirmation choices across all media families. |
| DateTime/Display/Language DTOs | Formats/timezone/relative dates, view modes and language. |
| DownloadManager DTO | Configured segments, keep-completed, schedule enabled and nested days/time/rate mapping, including null/zero/false. |
| Server DTO | Existing settings rows: machine IDs, aliases, manual rates and stream-downloader choice. Read current data; do not call getters that create rows for telemetry. |
| Network/Debug DTOs | Proxy/base URL, trust/IP/header-name settings and current debug/masking choices; they do not change accepted logging behavior. |
| HeaderAuthentication model | Explicit safe projection of enabled, mapping type, trusted proxies, logging, maximum header length and HTTPS requirement. No incoming header values, invented DTO-only settings or new mutation/watch API. |

The normal DTO excludes authentication secrets and legacy integration settings; keep those exclusions. Do not project database inventories or raw auth/integration entities into profiles. Profile state is latest-ingested best effort, not an atomic/linearizable settings mirror. No added locks, reconciliation, version scheme or recovery queue. Mutable changes not emitted by the existing stream are not permission to add watchers.

### Effective settings at operations

Configuration snapshots do not prove what an operation used. Typed facade methods receive values already resolved by the actual owner; they do not query business state to reconstruct them.

| Boundary | Effective values |
| --- | --- |
| Client selection, DownloadJob, Direct/DASH | Selected actual client; configured versus effective segments (`Math.Max(1, configured)` for Direct); source/task/media IDs and existing selected quality. Allow-stream setting is permission, not proof of DASH selection. |
| Scheduling/manual rate and actual client | Effective bytes/sec, manual cap, schedule/timezone/slot context where used. Positive configured limits convert by 1024; nonpositive means unlimited. Direct limits change live; DASH uses captured options. No product event per tick. |
| Task/folder resolution | Actual populated download/destination roots, task/integration/library/default precedence, existing source/library/server/media context. Do not describe a bypassed folder as effective. |
| Move/retention | Source/destination, actual moved/retained result, global keep choice, task override and typed retention reason (including same path/missing destination). False global keep does not prove movement. |
| Library/comparison | Actual force/access/enabled/ownership/media/pair context available at execution, not full settings on every event. |
| Integration/public operations | Actual integration/provider/category/folder/configured URL and existing resolved advertised callback/base URL. Never serialize API keys or authenticated download URLs. |
| Authentication/proxy | Existing non-secret mode/trust/HTTPS/header-name context, not presented credential/header values. |

## 5. Native correlation and error ownership

| Signal | Linkage |
| --- | --- |
| Analytics/native errors | Existing `distinct_id`, current W3C `$trace_id`/`$span_id`, `$session_id` when known. |
| Traces | Native OTLP IDs and `posthogDistinctId` / `sessionId` attributes plus business keys. |
| Logs | Native trace/span fields; `posthogDistinctId`, `sessionId`, job/fire/business context through Serilog LogContext. Resources do not substitute for actual trace/session linkage. |
| Browser later | Native `tracing_headers` for the backend hostname sends PostHog session/distinct/window headers. Backend uses its own UUID and session only as correlation, not authentication. No custom session UUID validator or identity trust scheme. |

- `TelemetryContextMiddleware` sits after routing and before auth middleware. It opens the facade request scope and captures/rethrows escaping unexpected exceptions once with method/route (not query/body). Caller-request cancellation is not an error; unrelated transport cancellation can be. Preserve normal auth/HTTP behavior.
- Ordinary awaited commands/HttpClient use native `Activity.Current`/ExecutionContext/W3C propagation.
- Quartz scheduling extensions store traceparent/tracestate/session as a few scalar `telemetry.*` JobDataMap entries. Filter them out of typed business/UI payloads. Preserve retry metadata and existing recovery behavior; native parser handles malformed/absent parents.
- Each actual job scope uses `BeginJob(..., context.GetTelemetryContext())`. The facade clears unrelated ambient Activity before creating a consumer root/child and restores it on disposal, with native logging scope ownership. Do not assume an Activity opened in `JobToBeExecuted` flows into `Execute` across async callbacks.
- Terminal consumed failed Results call `CaptureErrors` at the owner that handles them. Escaping Quartz exceptions are captured centrally by `AllJobListener` using the job's native parent/session and job/fire identity; no repeated per-job telemetry catch/rethrow. That listener error scope is a separate span, not a claim to keep an already-disposed job span alive. Existing job reporting/result semantics remain unchanged.
- Direct/DASH meaningful callbacks retain native captured parent/session when ExecutionContext does not flow; no per-progress span. Update response completion explicitly parents its launcher span.
- Download channels, library admission rows, move/recovery/coalesced work can lose original request ancestry before scheduling. Start fresh native roots and join by existing operation IDs. No session cache, DB columns or durable context store.
- Retry attempts use spans/logs, with one native issue at terminal unexpected failure. Offline/expected rejection/cancellation is a normal outcome. Move cleanup failure does not undo completed placement. Preserve original errors without synthetic wrapping/regex scrubbers.
- No global dedup store, `Exception.Data` marker, transport-failure analytics recursion or independent log sanitization.

The browser SDK has **no general browser span API** [R12]. Promise session/person→server trace→operation-ID correlation, not an uninterrupted browser-parented trace or restored initiating session after restart.

## 6. Frontend later — explicit gate

Backend phase 1 does not implement or alter these surfaces.

### Lifecycle and coverage

Use one client-only PostHog plugin, direct native SDK and typed finite event/action names (enum wire names); no frontend analytics framework.

- Extend the existing authenticated `AuthenticationStatusEndpoint` / `UserClaimsDTO` response with nullable bootstrap (embedded public host/token, same UUID, release/runtime). No anonymous config endpoint or runtime ingestion override. Missing/failed telemetry cannot fail authenticated status.
- Auth/global store initialize after authenticated status, before dependent setup. Native bootstrap identified ID, `identified_only`, `autocapture:false`, `capture_pageview:false`; one router hook emits native `$pageview` initially and after eligible navigation. Disable flags/surveys/experiments with supported native options while retaining replay configuration access.
- Configure native `tracing_headers` in the existing Axios boot seam for the backend **hostname** only; no promise of origin/port/path matching. Do not widen CORS or enable headers for Plex/Arr/third parties.
- At session reset/logout/401, stop replay/errors/headers, opt out and reset before clearing stores/rendering anonymous UI. Reset alone does not stop anonymous collection. Re-login uses native opt-in without an opt-in event, trusted identify/bootstrap and recording controls before actions/pageviews resume. No duplicate plugin/listener lifecycle.
- All authenticated pages get pageviews: dashboard/downloads/access history, every media-family overview/detail including unknown fallback, settings and authenticated setup. Anonymous login stays excluded.

| Frontend owners | Intent only, not backend outcome |
| --- | --- |
| Media overview/store/detail; photo browsing/inspection/selection/slideshow | Confirmed search/filter/sort/comparison/inspection/selection/slideshow actions, actual selection/context. No pagination/render/image request events. |
| Download store/preview/dialog/control | Preview/download/control request and confirmation dismissal with actual selection/action; no frontend completion event. |
| Library/access/server/connection stores | Refresh/cancel/configuration/inspection/control/connection CRUD/test/preferred intentions. |
| Account/dialog/auth stores | Authenticated account/token/credential actions and logout, never credential values; anonymous login result is backend-only. |
| Settings/folder/integration stores | Save/folder selection/setup/test/delete intentions, not duplicate current-profile publishing. |
| Update/notification/log/help/setup/navigation surfaces | Actual requested update/visibility/clear/view/filter/diagnostic/help/link/disclaimer/setup actions; no poll/SignalR/log-line events. |

Choose the component/store boundary that knows confirmed intent; do not instrument both wrappers for the same action. Native browser exceptions handle unexpected browser faults, not duplicate synthetic errors for each handled backend 4xx/5xx response.

### Replay and browser privacy

- Every eligible authenticated session, including short sessions: native replay enabled, unconditional 100% project trigger group and zero minimum duration. Verify policy before later rollout; do not mutate it now or force recording to bypass it. [R14]
- `maskAllInputs:true`, including password-reveal modes; targeted native masking/blocking on credential-bearing non-input content. Preserve ordinary UI. [R15]
- Cover generated token QSubHeader text in `AccountGenerateTokenDialog.vue`, token-validation dialogs, integration/setup raw error panels, authentication/password components, alert/GitHub-report links, live logs and download-log/error detail.
- Disable replay console and request/response body/header capture. Native `maskCapturedNetworkRequestFn` handles credential-bearing network/page URLs. Browser `before_send` excludes raw API error/body/credential-bearing analytics; backend acceptance of original native exceptions does not waive these browser/replay requirements.
- Source maps use native PostHog CLI/Nuxt upload with separate authorized CI management credentials, project ID, proper app-region management host and release match. Never ship upload credentials/private maps in public assets. No implied .NET PDB upload pipeline. [R16]

## 7. Clean cutover and focused verification

### Ordered changes

1. Keep native SDK/build embedding/logger ownership. Move typed facade contracts to Application.Contracts, centralize identity/labels/scopes, use direct enum names and singleton lifetime.
2. Replace comparison/profile collectors with the existing settings subscription and startup snapshot. Remove root endpoint telemetry hooks/dependencies without changing its business gate/scheduler/response behavior.
3. Migrate every coverage-table owner and relevant tests to typed methods. Keep original committed-transition tuples, effective settings, cancellation/partial-result semantics and operation IDs.
4. Remove obsolete API/helper/mapping/sanitizer code and associated tests; no compatibility shims or dual events. Complete all implementation/test edits before validation.
5. Run targeted diagnostics, semantic tests and hosted native serialization/export checks. **The maintainer explicitly waived remaining per-file CodeScene checks for this pass.** Record observed results below; no score-driven redesign or unrelated cleanup.

| Addition | Disposition |
| --- | --- |
| Generic `TrackAsync`/public dictionaries/profile methods and old Domain interface | Replace with typed Application.Contracts interface; migrate all mocks/callers. |
| `ConfigurationTelemetry`, `CollectTelemetryProfileCommand`, settings diff helper and obsolete tests | Remove; no DB profiles, extra collectors or mutation reconciliation. |
| `TelemetryPayloadSanitizer`, `BeforeSend`, enum-to-wire mapper | Remove; explicit payload selection, original native errors and direct enum names. |
| Per-feature telemetry-only user/build/settings dependencies, repeated context/capture wrappers | Remove; preserve dependencies/guards needed for actual business behavior. |
| Diagnostic endpoint/DTO/origin and existing frontend diagnostic integration | Preserve validation and admission wording; frontend phase remains separate. |
| Root committed status tuple contract and dispatcher | Preserve exact committed statuses and existing business/notification semantics; no post-update telemetry query. |
| Native OTel/Serilog/build setup | Keep, without second logger/provider or masking policy. |
| Already absent endpoint catalog/feature registry/per-setting/default model/remote masker/toggle | Keep absent. Do not resurrect interrupted designs. |
| Legacy clear hook/event | Remove telemetry-specific comparison/event; preserve real operation and existing notification stream. |

### Verification matrix

Use repository TUnit/Shouldly/Moq harnesses and dotnet-test-mcp. Native serialization/export uses synthetic credentials and isolated HTTP receivers/seams, never production ingestion. Assert semantic outcomes/relationships, not mock echoes or vendor-envelope property counts.

| Contract | Targeted evidence |
| --- | --- |
| Identity/admission/native profile API | `Application.UnitTests/Telemetry/Telemetry.UnitTests.cs`, `TelemetryNativeCapture.UnitTests.cs`: same startup UUID, no per-event lookup, unconfigured silence, native admission failure containment, enum names, `$set` versus history, original nested errors. No per-event flush. |
| Settings ownership | Facade tests exercise real `UserSettings.SettingsUpdated`, detached nested values, false/zero/null and safe legacy exclusions; disposal stops subscription. Boot tests prove initialization after default user and before background setup. Root endpoint tests retain scheduler-failure/response-failure/applied-state and concurrency behavior without telemetry gate/diff assertions. |
| Download roots/attempts/effective settings | Existing create/preview/control/clear tests; Data status-extension tests; dispatcher family suites; Direct/DASH/download/move/retry/scheduled-limit tests. Same status no event, leaf cannot complete parent, cancelled notification cannot erase committed root fact, retries are not logical starts, retention/effective rate/segments are actual resolved values. |
| Libraries/accounts/servers/connections | Existing queue/cancel/job/comparison/access/inspection/mutation suites: admission identity, skip/requeue versus completion, terminal persistence, partial offline/health results and actual values. No assertions for deleted DB profiles. |
| Integrations/public protocol | Existing setup/test and Torznab/add/delete/torrent tests: XML errors/`Fails.`/partial `Ok.`, RSS versus search, staged setup IDs, committed tasks before association failure, real mutation state and original errors. |
| Native async/error ownership | Quartz metadata serializer roundtrip/retry/payload-exclusion tests, facade ambient-context tests, `AllJobListener` and middleware tests. Expected cancellation not captured; unexpected original failure once; existing terminal status preserved; no unrelated ambient ancestry. |
| Real native export and logging | `AppHost.UnitTests/TelemetryLogConfig.UnitTests.cs`: real SDK + ASP.NET/HttpClient OTel + Serilog OTLP to loopback; trace/span/session/UUID linkage, configured destination/token, local/remote masked and unmasked logs, thresholds and unconfigured no transmission. Original backend native exception text is expected; automatic trace exception text remains absent. |
| Hosted persistence flows | Existing photo-selection download creation, photo-library refresh and public TorrentAddFailure integration cases use real hosted paths/native capture with exact domain/protocol assertions. Historical passes are not current evidence. |
| Failure isolation | Facade SDK rejection/throw and identity-startup failure tests; hosted unconfigured behavior; preserve native outage/shutdown behavior. Distinguish SDK queue rejection from an actually observed network outage rather than claiming one proves the other. |

After diagnostics, select the smallest affected classes by fully qualified test class and owning project. Shared interface/test-fixture changes justify broader affected-project coverage when needed; do not repeat successful checks without changed evidence. No IDE test configurations, formatting-only runs or pre-fix baseline reruns. Remaining per-file Code Health reviews are skipped at the maintainer's explicit request.

### Actual ingestion and later gates

- With an explicitly configured build and authorized maintainer access, observe a new settings history/profile update, root download flow, library/integration/public flow, native issue, trace and correlated Logs. Match UUID/native context/operation IDs and actual project/release. Do not send artificial errors or modify live services without authorization.
- Existing manually accepted diagnostics remain accepted, but do not validate revised feature payloads or browser replay.
- Later frontend: generate API through Bun with backend running via Rider; focused Vitest/Cypress plus browser checks of eligibility/logout/re-login, actual network/replay masking, short-session recording, source-map stack and browser-session→backend→worker linkage. Demonstrate durable gaps honestly.
- **Docker/desktop gate remains OPEN after local backend work.** Following maintainer merge/deployment, verify actual available platform artifacts, embedded pair/release/runtime, conflicting ingestion variables, destination, native products, startup/business/shutdown during outage and later browser rollout. Report exercised platforms only.

## 8. Completion ledger

- [x] Backend implementation authorized; minimal facade/settings/privacy/enum design explicitly approved by the maintainer.
- [x] Native SDK/product APIs and version compatibility grounded in the primary references below; complete ownership and exclusions retained.
- [x] Revised backend source/test cutover implemented.
- [x] Targeted diagnostics, unit tests and local native-export checks recorded; remaining per-file CodeScene reviews waived for this pass.
- [ ] Full-host integration verification — **OPEN by explicit maintainer decision to deliver without closing this gate**.
- [ ] New backend SaaS ingestion evidence observed with authorization.
- [ ] Later native frontend/replay/source-map phase authorized, implemented and verified.
- [ ] Docker/desktop packaged/deployed acceptance after maintainer merge/deployment — **OPEN**.

Current verification for the simplified backend:

- `Application.UnitTests`: 2,462 passed in the full 2,470-case run; all eight remaining cases passed after corrections in a focused rerun.
- `PublicApi.UnitTests`: all 246 passed.
- `AppHost.UnitTests`: 151 passed in the full 153-case run, including native SDK/ASP.NET/HttpClient/Serilog export checks; both remaining middleware cancellation cases passed in the focused rerun.
- `Data.UnitTests`: all 41 download-status/hierarchy cases and all eight photo hierarchy cases passed.
- Focused rerun evidence: dotnet-test-mcp run `caeee18978744b508e14dcd77c0c586f`, ten passed, zero failed. The broader run's retained console summaries are authoritative where the MCP wrapper reported `ResultFileMissing`.
- Full-host integration verification remains open with explicit maintainer approval to finish this pass. MCP runs of `CreateDownloadTasksEndpointPhotoSelectionIntegrationTests`, `RefreshPlexPhotoLibraryCommandIntegrationTests` and `TorrentAddFailureIntegrationTests` each exited 1 after the TUnit banner, without test results or a fresh report. None is recorded as passed.
- `IntegrationTestMode` is unset in the local shell. Whether the MCP process has it, and whether this explains the early exit, remains unconfirmed. The runner API has no per-run environment override. A process-scoped direct-command diagnostic was denied with the instruction to use test-runner MCP; that command was not executed. No permission configuration was changed.

Native local receiver evidence is not new SaaS ingestion evidence. Historical diagnostic delivery remains accepted; frontend and packaged/deployed gates remain open.

## 9. Primary references checked

- **R1:** [Person properties, structured values and ingestion-order updates](https://posthog.com/docs/product-analytics/person-properties).
- **R2:** [PostHog .NET 2.18.1 capture overloads](https://github.com/PostHog/posthog-dotnet/blob/PostHog-v2.18.1/src/PostHog/Capture/CaptureExtensions.cs), [.NET SDK](https://posthog.com/docs/libraries/dotnet).
- **R3:** [Versioned PostHogOptions and native batching](https://github.com/PostHog/posthog-dotnet/blob/PostHog-v2.18.1/src/PostHog/Config/PostHogOptions.cs).
- **R4:** [Versioned native exception builder](https://github.com/PostHog/posthog-dotnet/blob/PostHog-v2.18.1/src/PostHog/ErrorTracking/ExceptionPropertiesBuilder.cs), [.NET Error Tracking](https://posthog.com/docs/error-tracking/installation/dotnet).
- **R5:** [.NET Logs](https://posthog.com/docs/logs/installation/dotnet), [Serilog OTLP 4.2.0 source](https://github.com/serilog/serilog-sinks-opentelemetry/tree/v4.2.0/src/Serilog.Sinks.OpenTelemetry/Sinks/OpenTelemetry).
- **R6:** [.NET distributed tracing/native linking](https://posthog.com/docs/distributed-tracing/installation/dotnet), [.NET Activity concepts](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts).
- **R7:** [OTel 1.20.1 exporter/environment precedence](https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.20.1/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/OtlpExporterOptions.cs), [Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.20.1), [Exporter](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol/1.20.1), [ASP.NET Core](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore/1.20.1), [HttpClient](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http/1.20.1).
- **R8:** [Versioned ASP.NET instrumentation status/recording/query behavior](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/Instrumentation.AspNetCore-1.20.1/src/OpenTelemetry.Instrumentation.AspNetCore/Implementation/HttpInListener.cs).
- **R9:** [posthog-js 1.446.0 metadata](https://registry.npmjs.org/posthog-js/1.446.0), [browser configuration](https://posthog.com/docs/references/posthog-js/types/PostHogConfig), [Nuxt](https://posthog.com/docs/libraries/nuxt-js).
- **R11:** [FastEndpoints postprocessor exception propagation](https://fast-endpoints.com/docs/pre-post-processors#handling-unhandled-exceptions-with-post-processors), [validation/exception middleware](https://fast-endpoints.com/docs/exception-handler).
- **R12:** [Browser session headers versus browser spans](https://posthog.com/docs/distributed-tracing/installation/nextjs).
- **R13:** [Native browser identity/opt-in/out/errors/replay controls](https://posthog.com/docs/references/posthog-js).
- **R14:** [Replay recording conditions/sampling/minimum duration](https://posthog.com/docs/session-replay/how-to-control-which-sessions-you-record).
- **R15:** [Replay privacy and URL masking](https://posthog.com/docs/session-replay/privacy).
- **R16:** [Nuxt release/source-map upload](https://posthog.com/docs/error-tracking/upload-source-maps/nuxt).
