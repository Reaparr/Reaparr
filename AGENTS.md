## Project overview

Reaparr is a cross-platform Plex media downloader hosted on GitHub: https://github.com/Reaparr/Reaparr

| Layer    | Stack                                                                                       |
|----------|---------------------------------------------------------------------------------------------|
| Backend  | .NET 10, FastEndpoints, EF Core, Autofac, Quartz.NET, SignalR (MessagePack), Serilog, Polly |
| Frontend | Nuxt 4 / Vue 3, Pinia, Quasar, PrimeVue                                                     |
| Testing  | TUnit, Shouldly, Moq, Bogus; Vitest, Cypress                                                |

---

## Skills usage

 - **Reaparr-specific skills take priority.** Before acting on any task in this project, check for a matching `reaparr-*` skill and load it first. These skills encode project-specific conventions that override generic guidance.
- Detect and auto-load any other applicable skills before acting on a task.
- If multiple skills apply, load all relevant ones and follow their guidance unless it conflicts with higher-priority
  instructions in this file.
- Explicitly mention which skills were loaded and used in the response.

---

## Change discipline

- Prefer **small, focused diffs**.
- Keep behavior **deterministic** (especially in tests).
- Fix **root causes** — do not add hacks to suppress symptoms.
- Respect formatting, analyzers, EditorConfig, and linters.
- Formatting is automatic for both backend and frontend code. Leave it to the configured automation: never manually reformat code or run formatting-only commands, IDE reformat actions, or builds solely to format code. Keep edits focused on the requested behavior.
- Match existing naming conventions and folder layout.
- Prefer `==` or `is` for canonical internal string comparisons. Normalize external representations at their owning boundary; reserve case-insensitive matching for external contracts that require it rather than scattering `string.Equals` or `StringComparison.OrdinalIgnoreCase` through internal code.
- Avoid breaking public APIs unless explicitly coordinated.
- **Do not commit automatically.** The user reviews committed work carefully; only create commits when explicitly asked to commit. Plans should include verification checkpoints, not per-task commit steps.

---

## CodeScene

- Use the CodeScene MCP tools for Code Health checks. After completing related implementation and test edits, run `code_health_review` on every changed production source file.
- Exclude test files, fixtures, generated test data, and shared test infrastructure from per-file Code Health reviews and scoring. Do not restructure tests to satisfy CodeScene complexity or file-length metrics.
- CodeScene is a regression detector, not an optimization target. Prioritize correct requested behavior, clear responsibilities and control flow, then minimal necessary concepts, dependencies and indirection. A higher score is neither necessary nor sufficient to accept a refactor; a passing score is not a task completion condition.
- Before a metric-driven edit, state the concrete maintenance burden and what unnecessary work, state, duplication or dependency the change will remove. Explain why the complete affected call chain will be easier to understand. A lower complexity score or shorter method alone is not a justification.
- Address introduced production regressions within the approved scope through justified simplification: remove redundant work and state, reduce nesting, consolidate duplicated decisions, and separate genuinely distinct responsibilities.
- Existing Code Health findings in touched files are not automatically in scope. Remediate only findings introduced or materially worsened by the current change, unless broader refactoring was explicitly requested. Report unrelated findings without modifying them.
- Extract a method only when it introduces a meaningful domain operation, encapsulates a coherent responsibility, or removes substantive duplication. Its name and contract must let callers understand the operation without reading its implementation. Before extracting, identify the concrete comprehension or maintenance benefit independently of the score; if there is none, keep the code inline.
- Do not extract trivial expressions, single statements, forwarding wrappers, or arbitrary fragments solely to reduce method length or complexity scores. Moving branches into helpers without improving comprehension is not a fix. Method length alone neither justifies nor disqualifies an extraction.
- Evaluate the complete affected call chain. Prefer clearer control flow, fewer unnecessary parameters and dependencies, and less navigation—not merely smaller individual methods. Keep or restore the inline version when extraction only adds indirection.
- Useful extraction must preserve existing architectural boundaries. Keep business decisions, data access, mapping, and orchestration in their established owning layers. Do not move logic into a mapper, generic utility, base class, or new service merely to improve a metric.
- Use a bounded diagnostic workflow: complete the requested implementation and tests, review changed production files, then correct only independently justified regressions. After a justified correction, recheck the affected files to record remaining findings; do not run an edit/review loop until a target score is reached. Use `code_health_score` only when a numeric comparison is useful evidence, not as an acceptance gate.
- Before accepting metric-driven structural changes, obtain an independent, read-only, score-blind review. Give the reviewer the requested behavior, scope, baseline code, diff and complete affected call chain, but withhold CodeScene scores, reports and target thresholds until its verdict is recorded. Ask it to identify unnecessary helpers, types, interfaces, dependencies and state, and whether the change removes complexity or merely relocates it. The implementer must verify findings against source; reviewer suggestions do not authorize extra scope. If independent review is unavailable, report the missing review rather than claiming self-review was independent.
- If resolving a finding would require unnecessary abstraction, artificial fragmentation, an out-of-scope redesign or a behavior change, retain the clearer implementation, report the finding and tradeoff, and stop metric-driven editing. An unresolved diagnostic finding alone does not make the requested behavior incomplete. Never change thresholds, suppress findings or weaken tests to manufacture a pass. If an actual required CI check fails, report that blocker without gaming the check.
- Place regression tests according to behavior, fixture cohesion, and existing test conventions—not class length. Extend the existing test class when the regressions belong to its responsibility. Create a sibling test file only for a genuinely distinct behavioral responsibility or materially different setup.
- Naming an existing test file in a plan does not freeze test placement unless the user explicitly imposed that constraint. Routine placement decisions within the approved behavior and test scope do not require additional approval.
- For changes containing only tests, fixtures, or documentation, skip CodeScene checks. For mixed changes, evaluate production findings only.
- Code Health supplements behavioral tests and technical review; it does not replace them, relax test requirements, or authorize commits.

