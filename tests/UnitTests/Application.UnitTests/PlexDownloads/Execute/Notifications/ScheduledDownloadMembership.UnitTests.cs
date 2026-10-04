using Reaparr.External.Contracts;
using DownloadStatus = Reaparr.Domain.DownloadStatus;

namespace Reaparr.Application.UnitTests;

public class ScheduledDownloadMembershipUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    public async Task ShouldReallocateAfterDownloadingTransitions_WhenMoviesAndEpisodesShareAServer()
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
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = "Asia/Qatar";
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Sunday"] = new() { ["14:00"] = 5000, ["15:00"] = 1000, ["18:00"] = null } },
        };
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        var handler = new UpdateScheduledDownloadLimitsCommandHandler(
            new LoggerConfiguration().CreateLogger(), settings, Mock.Mock<IReaparrDbContextFactory>().Object, speedLimits,
            Mock.Mock<TimeProvider>().Object);
        var membershipAtTrigger = new List<int[]>();
        var limitsAtRefresh = new List<long>();
        var machineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(movies[0].PlexServerId);
        Mock.Mock<IDownloadTaskScheduler>().Setup(x => x.IsServerDownloading(movies[0].PlexServerId))
            .ReturnsAsync(true).Verifiable(Times.Exactly(4));
        Mock.Mock<TimeProvider>().Setup(x => x.GetUtcNow())
            .Returns(DateTimeOffset.Parse("2026-10-04T12:49:00Z")).Verifiable(Times.Exactly(8));
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<UpdateScheduledDownloadLimitsCommand>(c => c.LocalTime == null),
                CancellationToken.None))
            .Returns<UpdateScheduledDownloadLimitsCommand, CancellationToken>(async (command, token) =>
            {
                using var observedDb = IDbContext;
                membershipAtTrigger.Add(await observedDb.DownloadTaskMovieFile
                    .Where(x => x.DownloadStatus == DownloadStatus.Downloading).Select(x => x.PlexServerId)
                    .Union(observedDb.DownloadTaskTvShowEpisodeFile
                        .Where(x => x.DownloadStatus == DownloadStatus.Downloading).Select(x => x.PlexServerId))
                    .OrderBy(x => x).ToArrayAsync(CancellationToken));
                var result = await handler.ExecuteAsync(command, token);
                limitsAtRefresh.Add(speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier));
                return result;
            }).Verifiable(Times.Exactly(8));
        var sut = Sut;

        // Act
        await sut.OnStatusChangedAsync(movies[0].ToKey(), DownloadStatus.Downloading, CancellationToken);
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(1024000);
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
        membershipAtTrigger.ShouldBe(new int[][]
        {
            [movies[0].PlexServerId],
            [movies[0].PlexServerId],
            [movies[0].PlexServerId],
            [movies[0].PlexServerId],
            [movies[0].PlexServerId],
            [],
            [movies[0].PlexServerId],
            [],
        });
        limitsAtRefresh.ShouldBe(new long[] { 1024000, 1024000, 1024000, 1024000, 1024000, 0, 1024000, 0 });
        settings.ServerSettings.Data.ShouldBeEmpty();
        var movieStatuses = await dbContext.DownloadTaskMovieFile.AsNoTracking()
            .Where(x => x.Id == movies[0].Id || x.Id == movies[1].Id)
            .ToDictionaryAsync(x => x.Id, x => x.DownloadStatus, CancellationToken);
        movieStatuses[movies[0].Id].ShouldBe(DownloadStatus.DownloadClientError);
        movieStatuses[movies[1].Id].ShouldBe(DownloadStatus.DownloadFinished);
        (await dbContext.DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .Where(x => x.Id == episode.Id).Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
            .ShouldBe(DownloadStatus.Stopped);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<TimeProvider>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify();
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
        var machineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(file.PlexServerId);
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = "UTC";
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Sunday"] = new() { ["00:00"] = 1000 } },
        };
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        speedLimits.SetScheduledDownloadSpeedLimits(new Dictionary<string, long> { [machineIdentifier] = 1024000 });
        var handler = new UpdateScheduledDownloadLimitsCommandHandler(
            new LoggerConfiguration().CreateLogger(), settings, Mock.Mock<IReaparrDbContextFactory>().Object, speedLimits,
            Mock.Mock<TimeProvider>().Object);
        Mock.Mock<TimeProvider>().Setup(x => x.GetUtcNow())
            .Returns(DateTimeOffset.Parse("2026-10-04T12:49:00Z")).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .Returns<UpdateScheduledDownloadLimitsCommand, CancellationToken>(handler.ExecuteAsync).Verifiable(Times.Once());

        // Act
        await Sut.OnStatusChangedAsync(file.ToKey(), DownloadStatus.AutoPaused, CancellationToken);

        // Assert
        (await dbContext.DownloadTaskMovieFile.AsNoTracking().Where(x => x.Id == file.Id)
            .Select(x => x.DownloadStatus).SingleAsync(CancellationToken)).ShouldBe(DownloadStatus.AutoPaused);
        (await dbContext.DownloadTaskMovieFile.AnyAsync(x => x.DownloadStatus == DownloadStatus.Downloading, CancellationToken))
            .ShouldBeFalse();
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<TimeProvider>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify(x => x.IsServerDownloading(file.PlexServerId), Times.Never());
    }

    [Test]
    [Arguments(DownloadStatus.Queued)]
    [Arguments(DownloadStatus.Downloading)]
    public async Task ShouldStartDashWithTheCurrentScheduledLimit_WhenEnteringDownloading(DownloadStatus initialStatus)
    {
        // Arrange
        await SetupDatabase(85313, config => config.MovieDownloadTasksCount = 1);
        using var dbContext = IDbContext;
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            x => x.SetProperty(p => p.DownloadStatus, initialStatus), CancellationToken);
        var file = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        var machineIdentifier = await dbContext.GetPlexServerMachineIdentifierById(file.PlexServerId);
        var settings = new UserSettings();
        settings.DateTimeSettings.TimeZone = "Asia/Qatar";
        settings.DownloadManagerSettings.DownloadSchedule = new DownloadSchedule
        {
            Enabled = true,
            Days = new() { ["Sunday"] = new() { ["14:00"] = 5000, ["15:00"] = 1000, ["18:00"] = null } },
        };
        using var speedLimits = new DownloadSpeedLimitProvider(settings);
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(0);
        var handler = new UpdateScheduledDownloadLimitsCommandHandler(
            new LoggerConfiguration().CreateLogger(), settings, Mock.Mock<IReaparrDbContextFactory>().Object, speedLimits,
            Mock.Mock<TimeProvider>().Object);
        var wrapper = new Mock<IDashMpdCliWrapper>(MockBehavior.Strict);
        string? launchedLimit = null;
        wrapper.SetupGet(x => x.Progress).Returns(Observable.Empty<DashDownloadProgress>());
        wrapper.SetupGet(x => x.DownloadCompleted)
            .Returns(Observable.Empty<DashDownloadCompletedEventArgs>());
        wrapper.Setup(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()))
            .Returns<DashMpdCliOptions>(async options =>
            {
                (await dbContext.DownloadTaskMovieFile.Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
                    .ShouldBe(DownloadStatus.Downloading);
                launchedLimit = options.LimitRate;
                return Result.Ok();
            }).Verifiable(Times.Once());
        wrapper.Setup(x => x.StopAsync()).ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>().Setup(x => x.IsServerDownloading(file.PlexServerId))
            .ReturnsAsync(true).Verifiable(initialStatus == DownloadStatus.Queued ? Times.Once() : Times.Never());
        Mock.Mock<TimeProvider>().Setup(x => x.GetUtcNow())
            .Returns(DateTimeOffset.Parse("2026-10-04T12:49:00Z")).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>().Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .Returns<UpdateScheduledDownloadLimitsCommand, CancellationToken>(handler.ExecuteAsync).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<ICommand<Result<GetTranscodeUrlResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new GetTranscodeUrlResult
            {
                DownloadUrl = "https://plex.example/start.mpd",
                TranscodedQuality = VideoQuality.SD,
            })).Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<EnsureDownloadDirectoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok()).Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>().Setup(x => x.SendRefreshNotificationAsync(It.IsAny<RefreshDataType>()))
            .Returns(Task.CompletedTask);
        var dispatcher = Sut;
        await using var client = Mock.Create<DashPlexDownloadClient>(
            new NamedParameter("dashWrapper", wrapper.Object),
            new NamedParameter("downloadTaskUpdateDispatcher", dispatcher),
            new NamedParameter("speedLimits", speedLimits));

        // Act
        var result = await client.Start(file.ToKey(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        launchedLimit.ShouldBe("1024000");
        speedLimits.GetEffectiveDownloadSpeedLimit(machineIdentifier).ShouldBe(1024000);
        wrapper.Verify(x => x.StartAsync(It.IsAny<DashMpdCliOptions>()), Times.Once());
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<TimeProvider>().Verify();
        Mock.Mock<IDownloadTaskScheduler>().Verify();
    }
}
