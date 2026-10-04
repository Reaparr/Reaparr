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
    [Arguments(PlexMediaType.PhotoAlbum, false)]
    [Arguments(PlexMediaType.PhotoAlbum, true)]
    [Arguments(PlexMediaType.PhotoImage, false)]
    [Arguments(PlexMediaType.PhotoImage, true)]
    [Arguments(PlexMediaType.OtherVideos, false)]
    [Arguments(PlexMediaType.OtherVideos, true)]
    public async Task ShouldRejectEntireSelection_WhenUnsupportedTypeIsAloneOrMixedWithMovie(
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
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().PropertyName.ShouldBe($"DownloadMedias[{(mixedWithMovie ? 1 : 0)}].Type");
    }
}
