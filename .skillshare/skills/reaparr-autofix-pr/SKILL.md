---
name: reaparr-autofix-pr
description: Use on a Reaparr branch that already has an open PR when GitHub CI must be monitored and every dev-test.yml pipeline failure must be repaired in evidence-based batches with focused automatic commits and pushes. Never creates, readies, closes, or merges the PR.
---

# Reaparr PR Pipeline Repair

## Purpose

Repair an already-open Reaparr pull request until its GitHub Actions pipeline is green.

This skill assumes:

- it is invoked from the PR's head branch;
- an open GitHub PR already exists for that branch;
- the PR targets `dev`;
- GitHub CI is the source of truth for which pipeline jobs failed;
- pipeline repair commits should be pushed automatically so CI can rerun.

The operating loop is evidence-driven and batch-oriented:

1. find the existing PR for the current branch;
2. watch `.github/workflows/dev-test.yml` for the current remote HEAD and begin triage as soon as any job reaches a terminal failure;
3. inspect each terminally failed job's complete logs, annotations, and relevant artifacts while unrelated jobs continue;
4. diagnose all independent proven root causes from the current run before pushing; do not wait for unrelated jobs to finish before starting an evidenced repair;
5. perform a bounded failure-family sweep for equivalent occurrences;
6. fix the complete proven repair batch, using separate focused commits when appropriate;
7. complete the bounded local verification defined below; use existing CI evidence instead of rerunning a failing baseline;
8. push the verified commit batch once;
9. wait for CI on the new SHA and repeat until green or genuinely blocked.

Minimize expensive workflow runs without sacrificing evidence or verification. Commit granularity and push granularity are independent: keep commits focused, but normally push all verified repairs from one completed run together.

**Never merge the PR.** Merging is exclusively the user's responsibility.

## Mandatory CI-Driven Execution Contract

Use this sequence: **bind current SHA → read failed CI evidence → trace source → repair the complete batch → one bounded verification pass → commit and push once → watch replacement CI**.

- **Evidence first:** the failed CI run is the reproduction. When its logs/artifacts and source establish the cause, proceed directly to the fix. A local pre-fix reproduction is not a prerequisite.
- **Failure ledger:** keep a concise entry for each failed job: run/SHA, first causal error, supporting source/artifact, repair paths, and verification outcome. Every repair and diagnostic command must answer a question in that ledger.
- **Bounded verification:** finish all related implementation and test edits first. Then choose the smallest test/check that directly covers each changed behavior. Execute it once; a completed result remains valid until a relevant edit invalidates it.
- **CI owns full gates:** full test projects, solution-wide builds, frontend suites, Cypress matrices, coverage, Docker, and desktop builds run in the replacement workflow. Run a full gate locally only when the user explicitly requests it or a specific unresolved CI diagnosis cannot be answered by a narrower check. Mere local availability, shared-file changes, or a desire for extra confidence is not justification.
- **One timeout is enough:** before launching a local diagnostic or verification, set and record a finite execution timeout, or use tooling with a known finite limit. On expiry or runner unavailability, record incomplete verification and return to the repair/push/CI sequence. Do not extend the limit, switch runners, invoke direct executables, or add suites solely to obtain a local green result. Report the limitation; replacement CI must prove the gate.
- **Local failures are not new scope:** inspect their evidence once to distinguish an introduced repair regression from an unrelated issue. Correct an introduced regression, then rerun only the invalidated check. An unrelated local failure does not authorize repository repairs or a new investigation; record it and let the replacement CI establish whether it is pipeline-relevant.
- **Preserve the harness:** retain existing imports, test isolation attributes, cancellation behavior, and fixtures unless changing them is the evidenced repair. Review the complete repair diff before committing; accidental harness changes must be restored.
- **After push, watch CI:** do not start local validation while the replacement workflow runs. Diagnose new terminal CI failures from that exact SHA; green ends pipeline work immediately.

Before any local validation command, state the failed CI job, the changed behavior it verifies, and why the selected scope is minimal. If these cannot be named, omit the command. A permission denial still requires authorization for the underlying operation; this contract never authorizes another execution path.

## Strict Scope

This skill exists only to fix pipeline failures.

It may:

- inspect the existing PR and workflow runs;
- read failed-job logs, annotations, and artifacts;
- edit code, tests, configuration, workflows, and build files when necessary to fix a demonstrated CI failure;
- run only the bounded local checks allowed by the execution contract;
- create focused repair commits;
- push those commits to the current PR branch;
- wait for and monitor replacement CI runs;
- report when all applicable jobs are green.

It must not:

