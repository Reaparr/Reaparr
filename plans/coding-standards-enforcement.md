# Backend coding standards enforcement

Status: implementation and required verification complete, with the user-accepted local formatter execution gap. Backend-only scope, 2026-11-20.

## Objective and terminology

Replace recurring manual syntax review and score-driven agent rewrites with explicit compiler-backed diagnostics, narrowly scoped repairs, and reproducible CI checks. Preserve readable control flow and existing architectural boundaries.

- **Blocking rule:** a warning/error that makes the authoritative build fail. Reaparr already treats warnings as errors.
- **Non-blocking rule:** a diagnostic that does not fail the compiler build. Configure each rule deliberately as a suggestion/information, hidden/silent, or disabled according to its value. Diagnostic severity controls reporting, not policy obligation: agents must follow applicable globally defined standards even when their diagnostics are non-blocking. Disabled diagnostics do not create cleanup requirements; hidden diagnostics do not independently mandate automatic repairs.
- **Blocking target:** an approved rule whose activation is incomplete until existing findings are resolved without unauthorized behavior changes.
- **Mechanical repair:** changes syntax without changing observable behavior, overload/member binding, evaluation order, disposal lifetime, exception behavior, or test meaning.
- **Policy change:** changes to diagnostics, severity, exclusions, suppressions, package versions, formatting policy, or validation commands.

Success is specific rule enforcement and maintainable repairs, not a complexity score. An agent must not extract helpers, add interfaces/services, or reorganize responsibilities merely to satisfy diagnostics.

## Approved scope

1. Enable build-time C# style enforcement and promote the established preferences listed below.
2. Add pinned `Meziantou.Analyzer` and `Roslynator.Analyzers` references in root `Directory.Build.props`, with private assets and explicit rule selection.
3. Apply the approved blocking/advisory tiers below to maintained backend production and test code, with narrow framework-specific exceptions.
4. Permit repository-wide mechanical corrections for approved rules. Generated code must not be hand-edited.
5. Add non-mutating CSharpier CI verification and correct backend change-detection filters for policy files.
6. Provide reproducible agent repair/validation commands and measure the final validation path.
7. Identify local review requirements and the external human-review protection prerequisite honestly.

### Exclusions

- All frontend code/configuration, TypeScript strictness, `noImplicitAny`, ESLint, and the proposed Oxlint migration.
- New custom analyzers, architecture-test frameworks, StyleCop, Sonar, InspectCode, or Qodana changes.
- Blanket analyzer presets, method/file-length limits, complexity targets, documentation boilerplate, or broad modernization.
- Public contract changes, runtime bug fixes, or other behavior changes without separate approval per concrete finding.
- Remote GitHub inspection/configuration, commits, pushes, README/CHANGELOG changes, global instruction edits, and new skills/ADRs in this implementation.
- Central package-management migration or unrelated dependency updates.

## Current evidence and starting points

Verify these against current files before implementing; investigation line numbers can drift.

| Location | Observed state | Required action |
|---|---|---|
| `Directory.Build.props` | .NET 10/C# 14; nullable; warnings as errors; `AnalysisLevel=latest`; CA2016 listed in WarningsAsErrors | Preserve these; add explicit style activation and centralized analyzer references |
| `.editorconfig` | var/modifier/accessibility/predefined-type preferences are suggestions | Promote only the approved preferences |
| `.editorconfig` | CA1827 warning, CA1860 disabled; test CA1822 surface overrides | Preserve these decisions |
| `.editorconfig` | Rider-specific syntax/formatting preferences and some disabled rules | Keep advisory/off; do not imply compiler parity |
| Backend/test `.csproj` files | CSharpier.MsBuild 1.4.0 references are repeated | Reuse existing formatter; do not migrate its references incidentally |
| `.csharpierignore` | Existing migration exclusion | Preserve; inspect generated boundaries before choosing check scope |
| `Directory.Build.props`, `docker/Dockerfile` | CI/artifact formatting bypass | Preserve non-mutating CI builds; add an independent check-only gate |
| `.github/workflows/dev-test.yml` | Compiler build exists; backend path filter primarily watches src/tests | Include relevant root backend policy files and formatter/tool configuration |
| `.github/actions/dotnet-setup/action.yml` | .NET setup and locked restore conventions | Reuse existing setup, restore, and dependency cache patterns |
| `qodana.yaml`, `.github/workflows/qodana.yml` | Separate exclusions and non-PR inspection workflow | Not a substitute for this compiler gate; leave unchanged |

