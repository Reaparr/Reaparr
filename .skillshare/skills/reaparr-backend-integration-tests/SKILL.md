---
name: reaparr-backend-integration-tests
description: Use whenever creating, changing, debugging, or auditing Reaparr backend integration tests under tests/IntegrationTests. Enforces whole-class audits, red-first regression proof, exact converged state, and verification of every crossed runtime boundary.
---

# Reaparr Backend Integration Tests

## Required First Skill

Load `reaparr-backend` before this skill. Load `tunit` for runner behavior and the narrowest backend feature skill for the production path under test.

## Proof Standard

An integration test proves the hosted behavior across every boundary the scenario crosses. HTTP 200, a terminal job signal, or a non-empty table alone is never sufficient.

Every new or rewritten integration test must establish:

1. exact preconditions for the named target entities and relationships;
2. one production stimulus through the real hosted boundary;
3. the exact response/result shape and error-count semantics;
4. exact converged persisted and filesystem state;
5. exact job, scheduler, notification, hub, and external-request effects that the path crosses;
6. explicit non-effects for plausible wrong branches;
7. a counterfactual defect that would make the test fail.

There is no numeric assertion minimum. The crossed boundaries determine the proof.

## Mandatory Creation Workflow

### 1. Map the full hosted path

Read the endpoint/consumer, validator, dispatched commands, jobs, persistence models, external client boundary, and existing integration test class.

Build a behavior matrix containing every:

- validation and response path;
- success, early return, partial success, cancellation, and timeout path;
- database and filesystem mutation;
- queued or scheduled operation;
- notification/hub publication;
- outbound external request and response branch;
- eventual consistency transition.

Test every distinct observable success, early-return, and mutation path. Risk-rank failures; test every failure capable of data loss, false success, incorrect HTTP contract, wrong target scope, stuck jobs, or inconsistent durable state. Record why any mapped failure is omitted.

### 2. Audit the whole existing test class

When a production behavior or integration test class is touched, audit every test in that class against the matrix.

Strengthen, split, rename, or delete weak, stale, misleading, and duplicate tests. Do not retain tests that only prove status codes, non-empty results, eventual completion, or mock configuration.

Keep `ShouldExpectedBehavior_WhenCondition` names and ensure each name accurately describes the assertions.

### 3. Arrange exact deterministic state

- Inherit from `BaseIntegrationTests`.
- Create the hosted container with `CreateContainer(...)` and the narrowest supported override seam.
- Sign in the API client before authenticated calls.
- Use deterministic `UnitTestDataConfig`, `FakeDataConfig`, `PlexApiDataConfig`, and sandbox filesystem paths.
- Name the target rows and capture exact identities/relationships before Act.
- Add similar non-target rows when destructive filtering or scope is risky.
- Keep scenario setup and assertions visible inline. Assertion helpers are prohibited.

### 4. Prove reported regressions red first

Run the focused regression against pre-fix production behavior before accepting a fix. Prefer an isolated worktree or equivalent isolated copy and record the exact failing assertion.

If pre-fix execution cannot be performed safely, alert the user with every attempted method and exact blocker. Proceed with green proof only under an explicit unavailable-red exception.

### 5. Act once through the real boundary

Use the actual HTTP endpoint, job trigger, queue consumer, or hosted service entry point. Do not replace the behavior under test with a mocked command result.

External systems may be faked at the repository-supported boundary. Their request method, route, query, headers, body, invocation count, and response branch must be exact when contract-bearing.

### 6. Wait for a specific convergence condition

For asynchronous work:

- use `WaitForDatabaseConditionAsync(...)`, `WaitForDownloadStatusAsync(...)`, or `AwaitScheduler(...)`;
- create a fresh `IReaparrDbContext` per poll so tracking cannot hide transitions;
- wait on a specific state predicate, not elapsed time;
- treat the completion signal as permission to assert, not as proof of final behavior;
- assert the complete exact final snapshot after convergence.

### 7. Verify narrowly

Run diagnostics for changed C# files, then run the changed test or class through `dotnet-test-mcp`. A targeted green run is sufficient unless shared integration infrastructure changed or the user requests broader execution.

Never claim a test passed without completed runner output.

## Every-Crossed-Boundary Assertion Contract

### API/result boundary

Assert all applicable fields:

- exact HTTP status;
- typed response presence/absence;
- `IsSuccess`/`IsFailed`/cancellation;
- exact error count;
- contract-bearing response IDs, states, counts, and collections.

Do not stop at `IsSuccessStatusCode` or `IsSuccess`.

### Database boundary

For named target aggregates and join tables:

- prove exact pre-state identities and relationships;
- assert exact post-state row sets after deterministic projection;
- assert operation-owned mutable fields;
- assert deleted rows are absent;
- assert retained rows remain where scope is risky;
- reject `ShouldNotBeEmpty`, `ShouldBeGreaterThan(0)`, count-only checks, and existential membership loops when exact deterministic state is available.

Snapshot named target rows only; unrelated tables are outside the proof unless they provide a necessary isolation control.

### Job and scheduler boundary

Assert:

