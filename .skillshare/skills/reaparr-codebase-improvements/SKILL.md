---
name: reaparr-codebase-improvements
description: Use when a user requests a Reaparr codebase audit, ten actionable improvements, an improvement backlog, or user-selected fixes rather than automatic cleanup.
---

# Reaparr Codebase Improvements

Find ten worthwhile improvements, ranked **bugs first, performance second, general improvements third**. Explain what goes wrong and what changes in plain English. The user chooses the work; the agent owns complete implementation and honest verification.

**Flow:** Discover -> challenge findings -> preflight verification -> present choices -> implement one fix -> verify -> review -> commit -> next fix.

## Non-negotiable rules

- **Evidence before proposals; selection before edits; verification and review before commits.** Invoking this skill authorizes read-only investigation, not implementation. The only repository write allowed before selection is recording an explicit denial in root-level `IMPROVEMENTS.MD`.
- Consent names specific IDs, remedies, dependencies, and verification limits. Fix approval does not authorize extra scope. Defer separately authorizes the issue handling below; pushing and opening PRs require separate authorization.
- Keep one approved fix active. Complete its implementation, checks, review, and **one local commit** before editing the next fix. Preserve unrelated user work and unselected changes, including in shared files.
- Use repository instructions, relevant Reaparr skills, approved tool routes, and enforced permissions. This workflow requires no helper scripts, Python, paid service, or external-model service. A denied operation stays denied across tools and agents.
- Use the host's todo tool for execution status, not as a substitute for consent. Keep blocked or unverified changes uncommitted. The stop conditions below govern interruptions.
- Ten is a discovery target, not a quota for findings or implementation. Present fewer when fewer survive scrutiny; a clear no-findings result is valid.

## Changed-file budget — every fix round

Keep the cumulative branch diff within the user's **150-file maximum for CodeRabbit review**. A new round of fixes does not reset this budget.

- **Base:** use the current PR's actual base branch; if there is no PR, use the repository's documented integration target (`dev` for Reaparr). Record the base ref and comparison revision. If the base cannot be resolved, report the count as unknown and resolve it before implementation; never report zero or silently compare with another branch.
- **Count:** count changed-file entries in the full merge-base-to-HEAD diff, equivalent to `git diff --name-status --find-renames <base-ref>...HEAD`. Include additions, modifications, deletions, tests, documentation and generated files; count a detected rename once. This is a file count, not changed lines, commits, only this round's files, or the sum of per-commit counts. State when the comparison uses an unrefreshed local base ref.
- **Pending work:** separately calculate the projected total including staged, unstaged and untracked files intended for the PR, comparing their resulting state with the same merge base. Deduplicate overlapping paths instead of adding the working-tree count to the committed count. List intentional exclusions such as temporary workflow plans or unrelated changes kept out of the PR; never exclude a file already in the committed branch diff.
- **Report every round:** recompute and show `Changes vs <base>: N / 150 files; R remaining` at the start of every requested or resumed fix round, before its Fix choices, before each local commit, and in its completion or blocker report. Show the projected total separately when pending or selected work would change it. Record these values in the existing working record; do not rely on a count from an earlier round.
- **Preflight the selection:** before source edits, check the selected fixes' combined projected file footprint against the remaining capacity. Label estimates as estimates and refresh the actual count as files change.
- **Enforce the cap:** if the current or projected total exceeds 150, pause implementation and commits and use the question tool to resolve a smaller selection or separately reviewed branch/PR. Exactly 150 is allowed, including further fixes confined to already-counted files. Read-only investigation may continue, but approval of fixes does not waive the limit or authorize branch changes, history rewriting, dropping user work, pushing or opening PRs.

## 1. Scope and maintain one working record

Read the provided project instructions and applicable Reaparr skills. Establish the requested directories, exclusions, intended behavior, canonical patterns, and maintained build/test/runtime routes. A whole-codebase audit is not a branch-only review; recent changes are leads, not its boundary.

Read `IMPROVEMENTS.MD` when present. Compare underlying problems and remedies, not titles or paths, to exclude denied work. Consult relevant existing issues and earlier decisions; exclude fixed work and identify previously deferred or pending proposals honestly.

