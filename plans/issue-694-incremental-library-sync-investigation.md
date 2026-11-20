# Issue #694 — Plex incremental library synchronization

Investigation date: 2026-10-16. Issue: https://github.com/Reaparr/Reaparr/issues/694

## Scope and evidence

This issue requests investigation, a concrete integration design, and testable follow-up tasks. It does not authorize implementing an unqualified delta mechanism. No production code, schema, dependencies, or runtime behavior are changed by this investigation.

Companion evidence reports:

- [HTTP API evidence](694-plex-delta-api-evidence.md)
- [Events and alternatives](694-plex-event-alternatives.md)
- [Live API and signed-in browser evidence](694-plex-live-api-evidence.md)

Published documentation samples and live observations are separated. Authenticated tests exercised PMS 1.43.4.10903 / API 1.2.3; the older stored server was unreachable. No controlled library mutations were authorized or performed. Never interpret missing search results as proof that an undocumented endpoint does not exist.

## Decision

**A useful changed-item query exists and was reproduced live. A complete durable delta protocol was not established.** Use the ordinary section endpoint with a timestamp filter:

```text
GET /library/sections/{sectionKey}/all?type={mediaType}&updatedAt%3E%3E={lowerEpochSeconds}&X-Plex-Container-Start=0&X-Plex-Container-Size={pageSize}&sort=updatedAt:asc
```

The tested `>>` comparison is **strict**, not inclusive. Replay from before the last timestamp and deduplicate keys; do not checkpoint a page maximum and exclude unseen same-second ties. Ascending sorting above is a proposed integration choice, not the descending sort used in the live samples. Timestamp overlap mitigates boundary loss but does not prove coverage of every backdated or part/stream-only mutation.

Live evidence reduced 587 movies to 1 current item and 97,925 tracks to 12; episode filtering and filtered offset paging also worked. `addedAt` filtering worked for additions. The tested `deletedAt`, `contentChangedAt` and `changeStamp` filters instead returned ordinary current items; `/changes` and `/deleted` section routes returned 404. `/library/changestamp` exists but returns only a scalar.

**Recommended direction:** qualify and implement a hybrid: changed-item polling plus authoritative deletion reconciliation and periodic complete metadata repair. It can avoid re-reading rich metadata for every unchanged item, but it cannot honestly promise every sync is `O(changes)` while deletion/complete-mutation coverage is unresolved. Do not enable timestamp-only synchronization or silently defer deletion forever. The follow-up tasks below distinguish qualification work from implementation.

