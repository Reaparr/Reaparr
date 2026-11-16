---
name: reaparr-backend-warnings-autofix
description: Use when fixing Reaparr backend compiler warnings, analyzer warnings, Rider warnings, or warnings promoted to errors. Default to every project and file in the solution, including closed and unchanged files. Present an exact warning-count overview before fixes; exclude suggestions, hints, info, and weak warnings. Requires Rider quick-fixes before manual repairs, file-owned subagents, and complete final warning coverage; not runtime log warnings or general failing-test repair.
---

# Reaparr Backend Warnings Autofix

## Objective

Fix the requested warning inventory quickly, with minimal behavior-preserving changes. Parallelize investigation and edits; serialize validation. Completion means verified resolution of the requested diagnostics, not suppression or a prettier warning count.

**Fix everything first; validate last.** After initial inventory collection, finish fixes for **all in-scope warnings** and all related caller/test edits before running any tests, builds, lint, diagnostic refreshes, or CodeScene checks. This applies to the coordinator and every subagent: no per-file, per-worker, or per-batch validation between fixes. Read-only source/reference investigation may continue. If any requested fix is blocked, finish reachable edits, report the blocker, and keep validation pending rather than testing a partial fix set.

Load `reaparr-backend` first. Load `reaparr-backend-unit-tests` or `reaparr-backend-integration-tests` before touching their tests; load other specialist skills only for the affected behavior. Repository instructions and current configuration remain authoritative; this skill does not authorize additional scope or tool access.

## Warning-only scope

- Select diagnostics by their **effective severity**, not their wording, inspection category, available quick-fix, or a tool option named `warning`. Include actual `WARNING` diagnostics and identifiable warnings promoted to errors by the current configuration.
- Exclude `WEAK WARNING`, `SUGGESTION`, `HINT`, `INFO`, and other lower-severity findings from the fix ledger, warning totals, worker assignments, and completion criteria. Leave them unchanged, even when Rider offers a safe or convenient quick-fix. A style inspection explicitly configured as a warning is eligible; a style suggestion is not.
- Tools such as `get_file_problems(errorsOnly: false)` and `lint_files(min_severity: "warning")` can return suggestions and hints too. Filter their returned severity labels explicitly. If severity is missing or ambiguous, resolve it through Rider/current configuration before counting or assigning the finding.


## Mandatory Rider MCP tooling

- Use Rider MCP for warning inventories, semantic navigation/references, quick-fix discovery/application, and final inspections. This task-specific route takes precedence over generic native/LSP-first preferences in loaded skills, subject to higher-priority instructions. Native file reads and manual edits remain available; backend tests stay on `dotnet-test-mcp`.
- Read each tool's current schema before first use, then reuse known-good tools. Pass the absolute solution root as `rootFolder` wherever supported and solution-relative file paths. Never rely on the focused IDE window or infer argument names from prose examples.
- Open the target file through Rider before `mcp__rider_get_quick_fixes` or `mcp__rider_apply_quick_fix`. Discover its exact action text at the target's current 1-based line/column; apply only the returned `fixText`, scoped to that file and line. Do not invent action names or use ambiguous whole-file matching for repeated fixes.
- `get_quick_fixes` is implementation-time action discovery, not permission to refresh the diagnostic inventory between fixes. Rediscover an action at its updated location when preceding edits invalidate it; do not reuse stale coordinates or action text.
- Use `mcp__rider_get_file_problems` with `errorsOnly: false`, or `lint_files` with explicit file paths, to inspect the full file manifest required below. Apply the warning-only severity filter to every response. Run inventory diagnostics only for initial collection and final validation, not after each fix.
- A failed project/scope inspection, timeout, or response such as “No source files found” does **not** mean Rider File Problems is unavailable or the solution is clean. Continue with explicit per-file Rider diagnostics across the complete manifest. If batch responses are partial or omit file outcomes without documented clean-result semantics, resolve those paths through smaller batches or individual File Problems calls; do not retry an unchanged failing scope by habit.
- Stop affected warning work only when no supported Rider diagnostic route can inspect the required files. Report the exact unavailable operation and uncovered files/projects, not “Rider unavailable” when only the project-wide tool failed. Native enumeration may supplement the manifest; it does not replace Rider warning evidence. An absent or unsafe built-in fix still permits manual repair in the second pass.
 
