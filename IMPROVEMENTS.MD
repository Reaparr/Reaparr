# Denied improvement proposals

This file records proposals explicitly denied by the user. Check it before presenting new improvement proposals. Match the underlying problem and proposed fix, even if the title or file location has changed. Reconsider a denied proposal only when the user explicitly asks.

Deferred proposals, requests for details, and unanswered questions do not belong here.

## I01 — 2026-10-18 — Preserve legacy genres during schema upgrade

- **Problem:** The TEMP migration drops legacy genres and library links before creating replacement tables, without copying the saved data.
- **Proposed fix:** Add deterministic preservation/backfill SQL to keep genres and library/movie/show relationships through the upgrade.
- **Affected code:** `src/Data/Migrations/20261014090024_TEMP.cs`; migration regression tests.
- **Reason:** Not provided.

## I02 — 2026-10-18 — Reject incomplete Plex media snapshots

- **Problem:** Missing totals or incomplete successful pages can become an authoritative empty/partial inventory and trigger destructive reconciliation.
- **Proposed fix:** Validate containers, totals, and completeness at ingestion while continuing to accept confirmed-empty snapshots.
- **Affected code:** `PlexLibraryMediaApi`; `GetAllMediaByTypeFromPlexApiCommandHandler`; library refresh regression tests.
- **Reason:** Not provided.

## I03 — 2026-10-18 — Make media reconciliation atomic

- **Problem:** Cancellation or write failure between destructive deletes and replacement writes can leave partially synchronized media.
- **Proposed fix:** Use the existing transaction infrastructure for complete movie/TV/music/other-video reconciliation, including related data, streams, relationships, and metrics.
- **Affected code:** `src/Application/PlexLibraries/RefreshLibraryMedia/CRUD/`; participating reconciliation helpers and tests.
- **Reason:** Not provided.

## I07 — 2026-10-18 — Make legacy integration import replay-safe

- **Problem:** Database import commits before legacy settings are cleared on disk, so an interruption or settings-write failure can cause duplicate import conflicts on restart.
- **Proposed fix:** Recognize already-imported sources, preserve their identities/keys, and finish settings cleanup without overwriting unrelated integrations.
- **Affected code:** `MigrateLegacyArrSettingsCommandHandler`; integration migration regression tests.
- **Reason:** Not provided.

## I08 — 2026-10-18 — Keep setup unfinished when its final save fails

- **Problem:** Setup continues global initialization and navigates home even when its final settings save fails.
- **Proposed fix:** Gate completion/navigation on save success and retain a visible retryable setup state on failure.
- **Affected code:** `src/AppHost/ClientApp/src/pages/setup/index.vue`; settings/setup regression tests.
- **Reason:** Not provided.

## I11 — 2026-10-18 — Preserve existing files during replacement moves

- **Problem:** A fresh replacement move deletes an existing destination before the new copy succeeds, so a failed or cancelled copy can destroy a previously good file.
- **Proposed fix:** Copy to a task-specific destination staging file and replace the old destination only after successful validation, preserving pause/resume and keep-in-downloads behavior.
- **Affected code:** `MoveDownloadFileFromFileTaskCommandHandler`; `MoveFileWithResumeCommandHandler`; replacement-move regression tests.
- **Reason:** Not provided.

## I12 — 2026-10-18 — Give generated multipart filenames distinct part names

- **Problem:** Parts with identical release metadata can receive the same generated filename and share download and destination paths.
- **Proposed fix:** Add deterministic part suffixes for generated multipart movie/TV filenames used by newly generated and restarted tasks, preserving valid original and single-part filenames.
- **Affected code:** `BasePlexMediaDataMapper`; movie/TV download-task generation and restart mapping; filename/path regression tests.
- **Reason:** Not provided.
- **Narrow exception subsequently approved:** As part of I13, the user explicitly approved collision-safe generated names for multipart TV episode tasks only, including consistent restart naming. The broader movie naming proposal remains denied.

## I15 — 2026-10-18 — Record completed DASH output size before moving

- **Problem:** A successful DASH download can retain a zero byte total, causing the later move of its nonempty output to fail.
- **Proposed fix:** Read the completed DASH output size and persist accurate total/received bytes before announcing DownloadFinished, with normal failure handling for unavailable output.
- **Affected code:** `DashPlexDownloadClient.HandleDownloadCompleted`; DASH completion-to-move regression tests.
- **Reason:** Not provided.