No analyzer installation, compiler validation, formatter execution, or performance benchmark was performed during planning. No initial diagnostic count or compatible package version is claimed here.

## Rule contract

Use per-ID severities. Establish effective settings before claiming an SDK rule is newly enabled: SDK defaults and inherited configuration may already activate it.

### Established blocking preferences

Enable `EnforceCodeStyleInBuild=true`; retain `TreatWarningsAsErrors=true`.

| Diagnostic | Existing preference to enforce | Representative rejected -> accepted form |
|---|---|---|
| IDE0007 | `var` in all three currently configured contexts | `int count = 1;` -> `var count = 1;` where legal and semantics-preserving |
| IDE0036 | Existing modifier order | `static public ...` -> `public static ...` |
| IDE0040 | Explicit accessibility for non-interface members | An implicitly private class member -> the same member with `private` |
| IDE0049 | Explicitly disabled by superseding user decision; C# keyword preferences remain configured | Prefer `int`/`string`, but do not claim a verified IDE0049 compiler gate |
| CA2016 | Forward an in-scope cancellation token | Retain existing rule and approved deliberate non-forwarding exceptions |
| CA1827 | Prefer Any for boolean existence checks | `items.Count() > 0` -> `items.Any()` |

Resolve exact diagnostic applicability against the pinned SDK documentation. Do not invent a cast or explicit initializer type merely to force `var` where target typing is required.

Keep CA1860 disabled. Preserve optional brace-free single-statement bodies, traditional extension methods, and the current collection-expression preference. Parenthesis options marked `none` stay off. Do not promote every IDE rule by category.

Private-constant and private-static-readonly naming remains configured as currently written. Verify which naming inspections actually execute during the compiler build; IDE1006/naming behavior must not be advertised as a reliable build gate merely because the severity says warning. If the selected SDK cannot build-enforce it, report the IDE-only gap; adding a workaround/custom analyzer is not authorized.

### New blocking readability rules

Set warning severity; the existing warnings-as-errors policy makes these blocking. Review individual code fixes before applying them.

| ID | Rejected example | Preferred repair | Exceptions and repair risk |
|---|---|---|---|
| MA0071 | `if (bad) return; else Run();` | Guard followed by `Run();` | Preserve local scope and name binding. Use a coherent inline block if needed; do not extract a helper merely to remove else. |
| RCS1151 | A cast the semantic analyzer proves redundant, e.g. `(string)text` when `text` is already string | Remove the redundant cast | Confirm overload/extension/member binding. Do not remove casts necessary for semantics or use blanket text replacements. |

### Blocking correctness targets

These are approved blocking destinations, not permission to change behavior. If an existing finding requires a behavioral repair, present the finding, proposed change, regression evidence needed, and approval request. Pause activation of that affected rule until resolved. Do not suppress it or silently declare the migration complete.

| ID | Rejected example | Intended repair | Approval boundary |
|---|---|---|---|
| CA1828 | `await query.CountAsync(ct) > 0` | `await query.AnyAsync(ct)` | Only boolean emptiness checks; retain actual counts. Consider query-provider behavior and preserve tokens. |
| CA2012 | Consuming one ValueTask instance multiple times, e.g. two awaits on the same value | Consume once; use its single result, or deliberately convert once to Task if repeated observation is required | Representation/lifetime changes and new allocations are not automatic mechanical repairs. |
| MA0042 | Blocking on `.Result`/`.Wait()` where async work can be awaited | Await using the existing asynchronous call chain | Changes to signatures, exception timing, locking, or orchestration need approval; rule applicability has known context-sensitive cases. |
| MA0100 | `using var resource = Open(); return WorkAsync(resource);` | Await work before leaving the resource scope | Changes disposal lifetime and exception timing; individually approve and test. |
| MA0129 | `using (GetResourceAsync()) { ... }` when the task produces the intended disposable | Await acquisition, then dispose the produced resource | Confirm the intended resource and disposal ownership; individually approve behavioral repairs. |

### Non-blocking, rule-specific guidance

The following rules initially provide advisory guidance rather than compiler warnings. Select suggestion/information, hidden/silent, or disabled status individually when reviewing usefulness and noise; changes require policy approval. Agents must still follow applicable globally defined standards in new or changed code. A diagnostic alone does not authorize repository-wide cleanup or behavior changes; assess context and escalate conflicts rather than ignoring the standard or contorting the code.

