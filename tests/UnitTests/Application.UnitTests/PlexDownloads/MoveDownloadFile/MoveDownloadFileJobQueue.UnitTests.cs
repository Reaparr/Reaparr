namespace Reaparr.Application.UnitTests;

public class MoveDownloadFileJobQueueUnitTests : BaseUnitTest<MoveDownloadFileJobQueue>
{
    [Test]
    [Arguments(PlexMediaType.Movie, DownloadStatus.DownloadFinished)]
    [Arguments(PlexMediaType.Movie, DownloadStatus.MoveError)]
    [Arguments(PlexMediaType.Episode, DownloadStatus.DownloadFinished)]
    [Arguments(PlexMediaType.Episode, DownloadStatus.MoveError)]
    [Arguments(PlexMediaType.PhotoImage, DownloadStatus.DownloadFinished)]
    [Arguments(PlexMediaType.PhotoImage, DownloadStatus.MoveError)]
    [Arguments(PlexMediaType.MusicTrack, DownloadStatus.DownloadFinished)]
    [Arguments(PlexMediaType.MusicTrack, DownloadStatus.MoveError)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.DownloadFinished)]
    [Arguments(PlexMediaType.OtherVideos, DownloadStatus.MoveError)]
    public async Task ShouldScheduleOnlyOldestEligibleFileAcrossAllFamilies_WhenCheckingMoveQueue(
        PlexMediaType type,
        DownloadStatus status
    )
    {
        // Arrange
        await SetupDatabase(
            88301,
            config =>
            {
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistDownloadTasksCount = 1;
                config.MusicAlbumDownloadTasksCount = 1;
                config.MusicTrackDownloadTasksCount = 1;
                config.MusicTrackFileDownloadTasksCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var photoLibrary = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var album = FakeData.GetDownloadTaskPhotoAlbum(new Seed(88301)).Generate();
        var photoFile = album.Children.Single().Children.Single();
        foreach (
            var node in new DownloadTaskBase[] { album }
                .Concat(album.Children)
                .Append(photoFile)
        )
        {
            node.PlexServerId = photoLibrary.PlexServerId;
            node.PlexLibraryId = photoLibrary.Id;
        }
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        var files = new DownloadTaskFileBase[]
        {
            await dbContext.DownloadTaskMovieFile.AsTracking().SingleAsync(CancellationToken),
            await dbContext.DownloadTaskTvShowEpisodeFile.AsTracking().SingleAsync(CancellationToken),
            photoFile,
            await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken),
            await dbContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken),
        };
        var createdAt = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        foreach (var file in files)
        {
            file.DownloadStatus = DownloadStatus.Queued;
        }
        var target = files.Single(x => x.MediaType == type);
        var readyControl = files.First(x => x.Id != target.Id);
        target.DownloadStatus = status;
        readyControl.DownloadStatus = status;
        await dbContext.SaveChangesAsync(CancellationToken);
        var controlCreatedAt = createdAt.AddMinutes(5);
        var queuedCreatedAt = createdAt.AddMinutes(-10);
        await dbContext.DownloadTaskMovieFile.ExecuteUpdateAsync(
            set =>
                set.SetProperty(
                    x => x.CreatedAt,
                    x =>
                        x.Id == target.Id ? createdAt
                        : x.Id == readyControl.Id ? controlCreatedAt
                        : queuedCreatedAt
                ),
            CancellationToken
        );
        await dbContext.DownloadTaskTvShowEpisodeFile.ExecuteUpdateAsync(
            set =>
                set.SetProperty(
                    x => x.CreatedAt,
                    x =>
                        x.Id == target.Id ? createdAt
                        : x.Id == readyControl.Id ? controlCreatedAt
                        : queuedCreatedAt
                ),
            CancellationToken
        );
        await dbContext.DownloadTaskPhotoImageFiles.ExecuteUpdateAsync(
            set =>
                set.SetProperty(
                    x => x.CreatedAt,
                    x =>
                        x.Id == target.Id ? createdAt
                        : x.Id == readyControl.Id ? controlCreatedAt
                        : queuedCreatedAt
                ),
            CancellationToken
        );
        await dbContext.DownloadTaskMusicTrackFiles.ExecuteUpdateAsync(
            set =>
                set.SetProperty(
                    x => x.CreatedAt,
                    x =>
                        x.Id == target.Id ? createdAt
                        : x.Id == readyControl.Id ? controlCreatedAt
                        : queuedCreatedAt
                ),
            CancellationToken
        );
        await dbContext.DownloadTaskOtherVideoFiles.ExecuteUpdateAsync(
            set =>
                set.SetProperty(
                    x => x.CreatedAt,
                    x =>
                        x.Id == target.Id ? createdAt
                        : x.Id == readyControl.Id ? controlCreatedAt
                        : queuedCreatedAt
                ),
            CancellationToken
        );
        var before = files
            .Select(x =>
                (
                    x.Id,
                    x.DownloadStatus,
                    x.Id == target.Id ? createdAt
                    : x.Id == readyControl.Id ? controlCreatedAt
                    : queuedCreatedAt
                )
            )
            .OrderBy(x => x.Id)
            .ToArray();
        var expectedKey = target.ToKey();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.StartMoveDownloadFileJob(It.Is<DownloadTaskKey>(key => key == expectedKey), CancellationToken)
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.CheckMoveDownloadFileJobQueue(CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(
                x =>
                    x.StartMoveDownloadFileJob(
                        It.Is<DownloadTaskKey>(key => key != expectedKey),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
        var after = await dbContext
            .DownloadTaskMovieFile.Select(x => new
            {
                x.Id,
                x.DownloadStatus,
                x.CreatedAt,
            })
            .Concat(
                dbContext.DownloadTaskTvShowEpisodeFile.Select(x => new
                {
                    x.Id,
                    x.DownloadStatus,
                    x.CreatedAt,
                })
            )
            .Concat(
                dbContext.DownloadTaskPhotoImageFiles.Select(x => new
                {
                    x.Id,
                    x.DownloadStatus,
                    x.CreatedAt,
                })
            )
            .Concat(
                dbContext.DownloadTaskMusicTrackFiles.Select(x => new
                {
                    x.Id,
                    x.DownloadStatus,
                    x.CreatedAt,
                })
            )
            .Concat(
                dbContext.DownloadTaskOtherVideoFiles.Select(x => new
                {
                    x.Id,
                    x.DownloadStatus,
                    x.CreatedAt,
                })
            )
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        after.Select(x => (x.Id, x.DownloadStatus, x.CreatedAt)).ShouldBe(before);
    }

    [Test]
    public async Task ShouldReturnSuccessResult_WhenNoDownloadTaskIsReadyToMove()
    {
        // Arrange — tasks exist but none are in DownloadFinished or MoveError state
        await SetupDatabase(
            9876,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 3;
            }
        );

        var dbContext = IDbContext;
        var downloadTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        downloadTasks.SetDownloadStatus(DownloadStatus.Completed);
        await dbContext.SaveChangesAsync(CancellationToken);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never);

        // Act
        var result = await Sut.CheckMoveDownloadFileJobQueue(CancellationToken);

        // Assert: returns success — "nothing to move" is not an error, just a no-op
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken), Times.Never);
    }

    [Test]
    public async Task ShouldNotRunFileMoveJob_WhenNoDownloadTaskIsAvailable()
    {
        // Arrange
        await SetupDatabase(9, config => config.PlexServerCount = 1);

        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Never());

        // Act
        var result = await Sut.CheckMoveDownloadFileJobQueue(CancellationToken);

        // Assert: no task available is not an error, job is never started
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken), Times.Never);
    }

    [Test]
    public async Task ShouldReturnFailureResult_WhenStartMoveDownloadFileJobFails()
    {
        // Arrange
        await SetupDatabase(
            6661,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var downloadTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        downloadTasks.SetDownloadStatus(DownloadStatus.DownloadFinished);
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedKey = downloadTasks.Single().ToKey();

        var startResult = Result.Fail("Scheduler failed to start job");
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.StartMoveDownloadFileJob(It.Is<DownloadTaskKey>(key => key == expectedKey), CancellationToken)
            )
            .ReturnsAsync(startResult)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.CheckMoveDownloadFileJobQueue(CancellationToken);

        // Assert: the scheduler failure is propagated back to the caller
        result.ShouldNotBeNull();
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldBe(startResult.Errors);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken), Times.Once);
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
    }

    [Test]
    public async Task ShouldPreferOldestDownloadTask_WhenBothAreEligibleToMove()
    {
        // Arrange — both a movie and a TV episode are DownloadFinished; the SUT must pick the oldest
        await SetupDatabase(
            1122,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieFileTasks.SetDownloadStatus(DownloadStatus.DownloadFinished);
        var episodeFileTasks = await dbContext
            .DownloadTaskTvShowEpisodeFile.AsTracking()
            .ToListAsync(CancellationToken);
        episodeFileTasks.SetDownloadStatus(DownloadStatus.DownloadFinished);
        var now = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFileTasks.First().Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.CreatedAt, now.AddMinutes(10)),
                cancellationToken: CancellationToken
            );
        await dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == episodeFileTasks.First().Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, now), cancellationToken: CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);

        var expectedKey = episodeFileTasks.First().ToKey();

        DownloadTaskKey? capturedKey = null;
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.StartMoveDownloadFileJob(It.Is<DownloadTaskKey>(key => key == expectedKey), CancellationToken)
            )
            .Callback<DownloadTaskKey, CancellationToken>((k, _) => capturedKey = k)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.CheckMoveDownloadFileJobQueue(CancellationToken);

        // Assert: the oldest file is preferred regardless of type
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken), Times.Once);
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldBe(expectedKey);
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
    }

    [Test]
    public async Task ShouldPreferDownloadFinishedOverMoveError_WhenMoveErrorIsOlder()
    {
        // Arrange — DownloadFinished should win even if MoveError is older
        await SetupDatabase(
            7788,
            config =>
            {
                config.PlexServerCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
            }
        );

        var dbContext = IDbContext;
        var movieFileTasks = await dbContext.DownloadTaskMovieFile.AsTracking().ToListAsync(CancellationToken);
        movieFileTasks.SetDownloadStatus(DownloadStatus.MoveError);
        var episodeFileTasks = await dbContext
            .DownloadTaskTvShowEpisodeFile.AsTracking()
            .ToListAsync(CancellationToken);
        episodeFileTasks.SetDownloadStatus(DownloadStatus.DownloadFinished);
        await dbContext.SaveChangesAsync(CancellationToken);

        var now = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        await dbContext
            .DownloadTaskMovieFile.Where(x => x.Id == movieFileTasks.First().Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, now), cancellationToken: CancellationToken);
        await dbContext
            .DownloadTaskTvShowEpisodeFile.Where(x => x.Id == episodeFileTasks.First().Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.CreatedAt, now.AddMinutes(10)),
                cancellationToken: CancellationToken
            );
        await dbContext.SaveChangesAsync(CancellationToken);

        var expectedKey = episodeFileTasks.First().ToKey();

        DownloadTaskKey? capturedKey = null;
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x =>
                x.StartMoveDownloadFileJob(It.Is<DownloadTaskKey>(key => key == expectedKey), CancellationToken)
            )
            .Callback<DownloadTaskKey, CancellationToken>((k, _) => capturedKey = k)
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.CheckMoveDownloadFileJobQueue(CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Verify(x => x.StartMoveDownloadFileJob(It.IsAny<DownloadTaskKey>(), CancellationToken), Times.Once);
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldBe(expectedKey);
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
    }
}
