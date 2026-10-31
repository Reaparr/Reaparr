namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherMixedMediaUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    public async Task ShouldPersistBothProgressLogs_WhenMediaFamiliesGenerateTheSameLogId()
    {
        // Arrange
        await SetupDatabase(
            84336,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );
        using var arrangeContext = IDbContext;
        var movie = await arrangeContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var episode = await arrangeContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken);
        (await arrangeContext.DownloadTaskMovieFileLogs.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await arrangeContext.DownloadTaskTvShowEpisodeFileLogs.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var logsPersisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    It.IsAny<int>(),
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => SignalWhenLogsPersistedAsync(logsPersisted))
            .Verifiable(Times.AtLeastOnce());
        var sut = Sut;

        // Act
        var progress = new DownloadTaskProgress
        {
            DataTotal = 1000,
            DataReceived = 250,
            Percentage = 25,
            DownloadSpeed = 100,
        };
        sut.OnProgressUpdated(movie.ToKey(), progress);
        sut.OnProgressUpdated(episode.ToKey(), progress);
        await sut.StartAsync(CancellationToken.None);
        try
        {
            await logsPersisted.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }

        // Assert
        using var assertContext = IDbContext;
        var movieLogs = await assertContext.DownloadTaskMovieFileLogs.ToListAsync(CancellationToken);
        movieLogs
            .Select(x => (x.Id, x.DownloadTaskFileId, x.DownloadTaskMovieId, x.Status, x.LogLevel))
            .ShouldBe([(1, movie.Id, movie.ParentId, DownloadStatus.Downloading, NotificationLevel.Debug)]);
        var episodeLogs = await assertContext.DownloadTaskTvShowEpisodeFileLogs.ToListAsync(CancellationToken);
        episodeLogs
            .Select(x => (x.Id, x.DownloadTaskFileId, x.DownloadTaskTvShowEpisodeId, x.Status, x.LogLevel))
            .ShouldBe([(1, episode.Id, episode.ParentId, DownloadStatus.Downloading, NotificationLevel.Debug)]);
        var persistedMovie = await assertContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        (
            persistedMovie.DataTotal,
            persistedMovie.DataReceived,
            persistedMovie.Percentage,
            persistedMovie.DownloadSpeed
        ).ShouldBe((1000L, 250L, 25m, 100L));
        var persistedEpisode = await assertContext.DownloadTaskTvShowEpisodeFile.SingleAsync(CancellationToken);
        (
            persistedEpisode.DataTotal,
            persistedEpisode.DataReceived,
            persistedEpisode.Percentage,
            persistedEpisode.DownloadSpeed
        ).ShouldBe((1000L, 250L, 25m, 100L));
        Mock.Mock<IDownloadHubService>().Verify();
    }

    private async Task SignalWhenLogsPersistedAsync(TaskCompletionSource signal)
    {
        using var context = IDbContext;
        if (
            await context.DownloadTaskMovieFileLogs.CountAsync(CancellationToken) == 1
            && await context.DownloadTaskTvShowEpisodeFileLogs.CountAsync(CancellationToken) == 1
        )
            signal.TrySetResult();
    }
}