Keep one compact working record in host session/artifact storage. It holds:

- **Coverage:** areas and complete behaviors traced, exclusions, limits, and promising next areas.
- **Candidates:** trigger evidence, wrong result, existing protections, confidence, consumers, and unresolved assumptions.
- **Decisions:** stable ID -> approved/deferred/denied/details/pending; exact remedy, dependencies, agreed checks/limits, review mode, execution status, observed issue links and commit hashes.

Use todos for investigation, decisions, and one item per approved fix through its commit. Do not duplicate these records in several repository documents. When repository rules require a written plan, create **one compact temporary batch plan after selection** in the prescribed location, with a section per selected ID. Update the current section as needed rather than routinely creating a plan file for each small fix. **Never stage or commit generated workflow plans.** Retain them while the batch is in progress or blocked; clean them up at batch completion as described in section 6. README and CHANGELOG files remain untouched unless explicitly requested.

**Done when:** scope, exclusions, known tool limits, and the working record are explicit. Missing access is a coverage limit, not evidence of a defect.

## 2. Discover broadly, then try to disprove each finding

Start with targeted reads of entry points, boundaries, state, integrations, and relevant tests. Trace **input -> validation -> decision -> side effect -> response -> consumer**, including negative paths and outer handlers.

Keep a small coverage map across these lenses: correctness; data integrity; security/privacy; concurrency/reliability; performance/scale; API/integration contracts; test effectiveness; UX/accessibility; delivery/operations; maintainability. Mark each inspected, not applicable with a reason, or uninspected with a limit. One file does not establish subsystem coverage. Use the previous batch's unchecked areas to guide genuinely new exploration.

Delegate discovery only when independent substantial slices warrant it under host rules. Supply boundaries and evidence requirements, collect terminal results, and challenge claims yourself. Discovery is read-only; it does not run builds, lint, tests, or formatters.

### Evidence gate

For every shortlisted bug, record concrete answers before calling it confirmed:

| Required answer | Evidence needed |
|---|---|
| **Trigger** | A real user action, reachable caller, supported input, or documented external contract that can produce the condition. |
| **Wrong result** | The observable incorrect outcome, who it affects, and the intended outcome it violates. |
| **Existing protection** | Why validators, caller guards, outer error/finally handlers, transactions, scheduler rules, retries, state recalculation, or consumers do not already prevent or repair it. |

Make a separate challenge pass over the shortlist: attempt to refute the claim, follow early returns through outer handlers and eventual consumers, inspect relevant tests, and reject duplicates or intentional behavior. A TODO, suspicious line, static warning, test gap, or reviewer confidence is not proof.

For third-party assumptions, verify the relevant input/behavior with current primary documentation, source establishing the applicable contract, or sanitized runtime evidence. A synthetic test accepting unchanged parent timestamps proves conditional reconciliation behavior, **not** that Plex sends that combination. If that missing external fact determines whether the bug exists, keep it **Needs investigation** outside the actionable ten. Do not run expensive live checks merely to fill the batch; report an honest shortfall when evidence is unavailable.

Classify surviving work:

- **Confirmed defect:** trigger and complete path establish wrong behavior. State **reproduced** or **source-established**, and distinguish deployed-version assumptions.
- **Supported improvement:** concrete waste, usability cost, or maintenance consequence is evidenced without pretending current behavior is broken.
- **Needs investigation:** a material fact is unresolved; report the question separately, not as an implementation-ready fix.

Distinguish unbounded/redundant work from measured slowness. Do not invent benchmarks, incident frequency, exploitability, or speedups. Security findings need reachable attacker control and a demonstrated trust-boundary consequence.

Rank bugs by harm, confidence and reach, then performance, then concrete general improvements. Keep change risk separate from impact. Deduplicate by root cause/remedy; broaden into other relevant areas before declaring a shortfall.

**Done when:** each actionable proposal survives the trigger/result/protection challenge and has a bounded remedy. High confidence cannot replace missing evidence.

## 3. Preflight verification and prepare the proposals