- create a PR;
- change the PR base branch;
- mark a draft PR ready for review;
- edit PR title/body or labels unless strictly required to diagnose CI and explicitly requested;
- resolve review conversations or dismiss reviews;
- merge, auto-merge, close, or reopen the PR;
- rebase, rewrite history, amend already-pushed commits, or force-push;
- perform unrelated refactors, cleanup, feature work, or review remediation;
- claim the PR is approved or merge-ready beyond reporting pipeline status.

A green pipeline means only that the pipeline is green. The user decides whether and when to merge.

## Authorization Model

Invoking this skill is explicit authorization to make narrowly scoped pipeline-repair commits and push them to the existing current-branch PR without asking before each commit or push.

That standing authorization covers only changes directly justified by a failure in the current GitHub CI run. It does not authorize feature changes, broad refactors, history rewriting, PR metadata changes, branch changes, or merging.

Stop and ask the user when:

- no open PR unambiguously matches the current branch;
- the PR does not target `dev`;
- the checked-out branch is not the PR head branch;
- the local branch, remote PR head, and working tree cannot be reconciled safely;
- unrelated pre-existing local changes overlap the required repair;
- a fix would materially change intended product behavior or public APIs;
- a fix requires weakening a quality gate or making an anti-bypass exception;
- credentials, permissions, runner availability, infrastructure, or external services are the root cause and cannot be repaired in the repository;
- a push is rejected and resolving it would require rebasing, resetting, force-pushing, or overwriting another person's work.

## Required Skills and Tools

Load and follow the matching Reaparr skills before inspecting or changing an affected area:

- Backend: `reaparr-backend` first.
- Backend unit tests: then `reaparr-backend-unit-tests`.
- Backend integration tests: then `reaparr-backend-integration-tests`.
- Frontend: `reaparr-frontend` first.
- Frontend Vitest: then `reaparr-frontend-unit-tests`.
- Frontend Cypress: then `reaparr-frontend-integration-tests`.
- Commits: `reaparr-git-commit`.
- Desktop builds: `reaparr-desktop-build`.

Use native repository tooling for backend and frontend work, the documented GitHub tooling for PR lookup and CI evidence, and follow `AGENTS.md` throughout.

## Canonical Pipeline

At the beginning of a repair session, reread:

- `.github/workflows/dev-test.yml`;
- local composite actions invoked by it;
- reusable Docker and desktop workflows invoked by it;
- relevant package scripts and project-specific skills.

The checked-in workflow is canonical. Do not rely on a stale gate list in this skill if the workflow has changed.

The current workflow includes these possible gates:

- `Actionlint` for `.github/**` changes, with shellcheck and pyflakes enabled;
- frontend TypeScript typecheck;
- frontend Vitest unit tests;
- frontend ESLint;
- frontend Cypress integration tests in Firefox, parallelized through Cypress Cloud;
- backend locked restore and solution build on .NET 10;
- every backend TUnit project in the workflow's unit-test matrix;
- backend integration tests;
- code-coverage report generation;
- Docker builds for amd64 and arm64;
- desktop builds for Windows, Linux, and macOS.

Trust the workflow conditions when deciding whether a skipped job is legitimately non-applicable. Docker and desktop reusable workflows are downstream pipeline gates and must be monitored even when frontend/backend path-filtered jobs are skipped.

## Non-Negotiable Repair Rules

- Fix root causes, not symptoms.
- Keep each repair diff minimal, deterministic, and directly traceable to CI evidence.
- Never install host/system packages.
- Never use `rm`, `rmdir`, or `rm -rf`; use `trash` only when cleanup is necessary.
- Never force-push.
- Preserve user-authored and unrelated local changes.
- Never commit logs, coverage output, test results, screenshots, videos, build artifacts, secrets, or unrelated generated files.
- Do not make speculative edits before examining the failed job's complete logs and relevant artifacts.
- Diagnose each terminal failure completely before editing it. Before pushing, diagnose every failure in the completed current-SHA run.
- Keep unrelated repairs in separate focused commits, then batch their push after bounded verification outcomes are recorded.
- Before pushing, perform a bounded failure-family sweep across sibling tests, mirrored implementations, direct usages, and workflow matrices. Fix only equivalent occurrences proven by source evidence.
- Do not keep pushing after CI is fully green.

### Strict anti-bypass policy

Never obtain a green pipeline by:

- skipping, disabling, deleting, quarantining, or filtering out a failing test;
- weakening meaningful assertions or replacing them with tautologies;
- blindly updating snapshots or expected data;
- adding blanket lint, type, compiler, or analyzer suppressions;
- reducing coverage or excluding changed code from coverage;
- loosening workflow paths, conditions, required jobs, architectures, or platform targets;
- inflating retries, sleeps, waits, or timeouts to hide nondeterminism;
- swallowing exceptions, ignoring exit codes, or allowing failed commands to succeed;
- changing production behavior solely to satisfy a test whose expectation is incorrect.

