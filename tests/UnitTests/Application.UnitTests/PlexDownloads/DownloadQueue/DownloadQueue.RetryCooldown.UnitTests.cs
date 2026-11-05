using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Reaparr.Application.UnitTests;

public class DownloadQueueRetryCooldownUnitTests : BaseUnitTest<DownloadQueue>
{
    private readonly RetryClock _clock = new();

    public DownloadQueueRetryCooldownUnitTests() =>
        SetupDependencies(builder => builder.RegisterInstance(_clock).As<TimeProvider>());

    [Test]
    public async Task ShouldSkipRecentlyAttemptedQueuedTask_AndPickAnotherInsteadOnRapidReentry()
    {
        // Arrange
        await SetupDatabase(
            58119,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.MovieDownloadTasksCount = 3;
            }
        );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        // Act
        var firstPick = await Sut.CheckDownloadQueueServer(1);
        var secondPick = await Sut.CheckDownloadQueueServer(1);

        // Assert
        firstPick.IsSuccess.ShouldBeTrue();
        firstPick.Value.ShouldNotBeNull();
        secondPick.IsSuccess.ShouldBeTrue();
        secondPick.Value.ShouldNotBeNull();
        secondPick.Value.Id.ShouldNotBe(firstPick.Value.Id);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
    }

    [Test]
    public async Task ShouldSkipRecentlyAttemptedErrorTask_AndPickNextErrorTaskBeforeQueuedTask()
    {
        // Arrange
        await SetupDatabase(
            58120,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 3;
                config.MovieDownloadTasksCount = 3;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );

        var firstErrorTask = downloadTasks[0].Children[0];
        var secondErrorTask = downloadTasks[1].Children[0];
        var queuedTask = downloadTasks[2].Children[0];

        firstErrorTask.SetDownloadStatus(DownloadStatus.Error);
        downloadTasks[0].SetDownloadStatus(DownloadStatus.Error);

        secondErrorTask.SetDownloadStatus(DownloadStatus.Error);
        downloadTasks[1].SetDownloadStatus(DownloadStatus.Error);

        queuedTask.SetDownloadStatus(DownloadStatus.Queued);
        downloadTasks[2].SetDownloadStatus(DownloadStatus.Queued);

        await IDbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        // Act
        var firstPick = await Sut.CheckDownloadQueueServer(1);
        var secondPick = await Sut.CheckDownloadQueueServer(1);

        // Assert
        firstPick.IsSuccess.ShouldBeTrue();
        firstPick.Value.ShouldNotBeNull();
        firstPick.Value.Id.ShouldBe(firstErrorTask.Id);

        secondPick.IsSuccess.ShouldBeTrue();
        secondPick.Value.ShouldNotBeNull();
        secondPick.Value.Id.ShouldBe(secondErrorTask.Id);
        secondPick.Value.Id.ShouldNotBe(queuedTask.Id);

        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)
            );
    }

    [Test]
    public async Task ShouldReturnSchedulerFailureAndRetryTask_WhenSchedulingFails()
    {
        // Arrange
        await SetupDatabase(
            58123,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var expectedTask = downloadTasks[0].Children[0];
        expectedTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(expectedTask.PlexServerId))
            .ReturnsAsync([]);
        Mock.Mock<IDownloadTaskScheduler>()
            .SetupSequence(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("scheduler failure"))
            .ReturnsAsync(Result.Ok());

        // Act
        var firstResult = await Sut.CheckDownloadQueueServer(expectedTask.PlexServerId);
        var secondResult = await Sut.CheckDownloadQueueServer(expectedTask.PlexServerId);

        // Assert
        firstResult.IsFailed.ShouldBeTrue();
        firstResult.IsCancelled.ShouldBeFalse();
        firstResult.Errors.Count.ShouldBe(1);
        firstResult.ValueOrDefault.ShouldBeNull();
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Errors.Count.ShouldBe(0);
        secondResult.Value.Id.ShouldBe(expectedTask.Id);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key =>
                            key.Id == expectedTask.Id && key.PlexServerId == expectedTask.PlexServerId
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(2)
            );
    }

    [Test]
    public async Task ShouldReturnSchedulerCancellationAndRetryTask_WhenSchedulingIsCancelled()
    {
        // Arrange
        await SetupDatabase(
            58124,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var expectedTask = downloadTasks[0].Children[0];
        expectedTask.DownloadStatus.ShouldBe(DownloadStatus.Queued);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(expectedTask.PlexServerId))
            .ReturnsAsync([]);
        Mock.Mock<IDownloadTaskScheduler>()
            .SetupSequence(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResultExtensions.TaskIsCancelled(nameof(IDownloadTaskScheduler.StartDownloadTaskJob)))
            .ReturnsAsync(Result.Ok());

        // Act
        var firstResult = await Sut.CheckDownloadQueueServer(expectedTask.PlexServerId);
        var secondResult = await Sut.CheckDownloadQueueServer(expectedTask.PlexServerId);

        // Assert
        firstResult.IsCancelled.ShouldBeTrue();
        firstResult.IsFailed.ShouldBeTrue();
        firstResult.Errors.Count.ShouldBe(1);
        firstResult.ValueOrDefault.ShouldBeNull();
        secondResult.IsSuccess.ShouldBeTrue();
        secondResult.Errors.Count.ShouldBe(0);
        secondResult.Value.Id.ShouldBe(expectedTask.Id);
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x =>
                    x.StartDownloadTaskJob(
                        It.Is<DownloadTaskKey>(key =>
                            key.Id == expectedTask.Id && key.PlexServerId == expectedTask.PlexServerId
                        ),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(2)
            );
    }
    [Test]
    [Arguments(DownloadStatus.Queued)]
    [Arguments(DownloadStatus.AutoPaused)]
    [Arguments(DownloadStatus.ServerUnreachable)]
    [Arguments(DownloadStatus.DownloadClientError)]
    [Arguments(DownloadStatus.Error)]
    public async Task ShouldResumeExactFileWithoutAnotherEvent_WhenRetryCooldownExpires(DownloadStatus retryStatus)
    {
        // Arrange
        await SetupDatabase(58125, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var root = (await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)).Single();
        var file = root.Children.Single();
        var started = new ConcurrentQueue<DownloadTaskKey>();
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(root.PlexServerId))
            .ReturnsAsync([]);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(file.ToKey(), It.IsAny<CancellationToken>()))
            .Returns((DownloadTaskKey key, CancellationToken _) =>
            {
                started.Enqueue(key);
                if (started.Count == 2)
                    resumed.TrySetResult();
                return Task.FromResult(Result.Ok());
            })
            .Verifiable(Times.Exactly(2));
        var queue = Sut;
        var first = await queue.CheckDownloadQueueServer(root.PlexServerId, CancellationToken);
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        first.Value.Id.ShouldBe(file.Id);
        await IDbContext.SetDownloadStatus(file.ToKey(), retryStatus);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            var suppressed = await queue.CheckDownloadQueueServer(root.PlexServerId, CancellationToken);
            suppressed.IsSuccess.ShouldBeTrue();
            suppressed.Errors.Count.ShouldBe(0);
            suppressed.ValueOrDefault.ShouldBeNull();
        }
        using var workerCancellation = new CancellationTokenSource();

        // Act
        queue.Setup(workerCancellation.Token);
        try
        {
            var timer = await _clock.NextTimerAsync();
            timer.DueTime.ShouldBe(TimeSpan.FromSeconds(60));
            _clock.Advance(TimeSpan.FromSeconds(59));
            started.ToArray().ShouldBe([file.ToKey()]);
            _clock.Advance(TimeSpan.FromSeconds(1));
            timer.Fire();
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await StopQueueAsync(queue, workerCancellation);
        }

        // Assert
        started.ToArray().ShouldBe([file.ToKey(), file.ToKey()]);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    [Arguments("paused")]
    [Arguments("disabled")]
    [Arguments("shutdown")]
    public async Task ShouldNotResumeFile_WhenServerOrWorkerStopsBeforeCooldownExpires(string stopReason)
    {
        // Arrange
        await SetupDatabase(58126, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var file = (await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken))
            .Single().Children.Single();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(file.PlexServerId))
            .ReturnsAsync([]);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(file.ToKey(), It.IsAny<CancellationToken>()))
            .ReturnOk()
            .Verifiable(Times.Once());
        var queue = Sut;
        var first = await queue.CheckDownloadQueueServer(file.PlexServerId, CancellationToken);
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        first.Value.Id.ShouldBe(file.Id);
        var suppressed = await queue.CheckDownloadQueueServer(file.PlexServerId, CancellationToken);
        suppressed.IsSuccess.ShouldBeTrue();
        suppressed.Errors.Count.ShouldBe(0);
        suppressed.ValueOrDefault.ShouldBeNull();
        using var workerCancellation = new CancellationTokenSource();
        queue.Setup(workerCancellation.Token);
        var timer = await _clock.NextTimerAsync();
        if (stopReason == "paused")
            await IDbContext.PlexServers.ExecuteUpdateAsync(
                update => update.SetProperty(x => x.IsDownloadsPausedByUser, true), CancellationToken);
        if (stopReason == "disabled")
            await IDbContext.PlexServers.ExecuteUpdateAsync(
                update => update.SetProperty(x => x.IsEnabled, false), CancellationToken);
        _clock.DrainTimeReads();

        // Act
        try
        {
            if (stopReason == "shutdown")
                await StopQueueAsync(queue, workerCancellation);
            _clock.Advance(TimeSpan.FromSeconds(60));
            timer.Fire();
            if (stopReason != "shutdown")
            {
                await _clock.NextTimeReadAsync();
                await _clock.NextTimeReadAsync();
            }
        }
        finally
        {
            await StopQueueAsync(queue, workerCancellation);
        }

        // Assert
        await timer.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Test]
    public async Task ShouldWakeEachServerAtItsOwnDeadline_WhenCooldownsExpireAtDifferentTimes()
    {
        // Arrange
        await SetupDatabase(58127, config =>
        {
            config.PlexServerCount = 2;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 2;
        });
        var context = IDbContext;
        var roots = await context.DownloadTaskMovie.AsTracking().Include(x => x.Children)
            .OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var otherLibrary = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.Movie && x.PlexServerId != roots[0].PlexServerId, CancellationToken);
        new List<DownloadTaskMovie> { roots[1] }.SetRelationshipIds(otherLibrary.PlexServerId, otherLibrary.Id);
        await context.SaveChangesAsync(CancellationToken);
        var files = (await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken))
            .SelectMany(x => x.Children).OrderBy(x => x.PlexServerId).ToArray();
        files.Select(x => x.PlexServerId).Distinct().Count().ShouldBe(2);
        files.Length.ShouldBe(2);
        var started = new ConcurrentQueue<DownloadTaskKey>();
        var allResumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()))
            .Returns((DownloadTaskKey key, CancellationToken _) =>
            {
                started.Enqueue(key);
                if (started.Count == 4)
                    allResumed.TrySetResult();
                return Task.FromResult(Result.Ok());
            })
            .Verifiable(Times.Exactly(4));
        var queue = Sut;
        (await queue.CheckDownloadQueueServer(files[0].PlexServerId, CancellationToken)).Value.Id.ShouldBe(files[0].Id);
        _clock.Advance(TimeSpan.FromSeconds(20));
        (await queue.CheckDownloadQueueServer(files[1].PlexServerId, CancellationToken)).Value.Id.ShouldBe(files[1].Id);
        foreach (var file in files)
            (await queue.CheckDownloadQueueServer(file.PlexServerId, CancellationToken)).ValueOrDefault.ShouldBeNull();
        using var workerCancellation = new CancellationTokenSource();

        // Act
        queue.Setup(workerCancellation.Token);
        try
        {
            var firstTimer = await _clock.NextTimerAsync();
            firstTimer.DueTime.ShouldBe(TimeSpan.FromSeconds(40));
            _clock.Advance(TimeSpan.FromSeconds(40));
            firstTimer.Fire();
            var secondTimer = await _clock.NextTimerAsync();
            started.ToArray().ShouldBe([files[0].ToKey(), files[1].ToKey(), files[0].ToKey()]);
            secondTimer.DueTime.ShouldBe(TimeSpan.FromSeconds(20));
            _clock.Advance(TimeSpan.FromSeconds(20));
            secondTimer.Fire();
            await allResumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await StopQueueAsync(queue, workerCancellation);
        }

        // Assert
        started.ToArray().ShouldBe([files[0].ToKey(), files[1].ToKey(), files[0].ToKey(), files[1].ToKey()]);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
    }

    [Test]
    public async Task ShouldResumeRefreshedFileWithoutAnotherEvent_WhenRestartPublishesDuringCooldown()
    {
        // Arrange
        await SetupDatabase(58128, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 1;
            config.MovieDownloadTasksCount = 1;
        });
        var root = (await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken)).Single();
        var file = root.Children.Single();
        var source = await IDbContext.PlexMovieData.Include(x => x.PlexMovie).SingleAsync(CancellationToken);
        await IDbContext.DownloadTaskMovieFile.Where(x => x.Id == file.Id).ExecuteUpdateAsync(
            update => update.SetProperty(x => x.PlexApiMediaId, source.PlexApiMediaId)
                .SetProperty(x => x.PlexApiPartId, source.PlexApiPartId)
                .SetProperty(x => x.PlexApiRatingKey, source.PlexMovie!.PlexApiRatingKey)
                .SetProperty(x => x.DataReceived, 123), CancellationToken);
        var started = new ConcurrentQueue<DownloadTaskKey>();
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(file.PlexServerId))
            .ReturnsAsync([]);
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(file.ToKey(), It.IsAny<CancellationToken>()))
            .Returns((DownloadTaskKey key, CancellationToken _) =>
            {
                started.Enqueue(key);
                if (started.Count == 2)
                    resumed.TrySetResult();
                return Task.FromResult(Result.Ok());
            })
            .Verifiable(Times.Exactly(2));
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<StopDownloadTaskCommand>(command => command.DownloadTaskGuid == file.Id), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskUpdateDispatcher>()
            .Setup(x => x.OnStatusChangedAsync(It.IsAny<DownloadTaskKey>(), DownloadStatus.Restarting, CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Exactly(2));
        var queue = Sut;
        var queueHandler = new CheckDownloadQueueHandler(queue);
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(
                It.Is<CheckDownloadQueueEvent>(notification => notification.PlexServerIds.SequenceEqual(new[] { file.PlexServerId })),
                CancellationToken))
            .Returns((CheckDownloadQueueEvent notification, CancellationToken token) => queueHandler.HandleAsync(notification, token))
            .Verifiable(Times.Once());
        var initial = await queue.CheckDownloadQueueServer(file.PlexServerId, CancellationToken);
        initial.IsSuccess.ShouldBeTrue();
        initial.Errors.Count.ShouldBe(0);
        initial.Value.Id.ShouldBe(file.Id);
        using var workerCancellation = new CancellationTokenSource();
        queue.Setup(workerCancellation.Token);

        // Act
        Result restarted;
        try
        {
            restarted = await Mock.Create<RestartDownloadTaskCommandHandler>()
                .ExecuteAsync(new RestartDownloadTaskCommand(root.Id), CancellationToken);
            var timer = await _clock.NextTimerAsync();
            timer.DueTime.ShouldBe(TimeSpan.FromSeconds(60));
            var refreshed = await IDbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
            (refreshed.Id, refreshed.DownloadStatus, refreshed.DataReceived, refreshed.FileLocationUrl)
                .ShouldBe((file.Id, DownloadStatus.Queued, 0L, source.Key));
            started.ToArray().ShouldBe([file.ToKey()]);
            _clock.Advance(TimeSpan.FromSeconds(60));
            timer.Fire();
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            await StopQueueAsync(queue, workerCancellation);
        }

        // Assert
        restarted.IsSuccess.ShouldBeTrue();
        restarted.Errors.Count.ShouldBe(0);
        started.ToArray().ShouldBe([file.ToKey(), file.ToKey()]);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

    private static async Task StopQueueAsync(DownloadQueue queue, CancellationTokenSource cancellation)
    {
        await cancellation.CancelAsync();
        var worker = (Task)typeof(DownloadQueue)
            .GetField("_workerTask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(queue)!;
        await worker.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class RetryClock : TimeProvider
    {
        private long _ticks = DateTimeOffset.UnixEpoch.Ticks;
        private readonly Channel<PendingTimer> _timers = Channel.CreateUnbounded<PendingTimer>();
        private readonly Channel<long> _timeReads = Channel.CreateUnbounded<long>();

        public override DateTimeOffset GetUtcNow()
        {
            var ticks = Interlocked.Read(ref _ticks);
            _timeReads.Writer.TryWrite(ticks);
            return new DateTimeOffset(ticks, TimeSpan.Zero);
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new PendingTimer(callback, state, dueTime);
            _timers.Writer.TryWrite(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed) => Interlocked.Add(ref _ticks, elapsed.Ticks);

        public Task<PendingTimer> NextTimerAsync() =>
            _timers.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public Task<long> NextTimeReadAsync() =>
            _timeReads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public void DrainTimeReads()
        {
            while (_timeReads.Reader.TryRead(out _)) { }
        }
    }

    private sealed class PendingTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public TimeSpan DueTime { get; } = dueTime;
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Fire() => callback(state);
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => Disposed.TrySetResult();
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
