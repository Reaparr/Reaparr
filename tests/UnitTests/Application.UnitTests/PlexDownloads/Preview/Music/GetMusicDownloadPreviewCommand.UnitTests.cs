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
}
