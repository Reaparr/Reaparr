# Music / Photos / Other Videos audit follow-up

## Review protocol

- Implement approved fixes in the listed order. Default to one fix; an explicit user instruction may authorize a bounded batch.
- Each fix or explicitly authorized batch includes current-source discovery, focused regression proof, changed-file diagnostics, and targeted verification.
- Stop after each verified fix or batch for user review and end the review with a clickable question to continue or pause. Do not begin later work without user continuation.
- Preserve intervening user changes. Already-correct behavior stays unchanged.
- No automatic commits, staging, live/user database changes, or real-user download-file changes.
- Frontend Phase 8 remains separately deferred; these annotations do not authorize frontend implementation.

## Approved implementation TODOs

1. [x] **Annotation 4 — Preview / creation selection parity.** Music, Photos, and Other Videos preview now uses the first selector matching `MediaId`, resolves its local `DataId` without extra type/cardinality rejection, and retains all parts of the selected original. Absent/stale/foreign selectors no longer reject an otherwise valid request; creation keeps automatic fallback while preview may expose alternative originals. Media server/library ownership checks remain enforced. Verified; user approved continuation into fix 2.
2. [x] **Annotation 5 — Merged descendant selections.** Music artist/album and Photo album expansion now consume the existing merged same-scope request list rather than the first raw descendant DTO. Photo album inheritance also uses the merged ancestor selection. Later explicit choices following an empty duplicate survive expansion; original selector ordering, inherited fallback, completed-folder merge policy, and ownership guards remain unchanged. Verified; user approved continuation into fix 3.
3. [x] **Annotation 7 — DASH filename persistence.** DASH now persists normalized `Title` and `FileName` for Music Track, Photo Image, and Other Video file entities using the existing targeted update pattern. Verified fresh persisted names and completed-source/destination path resolution, with unchanged unrelated-file and Movie/TV controls. Direct/DASH selection remains unchanged; no DASH byte-identity claim. User approved continuation into fix 4.
4. [x] **Annotation 8 — Category-root cleanup boundaries.** Delete cleanup now stops at the `Music`, `Photos`, and `OtherVideos` category roots, matching the existing Movie/TV protection. Existing empty-directory recursion and active-directory checks remain unchanged. Isolated filesystem regressions cover empty, active-sibling, and unrelated-file cases across all three families; existing Movie/TV controls remain. User approved continuation into the next bounded batch.
5. [x] **Annotation 12 — Completed-task key query translation.** Rechecked the intervening implementation. Each typed EF query now materializes independently before in-memory distinct aggregation, avoiding EF's client-projection set-operation failure. Exact completed keys, mixed families, and non-completed/scope controls remain covered.
6. [x] **Annotation 13 — ReleaseSource JSON serialization.** `ReleaseSource.None` now has a valid non-empty JSON member name, and the existing enum wire-value helper recognizes `JsonStringEnumMemberNameAttribute` before `EnumMemberAttribute`. Serialized enum values and current missing-detail responses are covered without endpoint changes.
7. [x] **Annotation 14 — Sync admission.** Queue admission now filters selected libraries through the existing five-root `IsRootType()` rule after the existing enabled/access-scoped query. Unsupported-only requests do nothing; mixed requests retain Movie, TV, Music, Photos, and Other Videos.
8. [x] **Annotation 17 — Move-job outcomes.** Preserve failed/cancelled results through the job outcome boundary. Verify failure, cancellation, and normal completion without changing move-resume behavior.
9. [x] **Annotation 18 — Missing source lookup.** Return the existing failed/not-found result on missing rating-key/server matches, not successful ID `0`. Preserve server filtering and successful exact identity lookup; cover Movie and all new-family cases.

## Rechecks and upstream issues