| ID | Example signal | Preferred assessment / repair | Caveat |
|---|---|---|---|
| CA1508 | A condition is always true/false under the analyzer's dataflow | Verify reachability, then remove genuinely dead decisions | Concurrency and dataflow false positives; potentially expensive analysis. |
| CA2000 | An owned disposable is lost without disposal | Establish the real owner and lifetime before introducing disposal | Ownership transfer and factory/container conventions matter. |
| MA0140 | `if (Check()) Run(); else Run();` | Consolidate identical work only after preserving required evaluation of `Check()` | Removing the condition can remove side effects or exceptions. |
| MA0134 | `void Start() { WorkAsync(); }` | Await through an appropriate existing boundary, or use an approved existing detached-work/error-handling path | `_ = WorkAsync()` only marks intent; it does not handle rejection/failure or prove correct lifetime. Empty catches/discards are not accepted cosmetic repairs. |

### Explicitly omitted rules

Do not add MA0152 nested-await-to-Unwrap transformations, RCS1208 broad guard-return rewrites, or duplicate RCS1215/CA1508 diagnostics. Do not add MA0040 alongside CA2016, RCS1211 alongside MA0071, or CA1849 alongside MA0042 without a separately justified gap.

No blanket ConfigureAwait or async-wrapper transformations: exclude CA2007, RCS1090, RCS1174, MA0214, and MA0215 from this initiative. They can conflict or change disposal, context, and exception behavior.

## Implementation sequence

### 1. Resolve tool compatibility and effective configuration

- Load current backend and applicable test skills. Inspect native/IDE diagnostics and existing configuration before edits; do not run discovery builds.
- Select exact released analyzer package versions compatible with the installed .NET 10/C# 14 compiler and existing IDE. Verify official release/package metadata, analyzer assembly requirements, and available rule IDs. Record selected versions and why in this plan during implementation; do not use floating versions.
- Place both package references in root `Directory.Build.props`, as requested. Use `PrivateAssets=all` and appropriate analyzer/build assets, preserving normal package/tool conventions. Ensure production, tests, and backend build tooling import the policy; avoid duplicate analyzer loads and consumer/runtime asset leakage.
- Disable unapproved defaults for each added analyzer package using its documented mechanism, then opt into exact IDs. Verify actual Roslynator category behavior; a blanket global analyzer disable could accidentally disable SDK or framework/test analyzers and is not acceptable.
- Preserve existing SDK diagnostics and framework analyzers. Existing warning-as-error behavior must not be weakened to accommodate the migration.
- Update only affected package lock files using the repository's normal restore tooling and hooks. No unrelated package updates or central package-management migration.
- Discover existing local tool manifests and formatter commands. If no pinned check-capable CSharpier CLI exists, add a repository-local pinned tool manifest entry compatible with the existing 1.4.0 formatter. This reuses the approved formatter, not a replacement formatting system. Verify exact check command semantics from its versioned help/docs before encoding CI.

Completion: exact compatible versions, rule configuration, participating projects, and formatter check invocation are known; no package defaults or runtime assets leak unintentionally.

Selected implementation versions: Meziantou.Analyzer 3.0.341 and Roslynator.Analyzers 5.0.1. Both packages contain Roslyn 5.0-compatible analyzer assets for the installed .NET SDK 10.0.114; higher-version analyzer assets do not establish the minimum requirement. Meziantou defaults are disabled with its documented `MeziantouAnalysisMode=None`; Roslynator defaults are disabled with its documented category setting and approved per-ID overrides. CSharpier 1.4.0 is pinned in `.config/dotnet-tools.json`, matching existing MSBuild references. Its installed `check --help` confirms check mode does not write changes. Package restore and actual analyzer execution evidence remain separate.

Approved implementation exception: native OmniSharp diagnostics timed out on a known C# file after 120 seconds and workspace diagnostics could not resolve the project. The user authorized a compiler/analyzer inventory pass (`dotnet build Reaparr.sln --no-restore -p:CSharpier_Bypass=true`) before mechanical migration. This discovery pass is not final validation. Root analyzer references are conditioned on `.csproj` participation after an initial Docker Compose restore failure; normal solution restore then succeeded.

The user declined an inventory invocation with `TreatWarningsAsErrors=false`; retain strict promotion even during discovery. Resolve each known mechanical dependency batch before another strict inventory; do not weaken checked-in or invocation severity to bypass this decision.

Superseding user decision during implementation: explicitly disable RCS1238. Ternary operators, including nested ternaries, remain accepted syntax and must not be expanded merely to satisfy a style rule. Revert only the migration's own RCS1238 rewrites and remove its now-unnecessary RCS1238 pragmas. The earlier exception decision for EF projections is obsolete because the rule is globally disabled.

