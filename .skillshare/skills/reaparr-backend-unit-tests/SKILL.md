---
name: reaparr-backend-unit-tests
description: Use whenever creating, changing, debugging, or auditing Reaparr C# backend unit tests under tests/UnitTests. Enforces TUnit execution, whole-class coverage audits, red-first regression proof, exact persisted-state assertions, strict mock contracts, and mutation-resistant tests.
---

# Reaparr Backend Unit Tests

## Required First Skill

Load `reaparr-backend` before this skill. For command handlers also load `reaparr-command-handler-patterns`; for TUnit runner behavior load `tunit`.

## Proof Standard

A unit test is executable proof of an observable contract. Green is insufficient: a test must fail for the plausible defect it claims to prevent.

Every new or rewritten test must establish:

1. **Precondition** — the named target state exists before Act, with exact identities and relationships.
2. **Stimulus** — the production entry point runs once through the project-standard harness.
3. **Outcome** — result shape and exact error-count semantics are asserted.
4. **Mutation** — exact target rows, relationships, statuses, files, or values after Act are asserted.
5. **Interaction** — contract-bearing collaborator calls and plausible forbidden calls are verified with exact counts and arguments.
6. **Counterfactual** — identify the plausible defect the test kills and confirm the assertions would fail if the operation were omitted, mis-scoped, duplicated, or applied to the wrong entity.

There is no numeric assertion minimum. The observable contract determines the assertions.

## Mandatory Creation Workflow

### 1. Map the system before writing tests

Read the complete SUT method, validator, result models, persistence entities/configuration, and existing test class. Use symbol references for exported symbols when available.

Build a behavior matrix containing every:

- validator rejection;
- success and early-return path;
- add, update, delete, revoke, or no-op transition;
- emitted result/rapport row;
- database, filesystem, notification, event, scheduler, queue, or command side effect;
- distinct cancellation, partial-success, and dependency-failure path.

Test every distinct observable success, early-return, and mutation path. Risk-rank failure/cancellation paths; test every path capable of data loss, false success, wrong scope, inconsistent state, or incorrect downstream work. Record why any mapped failure path is omitted.

### 2. Audit the whole existing test class

When a production behavior or its test class is touched, audit every test in that class against the behavior matrix.

Each weak, stale, misleading, or duplicate test must be strengthened, split, renamed, or deleted. Do not leave weak legacy tests beside stronger replacements.

Use the existing `ShouldExpectedBehavior_WhenCondition` naming structure. The name must accurately describe what the assertions prove.

### 3. Design deterministic target state

Use stable seeds and existing `BaseTests` builders. For state-changing behavior:

- name the exact target rows;
- capture exact target identities and relationships before Act;
- add a similar non-target control row when scope is not otherwise obvious or a destructive filter is risky;
- project post-state into deterministic records and assert exact set equality;
- assert no missing, duplicate, or extra target rows.

Snapshot only rows relevant to the named scenario. Do not snapshot unrelated tables merely to inflate strictness.

### 4. Prove regression tests red first

For a reported bug, run the new focused regression against the pre-fix production behavior before accepting the fix.

Prefer an isolated worktree or equivalent isolated copy. If that is unavailable, attempt another safe method that preserves current user changes. Record the exact failing assertion.

If pre-fix execution is impossible, alert the user, state every attempted method and the exact blocker, and explicitly mark red proof unavailable. Green proof may proceed, but completion must carry that exception.

### 5. Implement with project harnesses

- TUnit: `[Test]`, `[Arguments]`, `async Task` as needed.
- Assertions: Shouldly.
- Mocks: Moq with strict, explicit contracts.
- Every test contains `// Arrange`, `// Act`, and `// Assert`.
- Data/context setup comes first; mock setups are the final Arrange step immediately before Act.
- Command-handler tests inherit `BaseCommandUnitTest<TCommand>` and call `TestHandlerExecuteAsync(...)` so validator and handler execute together.
- Endpoint tests use `BaseEndpointUnitTest<...>` or `BaseEndpointWithoutRequestUnitTest<...>` and call `TestEndpointHandleAsync(...)`.
- Use `SetupDatabase`, `SetupFileSystem`, `SetupDependencies`, `SetAppBuildInfo`, and existing shared helpers before creating local infrastructure.
- Keep scenario setup, mock contracts, and assertions visible in the test. Assertion helpers are not allowed.

### 6. Verify narrowly

Run native diagnostics for each changed test file, then run the changed test or class through `dotnet-test-mcp`. A targeted green run is sufficient unless the change affects shared test infrastructure or the user requests broader execution.

Never claim a test passed without completed execution output.

## Assertion Contract

### Result assertions

For every `Result` or `Result<T>`:

- success: assert `IsSuccess == true` and `Errors.Count == 0`;
- failure: assert `IsFailed == true` and the exact expected error count;
- cancellation: assert cancellation distinctly from ordinary failure;
- payload: assert every contract-bearing field or exact projected collection, not merely non-null/non-empty.

Assert concrete error details only when type, status, or metadata is the behavior under test. Human-readable wording is not pinned unless wording itself is a public contract.

### Persisted-state assertions

For every named target aggregate or join table:

- prove the target rows exist before Act;
- compare exact keys and relationships after Act;
- compare mutable fields owned by the operation;
- use exact collection equality after deterministic ordering or projection;
- prove deleted rows are absent and retained rows remain when scope is risky;
- distinguish deleting an access/join row from deleting the underlying entity.

`Any(...) == true`, `ShouldNotBeEmpty()`, or a count alone does not prove a collection mutation. Existential loops are not accepted for deterministic persisted collections.

### Mock interaction assertions

