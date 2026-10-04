namespace Reaparr.Application.UnitTests;

public class GetDownloadPreviewQueryValidatorUnitTests : BaseUnitTest<GetDownloadPreviewQueryValidator>
{
    [Test]
    [Arguments(PlexMediaType.Music, false)]
    [Arguments(PlexMediaType.Music, true)]
    [Arguments(PlexMediaType.Album, false)]
    [Arguments(PlexMediaType.Album, true)]
    [Arguments(PlexMediaType.Track, false)]
    [Arguments(PlexMediaType.Track, true)]
    [Arguments(PlexMediaType.PhotoAlbum, false)]
    [Arguments(PlexMediaType.PhotoAlbum, true)]
    [Arguments(PlexMediaType.Photos, false)]
    [Arguments(PlexMediaType.Photos, true)]
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