Approved MA0042 decisions: a single pragma around `ReaparrDbContextFactory.CreateAsync` preserves its synchronous-construction adapter and avoids the analyzer's recursive CreateAsync recommendation. The user separately approved replacing three fallback-probe `Cancel()` calls in `GetDirectDownloadUrlCommand` with awaited `CancelAsync()`, acknowledging callback scheduling/exception differences and requiring targeted regression coverage. Other `.Result` repairs are limited to tasks proven successfully completed (or result-only completion sources) before retrieval.

The user also approved asynchronous file writing in shared `MockDownloadService.Complete` rather than an MA0042 exception. Await writing before emitting progress/completion events, and verify the affected direct-download test behavior; this shared test-infrastructure change is not a formatting-only repair.

The strict inventory subsequently found MA0042 recommendations across test setup, assertions, disposal, database calls, and cancellation triggers. The user explicitly rejected advisory/disabled test-wide overrides and authorized migrating these test-only operations to awaited async counterparts, preserving tested APIs and assertion meaning. Verify all affected test projects after completing the migration; a test intentionally exercising a synchronous API must not be rewritten to test a different method merely to satisfy the analyzer.

### 2. Configure rules and perform approved corrections

- Add shared analyzer references/style activation and explicit `.editorconfig` severities from the rule contract.
- Keep production/test syntax consistent. Apply narrow documented framework exceptions only when justified; retain current TUnit CA1822 API-surface exceptions.
- Use safe Rider quick-fixes where suitable, followed by manual repairs. Apply only selected diagnostics to changed files; the initial approved migration may touch all maintained backend files. A code-fix provider is not proof of safety.
- Preserve generated-code boundaries. Do not classify ordinary authored code as generated to evade rules. Inspect migration/source-generator conventions; do not hand-edit emitted files or generated API output.
- Inspect diagnostics through native/IDE tooling during edits. Resolve all implementation and related test edits before final compiler/test validation.
- Escalate behavior-changing findings or readability regressions individually. An unresolved blocking target remains explicitly incomplete, not an advisory success disguised as completion.

Completion: approved mechanical corrections and necessary approved regression-test edits are finished; any behavior-changing dependency is either approved/resolved or clearly blocks activation.

### 3. Make CI and local evidence agree

- Reuse the existing .NET build job; avoid an extra full-solution build solely for the new gate.
- Preserve existing CSharpier CI/artifact bypasses so validation does not rewrite sources. Add a separate check-only invocation of the pinned CSharpier tool scoped to backend authored code and established exclusions.
- Verify the formatter scope does not inadvertently check/rewrite unrelated JSON/XML/frontend files. Preserve authored backend build-tool coverage; report formatter coverage gaps explicitly.
- Update backend change detection to include root `.editorconfig`, `Directory.Build.props`, relevant SDK/tool manifests, formatter configuration/ignore files, backend project/lock files, and changes to the gate itself. Workflow-trigger and job-level filters must agree.
- Preserve PR event coverage and existing test/build dependencies. A skipped job must not masquerade as evidence that changed policy passed.
- Do not alter frontend jobs, packaging behavior, Qodana, branch settings, or global rules.

Completion: a backend-policy-only change schedules the authoritative backend checks; build warnings fail CI; formatter drift fails without edits; ordinary frontend work is unchanged.

### 4. Validate after corrections are complete

Run the narrowest meaningful evidence once, broadening only for distinct uncovered behavior or a newly corrected failure:

1. Native/IDE diagnostics across affected authored backend files and shared configuration. Record unavailable or IDE-only coverage honestly.
2. Approved package restore/tool restore as necessary; preserve locked restore in the subsequent CI-compatible invocation.
3. Authoritative compiler/analyzer gate: existing `dotnet build Reaparr.sln --no-restore` with CI-style formatting bypass (e.g. the existing `CSharpier_Bypass` property). This execution verifies requested build-time enforcement; it is not an error-discovery build or a formatting build. Record elapsed time and diagnostic IDs.
4. The verified version-specific CSharpier check-only command over the agreed backend scope. Confirm it is non-mutating. Do not invent a command before checking the installed version.
5. Relevant backend tests through dotnet-test-mcp for code paths touched by mechanical repairs; approved behavior-changing fixes require targeted regressions. Use existing test classes/fixtures. If an integration fix is separately approved, follow the integration skill's required suite scope.
6. Validate the changed workflow with existing actionlint tooling. Review path-filter cases for policy-only, source/test, formatter/tool, and frontend-only changes; exercise those cases with existing supported workflow/filter tooling when available. If only structural review is possible, report that CI event execution is not proven locally.
7. CodeScene `code_health_review` for each changed production source file after all related edits, per repository rules. Existing findings are not cleanup permission; introduced findings need independently justified corrections, not score-driven extraction. Tests/docs/config-only changes do not require production reviews.

