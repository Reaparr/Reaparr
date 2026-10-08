namespace Reaparr.Application.UnitTests;

public class CreateDownloadTasksCommandHandlerUnitTests : BaseCommandUnitTest<CreateDownloadTasksCommand>
{
    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldPersistOnlySelectedOriginalHierarchyAndNotifyQueue_WhenSingleFamilyIsRequested(
        PlexMediaType type
    )
    {
        // Arrange
        var music = type is PlexMediaType.MusicArtist or PlexMediaType.MusicAlbum or PlexMediaType.MusicTrack;
        var photos = type is PlexMediaType.PhotoAlbum or PlexMediaType.PhotoImage;
        await SetupDatabase(
            62550,
            config =>
            {
                config.PlexMusicLibraryCount = music ? 1 : 0;
                config.MusicArtistCount = music ? 2 : 0;
                config.MusicAlbumCount = music ? 1 : 0;
                config.MusicTrackCount = music ? 1 : 0;
                config.PlexPhotoLibraryCount = photos ? 1 : 0;
                config.PhotoAlbumCount = photos ? 2 : 0;
                config.PhotoCount = photos ? 1 : 0;
                config.PlexOtherVideoLibraryCount = !music && !photos ? 1 : 0;
                config.OtherVideoCount = !music && !photos ? 2 : 0;
            }
        );
        var dbContext = IDbContext;
        BasePlexMedia selected;
        BasePlexMedia leaf;
        BasePlexMedia control;
        BasePlexMediaData data;
        if (music)
        {
            var artists = await dbContext
                .PlexArtists.Include(x => x.Albums)
                    .ThenInclude(x => x.Tracks)
                        .ThenInclude(x => x.MediaDataList)
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            var artist = artists[0];
            var album = artist.Albums.Single();
            var track = album.Tracks.Single();
            selected =
                type == PlexMediaType.MusicArtist ? artist
                : type == PlexMediaType.MusicAlbum ? album
                : track;
            leaf = track;
            data = track.MediaDataList.Single();
            control = artists[1];
        }
        else if (photos)
        {
            var albums = await dbContext
                .PlexPhotoAlbums.Include(x => x.Photos)
                    .ThenInclude(x => x.MediaDataList)
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            var album = albums[0];
            var image = album.Photos.Single();
            selected = type == PlexMediaType.PhotoAlbum ? album : image;
            leaf = image;
            data = image.MediaDataList.Single();
            control = albums[1];
        }
        else
        {
            var videos = await dbContext
                .PlexOtherVideos.Include(x => x.MediaDataList)
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            selected = videos[0];
            leaf = selected;
            data = videos[0].MediaDataList.Single();
            control = videos[1];
        }
        selected.PlexApiRatingKey.ShouldNotBe(control.PlexApiRatingKey);
        (await dbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        var command = new CreateDownloadTasksCommand(
            new CreateDownloadTasksRequest(
                [
                    new DownloadMediaDTO
                    {
                        Type = type,
                        PlexServerId = selected.PlexServerId,
                        PlexLibraryId = selected.PlexLibraryId,
                        MediaIds = [selected.Id],
                        Qualities = [],
                    },
                ],
                customDestinationFolderPath: "/creation-target"
            )
        );
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMusicArtistsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskMusicArtistsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMusicArtistsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(type == PlexMediaType.MusicArtist ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskMusicAlbumsCommand>(c =>
                        c.Request.CustomDestinationFolderPath == "/creation-target"
                        && c.Request.DownloadMedias.Where(x => x.Type == PlexMediaType.MusicAlbum)
                            .SelectMany(x => x.MediaIds).SequenceEqual(new[] { ((PlexMusicTrack)leaf).PlexAlbumId })
                        && c.Request.DownloadMedias.All(x =>
                            x.PlexServerId == selected.PlexServerId && x.PlexLibraryId == selected.PlexLibraryId)
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskMusicAlbumsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMusicAlbumsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(type is PlexMediaType.MusicArtist or PlexMediaType.MusicAlbum ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskMusicTracksCommand>(c =>
                        c.Request.CustomDestinationFolderPath == "/creation-target"
                        && c.Request.DownloadMedias.Where(x => x.Type == PlexMediaType.MusicTrack)
                            .SelectMany(x => x.MediaIds).SequenceEqual(new[] { leaf.Id })
                        && c.Request.DownloadMedias.All(x =>
                            x.PlexServerId == selected.PlexServerId && x.PlexLibraryId == selected.PlexLibraryId)
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskMusicTracksCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMusicTracksCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(music ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskPhotoAlbumsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskPhotoAlbumsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskPhotoAlbumsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(type == PlexMediaType.PhotoAlbum ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskPhotoImagesCommand>(c =>
                        c.Request.CustomDestinationFolderPath == "/creation-target"
                        && c.Request.DownloadMedias.Where(x => x.Type == PlexMediaType.PhotoImage)
                            .SelectMany(x => x.MediaIds).SequenceEqual(new[] { leaf.Id })
                        && c.Request.DownloadMedias.All(x =>
                            x.PlexServerId == selected.PlexServerId && x.PlexLibraryId == selected.PlexLibraryId)
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskPhotoImagesCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskPhotoImagesCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(photos ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskOtherVideosCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskOtherVideosCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskOtherVideosCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(!music && !photos ? Times.Once() : Times.Never());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { selected.PlexServerId })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(v => v.SequenceEqual(new[] { RefreshDataType.DownloadTasks }))
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            new DownloadTaskCreationReport
            {
                MusicArtists = type == PlexMediaType.MusicArtist ? 1 : 0,
                MusicAlbums = type is PlexMediaType.MusicArtist or PlexMediaType.MusicAlbum ? 1 : 0,
                MusicTracks = music ? 1 : 0,
                PhotoAlbums = type == PlexMediaType.PhotoAlbum ? 1 : 0,
                PhotoImages = photos ? 1 : 0,
                OtherVideos = type == PlexMediaType.OtherVideos ? 1 : 0,
            }
        );
        DownloadTaskFileBase file;
        if (music)
        {
            var trackSource = (PlexMusicTrack)leaf;
            var albumSource = trackSource.PlexAlbum!;
            var artistSource = albumSource.PlexArtist!;
            var root = await dbContext
                .DownloadTaskMusicArtists.Include(x => x.Children)
                    .ThenInclude(x => x.Children)
                        .ThenInclude(x => x.Children)
                .SingleAsync(CancellationToken);
            root.PlexApiRatingKey.ShouldBe(artistSource.PlexApiRatingKey);
            root.PlexServerId.ShouldBe(artistSource.PlexServerId);
            root.PlexLibraryId.ShouldBe(artistSource.PlexLibraryId);
            var albumTask = root.Children.Single();
            albumTask.PlexApiRatingKey.ShouldBe(albumSource.PlexApiRatingKey);
            albumTask.ParentId.ShouldBe(root.Id);
            var trackTask = albumTask.Children.Single();
            trackTask.PlexApiRatingKey.ShouldBe(leaf.PlexApiRatingKey);
            trackTask.ParentId.ShouldBe(albumTask.Id);
            var trackFile = trackTask.Children.Single();
            trackFile.ParentId.ShouldBe(trackTask.Id);
            file = trackFile;
            var log = await dbContext.DownloadTaskTrackFileLogs.SingleAsync(CancellationToken);
            (
                log.DownloadTaskFileId,
                log.DownloadTaskTrackId,
                log.DownloadTaskAlbumId,
                log.DownloadTaskArtistId,
                log.Status
            ).ShouldBe((file.Id, trackTask.Id, albumTask.Id, root.Id, DownloadStatus.Queued));
            (
                await dbContext
                    .PlexArtists.Select(x => x.PlexApiRatingKey)
                    .OrderBy(x => x)
                    .ToListAsync(CancellationToken)
            ).ShouldBe(new[] { artistSource.PlexApiRatingKey, control.PlexApiRatingKey }.Order());
        }
        else if (photos)
        {
            var imageSource = (PlexPhotoImage)leaf;
            var albumSource = await dbContext.PlexPhotoAlbums.SingleAsync(
                x => x.Id == imageSource.PlexPhotoAlbumId,
                CancellationToken
            );
            var root = await dbContext
                .DownloadTaskPhotoAlbums.Include(x => x.Children)
                    .ThenInclude(x => x.Children)
                .SingleAsync(CancellationToken);
            root.PlexApiRatingKey.ShouldBe(albumSource.PlexApiRatingKey);
            var imageTask = root.Children.Single();
            imageTask.PlexApiRatingKey.ShouldBe(leaf.PlexApiRatingKey);
            imageTask.ParentId.ShouldBe(root.Id);
            var imageFile = imageTask.Children.Single();
            imageFile.ParentId.ShouldBe(imageTask.Id);
            file = imageFile;
            var log = await dbContext.DownloadTaskPhotoImageFileLogs.SingleAsync(CancellationToken);
            (log.DownloadTaskFileId, log.DownloadTaskPhotoId, log.DownloadTaskPhotoAlbumId, log.Status).ShouldBe(
                (file.Id, imageTask.Id, root.Id, DownloadStatus.Queued)
            );
            (
                await dbContext
                    .PlexPhotoAlbums.Select(x => x.PlexApiRatingKey)
                    .OrderBy(x => x)
                    .ToListAsync(CancellationToken)
            ).ShouldBe(new[] { albumSource.PlexApiRatingKey, control.PlexApiRatingKey }.Order());
        }
        else
        {
            var root = await dbContext.DownloadTaskOtherVideos.Include(x => x.Children).SingleAsync(CancellationToken);
            root.PlexApiRatingKey.ShouldBe(selected.PlexApiRatingKey);
            var videoFile = root.Children.Single();
            videoFile.ParentId.ShouldBe(root.Id);
            file = videoFile;
            var log = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
            (log.DownloadTaskFileId, log.DownloadTaskOtherVideoId, log.Status).ShouldBe(
                (file.Id, root.Id, DownloadStatus.Queued)
            );
            (await dbContext.PlexOtherVideos.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
                new[] { selected.Id, control.Id }.Order()
            );
        }
        file.DirectoryMeta.DestinationRootPath.ShouldBe("/creation-target");
        (
            file.PlexServerId,
            file.PlexLibraryId,
            file.PlexApiRatingKey,
            file.PlexApiMediaId,
            file.PlexApiPartId,
            file.FileName,
            file.FileLocationUrl,
            file.DataTotal,
            file.DownloadStatus,
            file.DownloadClientType
        ).ShouldBe(
            (
                leaf.PlexServerId,
                leaf.PlexLibraryId,
                data.PlexApiRatingKey,
                data.PlexApiMediaId,
                data.PlexApiPartId,
                data.OriginalFilename.GetFileName(),
                data.Key,
                data.Size,
                DownloadStatus.Queued,
                PlexDownloadClientType.Direct
            )
        );
        (await dbContext.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskTvShow.CountAsync(CancellationToken)).ShouldBe(0);
        if (!music)
            (await dbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(0);
        if (!photos)
            (await dbContext.DownloadTaskPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        if (music || photos)
            (await dbContext.DownloadTaskOtherVideos.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskMoviesCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskTvShowsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    public async Task ShouldPersistAllFiveMediaFamiliesAndExactReport_WhenMixedSelectionsSpanServers()
    {
        // Arrange
        await SetupDatabase(
            62551,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 2;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 2;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 2;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var movies = await dbContext
            .PlexMovies.Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var shows = await dbContext
            .PlexTvShows.Include(x => x.Seasons)
                .ThenInclude(x => x.Episodes)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var artists = await dbContext
            .PlexArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var photoAlbums = await dbContext
            .PlexPhotoAlbums.Include(x => x.Photos)
                .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var videos = await dbContext
            .PlexOtherVideos.Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var movie = movies[0];
        var show = shows[0];
        var season = show.Seasons.Single();
        var episode = season.Episodes.Single();
        var artist = artists[0];
        var album = artist.Albums.Single();
        var track = album.Tracks.Single();
        var photoAlbum = photoAlbums[0];
        var image = photoAlbum.Photos.Single();
        var video = videos.Last();
        movie.PlexServerId.ShouldNotBe(video.PlexServerId);
        movies.Count.ShouldBe(4);
        shows.Count.ShouldBe(4);
        artists.Count.ShouldBe(4);
        photoAlbums.Count.ShouldBe(4);
        videos.Count.ShouldBe(4);
        var movieData = movie.MediaDataList.PickMediaQuality()!;
        var episodeData = episode.MediaDataList.PickMediaQuality()!;
        var trackData = track.MediaDataList.Single();
        var photoData = image.MediaDataList.Single();
        var videoData = video.MediaDataList.Single();
        var selectedSources = new BasePlexMedia[] { movie, show, artist, photoAlbum, video };
        var command = new CreateDownloadTasksCommand(
            new CreateDownloadTasksRequest(
                selectedSources
                    .Select(source => new DownloadMediaDTO
                    {
                        Type = source.Type,
                        PlexServerId = source.PlexServerId,
                        PlexLibraryId = source.PlexLibraryId,
                        MediaIds = [source.Id],
                        Qualities = [],
                    })
                    .ToList(),
                customDestinationFolderPath: "/mixed-target"
            )
        );
        var expectedServers = new[] { movie.PlexServerId, video.PlexServerId };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMoviesCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskMoviesCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMoviesCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskTvShowsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskTvShowsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskTvShowsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowSeasonsCommand>(c =>
                        c.Request.DownloadMedias.Count == 1
                        && c.Request.DownloadMedias[0].Type == PlexMediaType.Season
                        && c.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { season.Id })
                        && c.Request.DownloadMedias[0].PlexLibraryId == season.PlexLibraryId
                        && c.Request.DownloadMedias[0].PlexServerId == season.PlexServerId
                        && c.Request.CustomDestinationFolderPath == "/mixed-target"
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskTvShowSeasonsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskTvShowSeasonsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowEpisodesCommand>(c =>
                        c.Request.DownloadMedias.Count == 1
                        && c.Request.DownloadMedias[0].Type == PlexMediaType.Episode
                        && c.Request.DownloadMedias[0].MediaIds.SequenceEqual(new[] { episode.Id })
                        && c.Request.DownloadMedias[0].PlexLibraryId == episode.PlexLibraryId
                        && c.Request.DownloadMedias[0].PlexServerId == episode.PlexServerId
                        && c.Request.CustomDestinationFolderPath == "/mixed-target"
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskTvShowEpisodesCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskTvShowEpisodesCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(
                        c,
                        ct
                    )
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMusicArtistsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskMusicArtistsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMusicArtistsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskMusicAlbumsCommand>(c =>
                        c.Request.DownloadMedias.Where(x => x.Type == PlexMediaType.MusicAlbum)
                            .SelectMany(x => x.MediaIds).SequenceEqual(new[] { album.Id })
                        && c.Request.DownloadMedias.All(x =>
                            x.PlexServerId == album.PlexServerId && x.PlexLibraryId == album.PlexLibraryId)
                        && c.Request.CustomDestinationFolderPath == "/mixed-target"
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskMusicAlbumsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMusicAlbumsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskMusicTracksCommand>(c =>
                        c.Request.DownloadMedias.Where(x => x.Type == PlexMediaType.MusicTrack)
                            .SelectMany(x => x.MediaIds).SequenceEqual(new[] { track.Id })
                        && c.Request.DownloadMedias.All(x =>
                            x.PlexServerId == track.PlexServerId && x.PlexLibraryId == track.PlexLibraryId)
                        && c.Request.CustomDestinationFolderPath == "/mixed-target"
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskMusicTracksCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskMusicTracksCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskPhotoAlbumsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .Returns(
                (GenerateDownloadTaskPhotoAlbumsCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskPhotoAlbumsCommandHandler(
                        Serilog.Log.Logger,
                        dbContext,
                        Mock.Mock<ICommandExecutor>().Object
                    ).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskPhotoImagesCommand>(c =>
                        c.Request.DownloadMedias.Where(x => x.Type == PlexMediaType.PhotoImage)
                            .SelectMany(x => x.MediaIds).SequenceEqual(new[] { image.Id })
                        && c.Request.DownloadMedias.All(x =>
                            x.PlexServerId == image.PlexServerId && x.PlexLibraryId == image.PlexLibraryId)
                        && c.Request.CustomDestinationFolderPath == "/mixed-target"
                    ),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskPhotoImagesCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskPhotoImagesCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskOtherVideosCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .Returns(
                (GenerateDownloadTaskOtherVideosCommand c, CancellationToken ct) =>
                    new GenerateDownloadTaskOtherVideosCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(c, ct)
            )
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(expectedServers)),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x =>
                x.SendRefreshNotificationAsync(
                    It.Is<List<RefreshDataType>>(v => v.SequenceEqual(new[] { RefreshDataType.DownloadTasks }))
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

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
                PhotoAlbums = 1,
                PhotoImages = 1,
                OtherVideos = 1,
            }
        );
        result.Value.Total.ShouldBe(10);
        var movieTask = await dbContext.DownloadTaskMovie.Include(x => x.Children).SingleAsync(CancellationToken);
        (movieTask.PlexServerId, movieTask.PlexLibraryId, movieTask.PlexApiRatingKey).ShouldBe(
            (movie.PlexServerId, movie.PlexLibraryId, movie.PlexApiRatingKey)
        );
        var movieFile = movieTask.Children.Single();
        movieFile.ParentId.ShouldBe(movieTask.Id);
        var tvTask = await dbContext
            .DownloadTaskTvShow.Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .SingleAsync(CancellationToken);
        (tvTask.PlexServerId, tvTask.PlexLibraryId, tvTask.PlexApiRatingKey).ShouldBe(
            (show.PlexServerId, show.PlexLibraryId, show.PlexApiRatingKey)
        );
        var seasonTask = tvTask.Children.Single();
        (seasonTask.ParentId, seasonTask.PlexApiRatingKey).ShouldBe((tvTask.Id, season.PlexApiRatingKey));
        var episodeTask = seasonTask.Children.Single();
        (episodeTask.ParentId, episodeTask.PlexApiRatingKey).ShouldBe((seasonTask.Id, episode.PlexApiRatingKey));
        var episodeFile = episodeTask.Children.Single();
        episodeFile.ParentId.ShouldBe(episodeTask.Id);
        var musicTask = await dbContext
            .DownloadTaskMusicArtists.Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .SingleAsync(CancellationToken);
        (musicTask.PlexServerId, musicTask.PlexLibraryId, musicTask.PlexApiRatingKey).ShouldBe(
            (artist.PlexServerId, artist.PlexLibraryId, artist.PlexApiRatingKey)
        );
        var albumTask = musicTask.Children.Single();
        (albumTask.ParentId, albumTask.PlexApiRatingKey).ShouldBe((musicTask.Id, album.PlexApiRatingKey));
        var trackTask = albumTask.Children.Single();
        (trackTask.ParentId, trackTask.PlexApiRatingKey).ShouldBe((albumTask.Id, track.PlexApiRatingKey));
        var trackFile = trackTask.Children.Single();
        trackFile.ParentId.ShouldBe(trackTask.Id);
        var photoTask = await dbContext
            .DownloadTaskPhotoAlbums.Include(x => x.Children)
                .ThenInclude(x => x.Children)
            .SingleAsync(CancellationToken);
        (photoTask.PlexServerId, photoTask.PlexLibraryId, photoTask.PlexApiRatingKey).ShouldBe(
            (photoAlbum.PlexServerId, photoAlbum.PlexLibraryId, photoAlbum.PlexApiRatingKey)
        );
        var imageTask = photoTask.Children.Single();
        (imageTask.ParentId, imageTask.PlexApiRatingKey).ShouldBe((photoTask.Id, image.PlexApiRatingKey));
        var photoFile = imageTask.Children.Single();
        photoFile.ParentId.ShouldBe(imageTask.Id);
        var videoTask = await dbContext.DownloadTaskOtherVideos.Include(x => x.Children).SingleAsync(CancellationToken);
        (videoTask.PlexServerId, videoTask.PlexLibraryId, videoTask.PlexApiRatingKey).ShouldBe(
            (video.PlexServerId, video.PlexLibraryId, video.PlexApiRatingKey)
        );
        var videoFile = videoTask.Children.Single();
        videoFile.ParentId.ShouldBe(videoTask.Id);
        var actualFiles = new DownloadTaskFileBase[] { movieFile, episodeFile, trackFile, photoFile, videoFile };
        var expectedData = new BasePlexMediaData[] { movieData, episodeData, trackData, photoData, videoData };
        actualFiles
            .Select(x =>
                (
                    x.MediaType,
                    x.PlexServerId,
                    x.PlexLibraryId,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.DataTotal,
                    x.FileLocationUrl,
                    x.DownloadStatus,
                    x.DownloadClientType
                )
            )
            .ShouldBe(
                expectedData.Select(x =>
                    (
                        x.Type,
                        x.PlexServerId,
                        x.PlexLibraryId,
                        x.PlexApiRatingKey,
                        x.PlexApiMediaId,
                        x.PlexApiPartId,
                        x.Size,
                        x.Key,
                        DownloadStatus.Queued,
                        PlexDownloadClientType.Direct
                    )
                )
            );
        actualFiles
            .Select(x => x.FileName)
            .ShouldBe(
                new[]
                {
                    movieData.GetFileName,
                    episodeData.GetFileName,
                    trackData.OriginalFilename.GetFileName(),
                    photoData.OriginalFilename.GetFileName(),
                    videoData.OriginalFilename.GetFileName(),
                }
            );
        var musicRelative = Path.Combine(artist.Title.SanitizeFolderName(), album.Title.SanitizeFolderName());
        actualFiles
            .Select(x => x.DestinationDirectory)
            .ShouldBe(
                new[]
                {
                    Path.Combine("/mixed-target", movie.Title.SanitizeFolderName()),
                    Path.Combine("/mixed-target", show.Title.SanitizeFolderName(), season.Title.SanitizeFolderName()),
                    Path.Combine("/mixed-target", musicRelative),
                    Path.Combine("/mixed-target", photoAlbum.Title.SanitizeFolderName()),
                    Path.Combine("/mixed-target", video.Title.SanitizeFolderName()),
                }
            );
        new DownloadTaskBase[]
        {
            movieTask,
            tvTask,
            seasonTask,
            episodeTask,
            musicTask,
            albumTask,
            trackTask,
            photoTask,
            imageTask,
            videoTask,
        }.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Queued);
        (await dbContext.DownloadTaskMovieFileLogs.SingleAsync(CancellationToken)).DownloadTaskFileId.ShouldBe(
            movieFile.Id
        );
        var episodeLog = await dbContext.DownloadTaskTvShowEpisodeFileLogs.SingleAsync(CancellationToken);
        (
            episodeLog.DownloadTaskFileId,
            episodeLog.DownloadTaskTvShowEpisodeId,
            episodeLog.DownloadTaskTvShowSeasonId,
            episodeLog.DownloadTaskTvShowId
        ).ShouldBe((episodeFile.Id, episodeTask.Id, seasonTask.Id, tvTask.Id));
        var musicLog = await dbContext.DownloadTaskTrackFileLogs.SingleAsync(CancellationToken);
        (
            musicLog.DownloadTaskFileId,
            musicLog.DownloadTaskTrackId,
            musicLog.DownloadTaskAlbumId,
            musicLog.DownloadTaskArtistId
        ).ShouldBe((trackFile.Id, trackTask.Id, albumTask.Id, musicTask.Id));
        var photoLog = await dbContext.DownloadTaskPhotoImageFileLogs.SingleAsync(CancellationToken);
        (photoLog.DownloadTaskFileId, photoLog.DownloadTaskPhotoId, photoLog.DownloadTaskPhotoAlbumId).ShouldBe(
            (photoFile.Id, imageTask.Id, photoTask.Id)
        );
        var videoLog = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
        (videoLog.DownloadTaskFileId, videoLog.DownloadTaskOtherVideoId).ShouldBe((videoFile.Id, videoTask.Id));
        (await dbContext.PlexMovies.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
            movies.Select(x => x.Id).Order()
        );
        (await dbContext.PlexTvShows.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
            shows.Select(x => x.Id).Order()
        );
        (await dbContext.PlexArtists.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
            artists.Select(x => x.Id).Order()
        );
        (await dbContext.PlexPhotoAlbums.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
            photoAlbums.Select(x => x.Id).Order()
        );
        (await dbContext.PlexOtherVideos.Select(x => x.Id).OrderBy(x => x).ToListAsync(CancellationToken)).ShouldBe(
            videos.Select(x => x.Id).Order()
        );
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRejectMissingSelections_WhenRequestIsNullOrSelectionListIsEmpty(bool nullRequest)
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
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    [Arguments(PlexMediaType.OtherVideos)]
    public async Task ShouldStopWithoutQueueNotification_WhenOnlySelectedGeneratorFails(PlexMediaType failedType)
    {
        // Arrange
        var command = new CreateDownloadTasksCommand([
            new DownloadMediaDTO
            {
                Type = failedType,
                PlexServerId = 1,
                PlexLibraryId = 11,
                MediaIds = [101],
                Qualities = [],
            },
        ]);
        var error = new Error("Generation failure");
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMoviesCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.Movie ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskTvShowsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.TvShow ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowSeasonsCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.Season ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskTvShowEpisodesCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.Episode ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMusicArtistsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.MusicArtist ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMusicAlbumsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.MusicAlbum ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskMusicTracksCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.MusicTrack ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskPhotoAlbumsCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.PhotoAlbum ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(It.Is<GenerateDownloadTaskPhotoImagesCommand>(c => c.Request == command.Request), CancellationToken)
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.PhotoImage ? Times.Once() : Times.Never());
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskOtherVideosCommand>(c => c.Request == command.Request),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Fail<DownloadTaskCreationReport>(error))
            .Verifiable(failedType == PlexMediaType.OtherVideos ? Times.Once() : Times.Never());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ShouldBeSameAs(error);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>()
            .Verify(
                x => x.PublishAsync(It.IsAny<CheckDownloadQueueEvent>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
        Mock.Mock<INotificationHubService>()
            .Verify(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()), Times.Never());
    }

    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    public async Task ShouldPreferExplicitLeafOriginal_WhenAncestorAndLeafSelectionsOverlap(PlexMediaType ancestorType)
    {
        // Arrange
        var music = ancestorType != PlexMediaType.PhotoAlbum;
        await SetupDatabase(62552, config =>
        {
            config.PlexMusicLibraryCount = music ? 1 : 0;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
            config.PlexPhotoLibraryCount = music ? 0 : 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 1;
        });
        var dbContext = IDbContext;
        BasePlexMedia ancestor;
        BasePlexMedia leaf;
        BasePlexMediaData inheritedOriginal;
        BasePlexMediaData explicitOriginal;
        if (music)
        {
            var track = await dbContext.PlexTracks
                .Include(x => x.PlexAlbum).ThenInclude(x => x!.PlexArtist)
                .Include(x => x.MediaDataList).SingleAsync(CancellationToken);
            ancestor = ancestorType == PlexMediaType.MusicArtist ? track.PlexAlbum!.PlexArtist! : track.PlexAlbum!;
            leaf = track;
            inheritedOriginal = track.MediaDataList.Single();
            var alternate = FakeData.GetPlexMusicTrackMediaData(new Seed(62553))
                .RuleFor(x => x.Id, _ => 0)
                .RuleFor(x => x.PlexTrackId, _ => track.Id)
                .RuleFor(x => x.PlexLibraryId, _ => track.PlexLibraryId)
                .RuleFor(x => x.PlexServerId, _ => track.PlexServerId)
                .RuleFor(x => x.PlexApiRatingKey, _ => track.PlexApiRatingKey)
                .RuleFor(x => x.PlexApiMediaId, _ => inheritedOriginal.PlexApiMediaId + 1)
                .Generate();
            dbContext.PlexTrackData.Add(alternate);
            explicitOriginal = alternate;
        }
        else
        {
            var image = await dbContext.PlexPhotoImages
                .Include(x => x.PlexPhotoAlbum).Include(x => x.MediaDataList).SingleAsync(CancellationToken);
            ancestor = image.PlexPhotoAlbum!;
            leaf = image;
            inheritedOriginal = image.MediaDataList.Single();
            var alternate = FakeData.GetPlexPhotoMediaData(new Seed(62554))
                .RuleFor(x => x.Id, _ => 0)
                .RuleFor(x => x.PlexPhotoId, _ => image.Id)
                .RuleFor(x => x.PlexLibraryId, _ => image.PlexLibraryId)
                .RuleFor(x => x.PlexServerId, _ => image.PlexServerId)
                .RuleFor(x => x.PlexApiRatingKey, _ => image.PlexApiRatingKey)
                .RuleFor(x => x.PlexApiMediaId, _ => inheritedOriginal.PlexApiMediaId + 1)
                .Generate();
            dbContext.PlexPhotoData.Add(alternate);
            explicitOriginal = alternate;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        inheritedOriginal.PlexApiMediaId.ShouldNotBe(explicitOriginal.PlexApiMediaId);
        var parentSelection = new DownloadMediaDTO
        {
            Type = ancestorType,
            PlexServerId = leaf.PlexServerId,
            PlexLibraryId = leaf.PlexLibraryId,
            MediaIds = [ancestor.Id],
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaId = leaf.Id,
                    MediaDataType = leaf.Type,
                    DataId = inheritedOriginal.Id,
                    Quality = inheritedOriginal.Quality,
                },
            ],
        };
        var leafSelection = parentSelection with
        {
            Type = leaf.Type,
            MediaIds = [leaf.Id],
            Qualities =
            [
                new PlexMediaQualityDTO
                {
                    MediaId = leaf.Id,
                    MediaDataType = leaf.Type,
                    DataId = explicitOriginal.Id,
                    Quality = explicitOriginal.Quality,
                },
            ],
            KeepCompletedInDownloadFolder = true,
        };
        var command = new CreateDownloadTasksCommand(
            new CreateDownloadTasksRequest([parentSelection, leafSelection], customDestinationFolderPath: "/leaf-choice")
        );
        var executor = Mock.Mock<ICommandExecutor>();
        executor.Setup(x => x.Send(It.IsAny<ICommand<Result<DownloadTaskCreationReport>>>(), CancellationToken))
            .Returns((ICommand<Result<DownloadTaskCreationReport>> c, CancellationToken ct) => c switch
            {
                GenerateDownloadTaskMusicArtistsCommand artists =>
                    new GenerateDownloadTaskMusicArtistsCommandHandler(Serilog.Log.Logger, dbContext, executor.Object)
                        .ExecuteAsync(artists, ct),
                GenerateDownloadTaskMusicAlbumsCommand albums =>
                    new GenerateDownloadTaskMusicAlbumsCommandHandler(Serilog.Log.Logger, dbContext, executor.Object)
                        .ExecuteAsync(albums, ct),
                GenerateDownloadTaskMusicTracksCommand tracks =>
                    new GenerateDownloadTaskMusicTracksCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(tracks, ct),
                GenerateDownloadTaskPhotoAlbumsCommand albums =>
                    new GenerateDownloadTaskPhotoAlbumsCommandHandler(Serilog.Log.Logger, dbContext, executor.Object)
                        .ExecuteAsync(albums, ct),
                GenerateDownloadTaskPhotoImagesCommand images =>
                    new GenerateDownloadTaskPhotoImagesCommandHandler(Serilog.Log.Logger, dbContext).ExecuteAsync(images, ct),
                _ => throw new InvalidOperationException($"Unexpected generator {c.GetType().Name}"),
            })
            .Verifiable(Times.Exactly(ancestorType == PlexMediaType.MusicArtist ? 4 : 3));
        Mock.Mock<IEventPublisher>()
            .Setup(x => x.PublishAsync(
                It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { leaf.PlexServerId })),
                CancellationToken))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(
                It.Is<List<RefreshDataType>>(types => types.SequenceEqual(new[] { RefreshDataType.DownloadTasks }))))
            .Returns(Task.CompletedTask).Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport
        {
            MusicArtists = ancestorType == PlexMediaType.MusicArtist ? 1 : 0,
            MusicAlbums = music ? 1 : 0,
            MusicTracks = music ? 1 : 0,
            PhotoAlbums = music ? 0 : 1,
            PhotoImages = music ? 0 : 1,
        });
        DownloadTaskFileBase file = music
            ? await dbContext.DownloadTaskMusicTrackFiles.SingleAsync(CancellationToken)
            : await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        (file.PlexServerId, file.PlexLibraryId, file.PlexApiRatingKey, file.PlexApiMediaId, file.PlexApiPartId)
            .ShouldBe((leaf.PlexServerId, leaf.PlexLibraryId, explicitOriginal.PlexApiRatingKey,
                explicitOriginal.PlexApiMediaId, explicitOriginal.PlexApiPartId));
        file.DirectoryMeta.DestinationRootPath.ShouldBe("/leaf-choice");
        file.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeTrue();
        var loggedFileId = music
            ? await dbContext.DownloadTaskTrackFileLogs.Select(x => x.DownloadTaskFileId).SingleAsync(CancellationToken)
            : await dbContext.DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId).SingleAsync(CancellationToken);
        loggedFileId.ShouldBe(file.Id);
        executor.Verify();
        Mock.Mock<IEventPublisher>().Verify();
        Mock.Mock<INotificationHubService>().Verify();
    }
}