For each candidate, identify the smallest proof of the promised behavior and inspect its prerequisites **before asking for Fix approval**. Use maintained configuration, existing fixtures and narrow non-mutating readiness checks. Record tools/environments as available, unavailable, or unverified; an untested tool is not known-good. Reuse known-good routes rather than repeatedly rediscovering them.

| Verification route | What it can establish |
|---|---|
| Targeted backend tests | Deterministic logic, state transitions, boundaries, and failures. |
| Hosted HTTP/protocol tests | Real Reaparr routing, authentication, command execution and wire responses; not a real third-party process. |
| Browser workflow | Actual rendered interaction, loading/error states, recovery and browser networking. |
| External-service check | Behavior with a running, identified Plex/Sonarr/Radarr version and reachable callbacks. |
| Packaging or benchmark check | The specific startup/platform or measured scale claim that tests cannot establish. |

List which checks are required versus optional. If a required fixture, credential, permission, reviewer, or runtime is missing, resolve material alternatives through the question tool **before selection**: supply/provision an isolated fixture, explicitly accept a narrower verification claim where adequate, or leave the item blocked/deferred. Disclose provisioning side effects and obtain authorization; do not use production services by default. Never silently replace a required live check with mocks or call hosted tests live compatibility proof. A missing verifier is not itself a code bug.

Assign stable IDs after the highest known ID from this run or available prior records. Once presented, IDs stay fixed. Previously proposed work retains its ID and status rather than becoming a newly discovered finding.

### Proposal template

Before the cards, report the current changed-file budget, show **Checked / Not checked / Good next areas**, then a compact ranked table: ID, title, class, impact, change risk and relative effort. Put technical detail in question previews rather than repeating it throughout chat.

Each card contains:

1. **Problem and example:** who is affected; “When [specific action/input], [wrong result]. After the fix, [expected result].” Label source-only evidence and material uncertainty.
2. **Evidence:** exact repository-relative file/lines or symbols, relevant callers/tests, trigger proof, and the protection checked during the challenge pass.
3. **Fix and boundary:** smallest complete remedy, affected consumers, and behavior intentionally unchanged.
4. **Tradeoffs:** compatibility, migrations, dependencies, abstractions, deployment actions and mutually exclusive remedies. Nothing required is hidden as an implementation detail.
5. **Proof plan:** observable invariant, exact test seam/runtime scenario, preflight status, required checks and agreed limits. Say which checks have actually run.
6. **Classification, priority, confidence, risk and effort:** small/medium/large scope, not invented time estimates; include the review route defined below.

Define a **recommended set by exact IDs and brief reasons** once readiness and tradeoffs are known. Prefer a small coherent implementation set when fixes overlap or carry substantial risk; selecting all ten remains valid. Discovery breadth does not compel implementing every finding.

**Done when:** the user can evaluate both the change and its achievable proof without a predictable mid-batch environment surprise.

## 4. Obtain explicit decisions

Use the native question tool. For a full batch, submit **ten separate single-select proposal questions in one call**, followed by the review/commit question last. A smaller supported batch gets one question per item. Respect explicit tool question-count limits, using the fewest calls; an options limit is not a question-count limit.

Each proposal question is self-contained with four options:

| Option | Authorization |
|---|---|
| **Fix Ixx** | Approve the disclosed remedy, dependencies, regression coverage and agreed verification/review route. |
| **Defer Ixx** | Leave code unchanged; authorize creating a Reaparr/Reaparr GitHub issue through `gh`, or reusing an existing matching issue. |
| **Deny Ixx** | Record the denial in `IMPROVEMENTS.MD` and exclude that underlying problem/remedy from future proposals. |
| **Details Ixx** | Explain or investigate read-only, then ask again for Fix/Defer/Deny. No implementation authority. |

The final question is: **“Would you like to review each fix before it is committed, or should I complete all selected fixes without review pauses?”** No preselected answer:

- **Review each fix:** implement, verify and perform technical review; then show this fix's changes and wait for explicit approval to commit it before starting the next.
- **Implement all selected fixes:** authorize implementation, verification, technical review and one local commit per explicitly selected fix without human review pauses.