- exact job/queue identity and arguments;
- exact number of scheduled/enqueued executions;
- expected state progression and terminal state;
- absence of duplicate, stale, or wrong-target work;
- durable effects produced by completed work.

A scheduler completion event does not replace final database or filesystem assertions.

### Notification and hub boundary

Assert exact refresh types, payload fields, target audience, invocation count, and ordering when contractual. Assert non-publication on validation and failure paths where publication would be incorrect.

### Filesystem boundary

Use the active test `IPathProvider` and mock/sandbox filesystem. Assert exact expected source/destination paths, contents or sizes where contractual, moves/deletions, and retained non-target paths.

Long sleeps are prohibited. Poll a specific condition and assert final state.

### External request boundary

Assert exact request method, route, query parameters, headers/authentication, serialized body, count, and ordering when relevant. Generated payloads must use valid explicit contract shapes; do not rely on null/union coercion.

## Strict Template

```csharp
[Test]
public async Task ShouldPersistOnlyReturnedLibraries_WhenRefreshCompletes()
{
    // Arrange
    await using var container = await CreateContainer(config =>
    {
        config.DatabaseOptions = database =>
        {
            database.PlexAccountCount = 2;
            database.PlexServerCount = 1;
            database.PlexMovieLibraryCount = 2;
        };
        config.HttpClientOptions = http =>
        {
            // Configure the exact external response used by this scenario.
        };
    });
    await container.ApiClient.SignIn();

    await using var beforeContext = await container.DbContextFactory.CreateAsync();
    var before = await beforeContext.PlexAccountLibraries
        .OrderBy(x => x.PlexAccountId)
        .ThenBy(x => x.PlexLibraryId)
        .Select(x => new { x.PlexAccountId, x.PlexServerId, x.PlexLibraryId })
        .ToListAsync();
    before.ShouldBe(expectedBefore);

    // Act
    var response = await container.ApiClient.RefreshLibraryAccess(request);

    // Assert
    response.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Result.IsSuccess.ShouldBeTrue();
    response.Result.Errors.Count.ShouldBe(0);

    await container.WaitForDatabaseConditionAsync(async db =>
        await db.PlexAccountLibraries.CountAsync(x => x.PlexAccountId == targetAccountId) == expectedAfter.Count);

    await using var afterContext = await container.DbContextFactory.CreateAsync();
    var after = await afterContext.PlexAccountLibraries
        .Where(x => x.PlexAccountId == targetAccountId)
        .OrderBy(x => x.PlexLibraryId)
        .Select(x => new { x.PlexAccountId, x.PlexServerId, x.PlexLibraryId })
        .ToListAsync();
    after.ShouldBe(expectedAfter);

    // Assert exact external request, scheduled work, notifications, and non-effects
    // through the concrete BaseContainer facilities used by the real scenario.
}
```

The template is illustrative; use actual repository APIs and keep every assertion inline.

## Rejected Weak Pattern

```csharp
var expectedLibraryIds = libraries.Select(x => x.Id).ToList();

var response = await client.RefreshAccess(request);
await container.SchedulerService.AwaitScheduler(jobKey);

response.Response.IsSuccessStatusCode.ShouldBeTrue();
result.IsSuccess.ShouldBeTrue();
```

Rejected because:

- `expectedLibraryIds` is unused;
- response payload and error count are unverified;
- scheduler completion is treated as final proof;
- exact converged database state is absent;
- no external request, notification, filesystem, job payload, or non-effect is proven;
- omitting or mis-scoping the production mutation may leave the test green.

## Harness Rules

- Integration project: `tests/IntegrationTests/IntegrationTests/IntegrationTests.csproj`.
- Use `UnitTestDataConfig` through `CreateContainer(...)`.
- Prefer the real hosted command path. Override `ICommandExecutor` only when command dispatch itself is outside the behavior being integrated and the override seam is intentional.
- Use a fresh context for eventual-state polling and final assertions.
- Derive paths from the active `IPathProvider`.
- Use exact deterministic counts and collections.
- Every test contains `// Arrange`, `// Act`, and `// Assert`.

## Test Quality Gate

Reject tests that:

- only assert HTTP success or a boolean result;
- only assert a parent while omitting affected children/join rows;
- use non-empty, greater-than-zero, count-only, or existential assertions where exact state is deterministic;
- compute expected values without consuming them;
- hide assertions in helpers;
- use sleeps instead of convergence predicates;
- weaken assertions, extend timeouts, serialize tests, or add retries to mask a root cause;
- duplicate existing coverage without killing a distinct plausible defect.

## Definition of Done

All conditions are mandatory:

- complete hosted behavior and boundary matrix produced;
- whole existing test class audited;
- every retained test has a counterfactual defect it kills;
- every distinct observable success/early-return/mutation path covered;
- risky failures covered and omissions justified;
- reported regression demonstrated red against pre-fix behavior, or explicit unavailable-red exception reported;
- exact response, error count, target-state identities/relationships, convergence, and every crossed-boundary effect asserted;
- plausible wrong-branch non-effects asserted;
- no weak legacy tests, unused expectations, assertion helpers, sleeps, stale names, or status-only tests remain;
- changed C# files have zero diagnostics;
- targeted test/class execution passes.