---

## Safety constraints

> **NEVER** install system packages on the host unless explicitly instructed.

> **NEVER** use `rm`, `rmdir`, or `rm -rf` under any circumstances — even if the user requests it.

Use `trash` instead:

| Instead of     | Use            |
|----------------|----------------|
| `rm <file>`    | `trash <file>` |
| `rm -rf <dir>` | `trash <dir>`  |
| `rmdir <dir>`  | `trash <dir>`  |

On Linux, `trash` resolves to `gio trash` or `trash-cli`.

> **Code file deletions:** Use native filesystem tooling for source files:
> - Use `trash` for deletion; never use `rm`, `rmdir`, or `rm -rf`.
> - After deletion, update project references/imports and verify the repository state.


---
### Backend (`src/`)

If working on the backend, load the relevant available skills for project-specific conventions and .NET development best practices, prioritizing Reaparr-specific guidance. Use native repository tools (`read`, `edit`, `write`, `glob`, `lsp`, and short `bash` commands) for backend file reads, edits, searches, refactors, and diagnostics.

Backend tests should use the repository's native test tooling, preferably `dotnet-test-mcp` when available. Do not use IDE run configurations for test execution:

- `dotnet-test-mcp:list_test_projects`
- `dotnet-test-mcp:list_tests_summary`
- `dotnet-test-mcp:run_single_test`
- `dotnet-test-mcp:run_all_tests_in_class`
- `dotnet-test-mcp:run_all_tests_for_project`
- `dotnet-test-mcp:run_all_tests`

### Frontend (`src/AppHost/ClientApp/`)

If working on the frontend, then load `reaparr-frontend` skill first for project-specific frontend conventions. Use native repository tools (`read`, `edit`, `write`, `glob`, `lsp`, and short `bash` commands) for frontend file reads, edits, searches, refactors, and diagnostics.

> **Package manager:** The frontend uses **Bun exclusively** — never use npm, yarn, or pnpm.

> **API generation:** With the backend running through Rider, run `bun run generate-api-ts` from `src/AppHost/ClientApp`.


---

## Running Reaparr

Start the backend, then the frontend:

| Application | IDE / configuration | Project selector |
| --- | --- | --- |
| Backend | Rider: `Reaparr Docker Development` | `rootFolder`: absolute repository root |
| Frontend | WebStorm: `dev`, using Bun | `projectPath`: absolute `src/AppHost/ClientApp` path |

Use the IDE's `restart_run_configuration` with the selector above, `configurationName`, `mode: "run"`, and `waitForExit: false`. It starts the application or stops the existing matching instance before restarting; do not wait for these long-running processes to exit.

To stop, use `stop_run_configuration` with the same project selector and configuration name.

Use WebStorm for the frontend; only fall back to `bun run dev` from `src/AppHost/ClientApp` when WebStorm cannot launch it.

---

## Interaction rules

- **All agents operating in this project** must ask questions using clickable multiple-choice options via the question
  tool — never plain-text lists. Bundle related questions together whenever possible. Include a recommended option when appropriate. 
- Default to **read-only exploration and analysis**. Only write when edits are explicitly needed.
- For MCP-backed work, cache exact tool names after the first successful discovery/health check in a session. Reuse known-good tools instead of repeatedly rediscovering them; only rediscover when a tool fails or a new capability is needed.
- Store all generated plans under this repository’s `plans/` directory. Do not place plans in any external `.claude`
  directory or other out-of-repo location, regardless of which AI agent creates them.

---