## Field notes: the painful bits

These are the route-specific traps that waste the most time in this repository:

- **Use Rider, not Rider's shadows.** Rider caches, logs, and the generic project Problems view are not the File Problems surface. Start with the mounted Rider MCP tools; a cache scrape is not diagnostic evidence.
- **Open files are a priority list, not the scope.** `get_changelists` and `get_all_open_file_paths` help preserve user edits and order work. Neither their union nor affected callers is a complete solution inventory.
- **Batch every manifest entry.** `lint_files` takes explicit solution-relative file paths, not a directory wildcard. Split the complete manifest into sequential batches, reducing batch size or using individual File Problems calls when necessary. Record each file's outcome; a timeout, missing outcome, unsupported file, or partial response is a coverage gap, not zero warnings.
- **Weak warnings are not warnings.** A TUnit “can be static” suggestion, for example, stays out of this workflow rather than becoming an opportunistic cleanup.
- **Re-read after mechanical rewrites.** Deconstruction can leave the old local-variable access behind, switch conversion can lose enum exhaustiveness, and signature/rename fixes can leave callers stale. Search references and inspect the edited block before final validation.
- **Keep public contracts boring.** A performance warning to make a public/interface member static is not permission to change its API. Leave that single diagnostic explicitly open when the contract forbids the change; never fake compliance with a no-op instance read or a suppression.
- **If a worker stalls, reassign.** Keep independent work moving, then replace a stalled slice with a smaller, exact residual inventory. Repeated status waits do not advance the fix pass.

## Autonomous execution

- Start immediately from the requested scope and available evidence. Own discovery, prioritization, delegation, implementation, and final verification end to end; do not stop after a plan, worker handoff, or completed batch.
- Use your best engineering judgment for routine choices: infer intent from current contracts, callers, tests, configuration, and established patterns. Choose the smallest behavior-preserving fix when several equivalent solutions exist. Resolve uncertain technical facts through tools or focused subagent investigation, not questions to the user.
- Do not ask permission to begin, continue, delegate, select files, choose an equivalent implementation, or run already-authorized final checks. Do not ask for information available in the repository or tool output. Brief progress statements are optional; they must not become approval gates.
- Escalate only a material decision that evidence cannot resolve safely: genuinely ambiguous required behavior, a necessary out-of-scope/API/dependency/configuration change, destructive action requiring consent, or an enforced permission blocker. Ask one focused question with a recommendation through the question tool; bundle related decisions and continue independent permitted work.
- Best judgment does not bypass permissions, expand scope, conceal blockers, or suppress diagnostics. If safe completion requires a decision, make the missing decision concrete rather than repeatedly asking broad “how should I proceed” questions.

## 1. Establish the inventory once

