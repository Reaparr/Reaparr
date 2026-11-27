using System.Collections.Concurrent;
using System.Diagnostics;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherMovieUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    private async Task WaitForPatchCount<T>(IReadOnlyCollection<T> collection, int expectedCount)
    {
        for (var i = 0; i < 40 && collection.Count < expectedCount; i++)
            await Task.Delay(100, CancellationToken);

        collection.Count.ShouldBeGreaterThanOrEqualTo(expectedCount);
    }

    private async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeoutMs = 4_000, int pollIntervalMs = 100)
    {
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < TimeSpan.FromMilliseconds(timeoutMs))
        {
            if (await predicate())
                return;

            await Task.Delay(pollIntervalMs, CancellationToken);
        }

        (await predicate()).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldSendDeletedIds_WhenStatusChangedToDeleted()
    {
        // Arrange
        await SetupDatabase(84336, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());

        var capturedDeletedIds = new List<IReadOnlyCollection<Guid>?>();
        var capturedUpserts = new List<IReadOnlyCollection<DownloadPatchDTO>>();
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
                (_, _, upserts, deletedIds, _) =>
                {
                    capturedUpserts.Add(upserts);
                    capturedDeletedIds.Add(deletedIds);
                }
            )
            .Returns(Task.CompletedTask);

        var sut = Sut;

        // Act
        await sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Deleted, CancellationToken);

        // Assert

        capturedDeletedIds.ShouldNotBeEmpty();
        var deletedPatch = capturedDeletedIds.Last();
        deletedPatch.ShouldNotBeNull();
        deletedPatch.ShouldContain(movieFile.Id);

        capturedUpserts.Last().ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldSendHundredPercentPatch_WhenMovieFileIsCompletedWithStaleStoredPercentage()
    {
        // Arrange - movie file equivalent of the completed stale-percentage regression.
        await SetupDatabase(84332, config => config.MovieDownloadTasksCount = 1);

        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DataTotal, 2_000L)
                        .SetProperty(x => x.DataReceived, 40L)
                        .SetProperty(x => x.FileDataTransferred, 2_000L)
                        .SetProperty(x => x.CurrentFileTransferBytesOffset, 2_000L)
                        .SetProperty(x => x.FileTransferSpeed, 0L)
                        .SetProperty(x => x.DownloadSpeed, 0L)
                        .SetProperty(x => x.TimeRemaining, 0)
                        .SetProperty(x => x.Percentage, 2m)
                        .SetProperty(x => x.DownloadStatus, DownloadStatus.Completed),
                CancellationToken
            );

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

        // Act
        await sut.StartAsync(CancellationToken.None);
        sut.NotifyFileTransferProgress(movieFile.ToKey());

        await WaitForPatchCount(capturedPatches, 1);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        var fileTaskPatch = capturedPatches.SelectMany(x => x).FirstOrDefault(x => x.Id == movieFile.Id);
        fileTaskPatch.ShouldNotBeNull();
        fileTaskPatch.Status.ShouldBe(DownloadStatus.Completed);
        fileTaskPatch.Percentage.ShouldBe(100m);
    }

    [Test]
    public async Task ShouldSendPatchWithFileTransferPercentage_WhenNotifyFileTransferProgressIsCalled()
    {
        // Arrange — verifies that calling NotifyFileTransferProgress triggers the dispatcher flush,
        // which reads the persisted FileDataTransferred from the DB and broadcasts the correct percentage
        // (fixing the bug where no patch was sent during the move phase).
        await SetupDatabase(84330, config => config.MovieDownloadTasksCount = 1);

        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);

        // Simulate mid-move DB state: 50% through the file transfer
        await IDbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFile.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DataTotal, 1000L)
                        .SetProperty(x => x.FileDataTransferred, 500L)
                        .SetProperty(x => x.CurrentFileTransferBytesOffset, 500L)
                        .SetProperty(x => x.FileTransferSpeed, 50_000_000L)
                        .SetProperty(x => x.DownloadStatus, DownloadStatus.Moving),
                CancellationToken
            );

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
        await sut.StartAsync(CancellationToken.None);

        // Act
        sut.NotifyFileTransferProgress(movieFile.ToKey());

        await WaitForPatchCount(capturedPatches, 1);
        await sut.StopAsync(CancellationToken.None);

        // Assert — the dispatcher sent a patch carrying the current transfer progress.
        // The mapper derives Percentage from FileDataTransferred/DataTotal when in FileTransfer phase:
        // DataFormat.GetPercentage(500, 1000) = 50.00m
        capturedPatches.ShouldNotBeEmpty();
        var fileTaskPatch = capturedPatches.SelectMany(x => x).FirstOrDefault(x => x.Id == movieFile.Id);
        fileTaskPatch.ShouldNotBeNull();
        fileTaskPatch.Status.ShouldBe(DownloadStatus.Moving);
        fileTaskPatch.Percentage.ShouldBe(50.00m);
    }

    [Test]
    public async Task ShouldNotWriteDownloadingLogAfterPauseTransition()
    {
        await SetupDatabase(84329, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());

        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);

        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 500,
                Percentage = 50,
                DownloadSpeed = 10,
            }
        );

        await sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Paused, CancellationToken);

        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 950,
                Percentage = 95,
                DownloadSpeed = 10,
            }
        );

        await WaitUntilAsync(async () =>
        {
            var logs = await IDbContext
                .DownloadTaskMovieFileLogs.AsNoTracking()
                .Where(x => x.DownloadTaskFileId == movieFile.Id)
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);

            var pauseLog = logs.LastOrDefault(x => x.Status == DownloadStatus.Paused);
            return pauseLog is not null && !logs.Any(x => x.Id > pauseLog.Id && x.Status == DownloadStatus.Downloading);
        });
        await sut.StopAsync(CancellationToken.None);

        var logs = await IDbContext
            .DownloadTaskMovieFileLogs.AsNoTracking()
            .Where(x => x.DownloadTaskFileId == movieFile.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        var pauseLog = logs.LastOrDefault(x => x.Status == DownloadStatus.Paused);
        pauseLog.ShouldNotBeNull();

        logs.ShouldContain(x => x.Status == DownloadStatus.Downloading && x.Id < pauseLog.Id);

        logs.Where(x => x.Id > pauseLog.Id && x.Status == DownloadStatus.Downloading).ShouldBeEmpty();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldLogOnlyCurrentLifecycleProgress_WhenHubSendIsBlocked(bool pauseAndResume)
    {
        // Arrange
        await SetupDatabase(84350, config => config.MovieDownloadTasksCount = 1);
        var file = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = file.ToKey();
        var hubEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHub = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadTaskScheduler>().Setup(x => x.IsServerDownloading(file.PlexServerId)).ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        var hub = Mock.Mock<IDownloadHubService>();
        hub.Setup(x =>
                x.SendDownloadPatchAsync(
                    file.PlexServerId,
                    It.IsAny<long>(),
                    It.Is<IReadOnlyCollection<DownloadPatchDTO>>(patch =>
                        patch.Any(node => node.Id == file.Id && node.DataReceived == 500)
                    ),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() =>
            {
                hubEntered.TrySetResult();
                return releaseHub.Task;
            })
            .Verifiable(Times.Once());
        var sut = Sut;
        await sut.OnStatusChangedAsync(key, DownloadStatus.Downloading, CancellationToken);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 1_000,
                DataReceived = 500,
                Percentage = 50,
            }
        );

        // Act: run one flush and hold the hub send after progress has been persisted.
        var flush = (Task)
            typeof(DownloadTaskUpdateDispatcher)
                .GetMethod(
                    "FlushProgressAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .Invoke(sut, [CancellationToken])!;
        try
        {
            await hubEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (pauseAndResume)
            {
                await sut.OnStatusChangedAsync(key, DownloadStatus.Paused, CancellationToken);
                await sut.OnStatusChangedAsync(key, DownloadStatus.Downloading, CancellationToken);
            }
        }
        finally
        {
            releaseHub.TrySetResult();
            await flush.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // Assert: resuming does not make the old progress eligible for a new journey log.
        (await IDbContext.GetDownloadStatusAsync(key)).ShouldBe(DownloadStatus.Downloading);
        var logs = await IDbContext.DownloadTaskMovieFileLogs.CountAsync(
            x => x.DownloadTaskFileId == file.Id && x.Message.StartsWith("[DownloadTaskProgress "),
            CancellationToken
        );
        logs.ShouldBe(pauseAndResume ? 0 : 1);
        hub.Verify();
    }

    [Test]
    public async Task ShouldKeepAcceptingProgress_WhenPauseIsCancelledBeforeGateAcquisition()
    {
        await SetupDatabase(84345, config => config.MovieDownloadTasksCount = 1);
        var key = (await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken)).ToKey();
        var sut = Sut;
        var gates = (SemaphoreSlim[])
            typeof(DownloadTaskUpdateDispatcher)
                .GetField(
                    "_progressGates",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .GetValue(sut)!;
        var gate = gates[(int)((uint)key.Id.GetHashCode() % (uint)gates.Length)];
        await gate.WaitAsync(CancellationToken);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var pause = sut.OnStatusChangedAsync(key, DownloadStatus.Paused, cancellation.Token);
            await cancellation.CancelAsync();
            await pause;
        }
        finally
        {
            gate.Release();
        }
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 100,
                DataReceived = 25,
                DownloadSpeed = 10,
            }
        );
        var buffered =
            (ConcurrentDictionary<Guid, BufferedProgressUpdate>)
                typeof(DownloadTaskUpdateDispatcher)
                    .GetField(
                        "_progressByNodeId",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    )!
                    .GetValue(sut)!;
        buffered[key.Id].Progress!.DataReceived.ShouldBe(25);
        (await IDbContext.GetDownloadStatusAsync(key)).ShouldNotBe(DownloadStatus.Paused);
    }

    [Test]
    public async Task ShouldRestoreProgressAdmission_WhenPauseCannotPersistBufferedProgress()
    {
        await SetupDatabase(84347, config => config.MovieDownloadTasksCount = 1);
        var key = (await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken)).ToKey();
        var factory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        var failing = new Mock<IReaparrDbContext>(MockBehavior.Strict);
        failing.SetupGet(x => x.DownloadTaskMovieFile).Returns(IDbContext.DownloadTaskMovieFile);
        failing.Setup(x => x.Dispose());
        factory.Setup(x => x.CreateAsync()).ReturnsAsync(failing.Object);
        var sut = new DownloadTaskUpdateDispatcher(
            Mock.Container.Resolve<ILogger>(),
            factory.Object,
            Mock.Container.Resolve<IDownloadHubService>(),
            Mock.Container.Resolve<ICommandExecutor>(),
            Mock.Container.Resolve<IDownloadTaskScheduler>(),
            Mock.Container.Resolve<ITelemetry>()
        );
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 100,
                DataReceived = 25,
                DownloadSpeed = 10,
            }
        );

        await sut.OnStatusChangedAsync(key, DownloadStatus.Paused, CancellationToken);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 100,
                DataReceived = 50,
                DownloadSpeed = 10,
            }
        );

        var buffered =
            (ConcurrentDictionary<Guid, BufferedProgressUpdate>)
                typeof(DownloadTaskUpdateDispatcher)
                    .GetField(
                        "_progressByNodeId",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    )!
                    .GetValue(sut)!;
        buffered[key.Id].Progress!.DataReceived.ShouldBe(50);
        (await IDbContext.GetDownloadStatusAsync(key)).ShouldNotBe(DownloadStatus.Paused);
        factory.Verify(x => x.CreateAsync(), Times.Once());
    }

    [Test]
    public async Task ShouldLeaveEstimateAvailableToPause_WhenTimerWaitsForProgressGate()
    {
        await SetupDatabase(84346, config => config.MovieDownloadTasksCount = 1);
        var key = (await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken)).ToKey();
        var sut = Sut;
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 100,
                DataReceived = 25,
                DownloadSpeed = 10,
            }
        );
        var buffered =
            (ConcurrentDictionary<Guid, BufferedProgressUpdate>)
                typeof(DownloadTaskUpdateDispatcher)
                    .GetField(
                        "_progressByNodeId",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    )!
                    .GetValue(sut)!;
        var gates = (SemaphoreSlim[])
            typeof(DownloadTaskUpdateDispatcher)
                .GetField(
                    "_progressGates",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .GetValue(sut)!;
        var gate = gates[(int)((uint)key.Id.GetHashCode() % (uint)gates.Length)];
        var statuses =
            (ConcurrentDictionary<Guid, DownloadStatus>)
                typeof(DownloadTaskUpdateDispatcher)
                    .GetField(
                        "_statusByNodeId",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    )!
                    .GetValue(sut)!;
        await gate.WaitAsync(CancellationToken);
        var flush = (Task)
            typeof(DownloadTaskUpdateDispatcher)
                .GetMethod(
                    "FlushProgressAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .Invoke(sut, [CancellationToken])!;
        try
        {
            flush.IsCompleted.ShouldBeFalse();
            buffered.ContainsKey(key.Id).ShouldBeTrue();
            buffered.TryRemove(key.Id, out _).ShouldBeTrue();
            statuses[key.Id] = DownloadStatus.Paused;
        }
        finally
        {
            gate.Release();
            await flush.WaitAsync(TimeSpan.FromSeconds(10));
        }
        var after = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        after.DataReceived.ShouldNotBe(25);
    }

    [Test]
    public async Task ShouldDrainLatestProgress_WhenPauseIsWaitingForDatabaseAccess()
    {
        await SetupDatabase(84328, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        var initialProgress = new DownloadTaskProgress
        {
            DataTotal = 1000,
            DataReceived = 500,
            Percentage = 50,
            DownloadSpeed = 10,
        };

        var delayedDbContextFactory = new Mock<IReaparrDbContextFactory>();
        var createDbContextTcs = new TaskCompletionSource<IReaparrDbContext>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        delayedDbContextFactory.Setup(x => x.CreateAsync()).Returns(createDbContextTcs.Task).Verifiable(Times.Once());

        var downloadHubService = new Mock<IDownloadHubService>();
        downloadHubService
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var sut = new DownloadTaskUpdateDispatcher(
            new LoggerConfiguration().CreateLogger(),
            delayedDbContextFactory.Object,
            downloadHubService.Object,
            Mock.Mock<ICommandExecutor>().Object,
            Mock.Mock<IDownloadTaskScheduler>().Object,
            Mock.Mock<ITelemetry>().Object
        );

        sut.OnProgressUpdated(movieFile.ToKey(), initialProgress);

        var pauseTask = sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Paused, CancellationToken);

        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 999,
                Percentage = 99,
                DownloadSpeed = 10,
            }
        );

        createDbContextTcs.SetResult(IDbContext);
        await pauseTask;

        await WaitUntilAsync(async () =>
        {
            var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
            return updatedMovieFile.DownloadStatus == DownloadStatus.Paused && updatedMovieFile.DataReceived == 999;
        });

        var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        updatedMovieFile.DownloadStatus.ShouldBe(DownloadStatus.Paused);
        updatedMovieFile.DataReceived.ShouldBe(999);
    }

    [Test]
    [Arguments(DownloadStatus.Paused, 750, 75)]
    [Arguments(DownloadStatus.DownloadFinished, 1000, 100)]
    public async Task ShouldPersistBufferedProgressBeforeStatusChanges(
        DownloadStatus status,
        long received,
        int percentage
    )
    {
        await SetupDatabase(84327, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());

        var sut = Sut;
        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = received,
                Percentage = percentage,
                DownloadSpeed = status == DownloadStatus.Paused ? 10 : 0,
            }
        );

        await sut.OnStatusChangedAsync(movieFile.ToKey(), status, CancellationToken);

        var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        updatedMovieFile.DownloadStatus.ShouldBe(status);
        updatedMovieFile.DataReceived.ShouldBe(received);
        updatedMovieFile.DataTotal.ShouldBe(1000);
        updatedMovieFile.DownloadSpeed.ShouldBe(0);
    }

    [Test]
    public async Task ShouldIncreaseSequenceAcrossMixedStatusAndProgressPatches()
    {
        // Arrange
        await SetupDatabase(84326, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());

        var sequences = new ConcurrentBag<long>();
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
                (_, sequence, _, _, _) => sequences.Add(sequence)
            )
            .Returns(Task.CompletedTask);

        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);

        await sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Downloading, CancellationToken);
        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 2000,
                DataReceived = 1200,
                Percentage = 60,
                DownloadSpeed = 100,
            }
        );

        // Act
        await WaitForPatchCount(sequences, 2);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        sequences.Count.ShouldBeGreaterThanOrEqualTo(2);
        var orderedSequences = sequences.OrderBy(x => x).ToList();
        orderedSequences.Distinct().Count().ShouldBe(orderedSequences.Count);
        for (var i = 1; i < orderedSequences.Count; i++)
            orderedSequences[i].ShouldBeGreaterThan(orderedSequences[i - 1]);
    }

    [Test]
    public async Task ShouldRequeueClaimedProgress_WhenBufferedFlushFailsAfterNewerUpdateArrives()
    {
        // Arrange
        await SetupDatabase(84348, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        var key = movieFile.ToKey();
        var queryFailure = new InvalidOperationException("simulated flush failure");
        var failingContext = new Mock<IReaparrDbContext>(MockBehavior.Strict);
        failingContext.SetupGet(x => x.DownloadTaskMovieFile).Throws(queryFailure);
        failingContext.Setup(x => x.Dispose());
        var factory = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
        factory.Setup(x => x.CreateAsync()).ReturnsAsync(failingContext.Object).Verifiable(Times.Once());
        var sut = new DownloadTaskUpdateDispatcher(
            Mock.Container.Resolve<ILogger>(),
            factory.Object,
            Mock.Container.Resolve<IDownloadHubService>(),
            Mock.Container.Resolve<ICommandExecutor>(),
            Mock.Container.Resolve<IDownloadTaskScheduler>(),
            Mock.Container.Resolve<ITelemetry>()
        );
        var gates = (SemaphoreSlim[])
            typeof(DownloadTaskUpdateDispatcher)
                .GetField(
                    "_progressGates",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .GetValue(sut)!;
        var gate = gates[(int)((uint)key.Id.GetHashCode() % (uint)gates.Length)];
        await gate.WaitAsync(CancellationToken);
        sut.OnProgressUpdated(
            key,
            new DownloadTaskProgress
            {
                DataTotal = 100,
                DataReceived = 25,
                Percentage = 25,
                DownloadSpeed = 10,
            }
        );

        // Act
        var flush = (Task)
            typeof(DownloadTaskUpdateDispatcher)
                .GetMethod(
                    "FlushProgressAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .Invoke(sut, [CancellationToken])!;
        try
        {
            flush.IsCompleted.ShouldBeFalse();
            sut.OnProgressUpdated(
                key,
                new DownloadTaskProgress
                {
                    DataTotal = 100,
                    DataReceived = 50,
                    Percentage = 50,
                    DownloadSpeed = 10,
                }
            );
        }
        finally
        {
            gate.Release();
            await flush.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // Assert
        var buffered =
            (ConcurrentDictionary<Guid, BufferedProgressUpdate>)
                typeof(DownloadTaskUpdateDispatcher)
                    .GetField(
                        "_progressByNodeId",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                    )!
                    .GetValue(sut)!;
        buffered[key.Id].Progress!.DataReceived.ShouldBe(50);
        factory.Verify();
    }

    [Test]
    public async Task ShouldRetryBufferedProgress_WhenFirstPatchSendFails()
    {
        await SetupDatabase(84325, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

        var attempts = 0;
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
                (_, _, upserts, _, _) =>
                {
                    attempts++;
                    if (attempts == 1)
                        throw new InvalidOperationException("simulated send failure");

                    capturedPatches.Add(upserts);
                }
            )
            .Returns(Task.CompletedTask);

        var sut = Sut;
        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 500,
                Percentage = 50,
                DownloadSpeed = 10,
            }
        );

        await sut.StartAsync(CancellationToken.None);
        await WaitForPatchCount(capturedPatches, 1);
        await sut.StopAsync(CancellationToken.None);

        attempts.ShouldBeGreaterThanOrEqualTo(2);
        capturedPatches.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ShouldIncreasePatchSequence_WhenMultiplePatchesAreDispatchedForSameServer()
    {
        await SetupDatabase(84323, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(movieFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());

        var sequences = new ConcurrentBag<long>();
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
                (_, sequence, _, _, _) => sequences.Add(sequence)
            )
            .Returns(Task.CompletedTask);

        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);

        await sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Downloading, CancellationToken);
        await sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Paused, CancellationToken);

        await WaitForPatchCount(sequences, 2);
        await sut.StopAsync(CancellationToken.None);

        var orderedSequences = sequences.OrderBy(x => x).ToList();
        orderedSequences.Count.ShouldBeGreaterThanOrEqualTo(2);
        orderedSequences[1].ShouldBeGreaterThan(orderedSequences[0]);
    }

    [Test]
    public async Task ShouldPublishLatestProgressForBothRootsInOnePatch_OnTheOneSecondTick()
    {
        // Arrange
        await SetupDatabase(84346, config =>
        {
            config.PlexServerCount = 1;
            config.MovieDownloadTasksCount = 2;
        });
        var files = await IDbContext.DownloadTaskMovieFile.ToListAsync(CancellationToken);
        var serverId = files.Select(file => file.PlexServerId).Distinct().Single();
        var expectedIds = files.SelectMany(file => new[] { file.Id, file.ParentId }).Order().ToArray();
        expectedIds.Length.ShouldBe(4);
        var published = new TaskCompletionSource<(IReadOnlyCollection<DownloadPatchDTO> Patch, TimeSpan Elapsed)>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var stopwatch = new Stopwatch();
        var hub = Mock.Mock<IDownloadHubService>();
        hub.Setup(x =>
                x.SendDownloadPatchAsync(
                    serverId,
                    1,
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, upserts, _, _) => published.TrySetResult((upserts, stopwatch.Elapsed))
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        using var sut = Sut;

        // Act
        foreach (var file in files)
        {
            var progress = new DownloadTaskProgress
            {
                DataReceived = 250,
                DataTotal = 1000,
                Percentage = 25,
            };
            sut.OnProgressUpdated(file.ToKey(), progress);
            sut.OnProgressUpdated(
                file.ToKey(),
                new DownloadTaskProgress
                {
                    DataReceived = 500,
                    DataTotal = 1000,
                    Percentage = 50,
                }
            );
        }
        stopwatch.Start();
        await sut.StartAsync(CancellationToken);
        (IReadOnlyCollection<DownloadPatchDTO> Patch, TimeSpan Elapsed) publication;
        try
        {
            publication = await published.Task.WaitAsync(TimeSpan.FromSeconds(4), CancellationToken);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }

        // Assert
        publication.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(950));
        publication.Patch.Select(node => node.Id).Order().ToArray().ShouldBe(expectedIds);
        foreach (var file in files)
            publication.Patch.Single(node => node.Id == file.Id).DataReceived.ShouldBe(500);
        hub.Verify(
            x =>
                x.SendDownloadPatchAsync(
                    serverId,
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once()
        );
        hub.Verify();
    }
}
