# Music, Photos, and Other Videos — phased implementation plan

Issues: [#623 — Music downloads](https://github.com/Reaparr/Reaparr/issues/623), [#625 — Photo downloads](https://github.com/Reaparr/Reaparr/issues/625). Current enum terminology comes from `src/Domain/_Shared/Enums/PlexMediaType.cs`.

## Execution rules
- **NON-NEGOTIABLE: media-type workflows MUST remain parallel in structure and isolated from each other. NEVER combine family-specific workflows.** Movie, TV, Music, Photos, and Other Videos each retain their own workflow ownership. Behavioral parity is not permission to consolidate implementations. The mandatory isolation rules below override any conflicting reuse, generic-generation, or historical wording in this plan.

- Complete backend phases first, in numbered order, with review gates preserved. **Frontend work is a completely separate deferred Phase 8:** do not edit ClientApp, generate TypeScript, or run frontend/browser checks while completing the backend. Music comparison remains the last backend phase; schema/migration generation was authorized on 2026-10-08, but the user selected a plan-only update for comparison in this task.
- At each review gate, report changed files, schema/contract effects, working behavior, diagnostics/test/smoke evidence, and remaining risks. Offer approve-next-phase, request-changes, or pause through the question tool. Record approval before advancing; request completion approval after the final phase.
- Read current source and relevant Reaparr skills before editing. Reuse existing EF, command-handler, endpoint, queue, SignalR, Pinia/RxJS, and UI conventions. Keep diffs scoped; no automatic commits or unrelated refactors.
- Implement and verify each phase's deliverables; do not hide missing behavior behind empty success, video coercion, mocks, or placeholder handlers. Update affected behavioral tests and documentation within that phase.
- If implementation exposes a necessary change to an already approved phase or an established contract, explain it and obtain review before changing the approved design.
- Phase 1 was committed by the user; Phase 2 was accepted on 2026-10-03. Root-overview and Photos work were separately approved on 2026-10-04. The user subsequently directed that completing the backend is the immediate priority and frontend work must stop. This overrides the previous frontend-first next-action recommendation, not the source-path/log-mapping exclusions or migration approval boundary.
- Do not generate database migration files, update the model snapshot, alter the committed schema/base-class hierarchy, or make commits unless explicitly requested.

### Mandatory architecture boundary — NO COMBINED MEDIA-TYPE WORKFLOWS

- **MUST:** implement Movie, TV, Music, Photos, and Other Videos as separate, recognizable family workflows. Each family owns its commands/handlers, family-specific validation, selection/expansion, mapping, reconciliation, task generation, and behavioral tests. Artist/album/track belong to Music; album/image belong to Photos. Shared original-file characteristics do not make Music, Photos, and Other Videos one workflow.
- **MUST:** follow the existing Movie or TV implementation as a reference/template, then adapt it within the appropriate family's workflow. “Other Videos follows Movies” and “Music follows TV” mean equivalent organization and behavior, **not** invoking, merging into, or renaming another family's business workflow.
- **MUST NOT:** merge families into a generic, base, parameterized, strategy-driven, or multi-family business command/handler/helper. Do not rename a family-specific command to broaden its ownership. In particular, **`GenerateDownloadTaskOriginalMediaCommand` combining Music, Photos, and Other Videos is explicitly forbidden and rejected.** Do not recreate that consolidation under another name.
- **MUST NOT:** extract family-specific business logic into shared helpers or introduce registries/factories/frameworks merely to remove similar code. Small parallel implementations are preferable to coupling independent media families. Duplication is not authorization to consolidate.
- Existing genuinely shared infrastructure and established entrypoints remain shared: HTTP/request envelopes, top-level type dispatch, result/report aggregation, queue/Quartz scheduling, byte-transfer/file-movement machinery, and generic task transport/persistence operations. They may route or carry typed tasks; **they must not absorb or replace family-owned business workflows.** Do not duplicate infrastructure into separate queue/state-machine systems.
- Ordinary application requests may mix families and dispatch to the separate family workflows. **Integration requests are not mixed-family requests:** Sonarr uses its existing TV workflow and Radarr its existing Movie workflow. Do not add mixed-integration support or a combined generator, selector, validator, or reconciliation pipeline.
- “Parallel” means sibling, independently owned workflows with equivalent applicable behavior; it does **not** authorize concurrent execution, new scheduling pipelines, or changed transaction/failure semantics.
- Any future proposal to change this boundary requires a specific user request and explicit approval **before** code changes. General compatibility, reuse, cleanup, or simplification instructions never authorize combining workflows.
- **Rejected implementation and resumed correction — 2026-10-05:** the working-tree rename/consolidation into `GenerateDownloadTaskOriginalMediaCommand` violates this boundary and is not an accepted design or completed deliverable. After this plan-only prohibition, the user explicitly directed continuation with the constraint. First restore separate Music, Photos, and Other Videos generation and preview business paths, remove the rejected combined command and migrate its callers/tests; then resume backend compatibility verification. This authorizes correcting the isolation violation, not further consolidation or reopening excluded work.


## Current decisions and remaining TODOs — 2026-10-08

The user reviewed the source-only backend audit and corrected its requirements. These decisions supersede conflicting historical progress notes and older phase requirements below.

### Confirmed contracts

- **Detail is root-only.** `GetMediaDetailByIdEndpoint` accepts Movie, TvShow, MusicArtist, PhotoAlbum, and OtherVideos. Music artist detail includes albums/tracks/original data; photo album detail includes owned images/clips/original data. Standalone MusicAlbum, MusicTrack, and PhotoImage detail requests must remain rejected.
- **Creation follows Movie/TV selection semantics.** Use the first selector for a media ID, resolve its DataId within that media's originals, then use the existing fallback when absent/stale. Do not impose stricter mandatory-choice or selector-cardinality rules on the new families. Correct actual differences from the existing pipeline within each family's handler; audio-specific preference is separate later work.
- **Destination/path work is rejected.** Keep existing destination handling and duplicate checks unchanged. The user reverted the destination-aware deduplication implementation and its tests; do not reintroduce creation-time destination resolution, final-path comparison, or destination persistence changes.
- **Keep existing download-client selection.** `DownloadJob` reselects Direct/DASH using the server setting and Plex decision. A mapper's initial Direct value is not a Direct-only policy. Direct transfer can be checked for original-byte identity; DASH output is not promised to be byte-identical. No family-specific transcode prohibition or separate queue is required.
- **Integration scope is unchanged.** Mixed Sonarr/Radarr requests are unsupported and are not a compatibility acceptance scenario. Keep existing integration guards; do not add a mixed-request protocol, transaction, or rollback behavior.
- Photo-library clips remain Photos through retrieval, persistence, preview, and creation.
- Legacy persisted `"Artist"` remains readable. Newly created or refreshed library rows must persist canonical `"MusicArtist"`; this does not authorize a live-database backfill or prove every untouched legacy row was rewritten.
- **Music comparison is required and implementation is now approved.** The user explicitly requested the core type/regression gate followed immediately by Music comparison, without another phase-approval pause. Generate the Music comparison schema/migration through EF tooling; do not apply it to a live/user database. This supersedes the earlier plan-only comparison boundary, not the frontend, pathing, audio-quality, or shared-workflow exclusions.
- **Music edition identity — confirmed:** prefer exact MusicBrainz identity; when an exact album release ID is unavailable, use conservative metadata fallback within a matched artist: normalized album title/year, compatible available edition metadata, and disc/track layout. Conflicting release IDs or edition metadata prevent a fallback match. A release-group or recording match alone must not equate different editions. Reuse existing ownership/partial/pending states; audio-quality ranking and upgrade claims remain deferred.

### TODO — current backend work

- **Destination-aware deduplication — rejected and reverted:** not an implementation TODO. Preserve the restored generators and existing duplicate behavior.
- [x] **Music creation-selector parity — corrected separately from rollback:** removed Music's extra MediaDataType predicate. Like Movie/TV, use the first selector matching MediaId, resolve its DataId only within that track's originals, and retain automatic fallback when absent/stale/foreign. No mandatory-choice/cardinality rules, duplicate logic, or destination/path changes.

### TODO — remaining work, not implemented by this annotation update

- [ ] **Music comparison:** implement artist/album/track counterparts of the current Movie/TV comparison pipeline, including hit persistence, library-pair scheduling, owned/missing/pending projection, applicable overview/detail/filter integration, invalidation, and backend regression coverage. Keep Photos/Other Videos comparison non-applicable. Resolve Music-specific identity/audio-quality rules before claiming quality-upgrade parity; do not silently import the old bespoke uncertainty/state/specification design.
- [ ] **Music quality and family-applicability filters — later:** define audio quality separately from video resolution, then align supported filters and rejection rules. Track unsupported Photo relation filters here as well; do not solve this by putting audio specifications in VideoQuality.
- [ ] **Missing originals:** review selected leaves that warn/continue into successful zero/partial reports and ancestors saved without file work. Resolve the contract explicitly without silently changing established Movie/TV batch failure semantics.
- [x] **Photo preview selector membership — verified:** reject originals whose PhotoImage is outside that merged image selection or selected albums' descendants, including an unrelated photo separately selected in another request scope. Valid multipart originals and Photo-library clips retain their grouped size, representative DataId, and Photo hierarchy/type.
- [x] **Comparison boundary validation — verified:** the existing Movie/TV-only endpoint validator and handler fallback already match the downstream scheduling guard. Expanded validator and defensive-handler tests cover MusicArtist, PhotoAlbum, and OtherVideos roots without dispatching Movie/TV commands. Production comparison behavior is unchanged; enable Music only alongside its deferred comparison implementation, not by relaxing validation first.
- [ ] **Cleanup category boundary:** preserve empty Music/Photos/OtherVideos category roots consistently with Movies/TV; current cleanup can remove empty categories, not nonempty data.
- [x] **Canonical refreshed-library persistence — verified:** the shared existing-library update branch marks Type modified so the existing converter rewrites legacy `"Artist"` to `"MusicArtist"`, even when metadata and the materialized enum are unchanged. The isolated regression proves the refreshed row becomes queryable by canonical Type and MediaCount changes from -1 to the preserved MusicArtistCount, while an unrefreshed legacy library and account access remain unchanged. No migration or live-database changes.
- [ ] **Runtime/upgrade acceptance:** exercise applicable Direct/DASH behavior, multipart completion/recovery, movement, realtime payloads, and fresh/populated upgrades. Historical checks and source coverage are not latest-worktree runtime proof.
- [ ] **Explicit exclusions:** exact source-relative paths and shared log mapping remain unchanged and unsatisfied; the separately deferred shared-workflow TODOs below remain deferred.
- [ ] **Frontend Phase 8:** resume only on its separate branch/approval after backend review; no ClientApp edits or generated TypeScript in this task.

**Verification permission:** targeted backend tests, compilation, isolated test databases/filesystems, and Music comparison migration generation are authorized for this implementation task. No live/user data or frontend checks are authorized.

**Historical verification of the reverted implementation — 2026-10-08:** the earlier generator builds/tests and creation consumer checks passed before the user reverted that work. Those results are not current-worktree verification. No restored-generator checks were rerun to confirm the user's reported revert. Transfer/recovery, realtime, and upgrade acceptance remain unexercised.

**Canonical refresh verification — 2026-10-08:** the new regression failed before the fix because the refreshed row still did not match the canonical MusicArtist SQL predicate. After the shared update fix, the complete `AddOrUpdatePlexLibrariesCommandUnitTests` class passed through dotnet-test-mcp with exit code 0; Rider reported no errors in either edited C# file and the focused build succeeded. The runner misclassifies assertion failures as ResultFileMissing and returns inconsistent pass counters; use the actual failure output and final outcome/exit code, not its counters.

**Requested core gate verification — 2026-10-08:** targeted create/preview and all ten generator selection levels, five-family mixed creation, typed DTO mapping/lookup/progress, start/pause/stop/restart, completed-key clearing, typed deletion/cascades, recovery, and buffered status/progress notifications passed through dotnet-test-mcp. Existing Movie/TV controls and Sonarr/Radarr unsupported-family boundaries passed. The hosted mixed-family HTTP scenario completed the original download/verification/movement pipeline and compared stored source/destination bytes with an external-download test double; this is isolated hosted workflow proof, not a live Plex transfer or DASH/upgrade acceptance claim.

- Fixed the shared start container branch so Music artists/albums and Photo albums queue waiting siblings while preserving one-next-task scheduling.
- Fixed completed-key selection to project the known canonical type into SQL before concatenating typed queries; the all-hierarchy lookup and five-family completed-root regressions passed. The deferred incomplete Plex-catalog retrieval bug is unchanged.
- Corrected invalid fixtures rather than changing working production behavior: fresh dependency contexts for real generator handlers, explicit parent loading, computed Other Video aggregates, first-next continuation expectations, typed IDs, and exact ancestor deletion notifications.
- Rider diagnostics reported no errors in edited code; the focused start/status-key build succeeded. The broader Application run is not claimed clean or exhaustively classified. The user denied access to its broad-run session log; that log remains unread. Previously deferred shared-workflow and cleanup/path/log exclusions remain open.

**Music selector verification — 2026-10-08:** both new regression arguments failed before the fix on the persisted media/part identity. After removing only the extra selector-type predicate, `GenerateDownloadTaskMusicTracksCommandUnitTests` passed with exit code 0, and the existing creation-consumer ancestor/leaf precedence regression passed. Coverage includes first-selector precedence, local-original membership, fallback instead of using a later selector, absent/stale selectors, multipart expansion, existing duplicate behavior, hierarchy/log identities, and library ownership. Two existing fixtures required one local context for Add/Save and a distinct multipart PartIndex; their destination scenarios and assertions were retained without changing pathing behavior. Edited-file Rider diagnostics are clean and the final focused build succeeded; runner counters remain unreliable.

**Photo preview and comparison boundary verification — 2026-10-08:** all three new out-of-selection Photo regression arguments failed before the membership checks because the handler returned success, then passed afterward through the real query validation pipeline. Valid PhotoAlbum/PhotoImage selections of a multipart clip also passed, proving that selecting a non-representative part retains the whole original group, excludes the alternate version, and stays in the Photo hierarchy. The complete `GetDownloadPreviewQueryHandlerUnitTests` and `GetMediaComparisonDetailsEndpointUnitTests` classes passed through dotnet-test-mcp with exit code 0. Rider reported no errors in all three edited C# files and the focused build succeeded; pre-existing preview-file warnings remain outside scope. Runner assertion failures are misclassified as ResultFileMissing and pass counters remain inconsistent, so these claims use actual assertion output and outcome/exit code. No destination/pathing, frontend, live-data, migration, or Music comparison implementation changes.

## Historical progress — 2026-10-04

**Audit baseline:** `feb2f31b5` plus the working tree. The earlier progress audit was read-only. The subsequent backend-first execution updated backend detail projections/endpoints, applicable browse/DTO validation, and canonical enum regression expectations; targeted backend checks are recorded below. Source presence, successful checks, and user phase acceptance remain separate facts.

**Current priority — user direction:** finish the backend first. Frontend implementation, generated contracts, conversion tests, Cypress, and browser verification are deferred together to the separate Phase 8. The reported persisted `"Artist"` failure is diagnosed but remains open: the user explicitly selected **Keep migrations excluded**. Do not normalize the live database or restore runtime aliases. Phase 3 backend detail/validation implementation and targeted regressions are ready for review; creation, lifecycle, core regression, and finally separately approved music comparison remain. No source-path/shared-log-mapping changes are authorized.

### Phase status

| Phase | Current implementation/progress | Remaining acceptance work |
|---|---|---|
| 1 — Models | User-committed catalog/task models; later approved PhotoAlbum overview and download hierarchy changes are present. | Fresh migration application was exercised; upgrade with existing photo task/log data was not. Do not change the user's `TEMP` migration or shared log inheritance as incidental cleanup. |
| 2 — Synchronization | Accepted on 2026-10-03. Current dispatch/retrieval/sync includes MusicArtist → MusicAlbum → MusicTrack, PhotoAlbum → PhotoImage, and OtherVideos. | Earlier hosted evidence used synthetic Plex responses. No new live-library or current-worktree full sync run is claimed. |
| 3 — Backend APIs/cache | All five roots have canonical queries/cache workflows. Detail now supports MusicArtist → MusicAlbum → MusicTrack, PhotoAlbum → PhotoImage, and individual OtherVideos using the existing route/DTO. Original parts and grouped version selectors are preserved; applicable browse/DTO validation is aligned. | Targeted detail, root validation, music mapping, canonical enum, and all three direct/cached family browse classes passed. Phase review remains pending. No live Plex/HTTP upgrade acceptance or full backend completion is claimed; TypeScript/frontend acceptance belongs exclusively to Phase 8. |
| 4 — Preview/create | Photos album/image expansion, original-version/part selection, validation, persistence, reports, default/override destinations, and source-part/destination deduplication exist. | Music/Other Videos remain rejected by the download validator; their generation/preview flows and the fully mixed five-family request are not implemented. Exact source-path requirements remain unmet. |
| 5 — Transfer/lifecycle | Photos use the existing Direct client, queue, file movement, progress aggregation, start/stop/pause/restart/delete/clear/recovery, and cleanup paths. | Music/Other Videos are not connected end to end. No real image/clip byte-transfer, live SignalR, or full cold-recovery smoke is recorded. Photos still rebuild album-based paths rather than preserve source-relative directories. |
| 6 — Backend core regression | Partial enum/dispatch coverage and focused photo regressions exist; Movie/TV integration restrictions remain. | Resolve the reported legacy persisted `"Artist"` value; verify backend all-family requests, upgrades, original bytes, lifecycle/recovery, and SignalR payloads. No frontend dependency in this gate. |
| 7 — Music comparison backend, last | Not implemented; Movie/TV-only comparison boundaries remain. | Start only after core backend phases and explicit schema approval. Verify identity/quality/persistence/job/API behavior without frontend implementation. |
| 8 — Frontend, completely separate and deferred | Existing partial Photos UI is preserved; Music remains placeholder UI and Other Videos is routed to Movies. | Resume only after backend completion/review. Known Photos Cypress **1 passed / 4 failed** and frontend **210 passed / 2 failed** remain recorded, not an active task. Contracts, UI, comparison presentation, and all browser/frontend tests belong here. |

### Historical source evidence — superseded by the 2026-10-08 decisions

- **Authoritative identities:** `MusicArtist = 5 → MusicAlbum = 7 → MusicTrack = 8`; `PhotoAlbum = 9 → PhotoImage = 10`; `OtherVideos = 11`. The older `Music/Album/Track/Photos` names in historical notes are superseded, not aliases to restore.
- **Photo task tree:** `DownloadTaskPhotoAlbum → DownloadTaskPhotoImage → DownloadTaskPhotoImageFile`. `IReaparrDbContext` exposes `DownloadTaskPhotoAlbums`, while preserving `DownloadTaskPhotos`, `DownloadTaskPhotoFiles`, and `DownloadTaskPhotoFileLogs` for image/file/log storage. Shared log inheritance/ID mapping was explicitly left unchanged.
- **Photo generation:** `src/Application/PlexDownloads/Generate/GenerateDownloadTaskPhotosCommand.cs` validates library/server and every original selector, expands album ownership, chooses one Plex media version and all its parts, compares resolved destinations when deduplicating, and persists recalculated retained ancestor statuses. `CreateDownloadTasksCommandHandler` dispatches Photos alongside Movie/TV; `DownloadMediaDTOValidator` still excludes Music/Other Videos.
- **Backend detail contract:** `GetMediaDetailByIdEndpoint` retains Movie/TV root detail and supports MusicArtist with nested albums/tracks/original data, PhotoAlbum with owned images/clips/original data, and individual OtherVideos. MusicAlbum, MusicTrack, and PhotoImage are descendants, not standalone detail request types. Existing DTO shapes retain typed parent identity, metadata, original parts, and grouped version selectors; no new detail routes are required.
- **Photo path deviation:** `PlexMediaExtensions.Photo.cs` uses `OriginalFilename.GetFileName()` and `photoAlbum.Title.SanitizeFolderName()`. `DownloadTaskFileBase` places files at `<download-root>/Photos/<album>/<basename>` and `<destination-root>/<album>/<basename>`. Source-relative directories are not preserved. The user explicitly left this behavior unchanged after discussing the requirement; do not implement path/schema changes without renewed approval, and do not mark the exact-path invariant satisfied.
- **Deferred frontend boundary:** the attempted `bun run generate-ts` consumed Swagger from the wrong Rider/worktree instance and did not provide the current photo enum/task/report contract. Those generated-file changes are left as found, not reverted or declared correct. Frontend checks still recorded 210 passed / 2 failed and Photos Cypress 1 passed / 4 failed. The user then started the correct Rider instance and explicitly paused all frontend work. Do not regenerate, hand-edit, or verify ClientApp until the separate Phase 8 is resumed.
- **Remaining frontend coverage:** `ServerDrawer.vue` routes OtherVideos to `/movies/:id`; music pages are unsupported alerts. `downloadStore.getActiveDownloadList` traverses a fixed TV-depth tree, unlike the recursive queue lookup/rendering. Trace its consumers before changing it; it is not proof of the Cypress title failure. Photo poster comparison rendering is now gated to Movie/TV, but all table/filter/view-mode and metadata paths still need family-applicability acceptance.
- **Shared parity limits:** explicit-library browse checks enabled/type but bypasses the all-library account/owned/offline predicates (`MediaOverviewExtensions.ResolveAllowedLibraryIdsAsync`). The existing rebuild coordinator suppresses requests during a 15-minute cooldown; family snapshot fallback tests whether *any* allowed-library snapshot exists. These are source-observed acceptance risks, not newly reproduced failures. Verify current Movie/TV parity and cached/direct behavior before proposing a shared access/cache-policy change; no new cache pipeline or speculative state is authorized.
- **Persisted enum blocker — intentionally unresolved:** EF `PlexLibrary.Type`, `FolderPath.MediaType`, and `PlexComparisonState.MediaType` use the same strict `ToPlexMediaType` converter. `"Artist"` is rejected, causing the user-reported library-access refresh/database-read failure. A data cutover was proposed; the user selected **Keep migrations excluded**. No migration, alias, or live database mutation was made. Canonical name regression expectations were corrected, not the stored legacy data. Existing populated upgrade/library-refresh acceptance remains blocked until that exclusion is reopened.

### Executed evidence and limits

- **Earlier photo implementation:** 21 targeted backend test classes and three photo-log methods passed. Coverage included preview/create, persistence, lifecycle, aggregation, cleanup, and one Direct-download → move → completion regression. These results predate later fixes; they are not a latest-worktree full-suite pass.
- **Transfer evidence ceiling:** `tests/UnitTests/Application.UnitTests/PlexDownloads/Execute/DownloadJob.Photo.UnitTests.cs` runs real handlers with mocked transport/filesystem and four known bytes. It proves controlled lifecycle/movement behavior, not real Plex image/clip HTTP transfer or a browser SignalR session.
- **Later approved review fixes:** imports, retained ancestor statuses, multipart download/move resume, resolved-destination deduplication, mixed-preview originals, selector validation, and comparison guards were implemented. The recorded Rider build, three backend regression classes, and mixed-preview component test passed. Source paths were intentionally not changed.
- **Latest frontend suite:** 210 passed / 2 failed in unchanged photo-type conversion tests. Broader lint also reported untouched style errors. Neither result is a clean frontend acceptance gate.
- **Latest Photos browser run:** the fresh build ran after the standard Cypress port became available. The spec's response-body/Quasar-targeting fixes were applied; **1 passed / 4 failed** remained. The no-comparison-badge workflow passed; three workflows failed because photo-type fields were missing from requests, and one failed because the desktop queue title was not visible. No production fix for those four failures is recorded.
- **Migration:** `20261004192353_AddPhotoDownloadTaskHierarchy` applied after `20261004183754_TEMP` to a fresh isolated SQLite database; no live/user database was changed. The new required photo-log album FK uses an empty-GUID default without an explicit data backfill. An upgrade populated with existing photo logs is unverified; test that separately before claiming upgrade compatibility.
- **Permissions:** earlier isolated-directory/`env *` denials are historical prerequisites for those exact operations, not a current blanket Cypress blocker. The later standard-port run executed and failed as above. Do not retry a denied override through another tool/wrapper; request its narrow authorization only if it becomes necessary.
- **Current backend-first verification:** `dotnet-test-mcp` executed ten targeted classes with `Outcome: Passed` and exit code 0: `GetMediaDetailByIdEndpointUnitTests` (including existing Movie/TV behavior, new family hierarchy/parts, unsupported types, and missing-item 404s), `GetAllMediaByTypeEndpointUnitTests`, `PlexMediaDTOMapperPlexMusicUnitTests`, `PlexMediaTypeMappersUnitTests`, the three `GetMediaOverview{Music,Photo,OtherVideo}CommandUnitTests`, and the three `GetMediaByTypeCommandHandler{Music,PhotoAlbum,OtherVideo}UnitTests`. New tests exercised actual isolated SQLite reads and persisted multipart originals through the endpoint harness. Changed endpoint/test diagnostics returned `OK`. Runner aggregate counters/duration are inconsistent and were reported as a tool issue; no summed case count is claimed.
- **Current verification limits:** no new hosted HTTP/Plex transfer, existing-populated upgrade, full-solution/full-backend-suite, or frontend check was run. The reported legacy `"Artist"` failure was accepted as ground truth, not rerun to reproduce it. Canonical enum tests do not prove a legacy-data upgrade; the migration exclusion leaves that error unresolved. Source paths and shared log inheritance/mapping remain unchanged.

### Historical next-work list — use the current TODO section instead

1. **Persisted media-type upgrade failure — blocked by explicit exclusion.** Shared converter/persistence roles are traced; the user selected **Keep migrations excluded**. Preserve strict canonical parsing and existing data/migrations. A separately approved data migration/live application and populated upgrade/read verification are required to close this blocker; never claim that corrected canonical-name tests fixed legacy rows.
2. **Phase 3 backend implementation — historical review snapshot.** Existing detail API/DTO shapes expose root MusicArtist and PhotoAlbum detail with nested descendants and individual OtherVideos. Standalone descendant detail is not required. Earlier targeted backend classes passed; those results do not prove current-worktree filter validation or runtime acceptance. Leave inherited shared cache/access policies and explicit path exclusions unchanged.
3. **Finish Phases 4–5 backend creation and lifecycle.** Other Videos follows Movie orchestration; Music follows TV-style container expansion with original tracks. Extend existing validators, preview/create, task persistence, queue, movement, lifecycle, recovery, reports, and SignalR mappings. Verify all selection levels, overlap/resolved-destination deduplication, unresolved originals, mixed five-family requests, and original bytes. Existing source-path behavior/shared log mapping remain excluded unless reopened explicitly.
4. **Close Phase 6 backend regression, then Phase 7 music comparison backend.** Exercise real image/clip/audio/video originals, multipart interruption/recovery, movement, backend progress payloads, and populated upgrades. Run affected backend tests and actual backend scenarios; record remaining approved exclusions rather than claiming them satisfied. Music comparison remains last and needs its own schema approval.
5. **Only after backend completion, resume separate Phase 8 frontend.** Generate contracts from the verified current backend; resolve the two conversion failures/four Photos Cypress failures and finish Music/Other Videos routes, selectors, destinations, queue, and comparison presentation. Preserve request shapes and source provenance. No frontend work or checks are authorized during the current backend-first execution.

### Outstanding shared-workflow TODOs — explicitly deferred

The user selected **Only extend media-type support** during the full-backend parity audit. Preserve the existing Movie/TV semantics below; these are not fixed by adding Music, Photos, or Other Videos. Implement only after separate approval.

- [ ] **Genuine partial-move resume:** `MoveDownloadFileFromFileTaskCommandHandler.ExecuteAsync` deletes an existing destination and resets transfer offsets before calling the resumable mover. Distinguish a same-task partial destination from an unrelated target; preserve fresh-transfer overwrite behavior and verify retained prefix bytes, persisted offsets, cancellation, and retry.
- [ ] **Incomplete Plex retrieval safety:** `GetAllMediaByTypeFromPlexApiCommandHandler` converts missing count metadata to zero and missing page metadata to an empty list. Verify malformed successful responses cannot become authoritative empty catalogs and delete existing synchronized data; retain legitimately empty-library behavior.
- [ ] **Unsupported sync-queue eligibility:** `QueueLibrarySyncJobCommandHandler` admits every requested library type. Restrict queue insertion/reset to Movie, TvShow, MusicArtist, PhotoAlbum, and OtherVideos, with controls proving None/Unknown/Games remain untouched.
- [ ] **Movement job outcome reporting:** `MoveDownloadFileJob.Execute` returns from its inner task on cancelled/failed movement without recording the corresponding Quartz job outcome. Verify job outcome, durable MovePaused/MoveError state, next-queue behavior, and forbidden completion/cleanup remain consistent.


## Required behavior

| Family | Browse/select | Download | Cross-library comparison |
|---|---|---|---|
| Movies / TV Shows | Preserve existing workflows and hierarchy | Preserve existing quality selection, naming, creation/error rules, and task lifecycle | Preserve existing behavior |
| Music | Artists → album editions → tracks; select any level | Expand containers to original tracks | Implement in the final phase only |
| Photos | Albums → owned photos; album-root overview | Expand album/photo selections to original file rows | Not applicable: no jobs, badges, controls, or filters |
| Other Videos | Individual-video overview, search, selection; no folder explorer/subtree selection | Original videos | Not applicable: no jobs, badges, controls, or filters |

### Invariants

- Select one source original version per asset and every required file part through the existing creation pipeline. Keep established Direct/DASH client selection, including the server setting and Plex decision. Original-byte/embedded-metadata identity applies to Direct transfer, not as a guarantee for DASH/transcoded output.
- Match existing Movie/TV first-selector/DataId lookup and fallback semantics; preview may expose alternative originals. Neither missing selectors nor stale DataIds introduce a new mandatory-choice rejection policy. Music-specific audio preference/quality is explicitly later work.
- Mirror exact source-relative folders and filenames beneath the resolved destination. Reject invalid/unavailable paths and target-incompatible names through existing error handling; do not sanitize, rebuild metadata names, or add server/library/root namespaces. Photo albums select their owned photos rather than create duplicate output directories; source directory paths remain file metadata.
  - **Current deviation:** the implemented Photos mapper strips source directories and sanitizes the album title. The user left that behavior unchanged; this invariant is still open, not silently relaxed.
- Preserve automatic replacement of unrelated existing targets and genuine same-task resume as distinct behaviors.
- Allow requests mixing all five families. Resolve family from the source library: a video clip in a photo library stays Photos. Deduplicate overlapping selections by source file part plus resolved destination; do not omit families.
- Preserve the `CreateDownloadTasksRequest` envelope, existing destination default/shared-override resolution, and established creation/error/report rules. Reuse `DownloadMediaDTO.MediaIds` and `Qualities` entries' `MediaId`/`DataId` selectors; do not add per-group destination fields, a replacement request, atomic-batch semantics, or a new partial-result protocol. Do not encode audio/photo specifications in `VideoQuality`.
- Keep source IDs and Plex wire types distinct from domain enum values. Current backend hierarchy: `MusicArtist = 5 → MusicAlbum = 7 → MusicTrack = 8` and `PhotoAlbum = 9 → PhotoImage = 10`. Plex wire strings remain separate mappings. `None`, `Unknown`, and `Games` are not supported downloadable sources.
- Sonarr/Radarr, Torznab, and integration-specific download clients remain movie/TV-only. Sonarr follows the existing TV request workflow and Radarr the Movie workflow; mixed integration requests are unsupported, not a required acceptance scenario. No Lidarr or new indexer integration.

## Phase 1 — Database tables and EF models — completed

**Status:** the initial models were completed and committed by the user. Current source also includes the separately approved PhotoAlbum overview/task changes below; the initial committed model is not the complete current storage contract. No additional schema work is authorized by this progress update.

### Committed catalog and file-data hierarchy

| Family | CLR hierarchy | Existing DbSets |
|---|---|---|
| Music | `PlexMusicArtist → PlexMusicAlbum → PlexMusicTrack → PlexMusicTrackMediaData` | `PlexArtists`, `PlexAlbums`, `PlexTracks`, `PlexTrackData` |
| Photos | `PlexPhotoAlbum → PlexPhotoImage → PlexPhotoMediaData` | `PlexPhotoAlbums`, `PlexPhotoImages`, `PlexPhotoData` |
| Other Videos | `PlexOtherVideo → PlexOtherVideoMediaData` | `PlexOtherVideos`, `PlexOtherVideoData` |

- Music, individual photos, and Other Videos reuse `BasePlexMedia`; photo albums originally reused `BaseEntity`. The separately approved 2026-10-04 root-overview correction aligns `PlexPhotoAlbum` with `BasePlexMedia`. All three file-data types reuse `BasePlexMediaData`; no additional parent abstraction is introduced.
- Album-to-artist, track-to-album, photo-to-album, and file-to-media relationships use required internal Reaparr IDs. Resolve Plex parent rating keys to those IDs within the library; do not store Plex rating keys directly as database FKs.
- Catalog identities are unique by `(PlexLibraryId, PlexApiRatingKey)`. File rows are unique by `(PlexLibraryId, PlexApiPartId)`. Parent/library/server deletion cascades through owned catalog and file rows.
- Every original `Media/Part` is represented by a flat file-data row carrying Plex rating/media/part IDs, original filename, original part key, size, container, duration, and the retained codec fields. Music and Other Video rows additionally have `PartIndex` and optional source-path/video metadata. Photo rows have dimensions but no `PartIndex` or source-path fields; do not invent those columns.
- Photos have one owning album, with album-scoped photo sorting. No photo folders, generic assets, membership joins, standalone versions/parts, source-root tables, or stream tables are part of the committed model.
- Music keeps its nullable MusicBrainz/release/track metadata. Populate it only from identifiable source evidence; the current file-data model does not persist separate audio-stream specification rows.

### Counts, snapshots, and task storage

- Library counters now cover artists/albums/tracks, photo albums/photos/clips, and Other Videos. Computed `MediaCount` uses `ArtistCount` for Music, `PhotoAlbumCount` for Photos, and `OtherVideoCount` for Other Videos; existing movie/TV branches remain unchanged.
- The root overview entities are `MediaOverviewMusicArtistSnapshot`, `MediaOverviewPhotoAlbumSnapshot`, and `MediaOverviewOtherVideoSnapshot`. They share `BaseMediaOverviewSnapshot` and `MediaOverviewSnapshotConfigurationBase<TSnapshot>` while remaining independent EF entities with typed source relationships. No folder or leaf-photo snapshots remain.
- Music-artist, photo-album, and Other Video snapshots are exposed by the context and its interface. Other Video snapshots also persist `QualityRank` for Movie-equivalent quality sorting.
- Download-task/file/log storage retains the established base classes. Track, Photo Image, and Other Video file tasks retain `DownloadTaskFileBase`. Photos now have an album → image → file task tree; Music/Other Videos models alone do not establish implemented creation/lifecycle workflows.
- Photo ownership registrations/navigation properties and the shared library faker were updated to remove obsolete folder/asset references.

**Migration boundary:** Phase 2 did not authorize migration generation. Later explicit photo approvals produced the current migration chain, including `20261004183754_TEMP` and `20261004192353_AddPhotoDownloadTaskHierarchy`, plus the model snapshot. Fresh isolated application passed; populated upgrade and live provisioning are not proven. Preserve the user's migration and never silently alter a real user database.

**Evidence boundary:** obsolete pre-refactor migration, staged-file, removed-test, and five-snapshot notes have been removed from the active plan. They are not current verification claims. No checks are being run for this planning update.

**Next:** use the current-progress snapshot for the next action. Phase 2 remains accepted; Phase 3 and Photos runtime/frontend acceptance are only partially closed.

## Phase 2 — Movie/TV-parity synchronization — completed

**Status:** completed and accepted by user review on 2026-10-03. The user rejected the audit's retrieval and queue-eligibility findings; only the requested shared failed-sync-history cancellation correction was applied. The implementation and verification notes below are historical evidence except for the explicitly dated post-review correction. Hosted acceptance uses synthetic Plex responses and isolated SQLite, not a live Photos library. The separately approved root-overview correction is recorded under Phase 3.

### Governing decision: follow the existing workflows exactly

- Other Videos follows the Movie workflow. Music follows the TV hierarchy workflow. Photos follows the existing hierarchical workflow, adapted to album → photo.
- Use the same command/validator/handler organization, reconciliation approach, bulk-operation patterns, transaction boundaries, reporting, cancellation handling, optimization scheduling, progress updates, and library reload/stamp ownership.
- Preserve the top-level metadata-before-family sequence. **Do not introduce stronger cross-phase atomicity or redesign the shared metadata transactions.** A later descendant failure can leave metadata writes committed under the existing workflow; do not claim full-pipeline rollback.
- Normal incremental synchronization preserves matching catalog IDs. **`ForceMediaRefresh` follows the existing Movie/TV delete-and-rebuild behavior**, with reports reflecting actual replacement work.
- Movie/TV implementations are reference code, not refactoring targets. Necessary shared Phase 2 wiring fixes must preserve their behavior.
- These decisions supersede older suggestions of stronger transaction guarantees, generic reconciliation frameworks, new base classes, fetching layers, or extra validation frameworks.

### Scope and frozen boundaries

- Load `reaparr-backend` first, then `reaparr-command-handler-patterns` and the relevant backend testing skills. Follow repository tooling and conventions.
- Review staged and unstaged work, but change only Phase 2 synchronization, its direct dependencies, and affected tests/existing documentation. Preserve unrelated user changes; do not restore a deleted experimental implementation wholesale.
- Do not change the committed schema, entity hierarchy, or base classes. Do not generate, remove, regenerate, rename, edit, or apply migrations or model snapshots. Do not alter a real user database to make verification pass.
- The user subsequently approved upgrading `LukeHagar.PlexAPI.SDK` to **0.21.0** to use its native numeric `ListContentRequest.MediaType` property and remove the custom transport option. This supersedes the earlier 0.20.7 freeze. SDK representations and numeric Plex metadata IDs remain distinct.
- Do not restore `Get*LibrarySnapshotCommand` layers, enrichment/fallback fetches, repeated section/timestamp checks, or duplicate metadata-response construction.
- Do not restore the rejected normalized DTO additions: `RawType`, `Subtype`, `GrandparentRatingKey`, `SourceYear`, `SourceDuration`, `SourceIndex`, `SourceParentIndex`, or `SourceChildCount`, including nested media `SourceDuration`. Reuse the existing fields and their units/unknown-value conventions.
- Never reintroduce `TrackMediaVersionCount`, `TrackFilePartCount`, `PhotoMediaVersionCount`, `PhotoFilePartCount`, `OtherVideoMediaVersionCount`, or `OtherVideoFilePartCount`.
- No Phase 3 browse/cache/frontend work, download functionality, comparisons, speculative source-path ownership/preservation policies, unrelated formatting, or refactoring.
- Do not commit, stage, reset, stash, or switch branches.
- The user's latest correction authorizes the backend enum hierarchy below, limited to currently unstaged changes; stop afterward for review.

### 1. Read the canonical implementations

Under `src/Application/PlexLibraries/RefreshLibraryMedia/`, inspect:

- `CRUD/SyncPlexMoviesCommand.cs`
- `CRUD/SyncPlexTvShowsCommand.cs`
- `CRUD/InsertMediaMetaDataCommand.cs`
- `CRUD/SyncPlexLibraryMediaMetaDataCommand.cs`
- `ByType/RefreshPlexMovieLibraryCommand.cs`
- `ByType/RefreshPlexTvShowLibraryCommand.cs`
- `RefreshLibraryMediaCommand.cs`

Also inspect their existing tests, `src/Data.Contracts/Extensions/DbContext/DbContextExtensions.PlexLibrary.cs`, and the current Music, Photos, and Other Videos entities, EF configurations, mappers, and ByType handlers. Adapt only for their different types and committed relationships.

### 2. Implement the missing CRUD commands

Create the command, validator, and handler implementations in the existing `CRUD/` directory:

| Command | Existing workflow to follow | Committed hierarchy |
|---|---|---|
| `SyncPlexMusicCommand` | TV hierarchy reconciliation | `PlexMusicArtist → PlexMusicAlbum → PlexMusicTrack → PlexMusicTrackMediaData` |
| `SyncPlexPhotosCommand` | Hierarchical reconciliation, reduced to albums/photos | `PlexPhotoAlbum → PlexPhoto → PlexPhotoMediaData` |
| `SyncPlexOtherVideosCommand` | Movie reconciliation | `PlexOtherVideo → PlexOtherVideoMediaData` |

All three retain the common input shape: `(InsertMediaMetaDataCommandResponse LibraryMetadata, bool ForceMediaRefresh = false)`.

- Follow existing per-family report conventions. The ByType callers consume `ChangedItemCount`; compute it from actual created, updated, and deleted logical catalog rows, not media-version/file-part totals.
- Scope reads and mutations to the target library. Match logical entities by their existing Plex rating-key identity within that library.
- Preserve matching database IDs during normal incremental synchronization. Follow existing timestamp/change-detection and parent-move handling.
- Resolve real database parent IDs before writing children. Reparent surviving children before deleting obsolete parents with cascading relationships.
- Preserve unchanged media-data rows and replace changed rows according to the existing Movie/TV approach. Do not invent a separate file-reconciliation subsystem.
- Persist all mapped original media-data rows, not an arbitrarily selected version or part. Use only fields and relationships in the committed models; do not invent video-quality tables or media relations for Music, Photos, or Other Videos.
- Use existing bulk helpers and transaction patterns. Force refresh rebuilds the selected library's tree and reports the actual created/deleted work.
- Reuse `SetMusicMediaMetrics`, `SetPhotoMediaMetrics`, and `SetOtherVideoMediaMetrics` for logical counts and `MediaSize`.
- Keep synchronization stamps in the existing outer successful-refresh path, not the CRUD commands.

### 3. Complete the existing ByType integration

Complete `RefreshPlexMusicLibraryCommand.cs`, `RefreshPlexPhotoLibraryCommand.cs`, and `RefreshPlexOtherVideoLibraryCommand.cs`; do not redesign them.

- Music album/track and Photos leaf retrieval use the existing `GetLibraryMediaFromPlexApiCommand` contract with an explicit requested media type. Roots and descendants share one retrieval, sorting and typed-mapping path; there is no separate descendant orchestration method. Application retains hierarchy assembly and reconciliation. Other Videos uses its existing root catalog directly.
- Per the latest user decision, remove the newly introduced per-media-type retrieval commands, handlers, and photo response wrapper. Do not replace them with another retrieval layer.
- Application references `PlexApi.Contracts`, not the PlexApi implementation. All normalized media, metadata and filename mappers remain in `PlexApi`; actor, genre and country metadata crosses the contract as domain entities.
- Preserve the original shared metadata response instance and existing optimization, progress, logging, and reload sequencing.
- **Settled photo contract:** `LibraryMetadata.PhotoClipCount` carries the actual retrieved clip count from PlexApi. Assign it to the same `InsertMediaMetaDataCommandResponse` instance before calling `SyncPlexPhotosCommand`. Do not add a separate clip-count command argument or construct a replacement shared response.
- `PhotoCount` counts still photos only. Clips are additional and counted in `PhotoClipCount`; when the incoming photo collection contains both, the still-photo count excludes those clips.
- Retrieve required descendants even when roots are empty. Failed or cancelled requests must not invoke destructive family reconciliation. Successful empty catalogs follow the existing empty-library reconciliation behavior. Trust Plex response data; do not add pagination/header consistency validation.
- Default root retrieval remains root-only: preserve section refresh, local identity/configuration, natural sorting, `SortIndex`, typed root mapping, and metadata projection. Explicit descendant requests retrieve only the requested type, preserve existing parents, and do not refresh sections or restart progress. Do not pass a root-only hierarchy into full reconciliation as a complete catalog.

### 4. Finish shared Phase 2 wiring

#### Plex request typing

Inspect the existing generic count/page reader, client options/transport, media-type mappings, and `MockPlexApiServer`.

- Ensure count and page requests consistently request the intended concrete Plex item type. Keep library family, item kind, domain enum value, SDK representation, and numeric Plex metadata ID distinct.
- Preserve artist roots for Music, photo-album roots for Photos, and movie-type Plex items classified as Other Videos by their source library. Keep section classification at its existing boundary.
- Use actual Plex metadata query IDs, not domain enum ordinals: Movie=1, TV=2, Season=3, Episode=4, Artist=8, Album=9, Track=10, Photo=13, PhotoAlbum=14; Other Videos requests movie type 1. Other Videos query 1 is not an inverse mapping for clip type 12.
- Use SDK 0.21.0's native numeric `ListContentRequest.MediaType` on count and page requests. The prior 0.20.7 query enum cannot serialize casts to missing numeric values; it is not stale. Do not inject the type through client options or rewrite request URLs.
- Make the smallest correction at the existing request/client boundary. Preserve sorting, paging, authentication/query encoding, and the shared Movie/TV retrieval flow. Update affected callers and fixtures consistently; do not replace the pager, add another fetching abstraction, or restore the rejected duplicate-page guard/test.
- Historical helpers/options are not proof of current implementation. Inspect current source instead of blindly restoring names from an older plan.

Reference sources: [0.21.0 request](https://github.com/LukasParke/plexcsharp/blob/125af68293f87d20ee53e1f0479f1b5b91fb2ae9/LukeHagar/PlexAPI/SDK/Models/Requests/ListContentRequest.cs), [prior 0.20.7 request](https://github.com/LukasParke/plexcsharp/blob/652cfd239c52975d5224d3912b361043b5f79895/LukeHagar/PlexAPI/SDK/Models/Requests/ListContentRequest.cs), [prior query model](https://github.com/LukasParke/plexcsharp/blob/652cfd239c52975d5224d3912b361043b5f79895/LukeHagar/PlexAPI/SDK/Models/Components/MediaQuery.cs), [SDK enum](https://github.com/LukasParke/plexcsharp/blob/652cfd239c52975d5224d3912b361043b5f79895/LukeHagar/PlexAPI/SDK/Models/Components/MediaType.cs), [Plex metadata IDs](https://github.com/LukasParke/plex-api-spec/blob/c7b1e83df190c2bd1ce5678990d295f00a373fca/description.md).

#### Queue eligibility

In the existing `QueueLibrarySyncJobCommand` handler, restrict work to enabled, supported library families: Movie, TvShow, Music, Photos, and OtherVideos.

Apply the eligible-ID set before selecting/resetting existing queue entries, applying force-refresh changes, or inserting new entries. Preserve existing priorities, queue states, Quartz jobs, and scheduling; do not create family-specific queue systems.

#### Progress

Correct the existing application and SignalR calculations consistently:

- `Total = -1` means unknown and remains incomplete.
- Unknown totals do not make aggregate totals negative.
- Confirmed `Total = 0` can complete; an unknown zero aggregate must not be mistaken for completion.
- Overall completion still requires every expected item to complete.
- Preserve the existing progress protocol and family item sets.

Relevant files: `src/Application.Contracts/_Shared/DTO/LibraryProgress.cs`, `LibraryProgressItem.cs`, `src/SignalR.Contracts/DTO/LibrarySyncProgressDTO.cs`, and the existing `LibrarySyncProgressStore`.

#### Disable/re-enable

Preserve the existing selected-library purge, metric/stamp reset, queue cancellation/history, notifications, and fresh synchronization on re-enable. Keep overview rebuilding **Movie/TV-only** in `SetLibraryEnabledEndpoint`; do not implement Music, Photos, or Other Videos overview/cache support here.

### 5. Clean up the affected implementation only

Remove obsolete Phase 2 references, dead arguments, duplicate work, and stale tests/documentation resulting from this implementation. Keep all three workflows recognizably equivalent to their Movie/TV counterparts. Do not use historical successful-run notes as current verification or make unrelated formatting/refactoring changes.

### 6. Verification and completion

Follow repository diagnostics and test tooling. Use existing fake-data builders and isolated test databases.

Cover initial population; repeated unchanged synchronization; updates/deletions; parent moves and cascade-safe deletion; library/server isolation; original media-data persistence and unchanged-row behavior; force rebuild and accurate reports; empty catalogs; failed/cancelled requests skipping reconciliation; logical counts, photo clip counts, and media sizes; queue eligibility; progress; disable/re-enable; and Movie/TV regressions in affected shared paths.

Run affected unit tests and targeted integration coverage. Exercise a hosted/smoke refresh through the existing pipeline into SQLite, including disable/re-enable. Use the completed implementation, not stale production assemblies or mocked persistence. Label synthetic Plex fixtures as synthetic; they are not live Photos acceptance.

Do not alter real user data or generate migrations to make verification pass. Do not broaden scope to fix unrelated failures. Report changed files and their workflow equivalents, checks actually executed with results, and exact remaining blockers. Compiling three command types alone is not Phase 2 completion.

**Historical evidence status:** Rider solution build succeeded with no reported problems; edited-code error diagnostics were clean. Separate per-command unit classes passed. After the approved SDK upgrade, the complete Plex API unit-test project passed, and the full integration project exited with code `0`; its generated TUnit HTML report confirmed **45 passed**. The test MCP misparsed aggregate counts/durations and reported a false failed outcome for that successful integration run; the generated report was inspected directly.

#### Historical implementation and verification

| Requirement | Implementation | Executed proof |
|---|---|---|
| Movie/TV workflow parity | `SyncPlexOtherVideosCommand`, `SyncPlexPhotosCommand`, and `SyncPlexMusicCommand` use the existing family refresh sequence, logical CRUD reports, metadata-before-family ordering, and outer stamp ownership. | Music, Photos, and Other Videos CRUD classes, `SyncPlexMoviesCommandHandlerUnitTests`, `SyncPlexTvShowsCommandUnitTests`, and hosted refresh classes passed. |
| Music synchronization | Artist → album → track reconciliation preserves matching IDs and unchanged originals, supports parent moves, writes every original, updates metrics, and rebuilds on force. | `SyncPlexMusicCommandUnitTests`: initial, unchanged, update/delete, album move, track move, force, empty; selected-library/server isolation. |
| Photos synchronization | Album → photo reconciliation preserves originals and handles moves/cascades. Still counts exclude clips; the same metadata response carries the actual clip count. | `SyncPlexPhotosCommandUnitTests`, `GetLibraryMediaFromPlexApiCommandHandlerUnitTests`, and `RefreshPlexPhotoLibraryCommandUnitTests`: reconciliation, original mapping, typed hierarchy assembly and clip accounting. |
| Other Videos synchronization | Movie-equivalent reconciliation with stable matching IDs, changed-original replacement, logical reports, metrics, force rebuild, and empty-catalog deletion. | `SyncPlexOtherVideosCommandUnitTests`: initial, unchanged, update/delete, force, empty, multiple originals and isolation. |
| Retrieval | Music and Photos receive typed descendants through the existing PlexApi contracts boundary and shared retrieval/mapping path. Application stops before reconciliation on request failure or cancellation. Plex response data is trusted; original total-count default/clamping and fixed-stride paging are restored. | PlexApi tests cover sorting, originals, preserved collections on request failure/cancellation and normal paging/empty results. Refresh tests cover forbidden reconciliation and typed collection aliasing. |
| Request typing | User-approved SDK `0.21.0`; count and page requests use native numeric `MediaType`, numeric `SectionId`, and `Sort`, preserving paging and authentication without URL rewriting. Root reads remain Artist/PhotoAlbum only. | Complete Plex API unit and hosted integration suites passed, including Movie, TV, Music, Photos, and Other Videos refreshes. |
| Queue eligibility | Enabled supported IDs are selected before queue/reset/force mutations. | `QueueLibrarySyncJobCommandHandlerUnitTests` passed. |
| Progress | Expected types start at unknown `-1`; unknown totals remain incomplete, confirmed zero completes, and aggregate completion requires every expected item. | `LibrarySyncProgressStoreUnitTests` passed, including app/SignalR progress behavior. |
| Disable/re-enable | Existing selected-family purge/reset/cancellation/history/notification lifecycle is retained; overview rebuilds remain Movie/TV-only. | `SetLibraryEnabledEndpointUnitTests` passed. Hosted Photos disable purged selected data/reset state without changing Music/Other Videos; re-enable rebuilt the catalog. |
| Frozen boundaries | No agent changes to migrations, model snapshot, entities/base classes or Phase 3 functionality. The SDK upgrade was explicitly approved; unrelated user changes remain untouched. | Builds and tests used the committed model shape; no migration generation, real-user database mutation or git mutation was performed. |
| Hosted pipeline | Current AppHost endpoints → queue/Quartz → synthetic Plex HTTP → real SQLite persistence, followed by idempotent refresh and Photos disable/re-enable. | Separate `RefreshPlexMusicLibraryCommandIntegrationTests`, `RefreshPlexPhotoLibraryCommandIntegrationTests`, and `RefreshPlexOtherVideoLibraryCommandIntegrationTests` passed individually and in the full integration project, alongside `RefreshLibraryMediaEndpointIntegrationTests`. |

#### Final cutover and implementation notes

- Removed the three new album, track and photo descendant command/handler pairs and the photo retrieval response wrapper. Reused `GetLibraryMediaFromPlexApiCommand` with an optional media type instead of introducing another retrieval layer. Default calls remain root-only. Explicit Album/Song/Photos calls return typed collections through `LibraryMetadata`; Application assembles the trees without clearing an aliased result collection. Unit and integration tests retain one file/class per SUT.
- All normalized `PlexMediaDataMapper` partials, `LibraryMediaItemMappers`, and `MediaFileNameExtensions` remain in `PlexApi` under `Reaparr.PlexApi`. Removed the incorrect Application implementation project reference and global/direct imports. Restored Application's Polly version to `8.6.5`; the approved Plex SDK `0.21.0` remains unchanged. Metadata persistence consumes domain entities through `PlexApi.Contracts`.
- Contracts-boundary verification: clean native/IDE diagnostics and successful Rider solution build; 203 Plex API unit tests, 27 refresh unit tests, 23 metadata unit tests, and 45 hosted integration tests passed. Actual TUnit reports confirmed the Plex API and hosted totals because the test MCP misreports aggregate counts and background sessions disappear on completion. Hosted proof exercises AppHost HTTP → queue/Quartz → synthetic Plex HTTP → SQLite, including repeated refreshes and Photos disable/re-enable.
- Centralized numeric metadata IDs and agent/scanner-based library classification in `PlexMediaTypeToApiTypeExtensions`, preserving separate item-type conversion. Reused `LogIfFailed`, removed the duplicate progress counter, and flattened progress estimation. Comments remain; the page summary reflects requested media types.
- Removed `ReadHeader`, response-header fallback/validation, snapshot consistency checks/tests, descendant family/type guards and the separate `GetDescendants` workflow. Restored original total handling and fixed-stride paging. Mapping uses existing parent dictionaries inside the existing `Result.Try`, without a new command or abstraction. Current verification: clean native/IDE diagnostics, successful Rider build, 196 Plex API unit tests, 27 family refresh unit tests and 45 hosted integration tests passed.
- Each new CRUD handler owns its operation-local `BulkConfig`. Verification exposed concurrent mutation of the shared preset's operation type, which could turn an insert into an update and leave generated parent IDs unset. Local configuration fixed the failing reconciliation checks; the global preset and canonical Movie/TV handlers were not changed.
- Separate hosted Music and Other Videos scenarios check initial and repeated refreshes with exact persisted IDs, parents and originals. The Photos scenario checks 2 albums, 4 still photos and 2 additional clips, repeat refresh, selected-library cleanup, and reconstruction after re-enable; Music and Other Videos are pre-seeded isolation controls, not additional refresh SUTs. Re-enable waits for durable database convergence rather than a transient scheduler-idle gap.
- Removed `LibraryMediaTypeId` and the transport URL-rewriting code/test. SDK 0.21.0 compatibility changes are limited to native request fields, the protocol enum alias, optimized-streaming enum mapping, and existing strict SDK fixtures. New optional SDK fields are explicitly ignored by fixtures; strict validation remains enabled.
- Historical verification limitation: synthetic Plex fixtures are not live Photos acceptance. Phase 2 is accepted as completed by the user; this status does not imply that the latest audit's blocked test runs passed.

#### Post-review cancellation correction — 2026-10-03

- `CancelLibrarySyncJobCommandHandler` now treats `Failed` as a terminal no-op alongside `Completed` and `Cancelled`. It returns success without touching Quartz or rewriting retained history, allowing the existing disable/purge workflow to proceed. The rejected retrieval and queue-eligibility findings were not changed.
- Added parameterized terminal-history regression coverage for `Completed`, `Cancelled`, and `Failed`: status, completion timestamp, error message, and offline flag remain unchanged; scheduler and notification collaborators receive no calls.
- Rider's changed-file build succeeded with no reported problems; both edited C# files had clean error diagnostics. A throwaway console smoke reproduced the blockage before the fix and passed after it using the production cancellation handler and endpoint's disable workflow with isolated in-memory SQLite created via `EnsureCreated`. The selected Photos library was disabled and purged; a control album and the original Failed history remained; Quartz received no calls. This was not a hosted HTTP test.
- The targeted cancellation unit class discovered five cases, but all were blocked before their bodies by the existing test-session `PendingModelChangesWarning` for `ReaparrDbContext`. No successful TUnit execution is claimed. No migrations, model snapshot, warning suppression, or real user data were changed.

### Existing fake-data hierarchy contract — preserve

- `FakeDataConfig` exposes `MusicArtistCount` per music library, `MusicAlbumCount` per artist, and `MusicTrackCount` per album; `PhotoAlbumCount` per photo library, `PhotoCount` for still images per album, and `PhotoClipCount` for additional clips per album; `OtherVideoCount` per Other Videos library. All default to zero and repeat independently in every matching library on every configured server.
- `ShouldHaveMusicPlexLibrary`, `ShouldHavePhotoPlexLibrary`, and `ShouldHaveOtherVideoPlexLibrary` include their explicit library counts and media counts. Any positive descendant count creates the required library, but zero ancestor counts never fabricate parent media.
- Typed fakers and database seeding preserve parent, library, and server ownership, generate one original per leaf using `DownloadFileSizeInMb`, and persist family counts and media sizes. `PhotoCount = 40` and `PhotoAlbumCount = 50` mean 2,000 still photos per library; clips are additional. Movie/TV generation, `PlexApiDataConfig`, and download-task counts are unchanged.
- New faker rule definitions are cached in `private static readonly Faker<T>` fields. Factories clone the templates before applying caller-specific counts, file sizes, and seeds; still-image and clip originals have separate static templates. This avoids rebuilding the fixed rule chains without sharing mutable caller configuration.

**Review gate:** completed by explicit user acceptance on 2026-10-03. The requested failed-sync-history cancellation correction was applied and smoke-verified above. Later root-overview/Photos approvals are recorded below and in the current-progress snapshot; remaining Music/Other Videos work still requires explicit approval.

## Phase 3 — Query APIs, DTOs, and overview cache

**Status:** root detail and canonical/cache workflows are present. MusicArtist exposes nested albums/tracks and PhotoAlbum exposes owned images/clips; standalone descendant detail remains intentionally rejected. Earlier targeted verification is historical evidence, not a latest-worktree completion claim. Music quality/family-applicability filters are explicit later TODOs. Frontend contracts remain Phase 8.

**Review decision:** the user selected **Pause for review** after the targeted backend verification. Phase 4 is not approved or started. Preserve the current changes; frontend and migrations remain excluded, and the persisted `"Artist"` failure remains unresolved.


### Approved root-overview correction — historical evidence, 2026-10-04

- Other Videos follows the Movie handler template; Music artists and Photo Albums follow the TV handler template. Movie/TV handlers remain unchanged. All three use scoped filtering, snapshot paging, common statistics/navigation, phase logging, and real canonical query fallback.
- Photos-library requests resolve to `PhotoAlbum` roots. All root payloads use `PlexMediaSlimDTO` in `PagedMediaQueryResult.Items` and the existing HTTP `MediaList`; the separate `PhotoAlbums` result property is removed.
- The user explicitly approved aligning `PlexPhotoAlbum` with `BasePlexMedia`, persisting actual Plex thumbnails, maintaining derived album metrics, and generating migration/model-snapshot changes. The historical isolated smoke used a migration named `AlignPhotoAlbumMediaOverview`; that name does not describe the current migration chain. Current files include `20261004183754_TEMP` and `20261004192353_AddPhotoDownloadTaskHierarchy`; fresh application evidence is recorded in the current-progress snapshot.
- Within this narrow correction, cross-library comparison was excluded for all three types and no detail/child endpoints or frontend features were added. Later approved Photos detail/download/frontend work supersedes that scope limit for Photos only; Music/Other Videos comparison is still unavailable.
- Verification: affected Rider projects built; scoped SQLite overview/canonical, validation, photo ingestion/sync/purge, Plex mapping, unchanged Movie/TV, and hosted photo-refresh classes passed. A throwaway hosted HTTP executable exercised cold and rebuilt snapshot pages, exact IDs/totals, slim thumbnail metadata, hierarchy counts, navigation, Other Video quality ordering, and HTTP 400 comparison rejection. Full-solution compilation remains blocked by unrelated existing `Artist`/`Song` references in Domain mapper tests. The hosted fixture logged database-access errors during sandbox teardown after the successful assertions.


**Start here:** `src/Data/Queries/GetMediaByTypeCommandHandler.cs`, `src/Application/PlexMedia/`; `GetAllMediaByTypeEndpoint`, `GetMediaOverviewCommand`, `RebuildMediaOverviewCommand`.

1. Extend query dispatch, root-only detail, existing hierarchy browsing, projections, validation, search, sorting, and pagination for Music, Photos, and Other Videos. Return actual typed metadata, not empty or relabelled video DTOs. Standalone MusicAlbum/MusicTrack/PhotoImage detail endpoints are not required; defer Music quality/family-applicability filters to their explicit TODO.
2. Expose hierarchy/parent identity, original version/part selectors, counts/size, and appropriate metadata through existing backend contracts. Preserve library access and established disabled/owned/offline behavior. The exact source-relative-path requirement is still open and explicitly excluded from implementation; do not add path/schema changes without renewed approval.
3. Populate/query family-aware overview snapshots/indexes from Phase 1; Photos use album-root snapshots only. Extend rebuild, warmup, invalidation, and navigation indexes. Verify cached and direct queries agree after sync/purge.
4. Keep Photos/Other Videos comparison non-applicable and music comparison unavailable until the final phase. Do not manufacture missing/owned/upgrade state from absent comparison work.
5. Verify backend request/response contracts directly; leave TypeScript generation and all frontend changes to the separate Phase 8.

**Verify:** call actual backend browse/detail APIs through hierarchy/search/pagination and cached/direct paths, including established access/filter semantics and post-sync behavior. Inspect source IDs, metadata, totals, and backend contract assertions.

**Review gate:** backend API payloads, cache behavior, and Movie/TV compatibility. No frontend/browser acceptance dependency.

## Phase 4 — Download expansion, preview, and creation

**Status:** separate Music artist/album/track, Photo album/image, and Other Video generation/preview workflows are present in source. Destination-aware deduplication was rejected and reverted; preserve existing duplicate and destination handling. Music's selector-only correction has been restored independently of that rejected work. Missing-original handling remains an explicit TODO. Exact source-relative paths remain excluded.

**Start here:** `src/Application/PlexDownloads/`; `GetDownloadPreviewQuery`, `CreateDownloadTasksCommandHandler`, `DownloadMediaDTOValidator`, existing generation handlers and request DTOs.

1. Expand artist/album/track and photo-album/photo selections into file-bearing media items; handle individual Other Videos. Keep existing duplicate checks; no path-based deduplication change is authorized.
   - Keep Music, Photos, and Other Videos expansion/generation in separate family-owned commands/handlers. Preserve the Photos generator's family-specific identity; do not broaden it into an original-media generator.
2. Resolve the requested original through MediaId/DataId and the existing Movie/TV fallback semantics, then expand every required part of that version. Do not add a Music-only mandatory-choice policy; audio-version preference is separate later work.
3. Preserve existing creation/error semantics for absent/stale selectors. Keep destination resolution in the existing download job; do not add creation-time resolution or final-path comparisons. Missing-original success and target-path exclusions remain explicit TODOs.
4. Extend the existing mixed-family dispatcher and preview entrypoint to invoke/project separate family workflows; extend their typed task persistence, counts/size, creation reports, and notifications without merging generation logic. Retain original destination request data and Photos provenance for included clips.
5. Extend `DownloadTaskType` mappings, generic task projections, DB lookup/status/log queries, and validators so new tasks never become `None`, movie/TV aliases, or successful empty work.

**Verify:** invoke preview/create APIs for every selection level and a fully mixed request. Inspect persisted source/version/part identities, destinations, deduplication, counts, errors, and unchanged movie/TV quality selection.

**Review gate:** unchanged request contract, preview results, generated tasks, and creation compatibility. Stop for user review.

## Phase 5 — Transfer, paths, recovery, and progress

**Status:** all three new families are wired into existing client selection, queue, movement, lifecycle, progress, and recovery infrastructure. Direct/DASH selection is intentionally unchanged. Source coverage and historical focused checks do not establish real Plex transfer, realtime, or cold-recovery acceptance. Exact source paths and shared log mapping remain excluded.

**Start here:** `src/Application/PlexDownloads/_BackgroundJobs/`, task DB extensions, `DownloadTaskFileBase`, start/pause/restart/recovery/delete handlers, and SignalR dispatch/contracts.

1. Connect new file tasks to the established download queue and existing Direct/DASH selection. Respect the server setting and Plex decision; do not impose a new family-specific Direct-only rule or create a parallel queue/state machine.
2. Build safe download/destination paths from exact source-relative components across source/host OS conventions. Preserve path containment, temp-file handling, original names, automatic replacement, and separate same-task resume behavior.
3. Extend all task operations: queue priority/concurrency, pause/resume/cancel/restart/delete, movement, history/logging, terminal states, parent progress, and interrupted-task/startup recovery.
4. Update snapshot/patch mappings, SignalR/MessagePack contracts, task/report counters, and notifications together. Preserve existing transport and progress semantics.

**Verify:** exercise real audio, image, photo-library clip, and Other Video transfers into isolated destinations through applicable existing client-selection paths. Compare source/output bytes for Direct transfers; verify DASH selection/output behavior without promising byte identity. Exercise multipart completion, interrupted resume, replacement, pause/restart, cold recovery, and realtime progress. Check existing Movie/TV transfers.

**Review gate:** filesystem/byte proof, lifecycle transitions, recovery, and realtime evidence. Stop for user review.


## Phase 6 — Complete backend type coverage and core regression

**Status:** the requested targeted core type/regression gate passed as recorded above. Broader Direct/DASH, multipart/replacement, realtime transport, and populated-upgrade acceptance remain bounded by the explicit evidence/TODOs; no whole-project green claim is made. Music comparison implementation now proceeds immediately.

1. Reconcile relevant backend `PlexMediaType`, `DownloadTaskType`, and `FolderType` dispatch and conditionals across runtime code, EF persisted values, validators, backend DTOs, debug surfaces, and backend tests. Supported operations must work; non-applicable operations must not claim successful work. Frontend generated contracts/mocks/tests are Phase 8 only.
2. Verify existing Movie/TV integrations remain unchanged and new-family generator guards remain effective. Mixed Sonarr/Radarr requests are unsupported and do not require new prevalidation, batching, rollback, or mixed-request tests.
3. Remove obsolete unsupported guards/comments and accidental video fallbacks. Keep sentinel/unsupported-type error behavior explicit; do not build a speculative plugin/type framework.
4. Run affected backend suites and actual backend scenarios on fresh/upgraded DBs: all five families, originals, mixed requests, single-album photo ownership/cascades, multipart transfer, replacement/resume, SignalR payloads, and cold recovery. Record source-path/log-mapping exclusions; do not reopen them or require a browser/frontend suite for backend acceptance. Music comparison is the next backend phase, not part of this core gate.

**Review gate:** the user approved the targeted core gate followed immediately by Music comparison. Record verified evidence and remaining exclusions, then proceed to Phase 7 without another approval pause.

## Phase 7 — Music comparison backend, last

**Status:** implementation approved after the requested core type/regression gate, with no intervening approval pause. Music edition fallback was confirmed above. Photos/Other Videos stay non-applicable. This phase is backend-only; frontend presentation/generated contracts and audio-quality ranking remain deferred.

**Start here:** `src/Application/PlexLibraries/Comparison/`, `PlexLibraryComparisonJob`, `ApplyComparisonStateCommandHandler`, `GetMediaComparisonDetailsEndpoint`, scope/hit persistence, and backend comparison DTO/projection contracts.

1. Add family-owned Music artist/album/track comparison hit tables/configurations through the existing EF generation tooling in this approved implementation task. Schema/migration generation is authorized; do not apply migrations to a live/user database. Preserve existing Movie/TV tables and shared base-class hierarchy.
2. Follow the current Movie/TV pipeline for ownership detection, library-pair scheduling, persistence, scope invalidation, background execution, state projection, and comparison-detail responses. Keep Music business logic in its own sibling handlers, not a combined comparison framework.
3. Adapt matching to the existing Music artist/album/track identities and parent hierarchy using the confirmed conservative edition/track fallback rules above; do not use a release-group or recording match alone to equate different album editions.
4. Music quality is distinct from video resolution and remains a separate later TODO. Scope any audio-quality storage, ingestion, ranking, upgrade states, or extra uncertainty contracts explicitly; the older detailed specification/new-state design is not automatic authority to expand this phase.
5. Project the approved Music comparison states/counts into applicable backend overview, root-detail, comparison-detail, and filter paths using existing contracts where possible. Keep Photos/Other Videos comparison-free and all frontend work deferred.

**Verify:** artist/album/track identity and parent scoping, partial ownership, aggregation across owned libraries, stale/pending comparisons, hit replacement/cascades, scheduling/invalidation, and actual sync → comparison job → backend browse/root-detail flow. Verify audio upgrades only after their separate contract exists. Preserve Movie/TV results.

**Backend completion review gate:** music identity/quality evidence, projected counts, backend runtime behavior, and preserved Movie/TV comparison. Backend completion is the immediate goal; frontend acceptance is separate. Stop for review; do not commit automatically.

## Phase 8 — Frontend implementation and verification — separate, deferred

**Status:** paused by explicit user direction. Existing partial Photos UI and attempted generated-contract changes are preserved, not completed. The known Photos Cypress **1 passed / 4 failed** and frontend **210 passed / 2 failed** remain open here. Do not work on this phase while completing the backend.

**Prerequisite:** backend phases and backend completion review must be closed. Resume only with explicit frontend approval.

**Start here:** `src/AppHost/ClientApp/`; generated API contracts, music/photos/Other Videos pages, `ServerDrawer`, `Convert`, overview/settings/folder/download stores, table/poster/detail actions, `DirectoryBrowser`, and `DownloadConfirmation`.

1. Generate TypeScript from the verified running development backend with `bun run generate-ts`; never hand-edit generated files. Use Bun exclusively. Fix the deferred conversion/request failures and diagnose the desktop queue-title failure without weakening assertions.
2. Finish music artists → albums → tracks, photo albums → owned images/clips, and individual Other Videos routes/browse/detail/selection. Remove unsupported placeholders and OtherVideos→Movies/non-movie→TV fallbacks. Do not add Other Videos folder/subtree browsing.
3. Complete appropriate counts/icons/metadata/settings/view modes and destination default/override handling using existing UI/store conventions and request envelopes.
4. Resolve/display original-version choices through MediaId/DataId, including ordinary mixed-family requests, while matching the approved backend first-selector/fallback behavior. Do not add stricter mandatory-choice semantics independently in the frontend. Avoid false audio/photo video-quality presentation.
5. Verify queue/progress/history/lifecycle controls, parent aggregation, translations, and the now-completed backend music comparison presentation. Photos/Other Videos never show comparison badges/controls/filters.

**Verify:** affected Vitest tests, diagnostics/typecheck, all five existing Photos Cypress workflows, and complete Music/Other Videos/mixed-family browser scenarios. Inspect real request payloads, desktop/mobile visibility, destinations, originals, progress/recovery, and console/network errors.

**Final review gate:** separate frontend and application-wide end-to-end acceptance after backend completion. Stop for user acceptance; do not commit automatically.


## Verification standard

Each phase requires changed-file diagnostics, focused consumer-visible behavioral checks, and its listed smoke scenario. Tests alone are not runtime proof. Permanent tests cover uncertain invariants/transitions/errors, not wiring, source text, mock echoes, or incidental implementation details. Use isolated verification data/destinations; do not replace real user files. Report only checks actually exercised.

References: [Plex library types](https://support.plex.tv/articles/200288926-creating-libraries/), [Plex library views](https://support.plex.tv/articles/200392126-using-the-library-view/), MusicBrainz [Release](https://musicbrainz.org/doc/Release) / [Recording](https://musicbrainz.org/doc/Recording), [Windows naming constraints](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file).

## Backend enum hierarchy — historical user correction, superseded names

The following records an earlier naming decision and its checks. Current descriptive enum/entity names in the progress snapshot and active invariants supersede these names. Do not restore old identifiers or treat the checks below as current-worktree verification.

**Decision:** `Music = 5 → Album = 7 → Track = 8`, and `PhotoAlbum = 9 → Photos = 10`. Remove the domain `Artist` member; use `Music` for the existing artist root. `Track` replaces `Song` without changing numeric value 8. Keep the descriptive entities, relationships, collections, counts, and table names unchanged.

| Meaning | Domain type | Existing descriptive model |
|---|---|---|
| Music library / artist root | `Music` | `PlexMusicArtist`, `PlexArtists` |
| Music album | `Album` | `PlexMusicAlbum` |
| Music track / original | `Track` | `PlexMusicTrack`, `PlexMusicTrackMediaData` |
| Photo-album root | `PhotoAlbum` | `PlexPhotoAlbum` |
| Individual photo / original | `Photos` | `PlexPhoto`, `PlexPhotoMediaData` |

- Translate Music ↔ Plex SDK Artist / metadata ID 8, Album ↔ SDK Album / ID 9, and Track ↔ SDK Track / ID 10 at the existing boundary. Domain numeric values are not Plex wire IDs.
- Keep PhotoAlbum ↔ SDK PhotoAlbum / metadata ID 14, and Photos ↔ SDK Photo / ID 13. A Photos library requests PhotoAlbum roots; retain the existing album → owned-photo hierarchy.
- Music progress uses Music/Album/Track; photo progress uses PhotoAlbum/Photos.
- No Artist/Song domain aliases, singular Photo replacement, entity/table renames, schema changes, SDK upgrades, frontend changes, or client generation.
- Correct only currently unstaged work, leave the index and unrelated changes untouched, verify the corrected paths, then stop for user review. This correction does not authorize more Phase 3 implementation.
- Verification (2026-10-04): Rider reported no errors in all 23 corrected code/test files and the scoped backend/test build passed. An isolated in-memory SQLite smoke passed persisted types, numeric/JSON identities, Plex IDs and wire names, removed-alias rejection, Music → Album → Track and PhotoAlbum → Photos retrieval relationships, DTO parent identities, and both progress-stage sequences.
- The focused `PlexMediaTypeMappersUnitTests` run was blocked before assertions by the shared test-session migration hook (`PendingModelChangesWarning`); migrations and warning handling were left unchanged.

## Backend PlexMediaType audit — historical existing-workflow evidence

This audit predates the current canonical Music/Photo/Other queries and approved Photos generation/detail/lifecycle/frontend work. Its Movie/TV-only download/query statements, old enum names, and then-blocked test claims are historical, not current status. Use the current-progress snapshot; no later-phase completion is implied by this appendix.

**Scope:** resume the original backend enum audit; do not implement or remove Phase 3. The attempted Phase 3 cutback in this audit was undone to the exact pre-cutback worktree contents. Existing browse/detail/cache additions remain as found, not accepted as complete. Preserve the accepted synchronization design and existing Movie/TV workflows. No schema, index, or real-user-data write was performed.

### Grouped usage inventory

The pre-edit, scoped Rider source inventory contained **1,488 direct matches in 242 files**: **688 matches in 165 production files** and **800 matches in 77 test files**. Scope excludes ClientApp, migrations, model snapshots, and generated `bin`/`obj` output. Counts describe that inventory snapshot, not the subsequently rewritten/added regression tests. Graph searches and traces were used to inspect indirect consumers; current source, not stale graph/LSP references, determines the classifications below.

| Production area | Direct-reference files | Classification and disposition |
|---|---:|---|
| `src/Domain/` | 49 | Enum, catalog/file identities, task identities, base types, folder mappings, and validators. Music/Album/Track and PhotoAlbum/Photos identities are correct after the preceding correction; descriptive Artist/Photo CLR names and committed relationships stay unchanged. New task storage/types are not proof of an implemented downloader. |
| `src/PlexApi/` | 10 | Required typed retrieval/mapping paths already exist: library classification, native numeric requests, shared root/descendant command, metadata mapping, and existing Movie/TV retrieval helpers. Other Videos uses Plex movie request ID 1 without changing its domain/library family; photo-library clips retain Photos provenance. |
| `src/PlexApi.Contracts/` | 4 | Retrieval command, generic retrieval contract, progress DTO, and SDK enum conversion. SDK Artist/wire `artist` remain valid boundary representations of Music; numeric domain values are not Plex IDs. |
| `src/Application/` | 49 | Refresh dispatch and five ByType workflows, progress, update detection, purge/enable paths, folder commands, metadata/filter queries, download creation/preview, comparison, browse/detail, debug titles, and overview/cache consumers. Existing sync paths are supported; download guards were fixed below. Browse/detail/cache and their Music, Photos, and Other Videos projections are left untouched for Phase 3. Comparison stays Movie/TV-only. |
| `src/Application.Contracts/` | 22 | Progress/library/folder/media/download DTOs, selection validators, merge helpers, integration support, and comparison contracts. Generic DTO transport needs no new dispatch. Video-quality contracts are not repurposed for audio/photos. Existing partial Phase 3 DTOs remain untouched. |
| `src/Data/` | 3 | Library computed counts and destination-folder seed already cover all five library families. `GetMediaByTypeCommandHandler` remains Movie/TV-only; extending typed browse queries belongs to Phase 3. No schema or seed changes. |
| `src/Data.Contracts/` | 8 | Generic library/server/access/folder helpers already preserve type/scope. Rating-key lookup and generic download-task lookup/status/progress helpers have explicit existing Movie/TV branches; Music, Photos, and Other Videos browse lookup/download execution is deferred, not replaced with empty success or aliases. |
| `src/PublicAPI/` | 14 | Torrent metadata, add/download/info, Torznab search/RSS/projections/categories/enrichment. Deliberately Movie/Episode or Movie/TV-only; existing positive IDs and Radarr/Sonarr identity checks remain. No music/photo/Other Videos integration or indexer categories. |
| `src/Settings.Contracts/` | 3 | Display settings transport/interfaces/module; generic enum-bearing settings need no new execution path. |
| `src/SignalR/` | 1 | Server download-progress MessagePack DTO is generic transport, not authorization to add new download execution. |
| `src/SignalR.Contracts/` | 2 | Library sync progress and comparison completion transport. Existing family progress identities are correct; comparison applicability remains restricted. |

Indirect paths reviewed include the shared Plex retrieval → family refresh → typed CRUD/count/stamp/progress flow; library enable/disable → cancellation/purge/sync queue; DTO merge → preview; create endpoint and PublicAPI torrent add → create command → four generators → queue/notification; and the generic overview coordinator/Quartz lifecycle. Do not introduce parallel pipelines or new abstractions for these consumers.

### Focused changes

- `src/Application/PlexDownloads/Create/CreateDownloadTasksCommandHandler.cs`: apply the existing `DownloadMediaDTOValidator` to every input selection before handler dispatch, with null-element rejection. Unsupported-only requests previously returned successful zero-task reports. Mixed requests are now rejected at the top-level boundary before generator dispatch; the existing generator validators already enforce the same selection restriction.
- `src/Application/PlexDownloads/Preview/GetDownloadPreviewQuery.cs`: use that same validator rather than checking only `MediaIds.Any()`. Unsupported-only requests previously returned successful empty previews; mixed selections now fail before querying the supported subset.
- `tests/UnitTests/Application.UnitTests/PlexDownloads/Create/CreateDownloadTasksCommandHandler.UnitTests.cs`: use `BaseCommandUnitTest<CreateDownloadTasksCommand>`; replace direct-handler empty-success and wording-based assertions with validation failure, exact aggregates, generator stop/skip contracts, exact queue/notification payloads, and forbidden-call checks.
- `tests/UnitTests/Application.UnitTests/PlexDownloads/Preview/GetDownloadPreviewQueryValidator.UnitTests.cs`: add isolated validator regression coverage for Music, Album, Track, PhotoAlbum, Photos, and OtherVideos, alone and mixed with Movie; assert one failure at the actual unsupported selection's Type property.

This enforces the existing Movie/TvShow/Season/Episode selection contract; it does **not** implement Phase 4 downloads, introduce a new atomic-batch/result protocol, or widen quality/DTO/task contracts.

### Exercised evidence and limits

- Rider reported **zero errors in all four changed code/test files**. The scoped Application/Application.UnitTests IDE build completed successfully with no retained problems.
- A throwaway console invoked the production `ValidationPipeline` and actual create/preview handlers. Before the fix, all six unsupported-only selections returned success with zero errors; mixed selections reached downstream command/database boundaries. The pre-fix check exited **1**.
- After the fix, all **12 selection scenarios** (six types × alone/mixed) produced one failed result per create and preview invocation, with **zero generator/event/notification/database calls**. The check exited **0**.
- Existing Movie/TvShow/Season/Episode selectors remained valid. The actual create handler dispatched the four existing generators once each, returned the exact 3/2/4/10 aggregate (total 19), emitted one correctly scoped queue event, and sent one DownloadTasks refresh notification. Collaborator responses were controlled; this is command/middleware proof, not persisted download or hosted HTTP proof.
- The preceding isolated SQLite hierarchy/retrieval/progress proof remains recorded above. This audit did not repeat or claim live Plex, real file transfer, full Music, Photos, or Other Videos browse/cache, or hosted API acceptance.
- TUnit execution remains blocked by the already observed shared test-session migration-hook `PendingModelChangesWarning`. The blocker was not rerun merely to reconfirm it; the new/updated cases compiled but are **not claimed to have passed TUnit**. No migration, model snapshot, warning suppression, or user database change was made.
- Index coverage is best-effort. A recorded parse-partial gap in `GetLibraryMediaFromPlexApiCommandHandler.UnitTests.cs` was handled with direct source review. Some enum LSP references still name deleted/moved files and an old temporary smoke source; they are not current callsites.
- Rejected queue-eligibility findings remain unchanged per the user's earlier decision. Unrelated merge-helper, folder-assignment, and bulk-configuration concerns were not folded into the enum audit. Phase 3 remains explicitly deferred; no completion claim is made for its current partial additions.
- No stage, commit, reset, restore, or other index-writing command was run. The final cached-diff fingerprint differs from the previously recorded fingerprint; its cause was not established, and the index was not reset or overwritten. The external smoke project was safely trashed after verification.
