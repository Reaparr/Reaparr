namespace Reaparr.Application.UnitTests;

public partial class GenerateDownloadTaskMusicAlbumsCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskMusicAlbumsCommand>
{
    [Test]
    public async Task ShouldCreateMissingArtistAndAlbumAndForwardTracks_WhenAlbumIsSelected()
    {
        // Arrange
        await SetupDatabase(
            62603,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 2;
            }
        );
        var album = await IDbContext
            .PlexMusicAlbums.Include(x => x.PlexArtist)
            .Include(x => x.Tracks)
            .SingleAsync(CancellationToken);
        GenerateDownloadTaskMusicTracksCommand? forwarded = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicTracksCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<Result<DownloadTaskCreationReport>>, CancellationToken>(
                (value, _) => forwarded = (GenerateDownloadTaskMusicTracksCommand)value
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { MusicTracks = 2 }))
            .Verifiable(Times.Once());
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
            SelectionKind = null,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicAlbumsCommand(new CreateDownloadTasksRequest([selection], 8, "/albums"))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { MusicAlbums = 1, MusicTracks = 2 });
        var artistTask = await IDbContext
            .DownloadTaskMusicArtists.Include(x => x.Children)
            .SingleAsync(CancellationToken);
        artistTask.PlexApiRatingKey.ShouldBe(album.PlexArtist!.PlexApiRatingKey);
        artistTask.Children.Single().PlexApiRatingKey.ShouldBe(album.PlexApiRatingKey);
        forwarded.ShouldNotBeNull();
        forwarded.Request.DestinationFolderPathId.ShouldBe(8);
        forwarded.Request.CustomDestinationFolderPath.ShouldBe("/albums");
        forwarded
            .Request.DownloadMedias.SelectMany(x => x.MediaIds)
            .Order()
            .ShouldBe(album.Tracks.Select(x => x.Id).Order());
        forwarded.Request.DownloadMedias.ShouldAllBe(x => x.KeepCompletedInDownloadFolder);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldPreserveDirectTrackPlacement_WhenAlbumSelectionIsForwardedToRealTrackHandler()
    {
        // Arrange
        await SetupDatabase(
            62619,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 2;
            }
        );
        var dbContext = IDbContext;
        var album = await dbContext.PlexMusicAlbums.Include(x => x.PlexArtist).SingleAsync(CancellationToken);
        var tracks = await dbContext
            .PlexMusicTracks.Include(x => x.MediaDataList)
            .Where(x => x.PlexAlbumId == album.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var target = tracks[0];
        var sibling = tracks[1];
        var targetOriginal = target.MediaDataList.Single();
        var targetSecondPart = FakeData
            .GetPlexMusicTrackMediaData(new Seed(62620))
            .RuleFor(x => x.Id, _ => 0)
            .RuleFor(x => x.PlexTrackId, _ => target.Id)
            .RuleFor(x => x.PlexServerId, _ => target.PlexServerId)
            .RuleFor(x => x.PlexLibraryId, _ => target.PlexLibraryId)
            .RuleFor(x => x.PlexApiRatingKey, _ => target.PlexApiRatingKey)
            .RuleFor(x => x.PlexApiMediaId, _ => targetOriginal.PlexApiMediaId)
            .RuleFor(x => x.PartIndex, _ => targetOriginal.PartIndex + 1)
            .Generate();
        dbContext.PlexMusicTrackData.Add(targetSecondPart);
        await dbContext.SaveChangesAsync(CancellationToken);

        var albumSelection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
            SelectionKind = null,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id],
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
            [albumSelection, targetSelection, siblingSelection],
            destinationFolderPathId: 8,
            customDestinationFolderPath: "/albums"
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
            .Verifiable(Times.Exactly(2));

        // Act
        var first = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicAlbumsCommand(request)
        );
        var repeated = await executor.Object.Send(
            new GenerateDownloadTaskMusicTracksCommand(
                new CreateDownloadTasksRequest(
                    [targetSelection, siblingSelection],
                    destinationFolderPathId: 8,
                    customDestinationFolderPath: "/albums"
                )
            ),
            CancellationToken
        );

        // Assert
        first.IsSuccess.ShouldBeTrue();
        first.Errors.Count.ShouldBe(0);
        first.Value.ShouldBe(new DownloadTaskCreationReport { MusicAlbums = 1, MusicTracks = 2 });
        repeated.IsSuccess.ShouldBeTrue();
        repeated.Errors.Count.ShouldBe(0);
        repeated.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(1);
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
                        targetOriginal.PlexApiMediaId,
                        targetOriginal.PlexApiPartId,
                        false
                    ),
                    (
                        target.PlexApiRatingKey,
                        targetSecondPart.PlexApiMediaId,
                        targetSecondPart.PlexApiPartId,
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
        files.Count.ShouldBe(3);
        (await dbContext.DownloadTaskTrackFileLogs.CountAsync(CancellationToken)).ShouldBe(3);
        executor.Verify();
    }

    [Test]
    public async Task ShouldReuseExistingArtistAndAlbumWithoutCountingAlbum_WhenAlreadyPresent()
    {
        // Arrange
        await SetupDatabase(
            62604,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var dbContext = IDbContext;
        var album = await dbContext
            .PlexMusicAlbums.Include(x => x.PlexArtist)
            .Include(x => x.Tracks)
            .SingleAsync(CancellationToken);
        var artistTask = album.PlexArtist!.MapToDownloadTask(null);
        artistTask.Children.Add(album.MapToDownloadTask(artistTask, null));
        dbContext.DownloadTaskMusicArtists.Add(artistTask);
        await dbContext.SaveChangesAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
            SelectionKind = null,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id],
            Qualities = [],
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<GenerateDownloadTaskMusicTracksCommand>(c =>
                        c.Request.DownloadMedias.SelectMany(x => x.MediaIds)
                            .SequenceEqual(album.Tracks.Select(x => x.Id))
                        && c.Request.DownloadMedias.All(x =>
                            x.Type == PlexMediaType.MusicTrack
                            && x.PlexServerId == album.PlexServerId
                            && x.PlexLibraryId == album.PlexLibraryId
                        )
                    ),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport()))
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicAlbumsCommand(new CreateDownloadTasksRequest([selection]))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskMusicAlbums.CountAsync(CancellationToken)).ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}

public partial class GenerateDownloadTaskMusicAlbumsCommandUnitTests
{
    [Test]
    public async Task ShouldRejectAlbumFromAnotherLibraryWithoutCreatingParents_WhenOwnershipMismatches()
    {
        // Arrange
        await SetupDatabase(
            62610,
            config =>
            {
                config.PlexMusicLibraryCount = 2;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
            }
        );
        var albums = await IDbContext.PlexMusicAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
            SelectionKind = null,
            PlexServerId = albums[0].PlexServerId,
            PlexLibraryId = albums[1].PlexLibraryId,
            MediaIds = [albums[0].Id],
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicAlbumsCommand(new CreateDownloadTasksRequest([selection]))
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await IDbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(0);
        (await IDbContext.DownloadTaskMusicAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<GenerateDownloadTaskMusicTracksCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
