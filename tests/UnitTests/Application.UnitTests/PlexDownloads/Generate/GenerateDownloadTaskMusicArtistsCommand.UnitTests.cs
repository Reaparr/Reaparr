namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskMusicArtistsCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskMusicArtistsCommand>
{
    [Test]
    public async Task ShouldCreateArtistAndForwardAlbumsWithDescendantChoices_WhenArtistIsSelected()
    {
        // Arrange
        await SetupDatabase(
            62601,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var artist = await IDbContext
            .PlexMusicArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
            .SingleAsync(CancellationToken);
        var album = artist.Albums.Single();
        var track = album.Tracks.Single();
        var inherited = new PlexMediaQualityDTO
        {
            MediaId = track.Id,
            DataId = 101,
            MediaDataType = PlexMediaType.MusicTrack,
            VideoQuality = VideoQuality.Unknown,
        };
        var direct = inherited with { DataId = 202 };
        GenerateDownloadTaskMusicAlbumsCommand? forwarded = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicAlbumsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result<DownloadTaskCreationReport>>, CancellationToken>(
                (value, _) => forwarded = (GenerateDownloadTaskMusicAlbumsCommand)value
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { MusicAlbums = 1, MusicTracks = 1 }))
            .Verifiable(Times.Once());
        var request = new CreateDownloadTasksRequest(
            [
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.MusicArtist,
                    SelectionKind = null,
                    PlexServerId = artist.PlexServerId,
                    PlexLibraryId = artist.PlexLibraryId,
                    MediaIds = [artist.Id],
                    Qualities = [inherited],
                    KeepCompletedInDownloadFolder = true,
                },
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.MusicTrack,
                    SelectionKind = null,
                    PlexServerId = track.PlexServerId,
                    PlexLibraryId = track.PlexLibraryId,
                    MediaIds = [track.Id],
                    Qualities = [direct],
                },
            ],
            destinationFolderPathId: 7,
            customDestinationFolderPath: "/music"
        );

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicArtistsCommand(request)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(
            new DownloadTaskCreationReport
            {
                MusicArtists = 1,
                MusicAlbums = 1,
                MusicTracks = 1,
            }
        );
        var task = await IDbContext.DownloadTaskMusicArtists.SingleAsync(CancellationToken);
        (task.PlexApiRatingKey, task.PlexServerId, task.PlexLibraryId).ShouldBe(
            (artist.PlexApiRatingKey, artist.PlexServerId, artist.PlexLibraryId)
        );
        forwarded.ShouldNotBeNull();
        forwarded.Request.DestinationFolderPathId.ShouldBe(7);
        forwarded.Request.CustomDestinationFolderPath.ShouldBe("/music");
        forwarded.Request.DownloadMedias.Single(x => x.Type == PlexMediaType.MusicAlbum).MediaIds.ShouldBe([album.Id]);
        forwarded.Request.DownloadMedias.Single(x => x.Type == PlexMediaType.MusicTrack).Qualities.ShouldBe([direct]);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldPreserveDirectTrackPlacement_WhenArtistForwardingUsesRealAlbumAndTrackHandlers()
    {
        // Arrange
        await SetupDatabase(
            62621,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 2;
            }
        );
        var dbContext = IDbContext;
        var artist = await dbContext
            .PlexMusicArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
            .SingleAsync(CancellationToken);
        var album = artist.Albums.Single();
        var tracks = await dbContext
            .PlexMusicTracks.Include(x => x.MediaDataList)
            .Where(x => x.PlexAlbumId == album.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var target = tracks[0];
        var sibling = tracks[1];
        var artistSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicArtist,
            SelectionKind = null,
            PlexServerId = artist.PlexServerId,
            PlexLibraryId = artist.PlexLibraryId,
            MediaIds = [artist.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = true,
        };
        var targetSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            SelectionKind = null,
            PlexServerId = target.PlexServerId,
            PlexLibraryId = target.PlexLibraryId,
            MediaIds = [target.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = false,
        };
        var siblingSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicTrack,
            SelectionKind = null,
            PlexServerId = sibling.PlexServerId,
            PlexLibraryId = sibling.PlexLibraryId,
            MediaIds = [sibling.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = true,
        };
        var request = new CreateDownloadTasksRequest(
            [artistSelection, targetSelection, siblingSelection],
            destinationFolderPathId: 7,
            customDestinationFolderPath: "/artist"
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
                            GenerateDownloadTaskMusicAlbumsCommand albumsCommand =>
                                new GenerateDownloadTaskMusicAlbumsCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext,
                                    executor.Object
                                ).ExecuteAsync(albumsCommand, ct),
                            GenerateDownloadTaskMusicTracksCommand tracksCommand =>
                                new GenerateDownloadTaskMusicTracksCommandHandler(
                                    Serilog.Log.Logger,
                                    handlerContext
                                ).ExecuteAsync(tracksCommand, ct),
                            _ => throw new InvalidOperationException($"Unexpected generator {command.GetType().Name}"),
                        }
                    );
                }
            )
            .Verifiable(Times.Exactly(3));

        // Act
        var first = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicArtistsCommand(request)
        );
        var repeated = await executor.Object.Send(
            new GenerateDownloadTaskMusicTracksCommand(
                new CreateDownloadTasksRequest(
                    [targetSelection, siblingSelection],
                    destinationFolderPathId: 7,
                    customDestinationFolderPath: "/artist"
                )
            ),
            CancellationToken
        );

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        first.Value.ShouldBe(
            new DownloadTaskCreationReport
            {
                MusicArtists = 1,
                MusicAlbums = 1,
                MusicTracks = 2,
            }
        );
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Errors.Count.ShouldBe(0);
        repeated.Value.ShouldBe(new DownloadTaskCreationReport());

        var artistTask = await dbContext.DownloadTaskMusicArtists.SingleAsync(CancellationToken);
        (artistTask.PlexApiRatingKey, artistTask.PlexServerId, artistTask.PlexLibraryId).ShouldBe(
            (artist.PlexApiRatingKey, artist.PlexServerId, artist.PlexLibraryId)
        );
        (await dbContext.DownloadTaskMusicAlbums.CountAsync(CancellationToken)).ShouldBe(1);

        var files = await dbContext
            .DownloadTaskMusicTrackFiles.Include(x => x.Parent)
            .OrderBy(x => x.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiPartId)
            .ToListAsync(CancellationToken);
        files.Select(x => (x.Parent!.PlexApiRatingKey, x.PlexApiMediaId, x.PlexApiPartId, x.DirectoryMeta.KeepCompletedInDownloadFolder))
            .ShouldBe(
                new[]
                {
                    (
                        target.PlexApiRatingKey,
                        target.MediaDataList.Single().PlexApiMediaId,
                        target.MediaDataList.Single().PlexApiPartId,
                        false
                    ),
                    (
                        sibling.PlexApiRatingKey,
                        sibling.MediaDataList.Single().PlexApiMediaId,
                        sibling.MediaDataList.Single().PlexApiPartId,
                        true
                    ),
                }.OrderBy(x => x.Item1).ThenBy(x => x.Item3)
            );
        files.Count.ShouldBe(2);
        (await dbContext.DownloadTaskTrackFileLogs.CountAsync(CancellationToken)).ShouldBe(2);
        executor.Verify();
    }

    [Test]
    public async Task ShouldRejectArtistFromAnotherLibraryWithoutDispatching_WhenOwnershipMismatches()
    {
        // Arrange
        await SetupDatabase(
            62602,
            config =>
            {
                config.PlexMusicLibraryCount = 2;
                config.MusicArtistCount = 1;
            }
        );
        var artists = await IDbContext.PlexMusicArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicArtist,
            SelectionKind = null,
            PlexServerId = artists[0].PlexServerId,
            PlexLibraryId = artists[1].PlexLibraryId,
            MediaIds = [artists[0].Id],
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicArtistsCommand(new CreateDownloadTasksRequest([selection]))
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await IDbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskMusicAlbumsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