The primary-source basis is [Plex PMS API](https://developer.plex.tv/pms/), [Plex URL Commands](https://support.plex.tv/articles/201638786-plex-media-server-url-commands/), [Plex's ChangeStamp explanation](https://forums.plex.tv/t/what-information-does-the-value-in-contentchangedat-represent/935804), and [Plex Webhooks](https://support.plex.tv/articles/115002267687-webhooks/). Companion reports contain request forms, response samples and detailed citations.

## Current Reaparr integration boundaries

Source inspection and an outbound codebase-memory trace of `RefreshLibraryMediaCommandHandler.ExecuteAsync` establish these boundaries:

| Boundary | Current behavior | Consequence for a delta design |
|---|---|---|
| `src/Domain/Entities/Plex/PlexLibrary.cs:48-73` | Stores raw `ContentChangedAt`, nullable `SyncedContentChangedAt`, `SyncedAt`, section key and UUID. | Reuse these existing signals; do not reinterpret the raw changestamp as Unix time or a replay cursor. |
| `src/Application/PlexLibraries/RefreshLibraryAccess/CRUD/AddOrUpdatePlexLibrariesCommandHandler.cs:94-110` | Marks a library outdated when incoming content stamp differs from the successfully consumed stamp. | Existing coarse invalidation already avoids treating every access refresh as a content change. This does not identify changed items. |
| `src/PlexApi/PlexLibrary/GetLibraryMedia/GetLibraryMediaFromPlexApiCommandHandler.cs:35-118` | Refreshes section metadata, gets totals, retrieves media by type, maps DTOs into domain media, and assigns natural-sort indices. | Preserve type mapping and hierarchy behavior. A partial list cannot produce whole-library sort indices or totals. |
| `src/PlexApi/PlexMedia/GetAllMediaByTypeFromPlexAPI/GetAllMediaByTypeFromPlexAPICommandHandler.cs:45-86` | Gets a total and pages the entire media type into a list, updating progress and observing cancellation. | Network retrieval and client memory remain proportional to enumerated library size. Existing database incremental writes are not HTTP delta sync. |
| `src/PlexApi/PlexMedia/GetAllMediaByTypeFromPlexAPI/PlexLibraryMediaApi.cs:9-70` | Uses SDK `Content.ListContentAsync`, explicit type, offset/size, title sorting, no watched-state restriction, and GUID/meta inclusion for content pages. | A future adapter must preserve unrestricted library visibility and map complete required metadata, parts and streams. |
| `src/Application/PlexLibraries/RefreshLibraryMedia/RefreshLibraryMediaCommand.cs:53-137` | Inserts metadata, synchronizes relationships, dispatches family-specific refresh, then advances `SyncedAt` and `SyncedContentChangedAt`. | Advance an incremental checkpoint only after all corresponding required changes succeed, not after retrieval or after one family stage. Existing stages are not one library-wide atomic transaction. |
| `src/Application/PlexLibraries/RefreshLibraryMedia/CRUD/SyncPlexMoviesCommand.cs:140-165,192-239` | Compares the complete incoming key set with existing movies; absent keys are deleted. Updates are selected by `UpdatedAt`; related media rows are replaced and whole-library metrics set inside reconciliation transaction. | **Never pass a partial delta to this snapshot reconciler.** Explicit changed-key upserts must preserve untouched items and metrics; part/stream changes may require replacement even if parent `UpdatedAt` is unchanged. |
| `src/Application/PlexLibraries/RefreshLibraryMedia/ByType/RefreshPlexMovieLibraryCommand.cs:74-105` | Schedules database optimization and overview rebuild after reconciliation. | Preserve existing post-write cache/rebuild boundaries; do not publish complete progress before required work succeeds. |
| `src/Application/PlexLibraries/_BackgroundJobs/LibrarySync/LibrarySyncJob.cs:91-171` | Acquires overview coordination lease, dispatches refresh through the command executor, handles terminal queue states/cancellation, and schedules the next queued library. | Keep incremental work within this workflow; no parallel scheduler or alternate job lifecycle. |

Codebase-memory coverage checks for the core cited model, API, refresh, reconciler and job paths reported `metadata_match` with no recorded issues. This is best-effort index evidence, backed here by direct source reads, not proof of full graph completeness. Rider resolved the requested worktree; no code diagnostics/build/test is necessary for Markdown-only changes.

## Conditional integration design

The following is a design, not an assertion that Plex currently provides the required durable delta contract.

### Qualification gate

Require evidence for a server-version/library-type/authentication-scope combination before allowing incremental synchronization. It must supply either complete changes including tombstones and a resumable checkpoint, or a demonstrably complete substitute for each. A filter over surviving current items alone is insufficient for deletion correctness. Do not infer support from HTTP 200: unsupported query parameters may be ignored.

If any required guarantee is unproven, use the existing full snapshot path. Events may request an earlier sync but cannot advance a durable checkpoint merely by arriving.

### Checkpoint persistence and identity

Persist a successful checkpoint per existing server/library, bound to Plex server identity, library UUID/key, authorization visibility and protocol/version qualification. A genuine opaque token must remain opaque. A tested timestamp watermark is a different checkpoint kind, not a conversion of `ContentChangedAt`. Retain existing `SyncedContentChangedAt` for coarse freshness detection.

Generate any necessary EF migration using the repository migration tooling only after a mechanism is qualified. Do not add speculative schema fields now. Changed server/library identity, changed visibility scope, a detected counter regression or restore, or a protocol reset invalidates the baseline. A restore that retains identity and plausible timestamps may be undetectable: without a server incarnation marker, periodic complete reconciliation remains necessary.

### Retrieval and application

1. Serialize each library through the existing queue/job path. Missing checkpoint or forced refresh selects full synchronization.
2. Read the starting checkpoint and obtain a qualified upper boundary before fetching changes. Use server-issued cursor/snapshot semantics where available. A timestamp alternative needs tested server-time semantics, inclusive overlap and key deduplication; an overlap alone does not prove complete capture of late indexing or backdated updates.
3. Retrieve bounded pages, preserving cancellation and existing authenticated client/error boundaries. Enforce forward progress and reject inconsistent totals, repeated cursors and incomplete pages as failures, not successful empty deltas.
4. Resolve changed IDs to the complete metadata required by existing mappers, including hierarchy, file versions, parts and streams. Preserve server/library/rating-key scoping; a rating key is not a cross-server or restore-stable global identity. Refresh affected parents and previous parents for moves; treat merge/split identity replacement as explicit upserts and authoritative removals, never GUID-only deduplication.
5. Apply targeted idempotent upserts and explicit tombstones. Reuse DTO mapping and metadata insertion, but not full-set deletion/relationship cleanup. Unmentioned items and their relationships remain untouched. Recompute affected ordering, hierarchy metrics and overview data using existing ownership boundaries.
6. Commit the checkpoint only after all corresponding required writes succeed. Either commit changes and checkpoint atomically for bounded work, or replay idempotent bounded batches while retaining the old successful checkpoint until completion. Cancellation, page failure, enrichment failure, database failure and process interruption must leave that checkpoint unchanged. Do not hold a database transaction open during network requests.
7. Run existing post-commit overview/comparison scheduling and notifications at their established boundaries. Required scheduling failures need durable recovery through existing infrastructure, not a falsely successful checkpoint that silently loses required work.

### Deletions and recovery

- A tombstone must be authoritative for the same library and visibility scope. Authentication failure, revoked access, offline server, filtered empty result or metadata 404 alone is not proof of deletion.
- If no durable tombstones exist, an authoritative full key inventory or periodic full snapshot is still required. This is a hybrid optimization, **not** fully change-proportional synchronization.
- Missing/expired cursor, retention gap, incompatible server version, identity change, detected restore, unqualified media type or malformed pagination selects full sync without discarding the prior valid local data first.
- Full-sync fallback must fetch a complete authorized snapshot successfully before set-difference deletion. A failed fallback does not advance the old checkpoint or freshness stamp.
- Downtime beyond event coverage requires reconciliation. Restoring the same server database can invalidate rating-key assumptions even if the URL is unchanged.

## Performance model

[INFERENCE] For one media-type traversal with `N` items and page size `B`, the observed API loop uses one count request plus approximately `ceil(N/B)` content requests, excluding section/totals requests and other family traversals. It buffers `O(N)` mapped items. Database incremental writes do not eliminate this retrieval cost.

[INFERENCE] A complete delta feed with `D` changes, `R` additional detail requests and `C` protocol/control requests would cost approximately `C + ceil(D/B) + R`, with bounded page memory if the implementation does not accumulate all results. Server-side complexity is unknown: fewer returned bytes does not prove the server avoids scanning its database.

Illustration only, not a benchmark: `N=100,000`, `B=500` gives 201 requests for the existing count-plus-content traversal. With `D=100`, one control request and one complete delta page gives 2 requests (~99.0% fewer). If every changed ID requires its own detail request, 102 requests gives ~49.3% fewer. These optimistic figures do not include hierarchy fan-out, deletion inventory, reconciliation or ancillary requests; a hybrid must account for those separately. Capture actual bytes, requests, duration and server load before making performance claims.

## Concrete hybrid operating policy

1. Keep the existing full-sync baseline and `ContentChangedAt` comparison. Do not build another scheduler or treat an unchanged stamp as a guarantee about every external mutation.
2. After qualification, use per-type timestamp polling for current adds/updates, with an overlapping lower boundary and a tested upper boundary. Bind successful per-type watermarks to the library identity and access scope. All type work must succeed before the library's successful sync state advances.
3. Until a complete tombstone source is qualified, run an authoritative complete key inventory during a correctness-complete sync. If a compact inventory cannot preserve pagination and visibility guarantees, use the existing complete snapshot instead. The first live `includeFields` experiment stripped paging attributes; the corrected `includeFields=ratingKey,updatedAt,size,totalSize,offset` projection preserved them at offsets 0 and 2. That makes a compact inventory feasible on the tested build, not yet a proven snapshot under concurrent writes.
4. Compare only a successfully completed same-scope inventory against local keys before explicit deletions. If the library changes during offset paging, do not commit deletions from an unproven snapshot; retry within a bounded policy or leave the sync incomplete. A before/after stamp check is a candidate quiescence guard, not proof of an undocumented transaction contract.
5. Periodic full metadata reconciliation remains necessary until controlled tests establish part/stream, backdated update and restore coverage. Its cadence is a product decision for the implementation task, not an invented setting in this investigation. Report the hybrid's true complexity as `O(N keys + D rich items)` when inventory runs, plus periodic `O(N rich items)`.

## Follow-up tasks and acceptance tests

These are work packages, not code already implemented and not GitHub issues automatically created. No additional production features are included.

| Task | Dependencies / boundary | Required testable result |
|---|---|---|
| Q1 — Qualify timestamp retrieval | Authorized disposable PMS fixture; PlexApi boundary | Capture movies, episodes, tracks, other videos and photos on supported versions; include unsupported fields/ignored parameters, strict equality, same-second ties, multi-page sorting, empty libraries, both paging controls and upper/lower bounds. Establish actual maximum practical page sizes. |
| Q2 — Qualify mutation/deletion coverage | Q1; disposable owner-authorized library | Add; edit metadata; replace/append/remove versions, parts and streams; rename/move across parents/sections; merge/split; remove files and scan; test Trash before/after Empty Trash and direct deletion. Compare filtered results and notifications to a complete authoritative snapshot. Record missed changes rather than weakening assertions. |
| Q3 — Qualify compact inventory and recovery | Q1/Q2; HTTP API | Preserve `size`, `totalSize`, `offset` and keys when projecting fields; prove complete inventory termination, unchanged permissions and safe behavior under concurrent library mutation, access revocation, server restore/counter regression and downtime. If no safe inventory exists, select full snapshot fallback. |
| I1 — Persist successful checkpoints | Q1–Q3 establish viable protocol; Domain/Data | Generate EF migration for only required watermark/identity state. Missing/incompatible checkpoint uses full sync. Cancellation/crash/failure never advances it; same-server restore invalidation limits are documented and tested. |
| I2 — Add bounded changed-item retrieval | Q1, I1; PlexApi and contracts | Reuse existing authenticated client and DTO mapping. Test actual filtered request contract, bounded pages, cancellation, errors, ignored parameters, ties/overlap and no-progress responses. No partial response can masquerade as complete library data. |
| I3 — Implement targeted reconciliation | Q2/Q3, I2; Application/Data | Upsert changed keys without deleting untouched items or relationships. Delete only explicit authoritative missing/tombstone keys. Update complete version/part/stream state, hierarchy, affected sorting, metrics and overview dependencies. Test a one-item delta against a multi-item library, an empty delta, parent moves, merge replacement and cross-library isolation. |
| I4 — Integrate checkpoint/fallback lifecycle | I1–I3; existing job/queue | Wire through current refresh/job lifecycle; checkpoint after all required effects. Test partial-batch failure then replay, shutdown/cancellation, unavailable server, token expiry, scope change, invalid checkpoint, forced refresh, and failed full fallback with no destructive empty sync. |
| V1 — Compare against full synchronization | I4; integration fixture | After every Q2 mutation and recovery case, hybrid final state equals a fresh full sync, including deletions, media-data rows, relationships, hierarchy and metrics. Capture total requests, bytes, duration and server load for no-change/small-change/large-delta libraries. Include inventory and periodic repair costs in results. |

## Issue acceptance status

| Issue criterion | Status |
|---|---|
| Documented or reproducible mechanism / suitability verdict | Reproducible timestamp-filtered current-item query; no qualified complete durable delta/tombstone mechanism. No universal nonexistence claim. |
| Sample requests/responses for relevant versions | Published samples plus live PMS 1.43.4.10903 captures. Legacy target unreachable; other builds unverified. |
| Add/update/delete and checkpoint semantics | Timestamp boundary/paging behavior measured; deletion-feed gap and recovery policy documented. Controlled mutation semantics remain Q2, not claimed tested. |
| Authentication, headers, permissions, pagination, limits, types | Documented sources plus live shared-token movie/episode/track tests; photos/other-video live compatibility and page-size maxima unverified. |
| Concrete Reaparr design | Existing code boundaries, persistence, safe delta application, deletion inventory, recovery, full fallback and hybrid operating policy above. |
| Request/performance reduction | Live item-count reductions and conditional request estimates; one actual browser page's compressed/uncompressed lengths. No fabricated latency/load benchmark. |
| Testable follow-up work | Q1–Q3, I1–I4, V1 above, with explicit prerequisites and observable acceptance tests. |

The issue should **not** be closed as a proven, production-safe complete delta protocol or as an implemented feature. The reachable investigation/design work is recorded; multi-version and controlled-mutation qualification require suitable test targets.
