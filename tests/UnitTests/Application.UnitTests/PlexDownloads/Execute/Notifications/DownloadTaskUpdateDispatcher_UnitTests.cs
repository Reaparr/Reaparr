using System.Collections.Concurrent;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
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
    public async Task ShouldIncreasePatchSequence_WhenMultiplePatchesAreDispatchedForSameServer()
    {
        await SetupDatabase(84323, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

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
    public async Task ShouldIncreaseSequenceAcrossMixedStatusAndProgressPatches()
    {
        // Arrange
        await SetupDatabase(84326, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

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
    public async Task ShouldPersistBufferedProgressBeforeStatusBecomesPaused()
    {
        await SetupDatabase(84327, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);

        var sut = Sut;
        sut.OnProgressUpdated(
            movieFile.ToKey(),
            new DownloadTaskProgress
            {
                DataTotal = 1000,
                DataReceived = 750,
                Percentage = 75,
                DownloadSpeed = 10,
            }
        );

        await sut.OnStatusChangedAsync(movieFile.ToKey(), DownloadStatus.Paused, CancellationToken);

        var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        updatedMovieFile.DownloadStatus.ShouldBe(DownloadStatus.Paused);
        updatedMovieFile.DataReceived.ShouldBe(750);
        updatedMovieFile.DataTotal.ShouldBe(1000);
        updatedMovieFile.DownloadSpeed.ShouldBe(0);
    }

    [Test]
    public async Task ShouldIgnoreProgressUpdateAfterTaskHasBeenPaused()
    {
        await SetupDatabase(84328, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
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
            downloadHubService.Object
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
            return updatedMovieFile.DownloadStatus == DownloadStatus.Paused
                && updatedMovieFile.DataReceived == initialProgress.DataReceived;
        });

        var updatedMovieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);
        updatedMovieFile.DownloadStatus.ShouldBe(DownloadStatus.Paused);
        updatedMovieFile.DataReceived.ShouldBe(initialProgress.DataReceived);
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
    public async Task ShouldPersistCompletedEpisodeSnapshot_WhenSiblingEpisodeProgressArrivesBeforeFlush()
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
    public async Task ShouldNotWriteDownloadingLogAfterPauseTransition()
    {
        await SetupDatabase(84329, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.AsNoTracking().FirstAsync(CancellationToken);

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
            return pauseLog is not null
                && logs.Where(x => x.Id > pauseLog.Id && x.Status == DownloadStatus.Downloading).Count() == 0;
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
    public async Task ShouldSendDeletedIds_WhenStatusChangedToDeleted()
    {
        // Arrange
        await SetupDatabase(84336, config => config.MovieDownloadTasksCount = 1);
        var movieFile = await IDbContext.DownloadTaskMovieFile.FirstAsync(CancellationToken);

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
}
