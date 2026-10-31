using System.Text.Json;

namespace Reaparr.Domain.UnitTests;

public class EnumMapperExtensionsSortTypeUnitTests
{
    [Test]
    [Arguments("title", SortType.Title)]
    [Arguments("SortIndex", SortType.Title)]
    [Arguments("SearchTitle", SortType.Title)]
    [Arguments(" YEAR ", SortType.Year)]
    [Arguments("addedAt", SortType.AddedAt)]
    [Arguments("updatedAt", SortType.UpdatedAt)]
    [Arguments("duration", SortType.Duration)]
    [Arguments("mediaSize", SortType.MediaSize)]
    [Arguments("videoQuality", SortType.VideoQuality)]
    [Arguments("audioQuality", SortType.AudioQuality)]
    [Arguments("comparisonState", SortType.ComparisonState)]
    public void ShouldMapSupportedSortFields_ToSortType(string field, SortType expected)
    {
        // Arrange

        // Act
        var actual = field.ToSortType();

        // Assert
        actual.ShouldBe(expected);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("unknown")]
    [Arguments("0")]
    [Arguments("1")]
    [Arguments("99")]
    [Arguments("Title, Year")]
    [Arguments("Title, Title")]
    [Arguments("title:asc")]
    [Arguments("title:desc")]
    [Arguments("sortTitle")]
    [Arguments("quality")]
    public void ShouldThrowArgumentOutOfRangeException_WhenSortFieldIsUnsupported(string? field)
    {
        // Arrange

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => field!.ToSortType());