- Use the user's supplied diagnostics, saved CI/build output, or existing Rider diagnostics. A reported warning is evidence: do not rerun its producer just to reproduce it. Retain other diagnostic sources as evidence, but use Rider for this workflow's investigation.
- **Default scope is the entire .NET solution.** Unless the user explicitly names a narrower scope, inventory every solution project and its participating source/project/configuration files, including production, contracts, tests, shared/linked sources, and build/tooling projects. Include closed, unchanged, and previously clean files. Frontend-only code outside the .NET solution and runtime log messages remain outside this skill.
- Enumerate all projects through Rider's solution/project model and derive an explicit project-to-file manifest from their participating files. Supplement missing enumeration with the current solution/project definitions and native file discovery, accounting for linked/shared files and project include/exclude rules. Reconcile the manifest against every solution project; do not substitute a sample, open editors, changelists, or whichever files a broken project inspection happened to return. Generated-source warnings remain accounted for under the existing generator/ownership policy.
- Maintain a coverage ledger alongside the warning ledger: project, file, diagnostic source, completed/partial/timed-out/unsupported/uninspected status, and observed eligible warning count. Inspect every manifest entry through supported Rider File Problems tooling or a solution/scope inspection with explicit equivalent coverage. Finish all batches; do not stop after the first warning group, a fixed file count, or the open-file subset.
- Keep one compact warning ledger: diagnostic source and ID (or exact message when no ID is supplied), effective severity, project/file/location, message, root-cause group, owner, status, and final evidence. Count unique warning occurrences, not unique rule IDs: deduplicate repeated reports of the same source, rule/message, project/file, and location, while preserving distinct locations. Keep compiler/analyzer and IDE inspection inventories and totals distinct; do not sum overlapping sources into a supposedly unique combined total.
- Read the relevant `.editorconfig`, `Directory.Build.props`, and owning project configuration when the rule's activation or severity matters. Warnings promoted to errors remain in scope. Fix a blocking compiler error only when necessary for the requested warning work; unrelated failures remain separately reported.
- Inspect existing user changes through supported tooling and preserve them. Read the affected implementation and a relevant established example/test before choosing a fix. Use Rider semantic references for exported/shared symbol changes.
- **Present the baseline before any repair.** Before applying a quick-fix, editing source/tests, or releasing workers to make fixes, publish a warning overview containing the requested scope and diagnostic sources, `projects covered / total projects`, `files inspected / total files`, outstanding coverage gaps, the exact eligible warning count per source, the distinct affected-file count, and a table of `Rule/ID | Effective severity | Warning occurrences | Affected files`. Separate ordinary warnings from warnings promoted to errors; excluded lower-severity findings do not count toward either total. Read-only inventory collection may be delegated before this overview.
- Label counts from partial/timed-out inventories as exact **observed subtotals**, with the uninspected scope and unknown overall total explicitly stated. Do not estimate, treat missing results as zero, or begin repairs until the requested-scope inventory is complete or the user explicitly approves a narrower scope. Once the complete baseline is presented, proceed automatically; it is not a confirmation gate.
- Treat user-reported residual counts as existing evidence, not a reason to rerun diagnostics merely to confirm them. If the complete inventory differs from a reported count, explain the scope, severity, source, or snapshot difference using actual evidence. Never use a smaller open-file count to dismiss warnings in uninspected files.

**Exit:** Every requested-scope project and manifest entry has complete diagnostic coverage, the exact warning baseline and coverage totals have been presented, and every eligible warning has a concrete location and owner. Any uncovered file/project keeps collection incomplete and repairs pending; an explicit user-approved narrower scope is the only scope-reduction exception.

## 2. Partition by file ownership, not warning ID

Use subagents for independent work. The coordinator owns the ledger, shared-contract decisions, integration, and all validation.

1. Group diagnostics by common cause and owning files. Multiple diagnostic IDs in one file belong to one owner; one shared cause spanning several files belongs to one slice.
2. Identify cross-project contracts, shared test fixtures, generated sources, and files with existing user edits before assigning work. Keep overlapping/shared edits with one integration owner. Delegate independent caller investigation read-only until a shared contract decision is settled.
   Keep subagent research and fix assessment parallel, but coordinate Rider open/discover/apply sequences through one mutation owner per IDE session. Cross-file actions must own every affected file; skip uncontrolled solution-wide cleanup/fix-all actions. File ownership alone does not isolate shared editor state.
3. Dispatch genuinely independent slices together in one `tasks[]` batch. Prefer a few coherent slices over one agent per warning; size concurrency to the available workers and actual independent files. Use `scout` for unknown-location read-only discovery, `sonic` only for fully specified mechanical edits, and general task agents for fixes requiring judgment.
4. While workers act, handle coordinator-owned shared work or remaining inventory gaps. Consume delivered results without polling. Do not idle solely to wait for research that does not block useful work.
5. For a single tightly coupled file, keep the edit local and use a read-only subagent to challenge its semantics when useful. Do not manufacture parallel edits or redundant reviewers to appear concurrent. If delegation is unavailable, proceed locally and report that limitation; permission denials are not availability failures.

