namespace Reaparr.Application.UnitTests;

public class GetMusicDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetMusicDownloadPreviewCommand>
{
    [Test]
    [Arguments(PlexMediaType.MusicArtist)]
    [Arguments(PlexMediaType.MusicAlbum)]
    [Arguments(PlexMediaType.MusicTrack)]
    public async Task ShouldReturnTypedAncestorsAndSelectedDescendants_WhenMusicIsRequested(PlexMediaType type)
    {
        // Arrange
        await SetupDatabase(
            62801,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 2;
                config.MusicAlbumCount = 2;
                config.MusicTrackCount = 2;
            }
        );
        var artist = await IDbContext
            .PlexMusicArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var album = artist.Albums.OrderBy(x => x.Id).First();
        var track = album.Tracks.OrderBy(x => x.Id).First();
        var id = type switch
        {
            PlexMediaType.MusicArtist => artist.Id,
            PlexMediaType.MusicAlbum => album.Id,
            _ => track.Id,
        };
        var query = new GetMusicDownloadPreviewCommand([
            new DownloadMediaDTO
            {
                Type = type,
                SelectionKind = null,
                PlexServerId = artist.PlexServerId,
                PlexLibraryId = artist.PlexLibraryId,
                MediaIds = [id],
                Qualities = [],
            },
        ]);

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var previewArtist = result.Value.Single();
        previewArtist.MediaType.ShouldBe(PlexMediaType.MusicArtist);
        previewArtist.Children.ShouldNotBeEmpty();
        previewArtist.Size.ShouldBeGreaterThan(0);
        if (type == PlexMediaType.MusicTrack)
            previewArtist.Children.SelectMany(x => x.Children).Count().ShouldBe(1);
        else
            previewArtist.Children.SelectMany(x => x.Children).ShouldNotBeEmpty();
    }

    [Test]
    [Arguments(DownloadStatus.Queued, PlexMediaType.MusicArtist)]
    [Arguments(DownloadStatus.Completed, PlexMediaType.MusicArtist)]
    [Arguments(DownloadStatus.Queued, PlexMediaType.MusicAlbum)]
    [Arguments(DownloadStatus.Completed, PlexMediaType.MusicAlbum)]
    [Arguments(DownloadStatus.Queued, PlexMediaType.MusicTrack)]
    [Arguments(DownloadStatus.Completed, PlexMediaType.MusicTrack)]
    public async Task ShouldPreviewSelectedMusic_WhenMatchingDownloadTaskAlreadyExists(
        DownloadStatus status,
        PlexMediaType selectionType
    )
    {
        // Arrange
        await SetupDatabase(
            62813,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistCount = 1;
                config.MusicAlbumCount = 1;
                config.MusicTrackCount = 1;
            }
        );
        var dbContext = IDbContext;
        var artist = await dbContext
            .PlexMusicArtists.Include(x => x.Albums)
                .ThenInclude(x => x.Tracks)
                    .ThenInclude(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var album = artist.Albums.Single();
        var track = album.Tracks.Single();
        var selectedId = selectionType switch
        {
            PlexMediaType.MusicArtist => artist.Id,
            PlexMediaType.MusicAlbum => album.Id,
            _ => track.Id,
        };
        var selection = new DownloadMediaDTO
        {
            Type = selectionType,
            SelectionKind = null,
            PlexServerId = track.PlexServerId,
            PlexLibraryId = track.PlexLibraryId,
            MediaIds = [selectedId],
            Qualities = [],
        };
        var creator = Mock.Create<GenerateDownloadTaskMusicTracksCommandHandler>();
        var created = await creator.ExecuteAsync(
            new GenerateDownloadTaskMusicTracksCommand(
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.MusicTrack,
                        SelectionKind = null,
                        PlexServerId = track.PlexServerId,
                        PlexLibraryId = track.PlexLibraryId,
                        MediaIds = [track.Id],
                        Qualities = [],
                    },
                ]
            ),
            CancellationToken
        );
        created.IsSuccess.ShouldBeTrue();
        created.Errors.Count.ShouldBe(0);
        created.Value.ShouldBe(new DownloadTaskCreationReport { MusicTracks = 1 });
        var artistTask = await dbContext
            .DownloadTaskMusicArtists.AsTracking().Include(x => x.Children)
                .ThenInclude(x => x.Children)
                    .ThenInclude(x => x.Children)
            .SingleAsync(CancellationToken);
        var albumTask = artistTask.Children.Single();
        var trackTask = albumTask.Children.Single();
        var fileTask = trackTask.Children.Single();
        var data = track.MediaDataList.Single();
        artistTask.PlexApiRatingKey.ShouldBe(artist.PlexApiRatingKey);
        albumTask.PlexApiRatingKey.ShouldBe(album.PlexApiRatingKey);
        trackTask.PlexApiRatingKey.ShouldBe(track.PlexApiRatingKey);
        fileTask.PlexApiMediaId.ShouldBe(data.PlexApiMediaId);
        artistTask.DownloadStatus = status;
        albumTask.DownloadStatus = status;
        trackTask.DownloadStatus = status;
        fileTask.DownloadStatus = status;
        await dbContext.SaveChangesAsync(CancellationToken);
        (await dbContext.DownloadTaskMusicArtists.Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
            .ShouldBe(status);
        (await dbContext.DownloadTaskMusicAlbums.Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
            .ShouldBe(status);
        (await dbContext.DownloadTaskMusicTracks.Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
            .ShouldBe(status);
        (await dbContext.DownloadTaskMusicTrackFiles.Select(x => x.DownloadStatus).SingleAsync(CancellationToken))
            .ShouldBe(status);
        var expectedSize = track.MediaDataList.Sum(x => x.Size);
        var expectedPartCount = track.MediaDataList.Count;

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(
            new GetMusicDownloadPreviewCommand([selection])
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var artistPreview = result.Value.Single();
        artistPreview.Id.ShouldBe(artist.Id);
        artistPreview.Title.ShouldBe(artist.Title);
        artistPreview.MediaType.ShouldBe(PlexMediaType.MusicArtist);
        artistPreview.ChildCount.ShouldBe(1);
        artistPreview.Size.ShouldBe(expectedSize);
        artistPreview.Children.Select(x => (x.Id, x.Title, x.MediaType, x.ChildCount, x.Size))
            .ShouldBe([(album.Id, album.Title, PlexMediaType.MusicAlbum, 1, expectedSize)]);
        var albumPreview = artistPreview.Children.Single();
        albumPreview.Children.Select(x => (x.Id, x.Title, x.MediaType, x.ChildCount, x.Size))
            .ShouldBe([(track.Id, track.Title, PlexMediaType.MusicTrack, expectedPartCount, expectedSize)]);
        var trackPreview = albumPreview.Children.Single();
        trackPreview.Qualities
            .Select(x => (x.MediaId, x.DataId, x.MediaDataType, x.VideoQuality, x.AudioQuality))
            .ShouldBe([(track.Id, data.Id, PlexMediaType.MusicTrack, VideoQuality.None, data.AudioQuality)]);
    }
}
