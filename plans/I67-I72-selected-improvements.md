# Approved improvements I67–I72

Approved: all six remedies and their disclosed proof plans; implement sequentially with technical review and one local commit per ID, no human review pauses. No push/PR. A required verification/review blocker pauses the queue with changes uncommitted. Prior denials and unrelated work remain excluded.

## I67 — Frontend download request contract
Regenerate the current API client through Rider backend + Bun. Migrate all download DTO producers to photo Album/Asset kinds and non-photo null. Preserve validators; no new folder/version browser. Verify frontend payloads, real hosted client-shaped preview/create requests and exact task effects, non-photo cases, isolated browser payload flow. Independent shared-contract review; CodeScene review/score/safeguard. Compose with I69 later.
Additional explicit approval: migrate the current preview request object, photo task/report names (`PhotoFolder`/`photoFolders`, removing obsolete `PhotoData`), and media-data `videoQuality` field with their consumers/tests. No new photo features. Add preview-envelope, task/report and quality-selection/display proof.
Further explicit approval: annotate `PhotoItemKind` with the existing string-enum JSON converter, preserving numeric identities and validation, after raw hosted requests exposed string binding rejection.
Further explicit approval: reuse `DownloadMediaDTOValidator` in the preview/create HTTP request validators after hosted proof showed a mismatched kind was accepted. Do not change global command middleware.

## I68 — Folder role invariants
Reject canonical role changes and incompatible edits of assigned integration/library folders; preserve ordinary names/paths and valid unassigned changes. No schema/data repair or queued-path changes. The guard checks persisted IDs1–7 and current ownership, including disabled libraries, not the client's IsDefault flag. Integration folders retain DownloadFolder category; assigned library destinations retain their existing role. Reserved IDs8–10 stay editable. Observed: 28 SQLite endpoint/resolver cases, eight request cases, and the extended enabled/disabled-library parameterized regression passed. All three files Code Health10.0; final precommit gate passed. Independent role and proof reviews approved after correcting the disabled-library finding. Ready for local commit.

## I69 — Photo album members
Consume membership-scoped children without obsolete parent equality; retain server/library/type scope. No persisted single parent or version UI changes. Real DTO/detail response, component and browser proof including shared members. Compose with I67. Self-review and CodeScene gates.

## I70 — First connection check
Assign returned status when existing status is null; retain unrelated/deleted rows and failure/loading behavior. Store regression and browser first-check green without reload. Self-review and CodeScene gates.

## I71 — Selective torrent reads
Push requested hash restriction into movie/episode queries without changing ownership/category/status/path semantics. No indexes/projection overhaul/category-collation change. SQLite materialization and exact wire proof for one/list/all/no-match/mixed-case and controls. Independent query/protocol review and CodeScene gates.

## I72 — Preloaded connection selection
Share existing in-memory chooser with current query-loading caller; list endpoint uses preloaded graph. Preserve priority and failure semantics. SQLite bounded read counts and exact selection matrix; independent shared-policy review and CodeScene gates.

## Verification limits
No live Plex/Arr, benchmarks or packaging claim. Hosted HTTP uses isolated test fixtures; browser UI uses isolated API responses. I67 Rider startup/API generation, C# diagnostics, frontend typecheck/lint and targeted tests exercised successfully. Native frontend language server unavailable. Final-phase-only validation: no pre-fix red run under host rules. Required failures pause commit/queue.

## Execution
I67 committed locally as `9742401f5`: eight raw hosted cases pass; focused frontend unit/typecheck/lint pass; all28 browser workflows pass across unchanged passing specs and corrected affected-spec rerun. Independent reviews and CodeScene precommit gate passed. Browser responses are isolated fixtures, not live Plex. I68 active; I69–I72 queued and untouched. No push.