Give every worker this contract, filled with exact paths and diagnostics:

```text
Target: <owned files, diagnostic IDs/messages/locations, requested scope>.
Goal: Resolve only these assigned warning-severity diagnostics with minimal
behavior-preserving changes. Exclude suggestions, hints, info, and weak warnings,
including lower-severity findings exposed by quick-fix discovery.
Context: <repo root, current evidence, relevant examples, shared contracts>.
Ownership: Edit only <paths>. Request an ownership transfer before touching
another file; report newly discovered shared causes to the coordinator.
Guidance: Load reaparr-backend and applicable narrower skills. Preserve user
changes and repository conventions. Use mandatory Rider MCP tooling.
Execution: First assess built-in quick-fixes for owned diagnostics and send
exact actions and safety reasoning to the coordinator's Rider mutation owner.
Do not begin manual repairs until the coordinator closes the global quick-fix
pass. Then finish remaining implementation/test edits within owned files. Skip
tests, builds, lint, diagnostic refreshes, formatters, and CodeScene.
The coordinator validates only after ALL in-scope warning fixes and related
edits are integrated, not when an individual worker or batch finishes.
Do not commit or change warning configuration.
Return: Exact files changed, diagnostics addressed, reasoning, behavior risk,
affected callers/tests, unresolved items, and suggested targeted verification.
Do not claim a diagnostic resolved merely because the edit was applied.
```

**Exit:** Each file has at most one editor, every diagnostic is assigned, and all shared dependencies have a clear integration owner.

## 3. Fix causes, preserve contracts

Use two global implementation passes, with no tests or validation between them:

1. **Rider quick-fixes first.** Discover and assess Rider's built-in actions for every in-scope warning. Apply every applicable behavior-preserving fix through Rider MCP before starting manual warning repairs anywhere. Decline actions that suppress diagnostics, alter required contracts, or exceed scope. Record applied actions and why remaining diagnostics have no safe applicable built-in fix. A tool's confirmation proves application, not warning resolution.
2. **Manual remainder second.** Once all applicable safe quick-fixes across the inventory have been applied and all workers have reported, release the remaining warnings for manual repair. Use the smallest established correction; do not manually reproduce a suitable built-in action merely because hand-editing seems faster. Use Rider semantic refactoring for renames/signature changes and syntax-aware edits for manual codemods.

Apply a repeated fix across an owned slice only after checking its assumptions at each location. Preparing a manual repair during the first pass is allowed; applying it must wait for the global pass boundary. Complete related caller/test edits before final validation.

| Diagnostic family | Decision before editing |
| --- | --- |
| Nullable flow / initialization | Trace the producer, validator, constructor, and consumers. Establish the real invariant at its owner; preserve legitimate missing-value behavior. Do not hide uncertainty with `!`, `default!`, empty objects, or invented fallback values. |
| Async / cancellation | Preserve ordering, exception timing, cancellation ownership, and framework signatures. Respect intentional `CancellationToken.None` and the backend skill's bounded-operation rules. Avoid mechanical token forwarding, `Task.Run`, or sync-over-async. |
| Disposal / lifetime | Identify ownership and escape paths; preserve caller-owned resources, deferred enumeration, DI lifetime, and disposal order. |
| Unused members / naming / modifiers | Check semantic usages and external/reflection, serialization, DI, mapping, and framework conventions. A missing textual reference is not proof of dead code. Preserve explicit constructors and public contracts. |
| Obsolete APIs / analyzer API advice | Verify the replacement against the installed version and current primary documentation. Preserve behavior and migrate affected callers within scope; dependency upgrades require explicit approval. |
| Performance / collection warnings | Check enumeration count, EF query translation, ordering, equality, side effects, and allocations. An analyzer warning is not permission to change semantics or redesign the call chain. |
| Test warnings | Follow the owning test skill; retain assertions, determinism, and isolation. Fix fixtures to the real contract rather than broadening production behavior. |
| Generated / dependency / configuration warnings | Fix the owning source/template with its established generator when authorized. Leave generated output, migrations, and snapshots to their documented tooling. Report package/SDK/configuration changes needing approval instead of patching caches or installed dependencies. |

