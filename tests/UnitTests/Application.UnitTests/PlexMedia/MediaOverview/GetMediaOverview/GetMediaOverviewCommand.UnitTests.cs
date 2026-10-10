using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewCommandUnitTests
{
    [Test]
    [Arguments(PlexMediaType.Unknown)]
    [Arguments(PlexMediaType.PhotoImage)]
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
    [Arguments(PlexMediaType.MusicArtist)]
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
        result
            .Errors.Select(x => x.PropertyName)
            .ShouldBe(new[] { "Filter.Parameters.Page", "Filter.Parameters.PageSize" });
    }

    [Test]
    [Arguments(PlexMediaComparisonState.NotCompared, true)]
    [Arguments(PlexMediaComparisonState.Pending, true)]
    [Arguments(PlexMediaComparisonState.Owned, true)]
    [Arguments(PlexMediaComparisonState.Missing, true)]
    [Arguments(PlexMediaComparisonState.Partial, true)]
    [Arguments(PlexMediaComparisonState.HigherQuality, false)]
    [Arguments(PlexMediaComparisonState.PartialAndHigherQuality, false)]
    public void ShouldLimitMusicToCoverageStates_WhenValidatingEveryOverviewBoundary(PlexMediaComparisonState state, bool valid)
    {
        // Arrange
        var filter = CreateFilter(PlexMediaType.MusicArtist) with { ComparisonState = state };
        var request = new GetAllMediaByTypeRequest { MediaType = PlexMediaType.MusicArtist, ComparisonState = state };

        // Act
        var overview = new GetMediaOverviewCommandValidator().Validate(new GetMediaOverviewCommand(filter));
        var music = new GetMediaOverviewMusicCommandValidator().Validate(new GetMediaOverviewMusicCommand(filter));
        var endpoint = new GetAllMediaByTypeRequestValidator().Validate(request);

        // Assert
        overview.IsValid.ShouldBe(valid);
        music.IsValid.ShouldBe(valid);
        endpoint.IsValid.ShouldBe(valid);
        overview.Errors.Select(x => x.PropertyName).ShouldBe(valid ? [] : new[] { "Filter.ComparisonState" });
        music.Errors.Select(x => x.PropertyName).ShouldBe(valid ? [] : new[] { "Filter.ComparisonState" });
        endpoint.Errors.Select(x => x.PropertyName).ShouldBe(valid ? [] : new[] { "ComparisonState" });
    }

    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    public void ShouldPreserveVideoUpgradeFilters_WhenValidatingOverview(PlexMediaType mediaType)
    {
        // Arrange
        var filter = CreateFilter(mediaType) with { ComparisonState = PlexMediaComparisonState.PartialAndHigherQuality };

        // Act
        var result = new GetMediaOverviewCommandValidator().Validate(new GetMediaOverviewCommand(filter));

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
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
