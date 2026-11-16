# Selected security proposals — 2026-11-16

## Decisions and boundaries

- Push completed: `origin/secruity-audit` includes `8c91c1998`, `af7ea88ba`, `3d00bc2c7`.
- Approved fixes: 12, 13, 14, 16. Complete one fix, its tests/checks, review, and separate local commit before editing the next.
- Approved validation only: 17. Establish the proxy authorization contract and local behavior; do not change authentication for this proposal without a confirmed contract and new approval.
- Declined: 8, 9, 10, 11, 15, 18, 19, 20. Recorded in IMPROVEMENTS.MD; do not reopen.
- Latest user instruction: pause implementation, commit all current work, and push the branch. No live release or publication is authorized.
- Include the previously staged `tests/UnitTests/Data.UnitTests/Configurations/VideoGenreMigration.UnitTests.cs` in its own test commit without modifying its contents.
- Do not change README or CHANGELOG files.
- Remaining work is paused: proposal 14 needs an external-provider step-up design rather than a current-local-password requirement for all users; 16 is unimplemented; validation-only 17 is incomplete. Do not resume without user direction.

## Verification prerequisites

For proposal 12, use existing Actionlint and a focused independent source review; do not expand verification tooling. Actionlint passed for both changed release workflows. The earlier capability-only sandbox attempt was blocked before execution; no target code ran and no permission configuration changed. Live GitHub publication and end-to-end release authentication are not exercised by this check.

## 12 — Scope publishing credentials

Replace publishing-token checkout authentication with the job token and `persist-credentials: false`; restrict checkout token permission to read. Preserve full history, refs, package lifecycle hooks, and existing semantic-release GH_TOKEN authentication at its release step. Resolve exact action-token permission syntax from primary docs. Verify changed workflows with maintained Actionlint route, source/primary library auth trace, and bounded nonpublishing authentication proof where available. Obtain independent security review before separate commit.

Completed as local commit `83e14ea82`. Both release workflows use read-only job-token checkout with `persist-credentials: false`; publishing `GH_TOKEN` remains scoped to semantic-release. Actionlint passed for both workflows, and independent review found no required corrections. No live release or end-to-end GitHub authentication was exercised. No additional push occurred.

## 13 — Pin vpk CLI

Choose one reviewed, compatible exact vpk version and invoke the same version in packaging and upload, preserving existing tools/Build PATH contract. Prefer the smallest existing-compatible pin over an unnecessary second invocation convention. Verify workflow syntax and pin resolution using existing local tools/dependencies without installing or fetching dependencies. Report unavailable platform checks; resolve required verification before editing. Separate review and commit.

Completed as local commit `82b3e5e3e`: both global vpk install steps pin version `1.2.161`, matching the existing local tool. NuGet confirms the stable listed package; metadata confirms .NET 9 support matching CI setup. Actionlint passed and independent review found no required corrections. No installation, package build, upload, or publication was run for this configuration-only fix.

## 14 — Require fresh authentication for credential replacement

Map backend contract, user service, frontend form/store/generated callers, and real-auth regressions. Require current local password or a specifically approved fresh-authentication route before either username/password change. Resolve the proxy-user flow before editing. Preserve normal authenticated operations and existing security-stamp revocation. Required proof: wrong/missing step-up leaves credentials unchanged; valid step-up changes intended fields; stale-cookie behavior remains correct; actual browser form behavior. Independent authentication review and separate commit.

Paused before implementation. The user selected external-provider step-up design instead of requiring the current local password for all users. No request, service, frontend, or test code was changed for this proposal. Normal local runtime verification was not authorized; the user requested pause and commit/push instead.

## 16 — Private thumbnail responses

Match the maintained photo endpoint's private/client response-cache and Cookie variation contract while preserving existing duration/query variation, passive MIME protection, and ETags. Add meaningful response-level regression proof. Run targeted backend tests and CodeScene review on changed production source. Separate review and commit.

## 17 — Validate proxy identity lifecycle

Trace header assertion -> Identity cookie -> later request -> local credential revocation, middleware ordering, enabled settings, and source-visible proxy deployment contract. Use an isolated dummy-proxy scenario only if sandbox capability permits. Distinguish observed local-cookie reuse from an unproved external authorization bypass. Return evidence, intended-contract gaps, and smallest conditional remedy without edits.

## Completion

Commit this paused handoff separately under the user's request to preserve and push all current work. Report each commit and its observed verification limits. No new runtime tests are required for this pause/commit operation; do not describe the incomplete proposals as implemented.
