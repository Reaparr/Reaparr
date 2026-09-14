---
name: reaparr-sonarr-integration
description: Use for all Reaparr Sonarr integration work — lifecycle endpoints, setup and provisioning, Sonarr API clients or payloads, qBittorrent-compatible callbacks, Torznab TV indexing, integration authentication, troubleshooting, or tests.
---

# Reaparr Sonarr Integration

## Purpose

Load this skill before changing or diagnosing the Sonarr integration. Load `reaparr-backend` first for backend work, then the narrower command-handler, EF Core, or backend-test skill required by the change.

Treat the integration as a bidirectional protocol:

```text
Reaparr -> Sonarr API v3
  create/update/test/delete qBittorrent download client and Torznab indexer

Sonarr -> Reaparr public API
  Torznab episode discovery -> torrent upload -> qBittorrent-compatible status/removal
```

A setup is complete only when both directions work and both configured Sonarr resources validate.

## Invariants

- Sonarr owns **Episode** downloads only. `IntegrationIdentity.Supports` rejects other media types.
- One download task belongs to Sonarr, Radarr, or neither; it cannot belong to both.
- `SonarrApiKey` authenticates Reaparr calls to Sonarr.
- `TorznabApiKey` authenticates Sonarr indexer callbacks through the `apikey` query parameter.
- `QBittorrentApiKey` authenticates Sonarr download-client callbacks through `Authorization: Bearer`.
- The route integration ID and the presented key must identify the same integration. Authentication compares keys in fixed time and stores `IntegrationIdentity` in `HttpContext.Items`.
- `DisplayName`, `BaseUrl`, and `Category` are independently unique among Sonarr integrations.
- `DownloadFolderId` must reference a `FolderType.DownloadFolder`; deleting that folder is restricted while referenced.
- Integration-owned download tasks retain isolation through `WhereIntegrationIs`, `WhereIntegrationOwnershipMatches`, and `WhereIntegrationIsOrUnowned`.
- Keep credentials out of logs, test output, skill text, and troubleshooting reports.

## Lifecycle

### Create

`POST /api/Integration/Sonarr/Configure` trims inputs, verifies the download folder, rejects duplicate name/category/URL, generates independent Torznab and qBittorrent keys, and persists `Unconfigured`.

Creation does not provision Sonarr. Setup is a separate operation.

### Test connection

`TestConnectionToSonarrCommand` accepts either:

- no integration ID plus explicit URL and API key; or
- an integration ID, optionally with both replacement URL and API key.

It calls `GET api/v3/system/status` through a 15-second `HttpClient`. Stored-credential tests update `LastConnectionTestStatus`, HTTP status, error, and UTC timestamp. Ad-hoc credential tests do not mutate the integration.

### Setup

`POST /api/Integration/Sonarr/{integrationId}/Setup` executes a strict sequence:

1. Load the tracked integration.
2. Set up the qBittorrent-compatible download client.
3. Persist `ExternalDownloadClientId`.
4. Set up the Torznab indexer linked to that download-client ID.
5. Persist `ExternalIndexerId`.
6. Test both resource payloads concurrently with `Task.WhenAll`.
7. On full success, set `Configured`, record a successful connection test, and emit `Done` progress.

Each stage emits `IntegrationSetupProgressDTO`. Cancellation remains cancellation and stops later stages. A validation failure keeps already-created external IDs but does not mark the integration configured.

Setup is idempotent. Resource lookup prefers the stored external ID, then falls back to the case-insensitive name:

- download client: `Reaparr DownloadClient`
- indexer: `Reaparr`

Existing resources are updated; missing resources are created with `forceSave=true`.

### Update

`PUT /api/Integration/Sonarr/{integrationId}/Configure` updates name, URL, API key, category, and download folder. Current behavior changes a configured integration to `ChangesPending` only when the category changes. This differs from Radarr, which also reacts to URL and API-key changes. Preserve or deliberately reconcile that contract; do not assume symmetry.

Sonarr update validation requires an absolute HTTP(S) URL without query or fragment.

### Delete

