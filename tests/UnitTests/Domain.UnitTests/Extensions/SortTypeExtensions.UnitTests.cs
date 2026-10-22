using System.Text.Json;

namespace Reaparr.Domain.UnitTests;

public class SortTypeExtensionsUnitTests
{
    [Test]
    [Arguments("sortIndex", SortType.Title)]
    [Arguments("title", SortType.Title)]
    [Arguments("SortTitle", SortType.Title)]
    [Arguments("searchTitle", SortType.Title)]
    [Arguments(" YEAR ", SortType.Year)]
    [Arguments("addedAt", SortType.AddedAt)]
    [Arguments("updatedAt", SortType.UpdatedAt)]
    [Arguments("duration", SortType.Duration)]
    [Arguments("mediaSize", SortType.MediaSize)]
    [Arguments("videoQuality", SortType.VideoQuality)]
    [Arguments("quality", SortType.VideoQuality)]
    [Arguments("comparisonState", SortType.ComparisonState)]
    public void ShouldResolveNamedSortAndLegacyAliases_WhenParsingQueryField(string field, SortType expected)
    {
        // Arrange
        // Act
        var accepted = SortTypeExtensions.TryParse(field, out var actual);

        // Assert
        accepted.ShouldBeTrue();
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
    public void ShouldRejectNonFieldInput_WhenParsingQueryField(string? field)
    {
        // Arrange
        // Act
        var accepted = SortTypeExtensions.TryParse(field, out var actual);

        // Assert
        accepted.ShouldBeFalse();
        actual.ShouldBe(SortType.Title);
    }

    [Test]
    [Arguments(SortType.Title, "\"Title\"")]
    [Arguments(SortType.Year, "\"Year\"")]
    [Arguments(SortType.AddedAt, "\"AddedAt\"")]
    [Arguments(SortType.UpdatedAt, "\"UpdatedAt\"")]
    [Arguments(SortType.Duration, "\"Duration\"")]
    [Arguments(SortType.MediaSize, "\"MediaSize\"")]
    [Arguments(SortType.VideoQuality, "\"VideoQuality\"")]
    [Arguments(SortType.ComparisonState, "\"ComparisonState\"")]
    public void ShouldSerializeEverySortTypeAsItsNamedJsonValue_WhenWritingJson(
        SortType value,
        string expectedJson
    )
    {
        // Arrange

        // Act
        var json = JsonSerializer.Serialize(value);

        // Assert
        json.ShouldBe(expectedJson);
    }

    [Test]
    [Arguments("\"Title\"", SortType.Title)]
    [Arguments("\"Year\"", SortType.Year)]
    [Arguments("\"AddedAt\"", SortType.AddedAt)]
    [Arguments("\"UpdatedAt\"", SortType.UpdatedAt)]
    [Arguments("\"Duration\"", SortType.Duration)]
    [Arguments("\"MediaSize\"", SortType.MediaSize)]
    [Arguments("\"VideoQuality\"", SortType.VideoQuality)]
    [Arguments("\"ComparisonState\"", SortType.ComparisonState)]
    public void ShouldDeserializeEveryNamedSortTypeJsonValue_WhenReadingJson(
        string json,
        SortType expected
    )
    {
        // Arrange

        // Act
        var value = JsonSerializer.Deserialize<SortType>(json);

        // Assert
        value.ShouldBe(expected);
    }

    [Test]
    [Arguments(SortType.Title, "SortIndex")]
    [Arguments(SortType.Year, "Year")]
    [Arguments(SortType.AddedAt, "AddedAt")]
    [Arguments(SortType.UpdatedAt, "UpdatedAt")]
    [Arguments(SortType.Duration, "Duration")]
    [Arguments(SortType.MediaSize, "MediaSize")]
    [Arguments(SortType.VideoQuality, "VideoQuality")]
    [Arguments(SortType.ComparisonState, "ComparisonState")]
    public void ShouldResolveCanonicalQueryProperty_WhenSortTypeIsSupported(SortType sortType, string expected)
    {
        // Arrange
        // Act
        var field = sortType.ToQueryField();

        // Assert
        field.ShouldBe(expected);
    }

    [Test]
    public void ShouldRejectUndefinedSortType_WhenResolvingQueryProperty()
    {
        // Arrange
        var undefined = (SortType)99;

        // Act
        var act = () => undefined.ToQueryField();

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("sortType");
    }
}
