using Quartz;

namespace Reaparr.Application.UnitTests;

public class ScheduledDownloadMembershipUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    public async Task ShouldReallocateOnlyOnFirstAndLastFileTransitions_WhenMoviesAndEpisodesShareAServer()
    {
        // Arrange
        await SetupDatabase(85311, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieDownloadTasksCount = 2;
            config.TvShowDownloadTasksCount = 1;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
        });
        using var dbContext = IDbContext;
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, DownloadStatus.Queued), CancellationToken);
        await dbContext.DownloadTaskTvShowEpisodeFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, DownloadStatus.Queued), CancellationToken);
        var movies = await dbContext.DownloadTaskMovieFile.AsNoTracking().OrderBy(x => x.Id).Take(2).ToListAsync(CancellationToken);
        var episode = await dbContext.DownloadTaskTvShowEpisodeFile.AsNoTracking().FirstAsync(CancellationToken);
        movies.Count.ShouldBe(2);
        episode.PlexServerId.ShouldBe(movies[0].PlexServerId);
        movies[1].PlexServerId.ShouldBe(movies[0].PlexServerId);
        var membershipAtTrigger = new List<int[]>();
        var jobKey = UpdateScheduledDownloadLimitsJob.GetJobKey();
        Mock.Mock<IScheduler>().Setup(x => x.CheckExists(jobKey, CancellationToken.None))
            .ReturnsAsync(true).Verifiable(Times.Exactly(4));
        Mock.Mock<IScheduler>().Setup(x => x.TriggerJob(jobKey, CancellationToken.None))
            .Returns(async () =>
            {
                using var observedDb = IDbContext;
                membershipAtTrigger.Add(await observedDb.DownloadTaskMovieFile
                    .Where(x => x.DownloadStatus == DownloadStatus.Downloading).Select(x => x.PlexServerId)
                    .Union(observedDb.DownloadTaskTvShowEpisodeFile
                        .Where(x => x.DownloadStatus == DownloadStatus.Downloading).Select(x => x.PlexServerId))
                    .OrderBy(x => x).ToArrayAsync(CancellationToken));
            }).Verifiable(Times.Exactly(4));
        var sut = Sut;

        // Act
        await sut.OnStatusChangedAsync(movies[0].ToKey(), DownloadStatus.Downloading, CancellationToken);
        sut.OnProgressUpdated(movies[0].ToKey(), new DownloadTaskProgress
        {
            DataTotal = 1000, DataReceived = 500, Percentage = 50, DownloadSpeed = 100,
        });
        await sut.OnStatusChangedAsync(movies[1].ToKey(), DownloadStatus.Downloading, CancellationToken);
        await sut.OnStatusChangedAsync(episode.ToKey(), DownloadStatus.Downloading, CancellationToken);
        await sut.OnStatusChangedAsync(movies[0].ToKey(), DownloadStatus.Paused, CancellationToken);
        await sut.OnStatusChangedAsync(movies[1].ToKey(), DownloadStatus.DownloadFinished, CancellationToken);
        await sut.OnStatusChangedAsync(episode.ToKey(), DownloadStatus.Stopped, CancellationToken);
        await sut.OnStatusChangedAsync(movies[0].ToKey(), DownloadStatus.Downloading, CancellationToken);
        await sut.OnStatusChangedAsync(movies[0].ToKey(), DownloadStatus.DownloadClientError, CancellationToken);

        // Assert
        membershipAtTrigger.Count.ShouldBe(4);
        membershipAtTrigger[0].ShouldBe(new[] { movies[0].PlexServerId });
        membershipAtTrigger[1].ShouldBeEmpty();
        membershipAtTrigger[2].ShouldBe(new[] { movies[0].PlexServerId });
        membershipAtTrigger[3].ShouldBeEmpty();
        var movieStatuses = await dbContext.DownloadTaskMovieFile.AsNoTracking()
            .Where(x => x.Id == movies[0].Id || x.Id == movies[1].Id)
            .ToDictionaryAsync(x => x.Id, x => x.DownloadStatus, CancellationToken);
        movieStatuses[movies[0].Id].ShouldBe(DownloadStatus.DownloadClientError);
        movieStatuses[movies[1].Id].ShouldBe(DownloadStatus.DownloadFinished);
        (await dbContext.DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .Where(x => x.Id == episode.Id).Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
            .ShouldBe(DownloadStatus.Stopped);
        Mock.Mock<IScheduler>().Verify();
    }

    [Test]
    public async Task ShouldKeepPausedFilesExcluded_WhenTheLastDownloadingFileIsAutoPaused()
    {
        // Arrange
        await SetupDatabase(85312, config => config.MovieDownloadTasksCount = 1);
        using var dbContext = IDbContext;
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, DownloadStatus.Paused), CancellationToken);
        var file = await dbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        await dbContext.DownloadTaskMovieFile.Where(x => x.Id == file.Id).ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.DownloadStatus, DownloadStatus.Downloading), CancellationToken);
        var jobKey = UpdateScheduledDownloadLimitsJob.GetJobKey();
        Mock.Mock<IScheduler>().Setup(x => x.CheckExists(jobKey, CancellationToken.None))
            .ReturnsAsync(true).Verifiable(Times.Once());
        Mock.Mock<IScheduler>().Setup(x => x.TriggerJob(jobKey, CancellationToken.None))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        await Sut.OnStatusChangedAsync(file.ToKey(), DownloadStatus.AutoPaused, CancellationToken);

        // Assert
        (await dbContext.DownloadTaskMovieFile.AsNoTracking().Where(x => x.Id == file.Id)
            .Select(x => x.DownloadStatus).SingleAsync(CancellationToken)).ShouldBe(DownloadStatus.AutoPaused);
        (await dbContext.DownloadTaskMovieFile.AnyAsync(x => x.DownloadStatus == DownloadStatus.Downloading, CancellationToken))
            .ShouldBeFalse();
        Mock.Mock<IScheduler>().Verify();
    }
}
