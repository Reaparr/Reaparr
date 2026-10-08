namespace Reaparr.Application.UnitTests;

public partial class GenerateDownloadTaskMusicAlbumsCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskMusicAlbumsCommand>
{
    [Test]
    public async Task ShouldCreateMissingArtistAndAlbumAndForwardTracks_WhenAlbumIsSelected()
    {
        // Arrange
        await SetupDatabase(62603, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 2;
        });
        var album = await IDbContext.PlexAlbums.Include(x => x.PlexArtist).Include(x => x.Tracks).SingleAsync(CancellationToken);
        GenerateDownloadTaskMusicTracksCommand? forwarded = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicTracksCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateDownloadTaskMusicTracksCommand, CancellationToken>((value, _) => forwarded = value)
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { MusicTracks = 2 }))
            .Verifiable(Times.Once());
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskMusicAlbumsCommand(
                new CreateDownloadTasksRequest([selection], 8, "/albums")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { MusicAlbums = 1, MusicTracks = 2 });
        var artistTask = await IDbContext.DownloadTaskMusicArtists.Include(x => x.Children).SingleAsync(CancellationToken);
        artistTask.PlexApiRatingKey.ShouldBe(album.PlexArtist!.PlexApiRatingKey);
        artistTask.Children.Single().PlexApiRatingKey.ShouldBe(album.PlexApiRatingKey);
        forwarded.ShouldNotBeNull();
        forwarded.Request.DestinationFolderPathId.ShouldBe(8);
        forwarded.Request.CustomDestinationFolderPath.ShouldBe("/albums");
        forwarded.Request.DownloadMedias.SelectMany(x => x.MediaIds).Order().ShouldBe(album.Tracks.Select(x => x.Id).Order());
        forwarded.Request.DownloadMedias.ShouldAllBe(x => x.KeepCompletedInDownloadFolder);
        Mock.Mock<ICommandExecutor>().Verify();
    }

    [Test]
    public async Task ShouldReuseExistingArtistAndAlbumWithoutCountingAlbum_WhenAlreadyPresent()
    {
        // Arrange
        await SetupDatabase(62604, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var album = await IDbContext.PlexAlbums.Include(x => x.PlexArtist).Include(x => x.Tracks).SingleAsync(CancellationToken);
        var artistTask = album.PlexArtist!.MapToDownloadTask(null);
        artistTask.Children.Add(album.MapToDownloadTask(artistTask, null));
        IDbContext.DownloadTaskMusicArtists.Add(artistTask);
        await IDbContext.SaveChangesAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id],
            Qualities = [],
        };
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<GenerateDownloadTaskMusicTracksCommand>(c =>
                    c.Request.DownloadMedias.SelectMany(x => x.MediaIds).SequenceEqual(album.Tracks.Select(x => x.Id))
                    && c.Request.DownloadMedias.All(x =>
                        x.Type == PlexMediaType.MusicTrack
                        && x.PlexServerId == album.PlexServerId
                        && x.PlexLibraryId == album.PlexLibraryId)),
                CancellationToken))
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
        (await IDbContext.DownloadTaskMusicArtists.CountAsync(CancellationToken)).ShouldBe(1);
        (await IDbContext.DownloadTaskMusicAlbums.CountAsync(CancellationToken)).ShouldBe(1);
        Mock.Mock<ICommandExecutor>().Verify();
    }
}

public partial class GenerateDownloadTaskMusicAlbumsCommandUnitTests
{
    [Test]
    public async Task ShouldRejectAlbumFromAnotherLibraryWithoutCreatingParents_WhenOwnershipMismatches()
    {
        // Arrange
        await SetupDatabase(62610, config =>
        {
            config.PlexMusicLibraryCount = 2;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
        });
        var albums = await IDbContext.PlexAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicAlbum,
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
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<GenerateDownloadTaskMusicTracksCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }
}