Provide rejection evidence that the gate really runs the selected rules, not merely that the current tree is clean. Use an existing analyzer/policy test harness if available. Otherwise, with approval during implementation, use isolated temporary project inputs outside the repository that consume the real policy and contain representative violations/accepted forms. Verify at least one SDK style, Meziantou, and Roslynator blocking diagnostic produces a nonzero compiler result; confirm advisory severities are non-blocking in effective diagnostics. Use dedicated tools/direct existing commands, not ad hoc helper scripts, and do not edit production code just to break it. If no permitted harness can establish this, report the missing negative enforcement evidence rather than asserting it passed.

Completion: observed successful compiler/formatter/test evidence, expected rejection evidence, measured timings, and accurately bounded workflow/runtime evidence. No source/config implementation is claimed from this planning task.

## Agent repair contract after rollout

1. Read the diagnostic ID and its configured intent; prefer the smallest behavior-preserving repair in the owning layer.
2. Use the approved diagnostic's code action on affected files where safe. Preserve normal formatting automation; avoid whole-repository cleanup during routine feature work.
3. Complete related repairs/tests, then run the applicable compiler/analyzer and formatter gates and relevant tests. Do not substitute a code-fix check for compiler enforcement.
4. For a disputed diagnostic, present original code, rule ID, proposed repair, concrete semantic/readability tradeoff, and recommended decision. Escalate before changing policy or behavior.
5. Report actual diagnostics/checks and their outcomes, not score improvements. Explicit discard, empty catch, generated-code markers, suppressed rules, relaxed severities, and relocated complexity are not acceptable cosmetic repairs.

A procedural agent skill may be proposed after the commands and workflow are verified. An ADR is warranted only for a consequential, hard-to-reverse tradeoff; neither document is automatically created by this implementation.

## Integrity and human approval

Local-only scope cannot guarantee mandatory human approval against an agent that can edit the repository. Do not call these controls tamper-proof.

Policy review covers root build/style configuration, new analyzer versions and private assets, tool manifests, formatter configuration, CI commands/filters, new nested configuration overrides, inline pragma/SuppressMessage suppressions, NoWarn additions, and generated-code/exclusion changes. Preserve existing legitimate exceptions; new exceptions require exact ID, narrow scope, reason, and human approval.

External prerequisite, intentionally unapplied: the repository owner must configure and verify required status checks and required human review/ownership for policy changes on protected branches, including protection of the workflow and ownership mechanism itself. Actual permissions, bypass actors, and required-review enforcement need separate GitHub inspection/authorization. CODEOWNERS or a self-modifiable local script alone does not provide this guarantee. No suppression-detection script or fake approval check is introduced here.

## Observed implementation evidence

- Pinned analyzers restored successfully, including `dotnet restore Reaparr.sln --locked-mode` (3.09 seconds). References are restricted to `.csproj` projects, private to consumers; no frontend packages were changed.
- Final strict solution build: `dotnet build Reaparr.sln --no-restore -p:CSharpier_Bypass=true -v:minimal` passed in 20.53 seconds. Earlier discovery passes were user-authorized because native C# diagnostics timed out; their failures were repaired rather than suppressed globally.
- Negative compiler probes importing the real root props and an exact copied EditorConfig reject IDE0007, IDE0036, IDE0040, MA0071, and RCS1151 with nonzero results. Accepted-form and nested-ternary probes pass. IDE0049 did not report in the installed compiler path; the user requested explicitly disabling it while retaining the preference for C# keywords. The SDK style option severities must match the intended warning policy: diagnostic-ID severities alone did not produce rejection in the initial probe.
- Advisory awaitable-result probe builds without warnings/errors; this proves non-blocking compiler behavior, not that IDE suggestions were displayed.
- Unit project evidence: Data 368/368, PlexApi 263/263, PublicApi 271/271, Build 116/116, AppHost 171/171 passed. Application initially passed 2632/2633; the sole failure was the newly added cancellation-observation test. Its registration/WaitAsync callback race was repaired by observing cancellation at the pending operation instead. The complete GetDirectDownloadUrlCommand test class then passed 9/9 in 6.093 seconds. The full Application project was not rerun after that isolated test-only repair.
- Full integration project execution timed out at the test tool's 180-second limit without results; no successful full-suite claim. Changed classes passed separately: DiskProvider 1/1 (6.396 seconds), CreateDownloadTasksEndpoint 4/4 (16.229 seconds), GetAllMediaByTypeEndpoint 5/5 (13.128 seconds).
- `actionlint .github/workflows/dev-test.yml` passed. CI trigger behavior is structurally reviewed, not exercised by a GitHub event. Reviewer claims about Git pathspec exclusion failure were not substantiated: identical lists with/without an exclusion when no matching frontend C# files exist do not establish broken exclusion semantics. No speculative filter/cache changes were made from that claim.
- Check-only formatter command is installed/documented and wired into CI, but local execution was denied by the permission rule `xargs *`. A user-approved temporary exact-command allow was added, JSONC-validated, then removed without retry when the user reported the permission configuration had not reloaded. Disk restoration is verified and JSONC validates; no formatter pass/rejection evidence is claimed and active permission reload status is unverified. No temporary allow remains in the file.

