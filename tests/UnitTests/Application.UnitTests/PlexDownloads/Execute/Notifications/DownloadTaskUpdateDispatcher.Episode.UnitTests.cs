namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherEpisodeUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    public async Task ShouldSendHundredPercentPatch_WhenEpisodeFileIsCompletedWithStaleStoredPercentage()
    {
        // Arrange - regression for completed TV episode files where the DB row still holds a stale
        // download-phase percentage (for example 2%), but the completed patch must still report 100%.
        await SetupDatabase(
            84331,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var episodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.AsNoTracking().FirstAsync(CancellationToken);

        await IDbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == episodeFile.Id)
            .ExecuteUpdateAsync(
                p =>
                    p.SetProperty(x => x.DataTotal, 1_000L)
                        .SetProperty(x => x.DataReceived, 20L)
                        .SetProperty(x => x.FileDataTransferred, 1_000L)
                        .SetProperty(x => x.CurrentFileTransferBytesOffset, 1_000L)
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
        sut.NotifyFileTransferProgress(episodeFile.ToKey());

        await WaitForPatchCount(capturedPatches, 1);
        await sut.StopAsync(CancellationToken.None);

        // Assert
        var fileTaskPatch = capturedPatches.SelectMany(x => x).FirstOrDefault(x => x.Id == episodeFile.Id);
        fileTaskPatch.ShouldNotBeNull();
        fileTaskPatch.Status.ShouldBe(DownloadStatus.Completed);
        fileTaskPatch.Percentage.ShouldBe(100m);
    }
    [Test]
    public async Task ShouldIncludeSeasonAndTvShowInPatch_WhenEpisodeProgressIsUpdated()
    {
        // Arrange
        await SetupDatabase(
            84321,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var episodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);

        var season = await IDbContext.DownloadTaskTvShowSeason.FirstAsync(CancellationToken);
        var tvShow = await IDbContext.DownloadTaskTvShow.FirstAsync(CancellationToken);

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
        sut.OnProgressUpdated(
            episodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 500,
                Percentage = 50,
                DownloadSpeed = 100,
            }
        );

        await sut.StartAsync(CancellationToken.None);

        for (var i = 0; i < 30 && capturedPatches.Count == 0; i++)
        {
            await Task.Delay(100, CancellationToken);
        }

        await sut.StopAsync(CancellationToken.None);

        // Assert
        capturedPatches.ShouldNotBeEmpty();

        var patchIds = capturedPatches.SelectMany(x => x).Select(x => x.Id).ToHashSet();
        patchIds.ShouldContain(episodeFile.Id);
        patchIds.ShouldContain(season.Id);
        patchIds.ShouldContain(tvShow.Id);
    }
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
    public async Task ShouldIncludeSeasonAndTvShowInPatch_WhenEpisodeStatusChanges()
    {
        await SetupDatabase(
            84335,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );

        var episodeFiles = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        episodeFiles.Count.ShouldBe(2);

        var completedEpisodeFile = episodeFiles[0];
        var siblingEpisodeFile = episodeFiles[1];

        var sut = Sut;

        await sut.OnStatusChangedAsync(completedEpisodeFile.ToKey(), DownloadStatus.Downloading, CancellationToken);
        await sut.OnStatusChangedAsync(siblingEpisodeFile.ToKey(), DownloadStatus.Downloading, CancellationToken);

        var runningSnapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 30,
            Status = 2,
            Urls = ["http://plex/running.mkv"],
            TotalFileSize = 1_000,
            FileName = "running.mkv",
            DownloadingFileExtension = ".reaptemp",
            Chunks = [],
            IsSupportDownloadInRange = true,
        };

        var completedSnapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 100,
            Status = 5,
            Urls = ["http://plex/completed.mkv"],
            TotalFileSize = 1_000,
            FileName = "completed.mkv",
            DownloadingFileExtension = ".reaptemp",
            Chunks = [],
            IsSupportDownloadInRange = true,
        };

        var siblingSnapshot = new DirectDownloadSnapshot
        {
            SaveProgress = 25,
            Status = 2,
            Urls = ["http://plex/sibling.mkv"],
            TotalFileSize = 2_000,
            FileName = "sibling.mkv",
            DownloadingFileExtension = ".reaptemp",
            Chunks = [],
            IsSupportDownloadInRange = true,
        };

        sut.OnProgressUpdated(
            completedEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1_000,
                DataReceived = 300,
                Percentage = 30,
                DownloadSpeed = 30,
                TimeRemaining = 10,
            },
            runningSnapshot
        );

        sut.OnProgressUpdated(
            siblingEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 2_000,
                DataReceived = 400,
                Percentage = 20,
                DownloadSpeed = 40,
                TimeRemaining = 20,
            },
            siblingSnapshot
        );

        sut.OnProgressUpdated(
            completedEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1_000,
                DataReceived = 1_000,
                Percentage = 100,
                DownloadSpeed = 0,
                TimeRemaining = 0,
            },
            completedSnapshot
        );

        sut.OnProgressUpdated(
            siblingEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 2_000,
                DataReceived = 500,
                Percentage = 25,
                DownloadSpeed = 123,
                TimeRemaining = 12,
            },
            siblingSnapshot
        );

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(1500, CancellationToken);
        await sut.StopAsync(CancellationToken.None);

        var updatedCompletedEpisodeFile = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .FirstAsync(x => x.Id == completedEpisodeFile.Id, CancellationToken);
        var updatedSiblingEpisodeFile = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .FirstAsync(x => x.Id == siblingEpisodeFile.Id, CancellationToken);

        updatedCompletedEpisodeFile.DirectDownloadSnapshot.ShouldNotBeNull();
        updatedCompletedEpisodeFile.DirectDownloadSnapshot!.Status.ShouldBe(5);
        updatedCompletedEpisodeFile.DirectDownloadSnapshot.SaveProgress.ShouldBe(100);
        updatedCompletedEpisodeFile.DirectDownloadSnapshot.FileName.ShouldBe("completed.mkv");

        updatedSiblingEpisodeFile.DirectDownloadSnapshot.ShouldNotBeNull();
        updatedSiblingEpisodeFile.DirectDownloadSnapshot!.Status.ShouldBe(2);
        updatedSiblingEpisodeFile.DirectDownloadSnapshot.FileName.ShouldBe("sibling.mkv");
    }

    [Test]
    public async Task ShouldPersistCompletedEpisodeProgress_WhenSiblingEpisodeProgressArrivesBeforeFlush()
    {
        await SetupDatabase(
            84333,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 2;
            }
        );

        var episodeFiles = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);

        episodeFiles.Count.ShouldBe(2);

        var completedEpisodeFile = episodeFiles[0];
        var siblingEpisodeFile = episodeFiles[1];
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        Mock.Mock<IDownloadTaskScheduler>().Setup(x => x.IsServerDownloading(completedEpisodeFile.PlexServerId))
            .ReturnsAsync(true);

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

        var sut = Sut;

        await sut.StartAsync(CancellationToken.None);

        await sut.OnStatusChangedAsync(completedEpisodeFile.ToKey(), DownloadStatus.Downloading, CancellationToken);
        await sut.OnStatusChangedAsync(siblingEpisodeFile.ToKey(), DownloadStatus.Downloading, CancellationToken);

        sut.OnProgressUpdated(
            completedEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1_000,
                DataReceived = 300,
                Percentage = 30,
                DownloadSpeed = 30,
                TimeRemaining = 10,
            }
        );
        sut.OnProgressUpdated(
            siblingEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 2_000,
                DataReceived = 400,
                Percentage = 20,
                DownloadSpeed = 40,
                TimeRemaining = 20,
            }
        );

        await WaitForPatchCount(capturedPatches, 4);

        sut.OnProgressUpdated(
            completedEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1_000,
                DataReceived = 1_000,
                Percentage = 100,
                DownloadSpeed = 0,
                TimeRemaining = 0,
            }
        );

        sut.OnProgressUpdated(
            siblingEpisodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 2_000,
                DataReceived = 500,
                Percentage = 25,
                DownloadSpeed = 123,
                TimeRemaining = 12,
            }
        );

        await WaitUntilAsync(
            async () =>
            {
                var persistedEpisodeFiles = await IDbContext
                    .DownloadTaskTvShowEpisodeFile.AsNoTracking()
                    .Where(x => x.Id == completedEpisodeFile.Id || x.Id == siblingEpisodeFile.Id)
                    .ToListAsync(CancellationToken);

                var persistedCompletedEpisodeFile = persistedEpisodeFiles.Single(x => x.Id == completedEpisodeFile.Id);
                var persistedSiblingEpisodeFile = persistedEpisodeFiles.Single(x => x.Id == siblingEpisodeFile.Id);

                if (
                    persistedCompletedEpisodeFile
                    is not { DataReceived: 1_000, DataTotal: 1_000, Percentage: 100, DownloadSpeed: 0 }
                )
                    return false;

                return persistedSiblingEpisodeFile
                    is { DataReceived: 500, DataTotal: 2_000, Percentage: 25, DownloadSpeed: 123 };
            },
            timeoutMs: 10_000
        );

        await sut.StopAsync(CancellationToken.None);

        var updatedEpisodeFiles = await IDbContext
            .DownloadTaskTvShowEpisodeFile.AsNoTracking()
            .Where(x => x.Id == completedEpisodeFile.Id || x.Id == siblingEpisodeFile.Id)
            .ToListAsync(CancellationToken);

        var updatedCompletedEpisodeFile = updatedEpisodeFiles.Single(x => x.Id == completedEpisodeFile.Id);
        var updatedSiblingEpisodeFile = updatedEpisodeFiles.Single(x => x.Id == siblingEpisodeFile.Id);

        updatedCompletedEpisodeFile.DataReceived.ShouldBe(1_000);
        updatedCompletedEpisodeFile.DataTotal.ShouldBe(1_000);
        updatedCompletedEpisodeFile.Percentage.ShouldBe(100);
        updatedCompletedEpisodeFile.DownloadSpeed.ShouldBe(0);

        updatedSiblingEpisodeFile.DataReceived.ShouldBe(500);
        updatedSiblingEpisodeFile.DataTotal.ShouldBe(2_000);
        updatedSiblingEpisodeFile.Percentage.ShouldBe(25);
        updatedSiblingEpisodeFile.DownloadSpeed.ShouldBe(123);
    }
    [Test]
    public async Task ShouldIncludeSeasonAndTvShowInPatch_WhenEpisodeStatusChanges()
    {
        await SetupDatabase(
            84324,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var episodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(episodeFile.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        var season = await IDbContext.DownloadTaskTvShowSeason.FirstAsync(CancellationToken);
        var tvShow = await IDbContext.DownloadTaskTvShow.FirstAsync(CancellationToken);

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
            .Verifiable(Times.Once());

        var sut = Sut;
        await sut.StartAsync(CancellationToken.None);

        await sut.OnStatusChangedAsync(episodeFile.ToKey(), DownloadStatus.Downloading, CancellationToken);
        await WaitForPatchCount(capturedPatches, 1);
        await sut.StopAsync(CancellationToken.None);

        var patchIds = capturedPatches.SelectMany(x => x).Select(x => x.Id).ToHashSet();
        patchIds.ShouldContain(episodeFile.Id);
        patchIds.ShouldContain(season.Id);
        patchIds.ShouldContain(tvShow.Id);
    }
    [Test]
    public async Task ShouldSendOnlyLatestProgress_WhenMultipleUpdatesAreBufferedBeforeFlush()
    {
        await SetupDatabase(
            84322,
            config =>
            {
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var episodeFile = await IDbContext.DownloadTaskTvShowEpisodeFile.FirstAsync(CancellationToken);

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
            episodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 100,
                Percentage = 10,
                DownloadSpeed = 10,
                TimeRemaining = 90,
            }
        );
        sut.OnProgressUpdated(
            episodeFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 900,
                Percentage = 90,
                DownloadSpeed = 90,
                TimeRemaining = 12,
            }
        );

        await sut.StartAsync(CancellationToken.None);

        await WaitForPatchCount(capturedPatches, 1);
        await sut.StopAsync(CancellationToken.None);

        var leafPatch = capturedPatches.SelectMany(x => x).FirstOrDefault(x => x.Id == episodeFile.Id);

        leafPatch.ShouldNotBeNull();
        leafPatch.DataReceived.ShouldBe(900);
        leafPatch.Percentage.ShouldBe(90);
        leafPatch.TimeRemaining.ShouldBe(12);
    }
}