A test expectation may change only when CI exposed a genuine obsolete expectation and source/contracts prove the intended behavior changed. Preserve equivalent or stronger coverage and explain this in the repair commit.

## Phase 1 — Bind to the Existing PR

Before changing files:

1. read repository instructions and applicable skills;
2. record the current branch, upstream, local HEAD, remote HEAD, and working-tree status;
3. find the open PR whose head branch is the current branch;
4. verify the PR head repository/branch matches the push target;
5. verify its base is exactly `dev`;
6. record the PR number, URL, draft state, and current head SHA;
7. identify pre-existing staged, unstaged, and untracked files and preserve them.

Do not search for an arbitrary PR to repair. Do not switch to another PR branch automatically. If the current branch has no single matching open PR, stop and ask the user.

If the PR is draft and `Check-PR-Status` prevents the pipeline from running, report that as a blocker. Do not mark it ready.

## Phase 2 — Observe the Current-SHA Workflow

Do not begin by running every local suite speculatively. First wait for GitHub CI to identify actual pipeline failures.

For the current remote PR head SHA:

1. locate the `dev-test.yml` / `Execute Tests` workflow run triggered for that exact SHA;
2. if it has not appeared yet, poll until it appears;
3. if it is queued or in progress, poll continuously; when an individual job reaches a terminal failure, collect its evidence immediately while the remaining jobs continue;
4. ignore runs belonging to older SHAs;
5. record every failed, cancelled, timed-out, action-required, or unexpectedly missing job;
6. distinguish legitimate conditional skips from pipeline problems.

If the run completes green, stop without editing or pushing and report the green run URL and SHA.

If no run appears because the PR only changes a `paths-ignore` location, report that the workflow is intentionally not triggered. Do not create an empty commit or unrelated change to force CI.

## Phase 3 — Triage Failures from Evidence

For each failing job, retrieve:

- full job and step names;
- the first causal error, not only the final nonzero exit message;
- complete relevant stack traces and annotations;
- uploaded TRX, screenshots, videos, reports, or diagnostic artifacts when logs omit the causal detail;
- matrix project, platform, architecture, runtime, browser, and command;
- artifacts such as TRX, coverage diagnostics, Cypress screenshots/videos, or build logs when useful;
- whether other failures are downstream consequences of the same cause.

Group failures only when logs prove a common root cause. Prioritize:

1. workflow syntax/action failures that prevent other jobs from running;
2. restore/build failures;
3. earliest causal application or test failure;
4. independent unit/type/lint failures;
5. integration failures;
6. coverage/reporting failures;
7. Docker and desktop platform-specific failures.

For Cypress, fix the earliest uncaught application error before secondary connection or port errors. A test that passes only after CI retries remains suspect and should be investigated when it caused the failed run.

When a failure log is incomplete:

1. list all artifacts for the exact run ID;
2. do not assume the expected artifact name is present—matrix expressions may produce incomplete names such as `test-results-`;
3. associate artifacts by job, creation time, file contents, and project name;
4. inspect the relevant TRX or report for inner exceptions and full test output before editing.

Classify each failure before changing code:

- **deterministic repository failure:** reproducible locally or conclusively proven by source/configuration evidence;
- **likely repository failure:** repeated with the same causal signature and a supported code path;
- **transient infrastructure/runner failure:** download, runner, service, port, or process failure without a demonstrated repository cause;
- **unknown:** evidence is insufficient.

For transient or unknown failures, gather missing GitHub logs, annotations, and artifacts first. Rerun the affected GitHub job through the permitted mechanism when that is the narrowest way to distinguish infrastructure from repository behavior. Use a local diagnostic only for a specific unresolved question under the execution contract. A local timeout is incomplete verification, not a reason to switch runners or expand local execution.

## Phase 4 — Batched Repair Loop

Process terminal failures from the current-SHA run as one repair batch. A completed failed job may be diagnosed and repaired before unrelated jobs finish, but the batch must remain open until every applicable job in that run is terminal.

1. retrieve complete evidence for each terminal failed job before editing that job's failure;
2. use CI as the reproduction; run a local diagnostic only when a specific causal question remains unresolved;
3. trace each failing path and establish intended behavior from source and contracts;
4. perform a bounded failure-family sweep:
   - inspect sibling or mirrored tests and implementations;
   - search all direct usages of the changed contract, command, entity, or helper;
   - compare workflow matrices against files/projects that actually exist;
   - inspect equivalent insert, serialization, or mock setup patterns;
5. implement the smallest root-cause fixes for all proven occurrences;
6. select and execute the smallest behavior-covering checks under the execution contract;
7. record passed, failed, timed-out, or unavailable outcomes; leave complete gate execution to replacement CI;
8. inspect required diagnostics and the complete repair diff, preserving unrelated harness behavior;
9. create separate focused Reaparr-conformant commits for independent root causes;
10. after bounded verification and all current-run diagnoses are complete, push the commit batch once without force;
11. verify the PR head SHA updated to the final pushed commit;
12. wait for the workflow run for that new exact SHA;
13. inspect every job in the replacement run and repeat only for newly evidenced failures.