        // Assert
        exception.ParamName.ShouldBe("value");
        exception.ActualValue.ShouldBe(field);
    }

    [Test]
    [Arguments(SortType.Title, "\"title\"")]
    [Arguments(SortType.Year, "\"year\"")]
    [Arguments(SortType.AddedAt, "\"addedAt\"")]
    [Arguments(SortType.UpdatedAt, "\"updatedAt\"")]
    [Arguments(SortType.Duration, "\"duration\"")]
    [Arguments(SortType.MediaSize, "\"mediaSize\"")]
    [Arguments(SortType.VideoQuality, "\"videoQuality\"")]
    [Arguments(SortType.AudioQuality, "\"audioQuality\"")]
    [Arguments(SortType.ComparisonState, "\"comparisonState\"")]
    public void ShouldSerializeEverySortTypeAsItsNamedJsonValue_WhenWritingJson(SortType value, string expectedJson)
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(value);

        // Assert
        json.ShouldBe(expectedJson);
    }

    [Test]
    [Arguments("\"title\"", SortType.Title)]
    [Arguments("\"year\"", SortType.Year)]
    [Arguments("\"addedAt\"", SortType.AddedAt)]
    [Arguments("\"updatedAt\"", SortType.UpdatedAt)]
    [Arguments("\"duration\"", SortType.Duration)]
    [Arguments("\"mediaSize\"", SortType.MediaSize)]
    [Arguments("\"videoQuality\"", SortType.VideoQuality)]
    [Arguments("\"audioQuality\"", SortType.AudioQuality)]
    [Arguments("\"comparisonState\"", SortType.ComparisonState)]
    public void ShouldDeserializeEveryNamedSortTypeJsonValue_WhenReadingJson(string json, SortType expected)
    {
        // Arrange

        // Act
        var value = JsonSerializer.Deserialize<SortType>(json);

        // Assert
        value.ShouldBe(expected);
    }

    [Test]
    [Arguments(SortType.Title, "SortIndex")]
    [Arguments(SortType.Year, "year")]
    [Arguments(SortType.AddedAt, "addedAt")]
    [Arguments(SortType.UpdatedAt, "updatedAt")]
    [Arguments(SortType.Duration, "duration")]
    [Arguments(SortType.MediaSize, "mediaSize")]
    [Arguments(SortType.VideoQuality, "videoQuality")]
    [Arguments(SortType.AudioQuality, "audioQuality")]
    [Arguments(SortType.ComparisonState, "comparisonState")]
    public void ShouldRoundTripOrderingField_WhenSortTypeIsSupported(SortType sortType, string expected)
    {
        // Arrange

        // Act
        var property = sortType.ToSortValue();
        var parsed = property.ToSortType();

        // Assert
        property.ShouldBe(expected);
        parsed.ShouldBe(sortType);
    }

    [Test]
    [Arguments("title:asc", true)]
    [Arguments("comparisonState:desc,title:asc", true)]
    [Arguments("year:desc,SearchTitle:asc", true)]
    [Arguments("unknown:asc", false)]
    [Arguments("id:asc", false)]
    [Arguments("title:asc,id:asc", false)]
    [Arguments("quality:asc", false)]
    [Arguments("sortTitle:asc", false)]
    [Arguments("99:asc", false)]
    public void ShouldValidateEnumOnlyMediaSorts_WhenNoFieldRestrictionIsProvided(string sort, bool expected)
    {
        // Arrange
        // Act
        var valid = sort.IsSupportedSort();

        // Assert
        valid.ShouldBe(expected);
    }

    [Test]
    public void ShouldUseEnumNameFallback_WhenSortTypeIsUndefined()
    {
        // Arrange
        var undefined = (SortType)99;

        // Act
        var value = undefined.ToSortValue();

        // Assert
        value.ShouldBe("99");
    }

    [Test]
    [Arguments(null, true)]
    [Arguments("", true)]
    [Arguments(" ", true)]
    [Arguments("title:asc", true)]
    [Arguments("SearchTitle:desc", true)]
    [Arguments("addedAt:desc", true)]
    [Arguments("year:desc", false)]
    [Arguments("title:asc,addedAt:desc", false)]
    [Arguments("unknown:asc", false)]
    [Arguments("title:asc:desc", false)]
    [Arguments("title:asc,", false)]
    [Arguments("quality:asc", false)]
    [Arguments("id:asc", false)]
    public void ShouldValidateSingleSortAgainstAllowedTypes(string? sort, bool expected)
    {
        // Arrange
        // Act
        var valid = sort.IsSupportedSort(SortType.Title, SortType.AddedAt);

        // Assert
        valid.ShouldBe(expected);
    }

    [Test]
    [Arguments(PlexMediaType.Movie, "comparisonState:asc,title:desc", true)]
    [Arguments(PlexMediaType.TvShow, "comparisonState:desc", true)]
    [Arguments(PlexMediaType.MusicArtist, "comparisonState:asc", true)]
    [Arguments(PlexMediaType.PhotoAlbum, "comparisonState:asc", false)]
    [Arguments(PlexMediaType.OtherVideos, "title:asc,comparisonState:desc", false)]
    [Arguments(PlexMediaType.PhotoAlbum, "title:asc,addedAt:desc", true)]
    [Arguments(PlexMediaType.Movie, "title:asc,", false)]
    [Arguments(PlexMediaType.Movie, "title:asc:desc", false)]
    [Arguments(PlexMediaType.MusicArtist, "audioQuality:asc", true)]
    [Arguments(PlexMediaType.MusicArtist, "audioQuality:desc,title:asc", true)]
    [Arguments(PlexMediaType.MusicArtist, "comparisonState:asc,audioQuality:desc", true)]
    [Arguments(PlexMediaType.MusicArtist, "videoQuality:asc", false)]
    [Arguments(PlexMediaType.MusicArtist, "year:desc", false)]
    [Arguments(PlexMediaType.Movie, "audioQuality:asc", false)]
    [Arguments(PlexMediaType.TvShow, "audioQuality:desc", false)]
    [Arguments(PlexMediaType.OtherVideos, "audioQuality:asc", false)]
    [Arguments(PlexMediaType.PhotoAlbum, "audioQuality:asc", false)]
    [Arguments(PlexMediaType.PhotoAlbum, "videoQuality:asc", false)]
    [Arguments(PlexMediaType.PhotoAlbum, "year:asc", false)]
    [Arguments(PlexMediaType.Movie, "videoQuality:desc,year:asc", true)]
    [Arguments(PlexMediaType.OtherVideos, "videoQuality:asc,title:asc", true)]
    [Arguments(PlexMediaType.None, "title:asc", false)]
    [Arguments(PlexMediaType.None, "", true)]
    public void ShouldValidateParsedSortTypesAgainstMediaSupport(PlexMediaType mediaType, string sort, bool expected)
    {
        // Arrange
        // Act
        var valid = sort.IsSupportedSort(mediaType);

        // Assert
        valid.ShouldBe(expected);
    }

    [Test]
    [Arguments(SortDirection.Asc, "\"asc\"")]
    [Arguments(SortDirection.Desc, "\"desc\"")]
    [Arguments(SortDirection.NoSort, "\"no-sort\"")]
    public void ShouldUseFrontendDirectionNames_WhenSerializingAndDeserializingSortDirection(
        SortDirection direction,
        string json
    )
    {
        // Arrange

        // Act
        var serialized = JsonSerializer.Serialize(direction);
        var deserialized = JsonSerializer.Deserialize<SortDirection>(json);

        // Assert
        serialized.ShouldBe(json);
        deserialized.ShouldBe(direction);
    }
}