- [x] **Annotation 9 — Music comparison rechecked:** the current typed artist/album/track matcher, transactional persistence, job dispatch, comparison-state application, and browse/detail paths are implemented. Retire the old missing-implementation finding. Current album/track ingestion still leaves MusicBrainz release/recording IDs null, so ordinary synced data uses available fallback matching rather than those exact-ID branches. This narrower ingestion limit is not authorization for another fix. Source inspection only; no new comparison runtime run.
- [x] **Annotation 10 — Aggregate ownership rechecked:** Music artist/album and TV show/season updates are qualified by the ancestor's own timestamp (and parent identity where applicable). Descendant-only changes can therefore leave stored ancestor aggregates stale when ancestor timestamps stay unchanged. Movies have no equivalent hierarchy; their own originals have a separate timestamp-gated refresh risk unless forced. This is a conditional source-level risk, not a reproduced Plex timestamp guarantee. No aggregate edits.
- [x] **Annotation 15 — Overview cache issue:** created [Reaparr #681](https://github.com/Reaparr/Reaparr/issues/681) for mixed warm/cold library omissions. No local cache fix.
- [x] **Annotation 16 — Partial move recovery issue:** created [Reaparr #680](https://github.com/Reaparr/Reaparr/issues/680) for interrupted-move recovery deleting a valid partial destination/resetting progress. No local move-resume fix.
- [x] **Annotation 22 — Obsolete Part types issue:** created [Reaparr #682](https://github.com/Reaparr/Reaparr/issues/682) to remove `MoviePart`, `EpisodePart`, `MusicTrackPart`, `PhotoPart`, and `OtherVideoPart` in favor of their Data file entities. The issue requires complete caller/contract migration, multipart preservation, stable remaining enum numeric values, and persisted-key compatibility assessment. No local enum removal.

## Scope corrections and report removals

- **Annotations 1–2:** this file and the omp TODO list are the tracking sources; review checkpoints are mandatory, with bounded batches allowed only when explicitly authorized.
- **Annotation 3:** omit hosted-scenario exposition and test-tool-reporting exposition from the revised report.
- **Annotation 6:** leave descendant retrieval unchanged. Do not clear collections unnecessarily; the previous clearing proposal is withdrawn.
- **Annotation 11:** malformed/incomplete Plex retrieval safety is outside this task. No change or work item for it.
- **Annotation 19:** omit comparison-freshness discussion and changes.
- **Annotations 20–21:** omit fixture-failure and cleanup-validator naming exposition; do not expand this sequence into unrelated test-infrastructure repair.

## Verification and progress

Only record checks actually executed for each fix. Prior audit results are not a current baseline after intervening changes. Each completed fix must list files, focused verification, and its review checkpoint here; later fixes remain pending.

### Fix 1 — completed; user review checkpoint

- Changed `src/Application/PlexDownloads/Preview/GetDownloadPreviewQuery.cs` and `tests/UnitTests/Application.UnitTests/PlexDownloads/Preview/GetDownloadPreviewQueryHandler.UnitTests.cs`.
- Preview selects the first matching original group; later selectors do not override it. Multipart originals retain their summed size and part count, including Photo clips. Unmatched selectors are ignored; requested media still must belong to the requested server/library. Movie/TV production behavior and creation policy remain unchanged.
- Regression proof: `GetDownloadPreviewQueryHandlerUnitTests` failed before the production fix with 12 failing cases and 29 passing controls. The same class passed after the fix (exit 0).
- Covered absent/stale/foreign/unmatched selectors, first-selector precedence, mismatched selector types, multipart groups, overlapping typed IDs, Photo hierarchy, six wrong-server/library cases, and existing Movie/TV behavior.
- Rider reported no errors in either changed C# file; native LSP diagnostics were clean. Rider's incremental build of the affected files completed successfully with no retained problems.
- User approved fix 1 and authorized fix 2. See the next checkpoint below; fixes 3–9 remain review-gated.

### Fix 2 — completed; user review checkpoint

- Changed `src/Application/PlexDownloads/Generate/GenerateDownloadTaskMusicArtistsCommand.cs`, `GenerateDownloadTaskMusicAlbumsCommand.cs`, and `GenerateDownloadTaskPhotoAlbumsCommand.cs` in the same directory. Each handler reuses one existing `MergeAndGroupList()` result for ancestor and descendant lookup; no new helper, contract, merge policy, or dependency.
- Extended the existing real-handler regression in `tests/UnitTests/Application.UnitTests/PlexDownloads/Create/CreateDownloadTasksCommandHandler.UnitTests.cs`. Eleven cases cover Artist → Album, Artist → Track, Album → Track, and Photo Album → Image, including duplicate descendant DTOs, duplicate ancestors, and nonduplicate controls. Assertions cover exact creation reports, persisted original/file identity and metadata, completed-folder options, file logs, notifications, and unchanged input choices.
- Red proof: the focused parameterized regression ran before the production fix; five cases failed and six controls passed. Duplicate descendants produced two originals instead of one; duplicate Photo ancestors persisted the wrong original.
- Green proof: `CreateDownloadTasksCommandHandlerUnitTests`, `GenerateDownloadTaskMusicArtistsCommandUnitTests`, `GenerateDownloadTaskMusicAlbumsCommandUnitTests`, and `GenerateDownloadTaskPhotoAlbumsCommandUnitTests` all completed successfully (exit 0), including existing inheritance, reuse, ownership, validation, and child-failure controls.
- Native LSP and Rider diagnostics were clean for all four changed C# files. Rider's incremental build of the affected files completed successfully with no retained problems.
- User approved fix 2 and authorized Annotation 7, DASH normalized filename persistence. Subsequent fixes remain review-gated. No commits created.

### Fix 3 — completed; user review checkpoint

- Changed `src/Application/PlexDownloads/_BackgroundJobs/DownloadJob/DashDownloadClient/DashPlexDownloadClient.cs` and `tests/UnitTests/Application.UnitTests/PlexDownloads/Execute/DashDownloadClient/DashPlexDownloadClient.UnitTests.cs`.
- Extended the existing filename-persistence switch to update `Title` and `FileName` for the targeted Music Track, Photo Image, and Other Video file rows. Direct/DASH selection, normalization, notifications, and Movie/TV production behavior remain unchanged.
- Extended the existing regression to all five file families. It checks an exact normalized output name, persisted name/title through fresh database reads, matching completed-source and destination paths, and unchanged same-family and cross-family controls.
- Red proof: before the production change, the three new-family cases retained their original persisted filenames and failed; Movie and TV controls passed.
- Green proof: `DashPlexDownloadClientUnitTests` completed successfully (exit 0), including all five filename-persistence cases and existing completion, error, progress, and cancellation controls.
- Native LSP and Rider diagnostics were clean for both changed C# files. Rider's incremental build of the affected files completed successfully with no retained problems.
- User approved fix 3 through the clickable continuation question and authorized Annotation 8, category-root cleanup boundaries. Subsequent fixes remain review-gated. No commits created.

### Fix 4 — completed; user review checkpoint

- Changed `src/Application/PlexDownloads/Delete/DeleteDownloadTaskFilesCommandHandler.cs` and `tests/UnitTests/Application.UnitTests/PlexDownloads/Delete/DeleteDownloadTaskFilesCommand.UnitTests.cs`.
- Added only the three missing category stop-root cases: `Music`, `Photos`, and `OtherVideos`. Existing deletion, recursive-empty-folder checks, and active-directory protection remain unchanged.
- Nine isolated-filesystem regressions cover the three new families with empty task folders, active sibling tasks whose files do not yet exist, and unrelated retained file bytes. They check both temporary and plain target-file deletion, root preservation, eligible empty-folder removal, and unchanged sibling metadata/status.
- Existing Movie/TV root controls remain; the single-episode scenarios explicitly seed one season and one episode so empty-folder assertions do not conflict with active-sibling protection.
- Red proof: before the production change, the three empty-folder cases deleted their category roots and failed; the six sibling/unrelated-file controls passed.
- Green proof: `DeleteDownloadTaskFilesCommandUnitTests` completed successfully (exit 0), including all 21 test cases; the nine new-family cases and existing Movie/TV controls passed.
- Native LSP and Rider diagnostics were clean for both changed C# files. Rider's incremental build of the affected files completed successfully with no retained problems.
- User approved fix 4 and explicitly authorized this bounded three-fix batch. No commits created.

### Batch 2 — Annotations 12–14 completed; review checkpoint

- **Annotation 12:** Changed `src/Data.Contracts/Extensions/DbContext/DbContextExtensions.DownloadTasks.cs`. Each typed status query now executes independently before in-memory `Distinct()`, avoiding EF's projected multi-type `Concat` translation failure while preserving exact key/type/status filtering. Red proof: `ClearCompletedDownloadTasksByDownloadTaskKeyCommandMediaTypesUnitTests` failed in all five family cases with EF's client-projection set-operation exception. Green proof: that media-type class and `ClearCompletedDownloadTasksByDownloadTaskKeyCommandUnitTests` both exited 0.
- **Annotation 13:** Changed `src/Domain/_Shared/Enums/ReleaseSource.cs`, `src/Domain/_Shared/Extensions/EnumExtensions.cs`, and `tests/UnitTests/PublicApi.UnitTests/Indexer/Torznab/GetRssFeed/TorznabFeedItemProjection.UnitTests.cs`. Replaced the invalid empty JSON enum identifier with `None`; the existing wire-value helper now reads `JsonStringEnumMemberNameAttribute` before `EnumMemberAttribute`. Red proof: the new regression first reproduced the empty-identifier JSON exception, then exposed the old `WebDl`/`BluRayRemux`/`WebRip` wire values. Green proof: `TorznabFeedItemProjectionUnitTests` and `GetMediaDetailByIdEndpointUnitTests` exited 0.
- **Annotation 14:** Changed `src/Application/PlexLibraries/_BackgroundJobs/LibrarySync/Commands/QueueLibrarySyncJobCommand.cs`. Selected libraries are filtered through the existing `IsRootType()` rule after the existing enabled/access-scoped query, so unsupported-only requests return without dispatch and mixed requests retain all five supported roots. Red proof: the queue admission class failed on an unsupported `Games` queue and an unsupported-only dispatch. Green proof: `QueueLibrarySyncJobCommandHandlerUnitTests` exited 0.
- Rider reported no errors for all five changed C# files and the affected backend build succeeded with no retained problems. No endpoint, frontend, schema, or user-data changes were made.
- Review this batch before any further audit work.
 
### Batch 3 — Annotations 17–18 completed; review checkpoint

- **Annotation 17:** Changed `src/Application/PlexDownloads/_BackgroundJobs/MoveDownloadFileJob/MoveDownloadFileJob.cs` and strengthened the existing parameterized assertions in `tests/UnitTests/Application.UnitTests/PlexDownloads/MoveDownloadFile/Jobs/MoveDownloadFileJob.UnitTests.cs`. The job now returns handled move, reload, cleanup, and not-found results through the outer `Result.Try` boundary; failed outcomes set Quartz's failed result and queue once, cancellation sets Quartz's cancelled result, and normal/unauthorized/no-task paths remain successful no-ops. Targeted completion and incomplete-move methods passed. The full class reached 62 successes and one unrelated existing `Sequence contains more than one element` failure in `ShouldLeaveFilesAndStatusUnchanged_WhenQueuedJobIsNotAuthorizedToMove` at line 641; that failure was unchanged before and after this fix.
- **Annotation 18:** Changed `src/Data.Contracts/Extensions/DbContext/DbContextExtensions.PlexMedia.cs`. All supported media-type branches now convert a missing ID `0` into a typed failed result while retaining the existing rating-key plus server predicates. `ShouldResolveExactSourceIdentityAndRejectWrongServer_WhenLookingUpNewMedia`, `ShouldFindMovieId_WhenMediaKeyExists`, `ShouldFindTvShowId_WhenMediaKeyExists`, and `ShouldFail_WhenMediaNotFound` passed.
- Rider reported no errors for the changed production and test files; the final affected backend build succeeded with no retained problems. The complete media lookup test class passed. No endpoint, frontend, schema, or user-data changes were made; no commits created.

## Rebased review feedback — pn-bd7a69

These items record the user's feedback on the review of `f521fbd1baa5800f7b2bae8b8f0234ff0625a333...7e8a8a064288e80f1539b08031c667da114835ad`. They are pending implementation, not claims about the current worktree. Recheck each finding against current source before changing it; leave already-correct behavior unchanged. The review protocol above still applies.

### Approved implementation TODOs

1. [x] **Feedback 2 withdrawn.** The user confirms that stored libraries always have valid types. No root-type admission filter is required or approved.
2. [x] **Feedback 3 — Migrate enum text without changing folder paths.** Backfill only the persisted media-type enum text for existing `FolderPaths`: `Music` to `MusicArtist`, and `Photos` to `PhotoAlbum`. Preserve directory strings, including `/Music` and `/Photos`, row IDs, custom paths, and default directory names. Do not rename or move directories or add general runtime enum aliases. Verify upgrade and fresh-database reads and exact preservation of directory-path values.
3. [x] **Feedback 4 — Scheduled bandwidth membership.** Include Music Track, Photo Image, and Other Video file tables in `UpdateScheduledDownloadLimitsCommand.cs` participant discovery. Preserve per-server deduplication, manual caps, and the existing allocation policy. Verify new-family-only servers and mixed-family/mixed-server allocations under a finite schedule.
4. [x] **Feedback 5 — Quiesce comparisons before invalidating persisted results.** In `InvalidateLibraryComparisonJobsCommand.cs`, use the existing cancellation-and-wait path for every affected queued or running job before deleting comparison rows and scopes. Preserve cancellation/failure handling; do not delete results after unsuccessful cancellation or scheduler deletion. Verify that an in-flight ownership-change comparison cannot repopulate invalidated state after the command completes.
5. [x] **Feedback 7 — Reject unsupported PhotoAlbum filters.** Update the existing `GetAllMediaByTypeEndpoint.cs` validation to reject country, actor, genre, and quality filters that cannot apply to `PhotoAlbum`, rather than silently dropping them. Preserve supported PhotoAlbum queries and Movie/TV filtering. Verify rejection responses and exact results for supported controls.
6. [x] **Feedback 8 — Preserve category roots during move cleanup.** Stop `CleanUpDownloadTaskFolders.cs` at category roots for Photo and Other Video files; retain the existing eligible media-folder cleanup and Music/Movie/TV behavior. Use isolated filesystem regressions to verify root preservation, active siblings, and unrelated file bytes.
7. [x] **Feedback 9 — Movie dispatcher test constructor.** Supply the existing `ICommandExecutor` and `IDownloadTaskScheduler` mocks in `DownloadTaskUpdateDispatcher.Movie.UnitTests.cs`. Preserve the delayed-context pause regression and verify the affected dispatcher tests.
8. [x] **Feedback 10 — Invalid-payload move-job expectations.** Correct both invalid-payload queue expectations in `MoveDownloadFileJob.UnitTests.cs` to match the existing early failure return. Do not change production queue behavior to satisfy stale assertions. Verify invalid payload, missing task with valid payload, normal completion, and failure-path controls.
9. [x] **Feedback 11 — OtherVideos fallback assertion.** Update the remaining `"Other"` expectation in `PathProvider.UnitTests.cs` to `"OtherVideos"`. Verify both Docker data-path cases and existing dedicated-path overrides; do not change Music or Photos directory names.
10. [x] **Feedback 12 — Bound deletion directory-protection queries.** Replace the unrestricted full-queue hierarchy load in `DeleteDownloadTaskFilesCommandHandler.cs` with bounded active-directory candidate queries and only the required projections. Preserve protection for shared physical paths across media families and servers. Verify single-file deletion, active siblings without files yet, unrelated queues, and existing root boundaries.
11. [x] **Feedback 13 — Bound per-file cleanup queries.** Avoid materializing every active full file entity in `CleanUpDownloadTaskFolders.cs` for each completed file. Constrain candidate queries and project only what directory protection requires; retain cross-family and cross-server shared-path protection. Verify active-sibling behavior and that repeated cleanup does not fetch unrelated full file rows.
12. [x] **Feedback 14 — Other Videos quality-pagination index.** Add the `(PlexLibraryId, QualityRank)` index in `MediaOverviewOtherVideoSnapshotConfiguration.cs` using the existing configuration pattern. Generate the matching migration and model snapshot through the documented EF migration tooling. Verify the generated index and quality-sorted page results; do not claim measured speedups without measurements.

### Historical verification claims; not current regression proof

- `FolderPathMediaTypeMigrationUnitTests` passed upgrade and fresh-database cases; migration `20261009200009_NormalizeFolderPathMediaTypesAndAddOtherVideoQualityIndex` preserves IDs and directories, and its designer/model snapshot contain the `(PlexLibraryId, QualityRank)` index.
- Scheduled bandwidth, comparison invalidation, PhotoAlbum validation, move-file expectations, and Other Video quality paging tests passed.
- Cleanup, deletion, dispatcher, and path-provider test classes passed, including cross-family/shared-directory protections and category-root retention.
- Rider diagnostics reported no errors for changed files, and the affected solution build completed successfully with no retained problems.

### Feedback 6 — Sorting clarification; no implementation approval

The finding is about different ordering rules, not lost media or a required directory-name change. In the reviewed target, a library-specific direct query defaults to persisted `SortIndex`; explicit `Title` ordering uses `Title`. The snapshot resolver aliases those sorts onto the cached branch that uses `TitleRank`, which is built from `SearchTitle`.

Illustrative records, not observed user data:

| Record | SortIndex | Title | SearchTitle |
| --- | --- | --- | --- |
| A | 1 | The Zebra | Zebra |
| B | 2 | The Apple | Apple |
| C | 3 | Banana | Banana |

For ascending order:

- Direct default / `SortIndex`: A, B, C.
- Direct explicit `Title`: C, B, A.
- Cached `TitleRank` / `SearchTitle`: B, C, A.

With page size one, the default first page changes from A to B when the same request starts using the snapshot. The records remain present; ordering and page membership change. This specifically concerns the new-family sort parity finding, not the separately tracked mixed warm/cold omission issue #681.

The conservative proposed fix is to preserve the current direct-query semantics and bypass the cached path for any sort without an equivalent snapshot rank. Introducing distinct persisted ranks or unifying the sort meanings is not approved by this feedback. No sorting implementation TODO has been added.

## Current feedback — pn-ed84c2

Approved implementation is limited to comparison quiescence (feedback 5), invalid-payload move-job expectations (feedback 10), and candidate-bounded deletion and cleanup queries (feedback 12–13). Duplicate annotations refer to these same four changes.

The root-filtering finding is withdrawn under the user's valid-library-type invariant. Sorting feedback 6 requests clarification only: identical library-specific requests can switch from persisted `SortIndex` order to cached `SearchTitle` rank order after snapshot warmup, changing page membership without losing records. No sorting changes are approved.

Current verification uses actual TUnit execution through the .NET Tools MCP server and its TRX reports, not the dedicated test MCP's inconsistent pass summaries. Comparison invalidation passed 7/7, candidate-directory queries passed 55/55, move-job controls passed 63/63, deletion passed 21/21, and all cleanup families passed 110/110: 256 executed, 256 passed, none failed or skipped. Both affected test projects were compiled through MCP `Run` with `noBuild: false`. Rider reported zero errors in all ten edited code files. TRX reports are under `/tmp/reaparr-pn-ed84c2/`.

The directory helper uses a parameterized scalar-ID subquery followed by the normal EF metadata projection: these TPC-derived file entities reject entity `FromSqlRaw` queries. Candidate parameters are batched without limiting matching siblings. Unicode comparisons use the existing per-connection SQLite registration path with `StringComparer.OrdinalIgnoreCase`. No migration or index was added; the JSON path predicate can still scan table rows.

The invalid-payload fixture now supplies malformed JSON. An empty job-data map deserializes to a default payload and therefore does not exercise the existing payload-error early return. Both queue expectations are `Times.Never()`; production queue behavior remains unchanged.

Directory metadata is JSON-converted without a snapshot comparer, so fixtures explicitly mark edited entities modified before saving. The deletion fixture isolates unrelated queue metadata in a different physical directory: Music tracks and Photo images can share the same parent directory despite different task IDs.

Verification limits: Linux only; no full solution test run or measured performance comparison. Candidate filtering bounds materialization, not SQLite JSON predicate scanning. Comparison quiescence covers discovered affected jobs; independent new scheduling after discovery is not serialized by this change.
