namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskMusicArtistsCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskMusicArtistsCommand>
{
    [Test]
    public async Task ShouldCreateArtistAndForwardAlbumsWithDescendantChoices_WhenArtistIsSelected()
    {
        // Arrange
        await SetupDatabase(62601, config =>
        {
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistCount = 1;
            config.MusicAlbumCount = 1;
            config.MusicTrackCount = 1;
        });
        var artist = await IDbContext.PlexArtists.Include(x => x.Albums).ThenInclude(x => x.Tracks).SingleAsync(CancellationToken);
        var album = artist.Albums.Single();
        var track = album.Tracks.Single();
        var inherited = new PlexMediaQualityDTO
        {
            MediaId = track.Id,
            DataId = 101,
            MediaDataType = PlexMediaType.MusicTrack,
            Quality = VideoQuality.Unknown,
        };
        var direct = inherited with { DataId = 202 };
        GenerateDownloadTaskMusicAlbumsCommand? forwarded = null;
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<GenerateDownloadTaskMusicAlbumsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateDownloadTaskMusicAlbumsCommand, CancellationToken>((value, _) => forwarded = value)
            .ReturnsAsync(Result.Ok(new DownloadTaskCreationReport { MusicAlbums = 1, MusicTracks = 1 }))
            .Verifiable(Times.Once());
        var request = new CreateDownloadTasksRequest(
            [
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.MusicArtist,
                    PlexServerId = artist.PlexServerId,
                    PlexLibraryId = artist.PlexLibraryId,
                    MediaIds = [artist.Id],
                    Qualities = [inherited],
                    KeepCompletedInDownloadFolder = true,
                },
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.MusicTrack,
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
        result.Value.ShouldBe(new DownloadTaskCreationReport { MusicArtists = 1, MusicAlbums = 1, MusicTracks = 1 });
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
    public async Task ShouldRejectArtistFromAnotherLibraryWithoutDispatching_WhenOwnershipMismatches()
    {
        // Arrange
        await SetupDatabase(62602, config =>
        {
            config.PlexMusicLibraryCount = 2;
            config.MusicArtistCount = 1;
        });
        var artists = await IDbContext.PlexArtists.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.MusicArtist,
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
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(It.IsAny<GenerateDownloadTaskMusicAlbumsCommand>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }
}
