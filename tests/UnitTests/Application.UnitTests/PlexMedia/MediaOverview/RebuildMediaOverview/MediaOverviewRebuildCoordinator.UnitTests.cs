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
}