## I24 — 2026-10-18 — Ignore outdated Movie/TV comparison scopes in overview badges

- **Problem:** Movie/TV overview state projection can use completed scopes from libraries marked outdated and display stale ownership or upgrade badges.
- **Proposed fix:** Use existing current-library scope helpers in all four Movie/TV projection handlers, reset incoming states before early returns, and preserve current multi-library and Pending behavior.
- **Affected code:** `src/Application/PlexLibraries/Comparison/State/ApplyRemoteMovieComparisonStateCommand.cs`; owned Movie and remote/owned TV counterparts.
- **Reason:** Not provided.

## I42 — 2026-10-19 — Reject packaging output that equals its publish input

- **Problem:** Choosing the publish directory as the packaging artifact directory lets cleanup delete the files that packaging is about to consume.
- **Proposed fix:** Reject normalized artifact/publish directory equality before publishing or cleanup while preserving ordinary packaging paths.
- **Affected code:** `tools/Build/Workflows/DesktopPackageWorkflow.cs`; `tools/Build/_Shared/BuildPaths.cs`; packaging regression tests.
- **Reason:** Not provided.

## I48 — 2026-10-19 — Report failed automatic settings saves

- **Problem:** Generic settings autosave ignores failed response results, leaving local edits apparently saved until a reload restores the previous server values.
- **Proposed fix:** Inspect autosave results and show the existing error notification while preserving local edits, debounce behavior, and later user-triggered saves; no automatic retry or rollback.
- **Affected code:** `src/AppHost/ClientApp/src/store/settingsStore.ts`; autosave and browser regression tests.
- **Reason:** Not provided.

## I74 — 2026-10-21 — Make account deletion and cleanup atomic

- **Problem:** Cancellation or a database failure after deleting a Plex account can leave server/library cleanup unfinished; retrying the missing account returns before cleanup.
- **Proposed fix:** Wrap account, access-link, and orphan database deletion in the existing transaction helper, with notifications and overview rebuild after commit.
- **Affected code:** `src/Application/PlexAccounts/Delete/DeletePlexAccountByIdEndpoint.cs`; account-deletion rollback and cancellation regression tests.
- **Reason:** Not provided.

## I75 — 2026-10-21 — Remove disabled orphan libraries during account deletion

- **Problem:** Deleting the last account with access to a disabled library can leave that library behind when another account keeps its parent server accessible.
- **Proposed fix:** Ignore the enabled-library filter in the existing orphan cleanup, while preserving every library that still has an account reference.
- **Affected code:** `src/Application/PlexAccounts/Delete/DeletePlexAccountByIdEndpoint.cs`; disabled-library cleanup and retained-reference regression tests.
- **Reason:** Not provided.

## I77 — 2026-10-21 — Preserve live notifications during startup history loading

- **Problem:** A delayed startup history response can overwrite notifications already received through the live connection, hiding them until another setup or reload.
- **Proposed fix:** Merge notifications received during the pending snapshot into its result, deduplicating IDs without retaining stale pre-request rows.
- **Affected code:** `src/AppHost/ClientApp/src/store/notificationsStore.ts`; notification startup and live-delivery regression tests.
- **Reason:** Not provided.

## I88 — 2026-10-22 — Forget old Arr resource IDs when switching servers

- **Problem:** Changing an integration's server URL retains external resource IDs from the previous Arr instance, so setup or deletion can send requests for those IDs to the new instance.
- **Proposed fix:** Clear the external download-client and indexer IDs when the normalized server URL changes, preserving API-key-only changes and the current name-based resource adoption.
- **Affected code:** `src/Application/Integrations/Radarr/UpdateRadarrIntegration/UpdateRadarrIntegrationEndpoint.cs`; `src/Application/Integrations/Sonarr/UpdateSonarrIntegration/UpdateSonarrIntegrationEndpoint.cs`; setup/delete consumers and target-change regression tests.
- **Reason:** Not provided.

## Record format

For each denial, record:

- **ID and date:** proposal ID and decision date.
- **Problem:** a short, plain-English explanation.
- **Proposed fix:** the change the user rejected.
- **Affected code:** repository-relative paths or symbols.
- **Reason:** the user's reason, or “Not provided”.
