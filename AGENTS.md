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
- Match existing naming conventions and folder layout.
- Avoid breaking public APIs unless explicitly coordinated.
- **Do not commit automatically.** The user reviews committed work carefully; only create commits when explicitly asked to commit. Plans should include verification checkpoints, not per-task commit steps.

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

> **`generate-ts` prerequisite:** Start the backend in development mode with `dotnet run --project src/AppHost` before executing `bun run generate-ts`.


---

## Interaction rules

- **All agents operating in this project** must ask questions using clickable multiple-choice options via the question
  tool — never plain-text lists. Bundle related questions together whenever possible. Include a recommended option when appropriate. 
- Default to **read-only exploration and analysis**. Only write when edits are explicitly needed.
- For MCP-backed work, cache exact tool names after the first successful discovery/health check in a session. Reuse known-good tools instead of repeatedly rediscovering them; only rediscover when a tool fails or a new capability is needed.
- Store all generated plans under this repository’s `plans/` directory. Do not place plans in any external `.claude`
  directory or other out-of-repo location, regardless of which AI agent creates them.

---