Record the answers in the decision ledger. A default/recommendation, silence, dismissal or timeout is not consent. Do not preselect Fix/Defer when the UI cannot distinguish untouched defaults from submitted choices. An unanswered workflow mode blocks source edits; Fix alone does not authorize commits. With no selected fixes, the workflow choice authorizes nothing.

Explicit free-text selections, including “fix I02 and I07” or “implement the recommended set” when its exact IDs were already listed, are valid. Do not replay selection questions already answered. Other IDs remain pending, not denied or deferred; they need not block the explicitly selected set once its boundaries and workflow mode are resolved. “All the safe ones” without a defined set is ambiguous: ask a focused clickable question.

A material remedy/dependency/verification change needs fresh approval for the added or changed scope. Record the original approval separately from the pending decision. Save explicit denials and handle explicit deferrals even when implementation approval is incomplete. Question tool unavailable: provide the report and exact missing capability; make no source edits or assumed selections.

## 5. Implement, verify, review and commit one ID

Check the selected round's projected changed-file budget before editing. Add a todo for every approved ID in execution order, approved prerequisites first. Keep only the current fix active through its commit. Read current implementation, relevant callers, canonical tests, and actual model/harness contracts before editing; check mutability, identities, response types and fixture APIs instead of guessing them.

Implement only the selected remedy and necessary callers/contracts/regressions. Preserve unrelated changes. A bug test must distinguish the demonstrated wrong outcome from the correct one at a consumer-visible seam; mock echoes, wiring-only assertions, source-text checks, bare not-throw and arbitrary test-count targets are not proof.

### Verification

Finish all related implementation/test edits before final validation, following host rules; do not require pre-fix execution when the host disallows it. Use the preflighted route and smallest meaningful checks. Broaden for shared contracts, failures or uncovered behavior—not to inflate totals or repeat valid evidence. LSP “OK” is supplementary, not a substitute for compilation and behavioral evidence. Do not weaken tests or diagnostics to get a pass.

For fixes sharing a flow, assign a targeted **composition check** to the last relevant fix before its commit. For example, a changed start-switch flow should still work with a later changed pause handler. At batch end, run an additional cross-fix check only if a real interaction gap remains; do not automatically rerun every suite.

Record per ID: original failure/invariant -> exact check/scenario -> observed result -> limits. Prefer this over a headline test count. Use actual browsers for changed UI and runtime checks for claims deterministic tests cannot establish. Report compilation, test and tool failures distinctly.

### Risk-based technical review

Self-review every fix before committing: does it solve the original example, respect scope, preserve newer state/other owners, and have tests that catch a plausible broken implementation? Inspect the current diff through a permitted review route; remove accidental churn, not unrelated user work.

Require a **read-only independent review for high-risk changes**: destructive writes/files, download-job lifecycle, authentication/ownership, concurrency, migrations, or shared state/contracts with broad consumer impact. The reviewer checks the approved scope, diff, regression proof, surrounding protections and remaining risks. The implementer verifies findings against source; the reviewer cannot authorize extra scope. A local low-risk correction needs self-review, not an automatic reviewer fan-out. Use host-provided review capabilities; if required independent review is unavailable, apply the preflighted, explicitly agreed alternative or stop for a decision. Do not claim self-review was independent.

Address relevant review findings and rerun only affected checks before committing. A passing test is not a review waiver. Technical review applies in both workflow modes; human approval is additionally required only in **Review each fix** mode. Show that human the focused diff/walkthrough, affected files, tradeoffs and observed proof, then use the question tool for commit approval or feedback.

### Commit and advance

When verified, technically reviewed and commit-authorized, refresh and report the changed-file budget, then follow `reaparr-git-commit`. Stage only the current ID's implementation and regression coverage. Keep generated workflow plans, unrelated work and denial records out of staging and commits. Create its own local commit, record the **observed hash** in the host working record, mark its todo done, then start the next ID. No combined-ID commits, speculative later-fix edits, automatic pushes or history rewriting.

## Stop conditions and execution status

