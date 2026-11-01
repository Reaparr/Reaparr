using System.Collections.Concurrent;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherCommonUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    public async Task ShouldNotSendPatch_WhenProgressIsUpdatedForUnknownKey()
    {
        var capturedPatches = new List<IReadOnlyCollection<DownloadPatchDTO>>();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) => capturedPatches.Add(upserts)
            )
            .Returns(Task.CompletedTask);

        var sut = Sut;
        sut.OnProgressUpdated(
            new DownloadTaskKey
            {
                Id = Guid.NewGuid(),
                PlexServerId = 1,
                PlexLibraryId = 1,
                Type = DownloadTaskType.EpisodeData,
            },
            new DownloadTaskProgress
            {
                DataTotal = 10,
                DataReceived = 5,
                Percentage = 50,
                DownloadSpeed = 1,
            }
        );

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(1500, CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        capturedPatches.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPersistTerminalProgressAfterBufferedEstimate_WhenFinalProgressArrives()
    {
        // Arrange
        await SetupDatabase(
            84332,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = downloadTask.ToKey();
        var capturedPatches = new List<IReadOnlyCollection<DownloadPatchDTO>>();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) => capturedPatches.Add(upserts)
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());

        var finalProgress = new DownloadTaskProgress
        {
            DataTotal = 4_321,
            DataReceived = 4_321,
            Percentage = 100m,
            DownloadSpeed = 0,
            TimeRemaining = 0,
        };
        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 999,
                DataReceived = 500,
                Percentage = 50m,
                DownloadSpeed = 100,
                TimeRemaining = 12,
            }
        );

        // Act
        var result = await sut.PersistProgressAsync(key, finalProgress, cancellationToken: CancellationToken.None);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 111,
                DataReceived = 100,
                Percentage = 1m,
                DownloadSpeed = 2,
                TimeRemaining = 30,
            }
        );
        await sut.StopAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var persisted = await IDbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.DataTotal.ShouldBe(4_321);
        persisted.DataReceived.ShouldBe(4_321);
        persisted.Percentage.ShouldBe(100m);
        persisted.DownloadSpeed.ShouldBe(0);
        persisted.TimeRemaining.ShouldBe(0);
        capturedPatches
            .SelectMany(x => x)
            .ShouldContain(x =>
                x.Id == key.Id && x.DataTotal == 4_321 && x.DataReceived == 4_321 && x.Percentage == 100m
            );
        Mock.Mock<IDownloadHubService>().Verify();
    }

    [Test]
    public async Task ShouldPublishTransferProgressAfterTerminalProgress_WhenLateDownloadEstimateArrives()
    {
        // Arrange
        await SetupDatabase(
            84333,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexAccountCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = downloadTask.ToKey();
        var capturedPatches = new List<IReadOnlyCollection<DownloadPatchDTO>>();
        var transferPatchEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifyRequested = 0;
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) =>
                {
                    capturedPatches.Add(upserts);
                    if (
                        Volatile.Read(ref notifyRequested) != 0
                        && upserts.Any(x =>
                            x.Id == key.Id && x.DataTotal == 4_321 && x.DataReceived == 4_321 && x.Percentage == 100m
                        )
                    )
                        transferPatchEntered.TrySetResult();
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());

        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);
        var finalProgress = new DownloadTaskProgress
        {
            DataTotal = 4_321,
            DataReceived = 4_321,
            Percentage = 100m,
            DownloadSpeed = 0,
            TimeRemaining = 0,
        };

        // Act
        var result = await sut.PersistProgressAsync(key, finalProgress, cancellationToken: CancellationToken.None);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 999,
                DataReceived = 500,
                Percentage = 50m,
                DownloadSpeed = 100,
                TimeRemaining = 12,
            }
        );
        Volatile.Write(ref notifyRequested, 1);
        sut.NotifyFileTransferProgress(key);
        await transferPatchEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var persisted = await IDbContext.GetDownloadTaskFileAsync(key, CancellationToken);
        persisted.ShouldNotBeNull();
        persisted.DataTotal.ShouldBe(4_321);
        persisted.DataReceived.ShouldBe(4_321);
        persisted.Percentage.ShouldBe(100m);
        capturedPatches
            .SelectMany(x => x)
            .ShouldContain(x =>
                x.Id == key.Id && x.DataTotal == 4_321 && x.DataReceived == 4_321 && x.Percentage == 100m
            );
        Mock.Mock<IDownloadHubService>().Verify();
    }

    [Test]
    public async Task ShouldIncludePatchWatermark_WhenPatchCompletesBeforeSnapshot()
    {
        // Arrange
        await SetupDatabase(84334, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = movieFile.ToKey();
        var patchEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, _, _, _) => patchEntered.TrySetResult()
            )
            .Returns(() => releasePatch.Task)
            .Verifiable(Times.Once());

        var progress = new DownloadTaskProgress
        {
            DataTotal = 4_321,
            DataReceived = 1_234,
            Percentage = 28.6m,
            DownloadSpeed = 456,
            TimeRemaining = 19,
        };
        var sut = Sut;

        // Act
        var persistTask = sut.PersistProgressAsync(key, progress, cancellationToken: CancellationToken.None);
        Task<DownloadSnapshotDTO>? snapshotTask = null;
        try
        {
            await patchEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            snapshotTask = sut.GetSnapshotAsync(CancellationToken.None);
            snapshotTask.IsCompleted.ShouldBeFalse();
            releasePatch.TrySetResult();
            var persistResult = await persistTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            var snapshot = await snapshotTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            // Assert
            persistResult.IsSuccess.ShouldBeTrue();
            persistResult.Errors.Count.ShouldBe(0);
            snapshot.PatchSequenceByServer.ShouldBe(new Dictionary<int, long> { [key.PlexServerId] = 1 });
            snapshot.Downloads.Select(x => x.Id).ShouldBe([key.PlexServerId]);
            var root = snapshot.Downloads.Single().Downloads.Single();
            root.Children.Select(x => x.Id).ShouldBe([key.Id]);
            var child = root.Children.Single();
            child.DataTotal.ShouldBe(progress.DataTotal);
            child.DataReceived.ShouldBe(progress.DataReceived);
            // The tree mapper derives file percentage from DataReceived/DataTotal.
            child.Percentage.ShouldBe(28.56m);
            child.DownloadSpeed.ShouldBe(progress.DownloadSpeed);
            child.TimeRemaining.ShouldBe(progress.TimeRemaining);
            Mock.Mock<IDownloadHubService>().Verify();
        }
        finally
        {
            releasePatch.TrySetResult();
            await persistTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            if (snapshotTask is not null)
                await snapshotTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        }

    }

    [Test]
    public async Task ShouldPublishPatchAfterSnapshot_WhenSnapshotAcquiresPublicationGateFirst()
    {
        // Arrange
        await SetupDatabase(84335, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = movieFile.ToKey();
        var snapshotDataContext = IDbContext;
        var patchDataContext = IDbContext;
        var snapshotCreateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshotCreate = new TaskCompletionSource<IReaparrDbContext>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var snapshotReadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshotRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshotDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSnapshotDispose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicationOrder = new ConcurrentQueue<string>();
        var patchRootReadEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePatchRootRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var patchRootReadCount = 0;
        var patchContext = new Mock<IReaparrDbContext>(MockBehavior.Strict);
        patchContext
            .SetupGet(x => x.DownloadTaskMovie)
            .Returns(() =>
            {
                if (Interlocked.Exchange(ref patchRootReadCount, 1) == 0)
                {
                    publicationOrder.Enqueue("patch-root-read");
                    patchRootReadEntered.TrySetResult();
                    releasePatchRootRead.Task.GetAwaiter().GetResult();
                }

                return patchDataContext.DownloadTaskMovie;
            });
        patchContext.SetupGet(x => x.DownloadTaskMovieFile).Returns(patchDataContext.DownloadTaskMovieFile);
        patchContext.Setup(x => x.Dispose()).Callback(() => patchDataContext.Dispose());
        var snapshotContext = new Mock<IReaparrDbContext>(MockBehavior.Loose);
        snapshotContext.SetupGet(x => x.DownloadTaskMovie).Returns(snapshotDataContext.DownloadTaskMovie);
        snapshotContext
            .SetupGet(x => x.DownloadTaskTvShow)
            .Returns(() =>
            {
                snapshotReadEntered.TrySetResult();
                releaseSnapshotRead.Task.GetAwaiter().GetResult();
                return snapshotDataContext.DownloadTaskTvShow;
            });
        snapshotContext.SetupGet(x => x.DownloadTaskMusicArtists).Returns(snapshotDataContext.DownloadTaskMusicArtists);
        snapshotContext.SetupGet(x => x.DownloadTaskPhotoFolders).Returns(snapshotDataContext.DownloadTaskPhotoFolders);
        snapshotContext
            .SetupGet(x => x.DownloadTaskPhotoImageFiles)
            .Returns(snapshotDataContext.DownloadTaskPhotoImageFiles);
        snapshotContext.SetupGet(x => x.DownloadTaskOtherVideos).Returns(snapshotDataContext.DownloadTaskOtherVideos);
        snapshotContext
            .Setup(x => x.Dispose())
            .Callback(() =>
            {
                publicationOrder.Enqueue("snapshot");
                snapshotDisposed.TrySetResult();
                releaseSnapshotDispose.Task.GetAwaiter().GetResult();
                snapshotDataContext.Dispose();
            });
        var factory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        factory
            .SetupSequence(x => x.CreateAsync())
            .Returns(() =>
            {
                snapshotCreateEntered.TrySetResult();
                return releaseSnapshotCreate.Task;
            })
            .ReturnsAsync(patchContext.Object);
        SetupDependencies(builder =>
            builder.RegisterInstance(factory.Object).As<IReaparrDbContextFactory>().SingleInstance()
        );
        var published = new List<(long Sequence, List<Guid> Upserts)>();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, sequence, upserts, _, _) =>
                {
                    publicationOrder.Enqueue("patch");
                    published.Add((sequence, upserts.Select(x => x.Id).ToList()));
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        var sut = Sut;
        var progress = new DownloadTaskProgress
        {
            DataTotal = 1_000,
            DataReceived = 500,
            Percentage = 50,
            DownloadSpeed = 10,
            TimeRemaining = 30,
        };

        // Act
        Task<DownloadSnapshotDTO>? snapshotTask = null;
        Task<Result>? patchTask = null;
        try
        {
            snapshotTask = sut.GetSnapshotAsync(CancellationToken.None);
            await snapshotCreateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            releaseSnapshotCreate.TrySetResult(snapshotContext.Object);
            await snapshotReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            patchTask = Task.Run(
                () => sut.PersistProgressAsync(key, progress, cancellationToken: CancellationToken.None),
                CancellationToken
            );
            releaseSnapshotRead.TrySetResult();
            await snapshotDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            patchRootReadEntered.Task.IsCompleted.ShouldBeFalse();
            publicationOrder.ToArray().ShouldBe(["snapshot"]);
            releaseSnapshotDispose.TrySetResult();
            var snapshot = await snapshotTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            await patchRootReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            releasePatchRootRead.TrySetResult();
            var patchResult = await patchTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            // Assert
            snapshot.PatchSequenceByServer.ShouldBeEmpty();
            snapshot.Downloads.Select(x => x.Id).ShouldBe([key.PlexServerId]);
            patchResult.IsSuccess.ShouldBeTrue();
            patchResult.Errors.Count.ShouldBe(0);
            published.Count.ShouldBe(1);
            published[0].Sequence.ShouldBe(1);
            published[0].Upserts.ShouldContain(key.Id);
            publicationOrder.ToArray().ShouldBe(["snapshot", "patch-root-read", "patch"]);
            Mock.Mock<IDownloadHubService>().Verify();
        }
        finally
        {
            releaseSnapshotCreate.TrySetResult(snapshotContext.Object);
            releaseSnapshotRead.TrySetResult();
            releaseSnapshotDispose.TrySetResult();
            releasePatchRootRead.TrySetResult();
            if (snapshotTask is not null)
                await snapshotTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            if (patchTask is not null)
                await patchTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        }

    }

    [Test]
    public async Task ShouldRetainDeletionWatermark_WhenSnapshotTreeIsEmpty()
    {
        // Arrange
        await SetupDatabase(84336, config => config.PlexServerCount = 1);
        var key = new DownloadTaskKey
        {
            Id = Guid.NewGuid(),
            PlexServerId = 1,
            PlexLibraryId = 0,
            Type = DownloadTaskType.MovieData,
        };
        var published = new List<(int ServerId, long Sequence, List<Guid> DeletedIds)>();
        var deletionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDeletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (serverId, sequence, _, deletedIds, _) =>
                {
                    published.Add((serverId, sequence, deletedIds?.ToList() ?? []));
                    deletionEntered.TrySetResult();
                }
            )
            .Returns(() => releaseDeletion.Task)
            .Verifiable(Times.Once());
        var sut = Sut;

        // Act
        Task? deletionTask = null;
        Task<DownloadSnapshotDTO>? snapshotTask = null;
        try
        {
            deletionTask = sut.OnStatusChangedAsync(key, DownloadStatus.Deleted, CancellationToken.None);
            await deletionEntered.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            snapshotTask = sut.GetSnapshotAsync(CancellationToken.None);
            snapshotTask.IsCompleted.ShouldBeFalse();
            releaseDeletion.TrySetResult();
            await deletionTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            var snapshot = await snapshotTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

            // Assert
            snapshot.Downloads.ShouldBeEmpty();
            snapshot.PatchSequenceByServer.ShouldBe(new Dictionary<int, long> { [key.PlexServerId] = 1 });
            published.Count.ShouldBe(1);
            published[0].ServerId.ShouldBe(key.PlexServerId);
            published[0].Sequence.ShouldBe(1);
            published[0].DeletedIds.ShouldBe([key.Id]);
            Mock.Mock<IDownloadHubService>().Verify();
        }
        finally
        {
            releaseDeletion.TrySetResult();
            if (deletionTask is not null)
                await deletionTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            if (snapshotTask is not null)
                await snapshotTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        }

    }

    [Test]
    public async Task ShouldReleasePublicationGate_WhenSnapshotQueryFails()
    {
        // Arrange
        await SetupDatabase(84337, config => config.PlexServerCount = 1);
        var queryFailure = new InvalidOperationException("query failed");
        var failingContext = new Mock<IReaparrDbContext>(MockBehavior.Strict);
        failingContext.SetupGet(x => x.DownloadTaskMovie).Throws(queryFailure);
        failingContext.Setup(x => x.Dispose()).Verifiable(Times.Once());
        var succeedingContext = IDbContext;
        var factory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        factory
            .SetupSequence(x => x.CreateAsync())
            .ReturnsAsync(failingContext.Object)
            .ReturnsAsync(succeedingContext);
        SetupDependencies(builder =>
            builder.RegisterInstance(factory.Object).As<IReaparrDbContextFactory>().SingleInstance()
        );
        var sut = Sut;

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.GetSnapshotAsync(CancellationToken.None)
        );
        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        // Assert
        exception.ShouldBe(queryFailure);
        snapshot.Downloads.ShouldBeEmpty();
        snapshot.PatchSequenceByServer.ShouldBeEmpty();
        failingContext.Verify();
    }

    [Test]
    public async Task ShouldReleasePublicationGate_WhenPatchSendFails()
    {
        // Arrange
        await SetupDatabase(84338, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = movieFile.ToKey();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.FromException(new InvalidOperationException("send failed")))
            .Verifiable(Times.Once());
        var sut = Sut;

        // Act
        var result = await sut.PersistProgressAsync(
            key,
            new DownloadTaskProgress { DataTotal = 100, DataReceived = 25, Percentage = 25, DownloadSpeed = 5 },
            cancellationToken: CancellationToken.None
        );
        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        snapshot.PatchSequenceByServer.ShouldBe(new Dictionary<int, long> { [key.PlexServerId] = 1 });
        Mock.Mock<IDownloadHubService>().Verify();
    }

    [Test]
    public async Task ShouldReleasePublicationGate_WhenPatchSendIsCancelled()
    {
        // Arrange
        await SetupDatabase(84339, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = movieFile.ToKey();
        using var sendCancellation = new CancellationTokenSource();
        sendCancellation.Cancel();
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.FromCanceled(sendCancellation.Token))
            .Verifiable(Times.Once());
        var sut = Sut;

        // Act
        var result = await sut.PersistProgressAsync(
            key,
            new DownloadTaskProgress { DataTotal = 100, DataReceived = 25, Percentage = 25, DownloadSpeed = 5 },
            cancellationToken: CancellationToken.None
        );
        var snapshot = await sut.GetSnapshotAsync(CancellationToken.None);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        snapshot.PatchSequenceByServer.ShouldBe(new Dictionary<int, long> { [key.PlexServerId] = 1 });
        Mock.Mock<IDownloadHubService>().Verify();
    }
}
