namespace Reaparr.Domain.UnitTests;

public partial class PlexMediaTypeMappersUnitTests
{
    [Test]
    [Arguments(PlexMediaType.Movie, "movie")]
    [Arguments(PlexMediaType.TvShow, "show")]
    [Arguments(PlexMediaType.Season, "season")]
    [Arguments(PlexMediaType.Episode, "episode")]
    [Arguments(PlexMediaType.Music, "artist")]
    [Arguments(PlexMediaType.Album, "album")]
    [Arguments(PlexMediaType.Track, "track")]
    [Arguments(PlexMediaType.PhotoAlbum, "photoalbum")]
    [Arguments(PlexMediaType.Photos, "photo")]
    public void ShouldConvertEnumToPlexApiString_WhenValidEnumValueProvided(PlexMediaType input, string expected)
    {
        // Act
        var result = input.ToPlexApiString();

        // Assert
        result.ShouldBe(expected);
    }

    [Test]
    [Arguments(PlexMediaType.None)]
    [Arguments(PlexMediaType.OtherVideos)]
    [Arguments(PlexMediaType.Games)]
    [Arguments(PlexMediaType.Unknown)]
    [Arguments((PlexMediaType)999)]
    [Arguments((PlexMediaType)(-1))]
    [Arguments((PlexMediaType)100)]
    [Arguments((PlexMediaType)6)]
    public void ShouldThrowNotImplementedException_WhenUnsupportedEnumValueProvided(PlexMediaType input)
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() => input.ToPlexApiString());
    }

    [Test]
    public void ShouldHandleAllSupportedEnumValues_WhenConvertingToPlexApiString()
    {
        // Arrange - These are the enum values that have PlexApi string representations
        var supportedEnumValues = new Dictionary<PlexMediaType, string>
        {
            { PlexMediaType.Movie, "movie" },
            { PlexMediaType.TvShow, "show" },
            { PlexMediaType.Season, "season" },
            { PlexMediaType.Episode, "episode" },
            { PlexMediaType.Artist, "artist" },
            { PlexMediaType.Album, "album" },
            { PlexMediaType.Song, "track" },
            { PlexMediaType.PhotoAlbum, "photoalbum" },
            { PlexMediaType.Photos, "photo" },
        };

        // Act & Assert
        foreach (var (enumValue, expectedString) in supportedEnumValues)
        {
            // Convert enum to PlexApi string
            var result = enumValue.ToPlexApiString();

            // Verify the result matches expected value
            result.ShouldBe(
                expectedString,
                $"Expected {enumValue} to convert to '{expectedString}', but got '{result}'"
            );
        }
    }

    [Test]
    public void ShouldCompleteRoundTripConversion_WhenConvertingEnumToPlexApiStringAndBack()
    {
        // Arrange - These are the enum values that have PlexApi string representations
        var supportedEnumValues = new[]
        {
            PlexMediaType.Movie,
            PlexMediaType.TvShow,
            PlexMediaType.Season,
            PlexMediaType.Episode,
            PlexMediaType.Artist,
            PlexMediaType.Album,
            PlexMediaType.Song,
            PlexMediaType.PhotoAlbum,
            PlexMediaType.Photos,
        };

        // Act & Assert
        foreach (var originalEnumValue in supportedEnumValues)
        {
            // Convert enum to PlexApi string
            var plexApiString = originalEnumValue.ToPlexApiString();

            // Convert PlexApi string back to enum using ToPlexMediaType
            var convertedBackEnum = plexApiString.ToPlexMediaType();

            // Verify round-trip conversion works perfectly
            convertedBackEnum.ShouldBe(
                originalEnumValue,
                $"Round-trip conversion failed: {originalEnumValue} -> '{plexApiString}' -> {convertedBackEnum}"
            );
        }
    }

    [Test]
    public void ShouldReturnLowercaseStrings_WhenConvertingValidEnums()
    {
        // Arrange & Act & Assert - Test that strings are in lowercase format
        PlexMediaType.Movie.ToPlexApiString().ShouldBe("movie");
        PlexMediaType.TvShow.ToPlexApiString().ShouldBe("show");
        PlexMediaType.Season.ToPlexApiString().ShouldBe("season");
        PlexMediaType.Episode.ToPlexApiString().ShouldBe("episode");
        PlexMediaType.Artist.ToPlexApiString().ShouldBe("artist");
        PlexMediaType.Album.ToPlexApiString().ShouldBe("album");
        PlexMediaType.Song.ToPlexApiString().ShouldBe("track"); // Note: Song maps to "track"
        PlexMediaType.PhotoAlbum.ToPlexApiString().ShouldBe("photoalbum");
        PlexMediaType.Photos.ToPlexApiString().ShouldBe("photo");
    }

    [Test]
    public void ShouldReturnConsistentPlexApiResults_WhenCalledMultipleTimes()
    {
        // Arrange
        var testEnumValue = PlexMediaType.Movie;

        // Act
        var result1 = testEnumValue.ToPlexApiString();
        var result2 = testEnumValue.ToPlexApiString();
        var result3 = testEnumValue.ToPlexApiString();

        // Assert
        result1.ShouldBe(result2);
        result2.ShouldBe(result3);
        result1.ShouldBe("movie");
    }

    [Test]
    public void ShouldThrowForUnsupportedEnumValues_WhenCalledWithNonPlexApiValues()
    {
        // Arrange - These enum values do NOT have PlexApi string representations
        var unsupportedEnumValues = new[]
        {
            PlexMediaType.None,
            PlexMediaType.Music,
            PlexMediaType.OtherVideos,
            PlexMediaType.Games,
            PlexMediaType.Unknown,
        };

        // Act & Assert
        foreach (var enumValue in unsupportedEnumValues)
        {
            Should.Throw<NotImplementedException>(
                () => enumValue.ToPlexApiString(),
                $"Expected {enumValue} to throw NotImplementedException"
            );
        }
    }
}
