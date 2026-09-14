namespace Reaparr.Application.UnitTests;

public class MediaOverviewRebuildCoordinatorUnitTests
{
    [Test]
    public async Task ShouldWaitForActiveSyncBeforeGrantingRebuildLease()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        using var syncLease = await coordinator.AcquireLibrarySyncLeaseAsync(CancellationToken.None);
        var rebuildTask = coordinator.AcquireRebuildLeaseAsync(CancellationToken.None);

        // Act
        await Task.Delay(10);

        // Assert
        rebuildTask.IsCompleted.ShouldBeFalse();
        syncLease.Dispose();
        using var rebuildLease = await rebuildTask;
        rebuildLease.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldBlockNewSyncUntilRebuildLeaseIsReleased()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        using var rebuildLease = await coordinator.AcquireRebuildLeaseAsync(CancellationToken.None);
        var syncTask = coordinator.AcquireLibrarySyncLeaseAsync(CancellationToken.None);

        // Act
        await Task.Delay(10);

        // Assert
        syncTask.IsCompleted.ShouldBeFalse();
        rebuildLease.Dispose();
        using var syncLease = await syncTask;
        syncLease.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldReleaseRebuildRequestWhenAcquisitionIsCancelled()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        using var syncLease = await coordinator.AcquireLibrarySyncLeaseAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        var rebuildTask = coordinator.AcquireRebuildLeaseAsync(cancellation.Token);
        cancellation.Cancel();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(async () => await rebuildTask);

        // Assert
        syncLease.Dispose();
        using var nextRebuildLease = await coordinator.AcquireRebuildLeaseAsync(CancellationToken.None);
        nextRebuildLease.ShouldNotBeNull();
    }

    [Test]
    public void ShouldQueueOnlyOneTrigger_WhenMultipleInvalidationsArriveBeforeExecution()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;

        // Act
        var firstRequest = coordinator.RequestRebuild(now);
        var secondRequest = coordinator.RequestRebuild(now);

        // Assert
        firstRequest.ShouldBeTrue();
        secondRequest.ShouldBeFalse();
    }

    [Test]
    public void ShouldIgnoreInvalidationsForFifteenMinutes_AfterSuccessfulRebuild()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var completedAt = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(completedAt).ShouldBeTrue();
        coordinator.BeginRebuild(completedAt).ShouldBeTrue();

        // Act
        var followUpRequired = coordinator.CompleteRebuild(succeeded: true, completedAt);
        var withinCooldown = coordinator.RequestRebuild(completedAt.AddMinutes(14).AddSeconds(59));
        var atCooldownBoundary = coordinator.RequestRebuild(completedAt.AddMinutes(15));

        // Assert
        followUpRequired.ShouldBeFalse();
        withinCooldown.ShouldBeFalse();
        atCooldownBoundary.ShouldBeTrue();
    }

    [Test]
    public void ShouldQueueOneFollowUp_WhenRebuildFailsAfterInvalidationDuringExecution()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(now).ShouldBeTrue();
        coordinator.BeginRebuild(now).ShouldBeTrue();

        // Act
        var firstRequest = coordinator.RequestRebuild(now);
        var secondRequest = coordinator.RequestRebuild(now);
        var shouldTriggerFollowUp = coordinator.CompleteRebuild(succeeded: false, now);

        // Assert
        firstRequest.ShouldBeFalse();
        secondRequest.ShouldBeFalse();
        shouldTriggerFollowUp.ShouldBeTrue();
        coordinator.RequestRebuild(now).ShouldBeFalse();
    }

    [Test]
    public void ShouldAllowRetry_WhenPendingTriggerFails()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(now).ShouldBeTrue();

        // Act
        coordinator.CancelPendingRebuild();
        var retryRequest = coordinator.RequestRebuild(now);

        // Assert
        retryRequest.ShouldBeTrue();
    }
}