The user subsequently accepted closing formatter execution with this documented verification gap, while retaining the requirement for a full integration run. After the native `dotnet run` path was initially denied, the user reported allowing it and explicitly requested retry; the identical full-suite command then started successfully with a 1800-second outer deadline, using existing binaries and unchanged test timeouts. No further permission-configuration edits were made by the agent.

Full integration verification completed successfully through that authorized native path: **156 passed, 0 failed, 0 skipped**, test duration **3m 15.931s**, command wall time **197.82 seconds**, exit code 0. This supersedes the earlier runner-timeout limitation without altering test behavior or deadlines inside the suite.

- CodeScene reviews completed for all changed production source files. Interfaces yielded no score/content. Findings in existing large methods/dependency-heavy files were recorded without unrelated extraction or score-driven cleanup. The final middleware review reports unchanged complexity findings after the loop-keyword corrections.
- The external GitHub required-human-review prerequisite remains unapplied/unverified, as agreed. No commit, frontend migration, skill, ADR, or remote configuration change was made.

## Acceptance checklist

- [x] Only approved backend paths and affected locks/tool configuration changed; frontend untouched.
- [x] Both analyzer references are pinned in root Directory.Build.props with private assets; defaults are explicitly curated without disabling SDK/framework analyzers.
- [x] Verified SDK styles and MA0071/RCS1151 are build-enforced; RCS1238 and IDE0049 are explicitly disabled by user decision, with ternaries and C# keyword preferences preserved.
- [x] Each approved correctness target is activated and the strict compiler build is clean; concrete behavior changes/exceptions received approval.
- [x] Non-blocking rules have deliberate per-rule reporting settings; applicable global standards remain required independently of diagnostic severity.
- [x] Existing syntax choices, test meaning, generated boundaries, and contracts are preserved; separately approved changes have the bounded behavioral evidence described above.
- [x] Check-only formatter verification is wired into CI and policy-only changes trigger backend validation; local formatter execution remains unverified under the user-accepted limitation.
- [x] Compiler rejection/accepted-form evidence, clean build, affected tests, full integration suite, and elapsed times are recorded; local formatter pass/rejection evidence remains explicitly unverified under the accepted exception.
- [x] Required changed-production Code Health reviews are recorded without score gaming.
- [x] External human-review protection remains clearly marked unapplied/unverified.
- [x] No automatic commit, frontend migration, skill, ADR, README, or CHANGELOG edits.

## Primary references

- Microsoft build-time style/configuration: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview#code-style-analysis
- Diagnostic configuration: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options
- Diagnostic-scoped repair tooling: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
- Meziantou rules/configuration/fix availability: https://github.com/meziantou/Meziantou.Analyzer
- Meziantou per-rule documentation: https://github.com/meziantou/Meziantou.Analyzer/tree/main/docs/Rules
- Roslynator catalog/configuration: https://josefpihrt.github.io/docs/roslynator/analyzers/
- RCS1238: https://josefpihrt.github.io/docs/roslynator/analyzers/RCS1238
- RCS1151: https://josefpihrt.github.io/docs/roslynator/analyzers/RCS1151
- SDK quality rules: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/
- CSharpier CLI/version-specific check semantics to verify: https://csharpier.com/docs/CLI
