using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewCommandUnitTests
{
    [Test]
    [Arguments(PlexMediaType.Unknown)]
    [Arguments(PlexMediaType.Photos)]
    public void ShouldRejectUnsupportedMediaType_WhenValidatingRequest(PlexMediaType mediaType)
    {
        // Arrange
        var command = new GetMediaOverviewCommand(CreateFilter(mediaType));

        // Act
        var result = new GetMediaOverviewCommandValidator().Validate(command);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(x => x.PropertyName).ShouldBe(new[] { "Filter.MediaType" });
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.Music)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public void ShouldRejectInvalidPaging_WhenValidatingRequest(PlexMediaType mediaType)
    {
        // Arrange
        var filter = CreateFilter(mediaType) with
        {
            Parameters = new FlexQueryParameters { Page = 0, PageSize = 101 },
        };

        // Act
        var result = new GetMediaOverviewCommandValidator().Validate(new GetMediaOverviewCommand(filter));

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(x => x.PropertyName).ShouldBe(
            new[] { "Filter.Parameters.Page", "Filter.Parameters.PageSize" }
        );
    }

    private static MediaQueryFilter CreateFilter(PlexMediaType mediaType) =>
        new()
        {
            MediaType = mediaType,
            PlexLibraryId = 0,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = 2,
                PageSize = 25,
                Sort = "year:desc",
            },
        };
}
