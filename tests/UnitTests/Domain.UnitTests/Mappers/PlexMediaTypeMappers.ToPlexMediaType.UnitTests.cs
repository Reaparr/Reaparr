namespace Reaparr.Domain.UnitTests;

public partial class PlexMediaTypeMappersUnitTests
{
    [Test]
    [Arguments("None", PlexMediaType.None)]
    [Arguments("Movie", PlexMediaType.Movie)]
    [Arguments("TvShow", PlexMediaType.TvShow)]
    [Arguments("Season", PlexMediaType.Season)]
    [Arguments("Episode", PlexMediaType.Episode)]
    [Arguments("Music", PlexMediaType.Music)]
    [Arguments("Album", PlexMediaType.Album)]
    [Arguments("Track", PlexMediaType.Track)]
    [Arguments("PhotoAlbum", PlexMediaType.PhotoAlbum)]
    [Arguments("Photos", PlexMediaType.Photos)]
    [Arguments("OtherVideos", PlexMediaType.OtherVideos)]
    [Arguments("Games", PlexMediaType.Games)]
    [Arguments("Unknown", PlexMediaType.Unknown)]
    public void ShouldConvertEnumNameStrings_WhenValidEnumNameProvided(string input, PlexMediaType expected)
    {
        // Act
        var result = input.ToPlexMediaType();

        // Assert
        result.ShouldBe(expected);
    }

    [Test]
    [Arguments("movie", PlexMediaType.Movie)]
    [Arguments("show", PlexMediaType.TvShow)]
    [Arguments("season", PlexMediaType.Season)]
    [Arguments("episode", PlexMediaType.Episode)]
    [Arguments("artist", PlexMediaType.Music)]
    [Arguments("album", PlexMediaType.Album)]
    [Arguments("track", PlexMediaType.Track)]
    [Arguments("photoalbum", PlexMediaType.PhotoAlbum)]
    [Arguments("photo", PlexMediaType.Photos)]
    public void ShouldConvertPlexApiStrings_WhenValidPlexApiStringProvided(string input, PlexMediaType expected)
    {
        // Act
        var result = input.ToPlexMediaType();

        // Assert
        result.ShouldBe(expected);
    }

    [Test]
    [Arguments("invalid")]
    [Arguments("")]
    [Arguments("MOVIE")]
    [Arguments("SHOW")]
    [Arguments("randomstring")]
    [Arguments("123")]
    [Arguments("null")]
    [Arguments("Artist")]
    [Arguments("Song")]
    public void ShouldThrowNotImplementedException_WhenInvalidStringProvided(string input)
    {
        // Act & Assert
        Should.Throw<NotImplementedException>(() => input.ToPlexMediaType());
    }

    [Test]
    public void ShouldThrowNotImplementedException_WhenNullStringProvided()
    {
        // Arrange
        string? input = null;

        // Act & Assert
        Should.Throw<NotImplementedException>(() => input!.ToPlexMediaType());
    }

    [Test]
    public void ShouldHandleAllEnumValues_WhenConvertingFromEnumNameStrings()
    {
        // Arrange
        var allEnumValues = Enum.GetValues<PlexMediaType>();

        // Act & Assert
        foreach (var enumValue in allEnumValues)
        {
            // Convert enum to string using the ToPlexMediaTypeString method
            var enumAsString = enumValue.ToPlexMediaTypeString();

            // Convert back to enum using ToPlexMediaType
            var result = enumAsString.ToPlexMediaType();

            // Verify round-trip conversion works
            result.ShouldBe(enumValue, $"Failed to convert {enumValue} -> {enumAsString} -> {result}");
        }
    }

    [Test]
    public void ShouldHandleAllPlexApiMappings_WhenConvertingFromPlexApiStrings()
    {
        // Arrange - These are the enum values that have PlexApi string representations
        var enumsWithPlexApiStrings = new[]
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
        foreach (var enumValue in enumsWithPlexApiStrings)
        {
            // Convert enum to PlexApi string using the ToPlexApiString method
            var plexApiString = enumValue.ToPlexApiString();

            // Convert back to enum using ToPlexMediaType
            var result = plexApiString.ToPlexMediaType();

            // Verify round-trip conversion works
            result.ShouldBe(enumValue, $"Failed to convert {enumValue} -> {plexApiString} -> {result}");
        }
    }

    [Test]
    [Arguments("Music")] // Music enum exists but has no PlexApi string mapping
    [Arguments("None")] // None enum exists but has no PlexApi string mapping
    [Arguments("OtherVideos")] // OtherVideos enum exists but has no PlexApi string mapping
    [Arguments("Games")] // Games enum exists but has no PlexApi string mapping
    [Arguments("Unknown")] // Unknown enum exists but has no PlexApi string mapping
    public void ShouldHandleEnumNamesWithoutPlexApiMapping_WhenValidEnumNameProvided(string input)
    {
        // Act
        var result = input.ToPlexMediaType();

        // Assert
        result.ShouldBe(Enum.Parse<PlexMediaType>(input));
    }
}
