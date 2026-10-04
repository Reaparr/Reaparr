namespace Reaparr.Domain.UnitTests;

public partial class PlexMediaTypeMappersUnitTests
{
    [Test]
    [Arguments(PlexMediaType.None, "None")]
    [Arguments(PlexMediaType.Movie, "Movie")]
    [Arguments(PlexMediaType.TvShow, "TvShow")]
    [Arguments(PlexMediaType.Season, "Season")]
    [Arguments(PlexMediaType.Episode, "Episode")]
    [Arguments(PlexMediaType.Music, "Music")]
    [Arguments(PlexMediaType.Album, "Album")]
    [Arguments(PlexMediaType.Track, "Track")]
    [Arguments(PlexMediaType.PhotoAlbum, "PhotoAlbum")]
    [Arguments(PlexMediaType.Photos, "Photos")]
    [Arguments(PlexMediaType.OtherVideos, "OtherVideos")]
    [Arguments(PlexMediaType.Games, "Games")]
    [Arguments(PlexMediaType.Unknown, "Unknown")]
    public void ShouldConvertEnumToStringName_WhenValidEnumValueProvided(PlexMediaType input, string expected)
    {
        // Act
        var result = input.ToPlexMediaTypeString();

        // Assert
        result.ShouldBe(expected);
    }

    [Test]
    [MethodDataSource(nameof(GetInvalidEnumValues))]
    public void ShouldThrowNotImplementedException_WhenInvalidEnumValueProvided(PlexMediaType input)
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() => input.ToPlexMediaTypeString());
    }

    public static IEnumerable<PlexMediaType> GetInvalidEnumValues()
    {
        yield return (PlexMediaType)6;
        yield return (PlexMediaType)999;
        yield return (PlexMediaType)(-1);
        yield return (PlexMediaType)100;
        yield return (PlexMediaType)int.MaxValue;
        yield return (PlexMediaType)int.MinValue;
    }

    [Test]
    public void ShouldHandleAllEnumValues_WhenConvertingToString()
    {
        // Arrange
        var allEnumValues = Enum.GetValues<PlexMediaType>();

        // Act & Assert
        foreach (var enumValue in allEnumValues)
        {
            // Convert enum to string using ToPlexMediaTypeString
            var result = enumValue.ToPlexMediaTypeString();

            // Verify the result is not null or empty
            result.ShouldNotBeNullOrEmpty($"ToPlexMediaTypeString returned null/empty for {enumValue}");

            // Verify the result matches the enum name
            result.ShouldBe(
                enumValue.ToString(),
                $"Expected {enumValue} to convert to '{enumValue}', but got '{result}'"
            );
        }
    }

    [Test]
    public void ShouldCompleteRoundTripConversion_WhenConvertingEnumToStringAndBack()
    {
        // Arrange
        var allEnumValues = Enum.GetValues<PlexMediaType>();

        // Act & Assert
        foreach (var originalEnumValue in allEnumValues)
        {
            // Convert enum to string
            var stringValue = originalEnumValue.ToPlexMediaTypeString();

            // Convert string back to enum
            var convertedBackEnum = stringValue.ToPlexMediaType();

            // Verify round-trip conversion works perfectly
            convertedBackEnum.ShouldBe(
                originalEnumValue,
                $"Round-trip conversion failed: {originalEnumValue} -> '{stringValue}' -> {convertedBackEnum}"
            );
        }
    }

    [Test]
    public void ShouldReturnExpectedStringFormat_WhenConvertingValidEnums()
    {
        // Arrange & Act & Assert - Test that strings match exact enum names
        PlexMediaType.None.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.None));
        PlexMediaType.Movie.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Movie));
        PlexMediaType.TvShow.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.TvShow));
        PlexMediaType.Season.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Season));
        PlexMediaType.Episode.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Episode));
        PlexMediaType.Music.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Music));
        PlexMediaType.Album.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Album));
        PlexMediaType.Track.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Track));
        PlexMediaType.PhotoAlbum.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.PhotoAlbum));
        PlexMediaType.Photos.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Photos));
        PlexMediaType.OtherVideos.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.OtherVideos));
        PlexMediaType.Games.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Games));
        PlexMediaType.Unknown.ToPlexMediaTypeString().ShouldBe(nameof(PlexMediaType.Unknown));
    }

    [Test]
    public void ShouldReturnConsistentStringResults_WhenCalledMultipleTimes()
    {
        // Arrange
        var testEnumValue = PlexMediaType.Movie;

        // Act
        var result1 = testEnumValue.ToPlexMediaTypeString();
        var result2 = testEnumValue.ToPlexMediaTypeString();
        var result3 = testEnumValue.ToPlexMediaTypeString();

        // Assert
        result1.ShouldBe(result2);
        result2.ShouldBe(result3);
        result1.ShouldBe("Movie");
    }
}