`DELETE /api/Integration/Sonarr/{integrationId}` deletes the external indexer and download client before deleting the local integration. HTTP 404 for an external resource is accepted. Without `force=true`, client creation or external cleanup failure preserves the local integration. With force, local deletion proceeds.

Sonarr cleanup accumulates errors and attempts both external deletions unless cancellation occurs. This differs from Radarr's fail-fast cleanup.

## Provisioned Sonarr Resources

### Download client

Reaparr registers as qBittorrent:

- implementation/config contract: `QBittorrent` / `QBittorrentSettings`
- protocol: torrent
- priority: 1
- remove completed and failed downloads: enabled
- category field: `tvCategory = integration.Category`
- recent and older TV priorities: 0
- API key field: `integration.QBittorrentApiKey`
- host and port: the resolved public base URI
- `useSsl`: derived from whether `ReverseProxyUrl` is HTTPS
- `urlBase`: `BasePath/api/public/integrations/{id}/download-client`

The generated callback base comes from `INetworkSettings.Url`. `NetworkSettingsModule` uses `ReverseProxyUrl + BasePath`; with no valid public URL it currently falls back to `http://localhost:5000/`. In Docker, the configured URL must be reachable from Sonarr's network namespace. Browser reachability is not evidence of container-to-container reachability.

### Indexer

Reaparr registers as Torznab:

- base URL: `{INetworkSettings.Url}/api/public/integrations/{id}/indexer`
- API path: `/api`
- API key: `integration.TorznabApiKey`
- RSS, automatic search, and interactive search: enabled
- protocol: torrent
- priority: 25
- download client ID: the provisioned Reaparr download client
- supported categories: all categories in `IntegrationDefinitions.SupportedTorznabCategories`
- Sonarr-specific fields include season age, anime format, languages, minimum seeders, failure behavior, and seeding limits

Sonarr TV categories are the 5000 range. The public identity still enforces Episode ownership when a torrent is grabbed.

## Callback Pipeline

### Torznab

Route: `/api/public/integrations/{integrationId}/indexer/api`.

`TorznabRequest.Mode` is decisive:

- `t=caps` -> capabilities
- any query type with no query, IDs, season, or episode -> RSS
- otherwise -> active search

Sonarr's `/api/v3/indexer/test` sends a queryless TV request. It therefore executes `GetTorznabRssFeedCommand`, not `SearchTvCommand`. Diagnose setup-validation latency on the RSS path first.

The RSS path must remain bounded:

1. Resolve downloadable servers and accessible libraries.
2. Read parent episode IDs in batches, ordered by `AddedAt`, `PlexServerId`, and `PlexApiRatingKey`, all descending.
3. Apply server/library/category eligibility only to each bounded episode-data batch.
4. Project and globally order at most `offset + limit + 1` candidates.

The parent index must cover those columns in the same sequence; SQLite can reverse-scan the existing ascending composite index for this all-descending query. `PlexServerId` plus `PlexApiRatingKey` is the unique Plex media identity, so a database `Id` tie-breaker is unnecessary. Predicates that force table lookups can still turn a 100-item validation feed into a multi-second scan, especially on Unraid `/mnt/user` storage and during media-overview warmup.
Use exactly `AddedAt DESC`, `PlexServerId DESC`, and `PlexApiRatingKey DESC` in the parent query, media-data query, and final in-memory merge. Do not add database IDs, machine identifiers, media IDs, or part IDs as additional RSS sort keys.

### qBittorrent compatibility

Base route: `/api/public/integrations/{integrationId}/download-client/api/v2`.

The public API supplies version/preferences/category and torrent lifecycle endpoints expected by Sonarr. `torrents/add` parses Reaparr-generated torrent metadata, verifies Episode support, creates integration-owned download tasks, and stores the torrent info hash on the created file task. `torrents/info` reports progress and exposes completed tasks with `ratio_limit=0`, allowing Sonarr to decide they can be removed.

Torrent queries include tasks owned by this integration plus unowned legacy tasks, while excluding tasks owned by another integration.

## Troubleshooting

Trace the round trip in order; the first missing boundary is the fault domain:

