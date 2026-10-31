using System.Collections.Concurrent;

namespace Reaparr.Application.UnitTests;

public class DownloadQueueCheckDownloadQueueForAllServersUnitTests : BaseUnitTest<DownloadQueue>
{
    [Test]
    public async Task ShouldKickQueueForConfiguredPlexServers_WhenCheckingAllServers()
    {
        // Arrange
        await SetupDatabase(
            58121,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), CancellationToken))
            .ReturnOk();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        Sut.Setup(CancellationToken);

        // Act
        var result = await Sut.CheckDownloadQueueForAllServers(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await ShouldEventuallyAsync(() =>
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Verify(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), CancellationToken), Times.Once);
        });
    }

    [Test]
    public async Task ShouldContinueCheckingQueuedServers_WhenOneQueueCheckThrows()
    {
        // Arrange
        await SetupDatabase(
            58122,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.MovieDownloadTasksCount = 2;
            }
        );
        var dbContext = IDbContext;
        var downloadTasks = await dbContext.GetAllDownloadTasksByServerAsync(
            asTracking: true,
            cancellationToken: CancellationToken
        );
        downloadTasks.SetDownloadStatus(DownloadStatus.Queued);
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedKeys = downloadTasks.SelectMany(x => x.Children).Select(x => x.ToKey()).ToArray();
        expectedKeys.Length.ShouldBe(2);

        using var workerCancellation = new CancellationTokenSource();
        var workerToken = workerCancellation.Token;
        var firstAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var schedulerCalls = 0;
        var startedKeys = new ConcurrentQueue<DownloadTaskKey>();
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), workerToken))
            .Returns(
                (DownloadTaskKey key, CancellationToken _) =>
                {
                    startedKeys.Enqueue(key);
                    if (Interlocked.Increment(ref schedulerCalls) == 1)
                    {
                        firstAttempt.SetResult();
                        return Task.FromException<Result>(new InvalidOperationException("first queue check failed"));
                    }

                    secondAttempt.SetResult();
                    return Task.FromResult(Result.Ok());
                }
            )
            .Verifiable(Times.Exactly(2));
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.GetCurrentlyDownloadingKeysByServer(It.IsAny<int>()))
            .ReturnsAsync([]);

        var setupResult = Sut.Setup(workerToken);
        setupResult.IsSuccess.ShouldBeTrue();
        setupResult.Errors.Count.ShouldBe(0);

        // Act
        Result enqueueResult;
        try
        {
            enqueueResult = await Sut.CheckDownloadQueue(
                [downloadTasks[0].PlexServerId, downloadTasks[0].PlexServerId],
                CancellationToken.None
            );
            await firstAttempt.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await secondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            workerCancellation.Cancel();
        }

        // Assert
        enqueueResult.IsSuccess.ShouldBeTrue();
        enqueueResult.Errors.Count.ShouldBe(0);
        schedulerCalls.ShouldBe(2);
        startedKeys.ToArray().ShouldBe([expectedKeys[0], expectedKeys[0]]);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
    }

    private static async Task ShouldEventuallyAsync(Action assertion)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);
        Exception? lastException = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(50);
            }
        }

        if (lastException is not null)
            throw lastException;
    }
}
