using FlexQuery.NET.Models;

namespace Reaparr.Application.UnitTests;

public class GetMediaOverviewSortingValidationUnitTests
{
    /// <summary>
    /// Ensures direct overview commands reject audio ordering for families that do not expose audio-sort capability.
    /// </summary>
    /// <remarks>
    /// Direct command callers must receive a validation failure rather than silently using a snapshot's title rank.
    /// Checking the precise invalid property distinguishes sort rejection from unrelated media-family validation.
    /// </remarks>
    [Test]
    [Arguments(PlexMediaType.Movie)]
    [Arguments(PlexMediaType.TvShow)]
    [Arguments(PlexMediaType.PhotoAlbum)]
    [Arguments(PlexMediaType.OtherVideos)]
    public void ShouldRejectAudioQuality_WhenValidatingANonMusicOverviewCommand(PlexMediaType mediaType)
    {
        // Arrange
        var filter = new MediaQueryFilter
        {
            MediaType = mediaType,
            PlexLibraryId = 1,
            FilterOfflineMedia = false,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters { Sort = "audioQuality:desc" },
        };

        // Act
        var result = mediaType switch
        {
            PlexMediaType.Movie => new GetMediaOverviewMovieCommandValidator().Validate(new GetMediaOverviewMovieCommand(filter)),
            PlexMediaType.TvShow => new GetMediaOverviewTvShowCommandValidator().Validate(new GetMediaOverviewTvShowCommand(filter)),
            PlexMediaType.PhotoAlbum => new GetMediaOverviewPhotoCommandValidator().Validate(new GetMediaOverviewPhotoCommand(filter)),
            PlexMediaType.OtherVideos => new GetMediaOverviewOtherVideoCommandValidator().Validate(new GetMediaOverviewOtherVideoCommand(filter)),
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
        };

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.PropertyName).ShouldBe(["Filter.Parameters.Sort"]);
    }
}