1. Reaparr reaches `GET /api/v3/system/status`.
2. Reaparr lists and creates/updates Sonarr's download client.
3. Sonarr reaches Reaparr's qBittorrent version, preferences, and torrent-info endpoints.
4. Reaparr lists and creates/updates Sonarr's indexer.
5. Sonarr reaches the Torznab endpoint and receives valid XML.
6. Reaparr receives successful responses from both `/api/v3/downloadclient/test` and `/api/v3/indexer/test`.

For a 15-second setup cancellation:

- Correlate the setup request and callback with `TraceId`, integration ID, request path, and timestamp.
- Confirm whether the callback entered Reaparr before changing network settings.
- If qBittorrent callbacks succeed but indexer validation stalls after Torznab entry, profile `GetTorznabRssFeedCommand`; queryless validation does not execute active TV search.
- Inspect generated SQL and `EXPLAIN QUERY PLAN`. Require a covering ordered index and bounded rows before projection.
- Check concurrent media-overview rebuilds as an amplifier, not an automatic root cause.
- Preserve the 15-second client timeout, both resource tests, and real callback validation. Fix the slow or unreachable boundary rather than increasing timeouts, retrying setup, returning fake success, or skipping one resource.

A reverse-proxy 503 means the request reached the proxy but no upstream was available. A direct callback timeout means routing/listening or application work remained incomplete. Distinguish them with callback logs rather than the setup error text.

## Known Cross-Integration Differences

Do not mechanically copy Radarr behavior into Sonarr:

- Sonarr download-client category is `tvCategory`; Radarr uses `movieCategory`.
- Sonarr accepts Episode torrents; Radarr accepts Movie torrents.
- Sonarr external cleanup accumulates failures; Radarr cleanup is fail-fast.
- `TestAllSonarrDownloadClientsAsync` requires a 2xx response; Radarr also treats `400 Bad Request` as an accepted test result.
- Sonarr setup cancellation currently emits fewer early progress updates than Radarr.
- Sonarr update marks `ChangesPending` only for category changes; Radarr includes URL and API-key changes.

When harmonizing an intentional difference, update both implementations and their observable tests in one change.

## Tests

Load `reaparr-backend-unit-tests` and `tunit` before editing backend unit tests.

Use the nearest suites under:

- `tests/UnitTests/Application.UnitTests/Integrations/Sonarr/`
- `tests/UnitTests/PublicApi.UnitTests/Indexer/Torznab/`
- `tests/UnitTests/PublicApi.UnitTests/DownloadClient/`

Protect these contracts:

- create/update uniqueness and download-folder validation
- connection status mapping and stored-state mutation
- setup stage ordering, cancellation short-circuiting, external-ID persistence, and configured state
- exact Sonarr qBittorrent and Torznab payload fields
- both resource validations execute
- queryless TV requests route to RSS
- bounded RSS ordering and pagination
- bearer/query-key authentication and integration ownership
- Episode-only torrent acceptance

Run the narrowest affected class first, then the owning test project. For callback or performance fixes, tests are insufficient: exercise the real Sonarr setup and observe both callbacks.

## Source Map

- Lifecycle: `src/Application/Integrations/Sonarr/`
- Entity: `src/Domain/Entities/SonarrIntegration.cs`
- EF configuration: `src/Data/Configurations/SonarrIntegrationConfiguration.cs`
- HTTP factory/extensions: `src/Application/Integrations/Sonarr/_Shared/`
- API command adapters: `src/Application/Integrations/Sonarr/ConfigureSonarr/API/`
- Public authentication/routes: `src/PublicAPI/_Shared/Config/FastEndpoints/`
- Torznab: `src/PublicAPI/Indexer/Torznab/`
- qBittorrent compatibility: `src/PublicAPI/DownloadClient/`
- Identity/ownership: `src/Application.Contracts/_Shared/Extensions/IntegrationIdentityExtensions.cs` and `src/Data.Contracts/Extensions/Entities/DownloadTaskExtensions/`
- Network URL source: `src/Settings.Contracts/Modules/NetworkSettingsModule.cs`