Automatic commit/push is part of this loop. Do not ask for confirmation for each narrowly scoped repair. Do not push immediately after the first fix while other failures from the same completed run remain undiagnosed or while a bounded family sweep is incomplete.

### Selecting Local Verification

This section is not a command checklist. Read the affected package scripts/project configuration and choose only the command needed for the failed job and changed behavior.

- **Test failure:** run the exact failing method, parameterized cases, class, or frontend test file that covers the repair. Use the repository-approved TUnit test tooling or Bun/Vitest/Cypress route. CI already supplies pre-fix failure evidence.
- **Type/lint/build failure:** run the narrowest relevant compiler, typecheck, or lint check after editing. Run solution-wide compilation locally only when the demonstrated failure cannot be verified more narrowly.
- **Workflow failure:** inspect the changed workflow/action references and use targeted workflow validation when available. Do not run unrelated application suites.
- **Hosted-only failure:** make the source-evidenced repair and require replacement CI to verify the affected platform, architecture, credentials, or service.
- **Timeout/unavailable check:** record the exact command/tool, result, and unverified behavior. Continue with the evidenced batch and let replacement CI verify it; if evidence is insufficient to justify a repair, report that blocker instead of guessing.

Run only invalidated checks after correcting an introduced regression. A method run followed by its class, project, solution, and then another runner is not a verification strategy. Do not use auto-fix, suppression, retry, or timeout changes to manufacture passing evidence.

### Commit discipline

Before every automatic repair commit:

- inspect staged, unstaged, and untracked files;
- stage explicit intended paths only—never use indiscriminate `git add .`;
- exclude pre-existing user changes and generated artifacts;
- inspect the staged diff;
- use `reaparr-git-commit` conventions;
- include no AI attribution;
- do not amend a pushed commit.

Use separate commits for independent root causes unless one change necessarily fixes them together. Push the complete diagnosed batch once after recording bounded verification outcomes. A documented local timeout does not require a full-suite rerun before pushing; the replacement workflow is the full-gate verifier.

### Pre-push consistency checks

Before pushing the batch:

- verify every workflow matrix project/path exists and every removed or renamed project is absent;
- verify changed workflow references, local actions, reusable workflows, package scripts, and artifact names are internally consistent;
- search for equivalent occurrences of each demonstrated defect family;
- run `git diff --check`, inspect every staged diff, and confirm unrelated working-tree changes remain unstaged;
- record each selected local check's outcome and the complete gates intentionally delegated to replacement CI.

## Phase 5 — Evaluate Every Replacement Run

After each push, start evaluation from the new SHA. Previous green jobs are stale evidence.

A replacement run can reveal:

- the original failure remains: revisit the diagnosis rather than stacking speculative edits;
- the original failure is fixed but another job now fails: collect all failures from the completed replacement run and begin a new batched repair;
- a transient infrastructure failure occurs: verify from logs; rerun through the permitted GitHub mechanism if appropriate, but do not change code to disguise infrastructure problems;
- a permission/secret/runner outage blocks CI: report the blocker and stop when repository code cannot resolve it;
- all applicable jobs pass: finish without further commits.

A cancelled, skipped, neutral, or missing job is not automatically success. Confirm from `dev-test.yml` that a skip is expected. Required checks must be conclusively successful on the current PR head SHA.

## Completion Condition

The repair loop is complete only when the workflow for the current PR head SHA shows:

- no failed, cancelled, timed-out, action-required, or unexpectedly missing required jobs;
- every path-applicable frontend/backend/actionlint job is successful;
- backend matrix children and integration/coverage jobs are successful when applicable;
- Docker amd64 and arm64 jobs are successful;
- desktop Windows, Linux, and macOS jobs are successful;
- every skipped job is explainable by the checked-in workflow condition.

Then stop. Do not merge, enable auto-merge, mark ready, resolve reviews, or make cleanup commits.

## Completion Report

Report pipeline evidence only:

- PR number and URL;
- final PR head SHA;
- each failure root cause and the focused commit that fixed it;
- failure-family sweeps performed and any equivalent occurrences fixed;
- local focused checks and complete affected gates run, including explicit timeouts or hosted-only omissions;
- final GitHub workflow run URL and status;
- any legitimately skipped jobs and their workflow conditions;
- any blocker if the pipeline could not be made green.

End with one of these precise outcomes:

- **Pipeline green — PR left open for the user to review and merge.**
- **Pipeline repair blocked — PR left open; user action required:** followed by the blocker.

Never state that the PR was merged or that the skill will merge it.
