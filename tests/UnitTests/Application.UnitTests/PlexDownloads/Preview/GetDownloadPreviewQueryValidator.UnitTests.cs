namespace Reaparr.Application.UnitTests;

public class GetDownloadPreviewQueryValidatorUnitTests : BaseUnitTest<GetDownloadPreviewQueryValidator>
{
    [Test]
    [Arguments(PlexMediaType.MusicArtist, false)]
    [Arguments(PlexMediaType.MusicArtist, true)]
    [Arguments(PlexMediaType.MusicAlbum, false)]
    [Arguments(PlexMediaType.MusicAlbum, true)]
    [Arguments(PlexMediaType.MusicTrack, false)]
    [Arguments(PlexMediaType.MusicTrack, true)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldAcceptEntireSelection_WhenMusicOrOtherVideoIsAloneOrMixedWithMovie(
        PlexMediaType type,
        bool mixedWithMovie
    )
    {
        // Arrange
        var selections = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = type,
                PlexServerId = 1,
                PlexLibraryId = 11,
                MediaIds = [101],
                Qualities = [],
            },
        };
        if (mixedWithMovie)
            selections.Insert(
                0,
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.Movie,
                    PlexServerId = 1,
                    PlexLibraryId = 11,
                    MediaIds = [102],
                    Qualities = [],
                }
            );
        var query = new GetDownloadPreviewQuery(selections);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.PhotoImage)]
    public async Task ShouldAcceptPhotoSelections_WhenAlbumOrImageIsSelected(PlexMediaType type)
    {
        // Arrange
        var query = new GetDownloadPreviewQuery([
            new DownloadMediaDTO
            {
                Type = type,
                PlexServerId = 1,
                PlexLibraryId = 11,
                MediaIds = [101],
                Qualities = [],
            },
        ]);

        // Act
        var result = await Sut.ValidateAsync(query, CancellationToken);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
}
