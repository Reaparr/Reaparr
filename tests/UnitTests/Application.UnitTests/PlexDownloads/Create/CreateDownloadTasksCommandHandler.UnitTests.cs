namespace Reaparr.Application.UnitTests;

public class CreateDownloadTasksCommandHandlerUnitTests : BaseCommandUnitTest<CreateDownloadTasksCommand>
{
    [Test]
    public async Task ShouldCreateDirectPhotoFolderAndFile_AndReportPhotoFolderAndImageCounts()
    {
        // Arrange
        await SetupDatabase(
            62501,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest(
            [CreatePhotoAssetSelection(media)],
            customDestinationFolderPath: "/create-target"
        );
        var command = new CreateDownloadTasksCommand(request);
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskPhotoImagesCommand>(c => c.Request == request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskPhotoImagesCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskPhotoImagesCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { media.PlexServerId })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(x => x.SequenceEqual(new[] { RefreshDataType.DownloadTasks }))
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 1 });
        var folder = await dbContext.DownloadTaskPhotoFolders.Include(x => x.Files).SingleAsync(CancellationToken);
        var file = folder.Files.Single();
        file.ParentId.ShouldBe(folder.Id);
        file.DirectoryMeta.DestinationRootPath.ShouldBe("/create-target");
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldKeepOtherFamilyValidationAndNotDispatchPhotoGenerator_WhenQualityIsInvalid()
    {
        // Arrange
        await SetupDatabase(
            62502,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var media = await IDbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var command = new CreateDownloadTasksCommand(
            new CreateDownloadTasksRequest([
                CreatePhotoAssetSelection(
                    media,
                    [
                        new PlexMediaQualityDTO
                        {
                            MediaId = media.Id,
                            DataId = media.Id,
                            MediaDataType = PlexMediaType.PhotoImage,
                            VideoQuality = VideoQuality.Unknown,
                        },
                    ]
                ),
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.Movie,
                    SelectionKind = null,
                    PlexServerId = media.PlexServerId,
                    PlexLibraryId = media.PlexLibraryId,
                    MediaIds = [media.Id],
                    Qualities = [],
                },
            ])
        );

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.ToResult().Has400BadRequestError().ShouldBeTrue();
        result.Errors.ShouldContain(x => x.Message == "Photo selections must not include quality or part selectors.");
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskPhotoImagesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        (await IDbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldReturnGeneratorFailureWithoutNotification_WhenPhotoGeneratorFails()
    {
        // Arrange
        await SetupDatabase(
            62503,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var media = await IDbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest([CreatePhotoAssetSelection(media)]);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskPhotoImagesCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>("photo generator failed"))
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), CancellationToken))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new CreateDownloadTasksCommand(request));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRejectMissingSelectionsWithoutDispatchingGenerators(bool nullRequest)
    {
        // Arrange
        var command = nullRequest
            ? new CreateDownloadTasksCommand((CreateDownloadTasksRequest)null!)
            : new CreateDownloadTasksCommand([]);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<ICommand<Result<DownloadTaskCreationReport>>>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.Season)]
    [Arguments(PlexMediaType.Episode)]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldStopWithoutQueueNotification_WhenNonPhotoGeneratorFails(PlexMediaType failedType)
    {
        // Arrange
        var command = new CreateDownloadTasksCommand([
            new DownloadMediaDTO
            {
                Type = failedType,
                SelectionKind = null,
                PlexServerId = 1,
                PlexLibraryId = 11,
                MediaIds = [101],
                Qualities = [],
            },
        ]);
        var error = new Error("Generation failure");
        var executor = Mock.Mock<ICommandExecutor>();
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMoviesCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.Movie ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskTvShowsCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.TvShow ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskTvShowSeasonsCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.Season ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskTvShowEpisodesCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.Episode ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicArtistsCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.MusicArtist ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicAlbumsCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.MusicAlbum ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicTracksCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.MusicTrack ? Times.Once() : Times.Never());
        executor
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskOtherVideosCommand>(), CancellationToken))
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.OtherVideos ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeSameAs(error);
        executor.Verify();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }

    [Test]
    public async Task ShouldPersistAllFiveFamiliesAndReportEveryCreatedTask_WhenSelectionsAreMixed()
    {
        // Arrange
        await SetupDatabase(
            62504,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var movie = await dbContext.PlexMovies.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var show = await dbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var artist = await dbContext
            .PlexMusicArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var photo = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var video = await dbContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var season = show.Seasons.Single();
        var episode = season.Episodes.Single();
        var album = artist.Albums.Single();
        var track = album.Tracks.Single();
        var request = new CreateDownloadTasksRequest(
            [
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.Movie,
                    SelectionKind = null,
                    PlexServerId = movie.PlexServerId,
                    PlexLibraryId = movie.PlexLibraryId,
                    MediaIds = [movie.Id],
                    Qualities = [],
                },
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.TvShow,
                    SelectionKind = null,
                    PlexServerId = show.PlexServerId,
                    PlexLibraryId = show.PlexLibraryId,
                    MediaIds = [show.Id],
                    Qualities = [],
                },
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.MusicArtist,
                    SelectionKind = null,
                    PlexServerId = artist.PlexServerId,
                    PlexLibraryId = artist.PlexLibraryId,
                    MediaIds = [artist.Id],
                    Qualities = [],
                },
                CreatePhotoAssetSelection(photo),
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.OtherVideos,
                    SelectionKind = null,
                    PlexServerId = video.PlexServerId,
                    PlexLibraryId = video.PlexLibraryId,
                    MediaIds = [video.Id],
                    Qualities = [],
                },
            ],
            customDestinationFolderPath: "/mixed-target"
        );
        var executor = Mock.Mock<ICommandExecutor>();
        executor
            .Setup(x => x.Send(It.IsAny<ICommand<Result<DownloadTaskCreationReport>>>(), CancellationToken))
            .Returns(
                async (ICommand<Result<DownloadTaskCreationReport>> command, CancellationToken ct) =>
                {
                    using var handlerContext = IDbContext;
                    return await (
                        command switch
                        {
                            GenerateDownloadTaskMoviesCommand movies => new GenerateDownloadTaskMoviesCommandHandler(
                                Serilog.Log.Logger,
                                handlerContext
                            ).ExecuteAsync(movies, ct),
                            GenerateDownloadTaskTvShowsCommand shows => new GenerateDownloadTaskTvShowsCommandHandler(
                                Serilog.Log.Logger,
                                handlerContext,
                                executor.Object
                            ).ExecuteAsync(shows, ct),
                            GenerateDownloadTaskTvShowSeasonsCommand seasons =>
                                new GenerateDownloadTaskTvShowSeasonsCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext,
                                    executor.Object
                                ).ExecuteAsync(seasons, ct),
                            GenerateDownloadTaskTvShowEpisodesCommand episodes =>
                                new GenerateDownloadTaskTvShowEpisodesCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext
                                ).ExecuteAsync(episodes, ct),
                            GenerateDownloadTaskMusicArtistsCommand artists =>
                                new GenerateDownloadTaskMusicArtistsCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext,
                                    executor.Object
                                ).ExecuteAsync(artists, ct),
                            GenerateDownloadTaskMusicAlbumsCommand albums =>
                                new GenerateDownloadTaskMusicAlbumsCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext,
                                    executor.Object
                                ).ExecuteAsync(albums, ct),
                            GenerateDownloadTaskMusicTracksCommand tracks =>
                                new GenerateDownloadTaskMusicTracksCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext
                                ).ExecuteAsync(tracks, ct),
                            GenerateDownloadTaskPhotoImagesCommand images =>
                                new GenerateDownloadTaskPhotoImagesCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext
                                ).ExecuteAsync(images, ct),
                            GenerateDownloadTaskOtherVideosCommand otherVideos =>
                                new GenerateDownloadTaskOtherVideosCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext
                                ).ExecuteAsync(otherVideos, ct),
                            _ => throw new InvalidOperationException($"Unexpected generator {command.GetType().Name}"),
                        }
                    );
                }
            )
            .Verifiable();
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { movie.PlexServerId })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(x => x.SequenceEqual(new[] { RefreshDataType.DownloadTasks }))
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(new CreateDownloadTasksCommand(request));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            new DownloadTaskCreationReport
            {
                Movies = 1,
                TvShows = 1,
                Seasons = 1,
                Episodes = 1,
                MusicArtists = 1,
                MusicAlbums = 1,
                MusicTracks = 1,
                PhotoFolders = 1,
                PhotoImages = 1,
                OtherVideos = 1,
            }
        );
        result.Value.Total.ShouldBe(10);
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskTvShow.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskTvShowSeason.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskTvShowEpisode.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMusicAlbums.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMusicTracks.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMovieFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskTvShowEpisodeFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskTrackFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskOtherVideoFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    private static DownloadMediaDTO CreatePhotoAssetSelection(
        PlexPhotoMediaData media,
        List<PlexMediaQualityDTO>? qualities = null
    ) =>
        new()
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            PlexServerId = media.PlexServerId,
            PlexLibraryId = media.PlexLibraryId,
            MediaIds = [media.Id],
            Qualities = qualities ?? [],
        };
}
