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
        // ReSharper disable once DisposeOnUsingVariable
        syncLease.Dispose();
        using var rebuildLease = await rebuildTask;
        rebuildLease.ShouldNotBeNull();
    }

    [Test]
    public async Task ShouldAllowIndependentSync_WhenRebuildIsWaitingForAnotherSync()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var firstSyncLease = await coordinator.AcquireLibrarySyncLeaseAsync(cancellation.Token);
        var rebuildTask = coordinator.AcquireRebuildLeaseAsync(cancellation.Token);
        rebuildTask.IsCompleted.ShouldBeFalse();

        // Act
        var secondSyncTask = coordinator.AcquireLibrarySyncLeaseAsync(cancellation.Token);

        // Assert
        secondSyncTask.IsCompletedSuccessfully.ShouldBeTrue();
        using var secondSyncLease = await secondSyncTask;
        rebuildTask.IsCompleted.ShouldBeFalse();
        // ReSharper disable once DisposeOnUsingVariable
        firstSyncLease.Dispose();
        rebuildTask.IsCompleted.ShouldBeFalse();
        // ReSharper disable once DisposeOnUsingVariable
        secondSyncLease.Dispose();
        using var rebuildLease = await rebuildTask.WaitAsync(cancellation.Token);
        var nextSyncTask = coordinator.AcquireLibrarySyncLeaseAsync(cancellation.Token);
        nextSyncTask.IsCompleted.ShouldBeFalse();
        // ReSharper disable once DisposeOnUsingVariable
        rebuildLease.Dispose();
        using var nextSyncLease = await nextSyncTask.WaitAsync(cancellation.Token);
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
        // ReSharper disable once DisposeOnUsingVariable
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
        // ReSharper disable once DisposeOnUsingVariable
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
        var firstRequest = coordinator.RequestRebuild(null, now);
        var secondRequest = coordinator.RequestRebuild(null, now);

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
        coordinator.RequestRebuild(null, completedAt).ShouldBeTrue();
        coordinator.BeginRebuild(completedAt).ShouldNotBeNull();

        // Act
        var followUpRequired = coordinator.CompleteRebuild(succeeded: true, completedAt);
        var withinCooldown = coordinator.RequestRebuild(null, completedAt.AddMinutes(14).AddSeconds(59));
        var atCooldownBoundary = coordinator.RequestRebuild(null, completedAt.AddMinutes(15));

        // Assert
        followUpRequired.ShouldBeNull();
        withinCooldown.ShouldBeFalse();
        atCooldownBoundary.ShouldBeTrue();
    }

    [Test]
    public void ShouldQueueOneFollowUp_WhenRebuildFailsAfterInvalidationDuringExecution()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(null, now).ShouldBeTrue();
        coordinator.BeginRebuild(now).ShouldNotBeNull();

        // Act
        var firstRequest = coordinator.RequestRebuild(null, now);
        var secondRequest = coordinator.RequestRebuild(null, now);
        var shouldTriggerFollowUp = coordinator.CompleteRebuild(succeeded: false, now);

        // Assert
        firstRequest.ShouldBeFalse();
        secondRequest.ShouldBeFalse();
        shouldTriggerFollowUp.ShouldNotBeNull();
        coordinator.RequestRebuild(null, now).ShouldBeFalse();
    }

    [Test]
    public void ShouldAllowRetry_WhenPendingTriggerFails()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(null, now).ShouldBeTrue();

        // Act
        coordinator.CancelPendingRebuild();
        var retryRequest = coordinator.RequestRebuild(null, now);

        // Assert
        retryRequest.ShouldBeTrue();
    }

    [Test]
    public void ShouldKeepDistinctMediaTypes_WhenRequestsArriveBeforeAndDuringRebuild()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(PlexMediaType.Movie, now).ShouldBeTrue();
        coordinator.RequestRebuild(PlexMediaType.MusicArtist, now).ShouldBeFalse();

        // Act
        var firstScope = coordinator.BeginRebuild(now);
        coordinator.RequestRebuild(PlexMediaType.PhotoAlbum, now).ShouldBeFalse();
        var followUpScope = coordinator.CompleteRebuild(succeeded: true, now);

        // Assert
        firstScope.ShouldNotBeNull();
        firstScope.RebuildAll.ShouldBeFalse();
        firstScope
            .MediaTypes.OrderBy(x => x)
            .ShouldBe(new[] { PlexMediaType.Movie, PlexMediaType.MusicArtist }.OrderBy(x => x));
        followUpScope.ShouldNotBeNull();
        followUpScope.RebuildAll.ShouldBeFalse();
        followUpScope.MediaTypes.ShouldBe(new[] { PlexMediaType.PhotoAlbum });
    }

    [Test]
    public async Task ShouldRetainEveryPendingMediaType_WhenRequestsArriveDuringActiveRebuild()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(PlexMediaType.Movie, now).ShouldBeTrue();
        coordinator.BeginRebuild(now).ShouldNotBeNull();

        // Act
        var requestResults = await Task.WhenAll(
            MediaOverviewRebuildCoordinator.AllMediaTypes.Select(mediaType =>
                Task.Run(() => coordinator.RequestRebuild(mediaType, now))
            )
        );
        var followUpScope = coordinator.CompleteRebuild(succeeded: false, now);

        // Assert
        requestResults.ShouldAllBe(triggered => !triggered);
        followUpScope.ShouldNotBeNull();
        followUpScope.RebuildAll.ShouldBeFalse();
        followUpScope
            .MediaTypes.OrderBy(x => x)
            .ShouldBe(MediaOverviewRebuildCoordinator.AllMediaTypes.OrderBy(x => x));
    }

    [Test]
    public void ShouldPromotePendingSpecificRequestToGlobalRequest()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = DateTimeOffset.UtcNow;
        coordinator.RequestRebuild(PlexMediaType.Movie, now).ShouldBeTrue();
        coordinator.RequestRebuild(null, now).ShouldBeFalse();

        // Act
        var scope = coordinator.BeginRebuild(now);

        // Assert
        scope.ShouldNotBeNull();
        scope.RebuildAll.ShouldBeTrue();
        scope.MediaTypes.ShouldBe(MediaOverviewRebuildCoordinator.AllMediaTypes);
    }

    [Test]
    public void ShouldRebuildEveryFamily_WhenScheduledWithoutPendingInvalidations()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

        // Act
        var scope = coordinator.BeginRebuild(now);
        var concurrentScope = coordinator.BeginRebuild(now);

        // Assert
        scope.ShouldNotBeNull();
        scope.RebuildAll.ShouldBeTrue();
        scope.MediaTypes.ShouldBe(MediaOverviewRebuildCoordinator.AllMediaTypes);
        concurrentScope.ShouldBeNull();
        coordinator.CompleteRebuild(succeeded: true, now).ShouldBeNull();
    }

    [Test]
    public void ShouldResumeScheduledRebuildsAfterCooldown_WhenInvalidationWasSuppressed()
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        coordinator.RequestRebuild(null, now).ShouldBeTrue();
        coordinator.BeginRebuild(now).ShouldNotBeNull();
        coordinator.CompleteRebuild(succeeded: true, now).ShouldBeNull();

        // Act
        var invalidationAccepted = coordinator.RequestRebuild(PlexMediaType.Movie, now.AddMinutes(1));
        var withinCooldown = coordinator.BeginRebuild(now.AddMinutes(14));
        var periodicScope = coordinator.BeginRebuild(now.AddMinutes(15));

        // Assert
        invalidationAccepted.ShouldBeFalse();
        withinCooldown.ShouldBeNull();
        periodicScope.ShouldNotBeNull();
        periodicScope.RebuildAll.ShouldBeTrue();
        periodicScope.MediaTypes.ShouldBe(MediaOverviewRebuildCoordinator.AllMediaTypes);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldRetainFailedFamilies_WhenAnotherFamilyRequiresFollowUp(bool rebuildAll)
    {
        // Arrange
        var coordinator = new MediaOverviewRebuildCoordinator();
        var now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        coordinator.RequestRebuild(rebuildAll ? null : PlexMediaType.Movie, now).ShouldBeTrue();
        var activeScope = coordinator.BeginRebuild(now);
        activeScope.ShouldNotBeNull();
        coordinator.RequestRebuild(PlexMediaType.TvShow, now).ShouldBeFalse();

        // Act
        var followUpScope = coordinator.CompleteRebuild(succeeded: false, now);
        var dispatchedScope = coordinator.BeginRebuild(now);

        // Assert
        followUpScope.ShouldNotBeNull();
        followUpScope.RebuildAll.ShouldBe(rebuildAll);
        var expectedTypes = rebuildAll
            ? MediaOverviewRebuildCoordinator.AllMediaTypes.OrderBy(x => x)
            : new[] { PlexMediaType.Movie, PlexMediaType.TvShow }.OrderBy(x => x);
        followUpScope.MediaTypes.OrderBy(x => x).ShouldBe(expectedTypes);
        dispatchedScope.ShouldNotBeNull();
        dispatchedScope.RebuildAll.ShouldBe(rebuildAll);
        dispatchedScope.MediaTypes.OrderBy(x => x).ShouldBe(expectedTypes);
        coordinator.CompleteRebuild(succeeded: true, now).ShouldBeNull();
    }
}
