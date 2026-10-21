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
Removed obsolete parent equality; retained server/library/photo-type guards. No backend parent/version changes. Nine component regressions, the existing real endpoint null-parent DTO case, targeted lint, and all15 Firefox lifecycle workflows passed. Shared-member album switches reset selection, and leaf/album payloads compose with I67. Self-review and CodeScene precommit gate passed. Browser proof uses isolated responses, not live Plex. Ready for local commit.

## I70 — First connection check
Assign the complete returned status on the existing row, including null-to-first-check transitions; preserve unrelated/deleted rows and failure/loading behavior. Four store regressions, targeted lint and the Firefox first-check workflow passed. The row turns green, then red on a later failure, without a connection-list refresh. Initial browser fixture navigation and lint layout were corrected, not production behavior. Self-review passed; CodeScene10.0 on all three source files. Ready for final gate and local commit.

## I71 — Selective torrent reads
Moved the normalized hash filter into both SQLite task queries; kept ownership, in-memory category comparison, status and paths unchanged. Six row-read cases passed: single hash1/0 or0/1, two-family list1/1, no match0/0, all/omitted20/20. Existing19 category/status/ownership cases and one authenticated hosted workflow with five wire requests passed. No latency benchmark, index or schema claim. Independent query and proof reviews approved. Test import/fixture complexity corrected; both test files10.0 and final CodeScene gate passed (pre-existing production status-map complexity unchanged). Ready for local commit.

## I72 — Preloaded connection selection
The list endpoint now selects from its preloaded graph through the shared server-model chooser; other callers retain database loading. Priority, failures, order and status objects are preserved. Observed: ten policy cases, 22 existing database chooser/token cases and three serialized endpoint cases passed. Exact native SQLite query count stayed one at both1/20 servers; status/identity snapshots and HTTP200 were preserved for preferred, offline, unchecked, disabled and empty graphs. Production split-query configuration was not measured. Independent policy/proof reviews cleared the final implementation. New tests and list endpoint score10.0; pre-existing token-method complexity is unchanged. Final safeguard passed, reporting improved chooser complexity and length.

## Verification limits
No live Plex/Arr, benchmarks or packaging claim. Hosted HTTP uses isolated test fixtures; browser UI uses isolated API responses. I67 Rider startup/API generation, C# diagnostics, frontend typecheck/lint and targeted tests exercised successfully. Native frontend language server unavailable. Final-phase-only validation: no pre-fix red run under host rules. Required failures pause commit/queue.

## Execution
Execute I67 → I68 → I69 → I70 → I71 → I72, one verified/reviewed local commit each. The host execution record holds observed results and commit hashes. No push. Runtime claims use isolated fixtures, not live Plex/Arr.