| Condition | Required action |
|---|---|
| Finding disproved or behavior already correct | Explain and record the correction; leave behavior unchanged. This is not a user denial. |
| Added scope, dependency, changed acceptance criteria or mutually exclusive remedy | Keep original consent recorded; pause this fix and obtain the exact missing decision. |
| Required edits cannot be completed | Mark **implementation blocked**, name the prerequisite and finish reachable work for this ID. |
| Edits complete but a required check is unavailable/failing | Mark **implemented but unverified; completion blocked on verification**. No commit or claim of completion. |
| Required technical/human review or commit operation unavailable/denied | Preserve changes, record the blocker and pause the queue. No equivalent-tool bypass. |

A blocked item never authorizes starting the next fix; pause any todo the tool advances automatically. Report what was tried and the exact required action. If a final composition check reveals a gap after commits, pause batch completion, identify the owning remedy, and obtain any necessary scope/commit-plan approval; do not silently combine fixes or rewrite history.

## 6. Close the batch and decide whether to continue

After all selected fixes are verified, reviewed, committed and their todos done, preserve the final decisions, proof, limits and commit hashes in the host working record. Then **trash this batch's generated plan** using repository-approved trash tooling and verify the file is gone before asking whether to continue. Keep plans for blocked or unfinished batches and leave unrelated plans untouched. Report a cleanup failure rather than claiming the batch plan was removed.

Report briefly:

- Current changed-file budget against the identified base branch, including remaining capacity and any pending projected total.
- Actual **Checked / Not checked / Good next areas**, whether ten supported proposals were found, and corrections to earlier claims.
- Per selected ID: behavior changed, affected paths, exact observed proof and limits, review outcome and commit hash (or explicit uncommitted/blocking status).
- Deferred issue URLs, recorded denials or persistence blockers, and pending decisions. Deferred/unselected work is not unfinished authorized implementation.
- Material verification limits and skills used. Lead with proved behavior, not test volume.

After every selected fix is verified, reviewed, committed and its todo done, ask through the question tool: **“Would you like me to find another ten improvements?”** Options **Find another ten** / **Finish here**, no default. With no selected/committed fixes, this post-commit question does not apply.

Only an explicit **Find another ten** starts a new read-only audit. Carry forward the compact coverage map and decision history, recheck denials/issues/current source, prioritize promising unchecked areas, preserve existing IDs, and number genuinely new proposals after the highest known ID. Previously deferred/pending work is labeled and linked, not recycled as new discoveries to fill ten. Each new batch needs fresh implementation and workflow decisions. An unanswered continuation question ends at that decision boundary; it authorizes no further work.

## Reference: deferred issues and denials

### Save an explicit deferral

Use `gh` through the permitted shell route with **`--repo Reaparr/Reaparr` on every command**. First search all issue states for the same problem/remedy:

```bash
gh issue list --repo Reaparr/Reaparr --state all --search "<relevant terms>" --json number,title,body,state,url
```

Inspect likely matches and refine capped/broad searches. Reuse a matching issue; for a closed match, read the resolution and check current source, then report its status without automatically reopening, commenting or replacing it.

When no match exists, write one issue body outside the repository through native file tools and create it:

```bash
gh issue create --repo Reaparr/Reaparr --title "<plain-English title>" --body-file "<temporary body path>"
```

Include ID/date, user-deferred status, problem/example, source evidence, proposed remedy, priority, tradeoffs and verification plan. Honor user notes: an investigation request must state the unresolved assumption and how to disprove it, not publish a confirmed-bug claim. Omit secrets, private media and addresses.

Record the actual issue number/URL and whether created or reused. Missing `gh`, authentication, network or permission leaves filing blocked; do not switch to API/browser publication. If creation outcome is uncertain, search before retrying. No issue publication follows an unanswered question or a mere Details choice.

### Record an explicit denial

Create or append root-level `IMPROVEMENTS.MD`, preserving existing entries and unrelated content. Record ID/date, plain-English problem, proposed fix, affected symbols/paths and user reason, or **Not provided**. Keep this file for denials, not deferred backlog.

A renamed/moved implementation or new evidence does not reopen a denial; only an explicit user request does. Details, deferral, silence and disproven claims are not denials. Report read/write blockers rather than claiming persistence or duplicate prevention succeeded.