Every mock setup must end in `.Verifiable(Times.X())` with the exact count, followed by explicit verification in Assert.

For contract-bearing arguments use `It.Is<T>(...)` and assert:

- exact entity/account/server/library IDs;
- exact collection membership and ordering when order is contractual;
- exact flags, statuses, DTO fields, and cancellation token semantics;
- exact number of invocations.

`It.IsAny<T>()` is allowed only for a parameter irrelevant to the test's contract. Broad setup does not excuse broad verification.

Explicitly verify `Times.Never()` for plausible alternate-branch collaborators whose execution would be a bug. `VerifyNoOtherCalls()` is not required.

Mocks return expected values; they do not secretly perform production database writes or other business side effects.

### Side-effect assertions

If the SUT crosses a boundary, either observe the real effect or verify the exact delegated contract:

- database rows and relationships;
- files and directories;
- command/event payloads;
- scheduler/job requests;
- hub/notification payloads;
- external request method, route, query, headers, and body where relevant.

A test whose name claims invalidation, deletion, notification, scheduling, or persistence must directly prove that effect.

## Strict Template

```csharp
public class RevokeAccessCommandUnitTests : BaseCommandUnitTest<RevokeAccessCommand>
{
    [Test]
    public async Task ShouldRemoveOnlyTargetLibraryAccess_WhenAccountLosesServerAccess()
    {
        // Arrange
        await SetupDatabase(42001, config =>
        {
            config.PlexAccountCount = 2;
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
        });

        var dbContext = IDbContext;
        var accounts = await dbContext.PlexAccounts.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetAccountId = accounts[0].Id;
        var retainedAccountId = accounts[1].Id;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);

        var before = await dbContext.PlexAccountLibraries
            .Where(x => x.PlexLibraryId == libraryId)
            .OrderBy(x => x.PlexAccountId)
            .Select(x => new { x.PlexAccountId, x.PlexServerId, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        before.Select(x => x.PlexAccountId).ShouldBe([targetAccountId, retainedAccountId]);

        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<InvalidateLibraryCommand>(c => c.PlexLibraryId == libraryId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new RevokeAccessCommand(targetAccountId, libraryId));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);

        var after = await dbContext.PlexAccountLibraries
            .Where(x => x.PlexLibraryId == libraryId)
            .Select(x => new { x.PlexAccountId, x.PlexServerId, x.PlexLibraryId })
            .ToListAsync(CancellationToken);
        after.ShouldBe([
            new
            {
                PlexAccountId = retainedAccountId,
                PlexServerId = before.Single(x => x.PlexAccountId == retainedAccountId).PlexServerId,
                PlexLibraryId = libraryId,
            },
        ]);

        Mock.Mock<ICommandExecutor>().Verify();
    }
}
```

The template is illustrative; use real project types and assert the actual contract.

## Rejected Weak Pattern

```csharp
var expectedLibraryIds = libraries.Select(x => x.Id).ToList();

Mock.SetupCommand(() => new QueueMediaOverviewRebuildCommand())
    .ReturnsAsync(Result.Ok());

var result = await Sut.ExecuteAsync(request, CancellationToken);

result.IsSuccess.ShouldBeTrue();
Mock.Mock<ICommandExecutor>().Verify(
    x => x.Send(It.IsAny<QueueMediaOverviewRebuildCommand>(), It.IsAny<CancellationToken>()),
    Times.Once());
```

Rejected because:

- `expectedLibraryIds` is unused;
- direct handler execution bypasses validator coverage;
- success lacks exact error-count and payload assertions;
- no pre-state or exact post-state is proven;
- broad matchers do not prove the command contract;
- the claimed library effect is unobserved;
- no forbidden alternate call is checked;
- omitting or mis-scoping the mutation may leave the test green.

## Project Placement and Infrastructure

- Place tests in `tests/UnitTests/<OwningProject>.UnitTests/`, mirroring the SUT path.
- Namespace is the owning project's root test namespace, not a folder-derived namespace.
- Test file: `<SutFileName>.UnitTests.cs`; class: `<SutFileName>UnitTests`.
- Use one `var dbContext = IDbContext;` per test after `SetupDatabase(...)`.
- Resolve paths through `IPathProvider`; never hardcode sandbox paths.
- Use `MockFileSystem` through `SetupFileSystem` for filesystem behavior.
- Prefer a real `UserSettings` when setup may serialize settings.
- Complete environment/build-info overrides before resolving `Sut`.
- Prefer domain types/enums over stringly helpers.
- Mock setups remain inline per test; constructor-level shared setup is prohibited.

## Endpoint Requirements

- Request endpoint: `BaseEndpointUnitTest<TEndpoint, TRequest, TResponse>`.
- No-request endpoint: `BaseEndpointWithoutRequestUnitTest<TEndpoint, TResponse>`.
- Assert validation state before accessing `RequiredResult`.
- On validation failure assert the endpoint did not execute and downstream commands were called `Times.Never()`.
- Match the actual runtime response type, not only the OpenAPI type.
- Register only missing non-standard services through the endpoint helper's `extraServices` parameter.

## Definition of Done

All conditions are mandatory:

- complete behavior matrix produced;
- whole existing test class audited;
- each retained test has a clear counterfactual defect it kills;
- every distinct observable success/early-return/mutation path covered;
- risky failure paths covered and omissions justified;
- reported regression demonstrated red against pre-fix behavior, or explicit unavailable-red exception reported;
- exact result, target-state, collection, interaction, and non-interaction assertions present;
- no unused expected values, broad contract matchers, stale names, assertion helpers, duplicate coverage, or success-only tests remain;
- changed files have zero diagnostics;
- targeted test/class execution passes.