- Preserve warning severities and enforcement. No new pragmas, `NoWarn`, suppression attributes, exclusions, disabled analyzers, weakened assertions, or warnings-as-errors changes to manufacture a clean result.
- Preserve result/error propagation, API and persistence contracts, serialization, logging semantics, and cancellation behavior. Behavior changes or new dependencies/abstractions require the user's decision; a warning is not authorization for a redesign.
- If existing code is correct and a diagnostic conflicts with a required contract, document the conflict and the narrow decision needed. Do not silently suppress it or call it fixed.
- Add or update regression tests when a fix changes executable logic or exposes a plausible consumer-visible failure. Pure naming/import/modifier corrections need diagnostic evidence rather than new wiring-only tests.
- Finish all related implementation and test edits across workers before validation. Workers' summaries are claims, not verification; inspect the actual focused changes and affected call chain through supported tools once at integration.

**Exit:** All in-scope warning fixes and related caller/test edits are integrated, with no workers still editing the validation scope. If a requested fix remains blocked, report it and leave the final validation gate pending.

## 4. Validate after all fixes, across the complete scope

Enter this phase only after the entire requested warning inventory is fixed in source. Do not alternate warning batches with test runs. If another loaded workflow suggests intermediate tests, use this final-only gate for this warning-remediation task, subject to higher-priority instructions.

The coordinator runs the final gate; workers do not launch competing checks against shared `bin/` and `obj/` outputs.

1. Refresh Rider diagnostics across the **entire original requested-scope manifest**, not just changed files, open editors, known warning locations, or affected callers. Reconcile added/deleted/linked files and include any new participating files; keep coverage totals explicit. Use the same File Problems fallback when project-wide inspection fails. Apply the baseline severity filter: suggestions, hints, info, and weak warnings neither require fixes nor block completion. Check all remaining warnings and introduced errors/warnings, and verify original non-Rider diagnostic sources when required. If eligible warnings remain or new related errors/warnings appear, finish the entire correction set using safe Rider quick-fixes before manual repairs; keep tests pending until the diagnostic gate is clean.
2. Run the smallest behavior-covering backend tests through `dotnet-test-mcp`, following the owning test skill's mandatory gates. Serialize test/build commands. Expand coverage for shared-contract changes or uncovered risk, not as a routine full-solution sweep. Build only when required by the approved workflow or test execution, never to replace diagnostics.
3. After all implementation/test edits, run CodeScene `code_health_review` on each changed production source file; skip tests, fixtures, generated test data, and documentation. Follow repository regression-handling rules; a score alone does not justify refactoring.
4. If validation finds a new issue caused by the change, fix that cause and recheck invalidated evidence, including all files whose diagnostic coverage could be affected. Retain still-valid per-file results only when the correction cannot affect them; completion still requires full manifest coverage. Diagnose unrelated failures separately; do not rerun unchanged failures or unchanged timed-out scopes by habit.
5. Reconcile every warning and coverage ledger entry against final evidence. An edit, an empty partial response, “No source files found,” a timeout, an omitted file outcome, or an unavailable analyzer is not a pass. Name every remaining unverified file/project and the diagnostic operation required.

**Exit:** Report remaining warnings across the complete requested scope. Claim “all solution warnings fixed” only when every solution project and participating manifest entry has complete final coverage and the eligible remaining-warning count is **zero**. Any residual warning, blocked fix, or coverage gap means the solution-wide task remains incomplete, even if every edited/open file is clean.

## Delivery

Keep the final response short:

- Initial/final project and file coverage totals, baseline/resolved/remaining warning-occurrence counts per diagnostic source, and files changed. Label subset results as subset results.
- Exact diagnostic/test/CodeScene checks exercised and their observed outcomes; distinguish a failed project-wide tool from successful File Problems coverage.
- Remaining warnings, unrelated failures, approval blockers, and unverified coverage.
- Skills used. No commits, pushes, dependency/configuration changes, or unrelated cleanup unless explicitly requested.
