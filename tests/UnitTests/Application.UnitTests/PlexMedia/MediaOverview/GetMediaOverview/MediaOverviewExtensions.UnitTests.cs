using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application.UnitTests;

public class MediaOverviewExtensionsUnitTests
{
    [Test]
    public void ShouldDefaultToTitleAscending_WhenSortIsEmpty()
    {
        // Arrange
        var options = new QueryOptions();

        // Act
        var result = options.ResolveSort();

        result.ShouldNotBeNull();
        result.Value.ShouldBe((SortType.Title, false));
    }

    [Test]
    [Arguments("sortIndex", SortType.Title)]
    [Arguments(" searchTitle ", SortType.Title)]
    [Arguments("quality", SortType.VideoQuality)]
    public void ShouldResolveLegacySortAlias_WhenOneSortIsRequested(string field, SortType expectedField)
    {
        // Arrange
        var options = new QueryOptions();
        options.Sort.Add(new SortNode { Field = field, Descending = true });

        // Act
        var result = options.ResolveSort();

        result.ShouldNotBeNull();
        result.Value.ShouldBe((expectedField, true));
    }

    [Test]
    public void ShouldReturnNull_WhenSortFieldIsUnsupported()
    {
        // Arrange
        var options = new QueryOptions();
        options.Sort.Add(new SortNode { Field = "unsupported", Descending = false });

        // Act
        var result = options.ResolveSort();

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    [Arguments("sortIndex:asc", SortType.Title, false)]
    [Arguments("addedAt:desc", SortType.AddedAt, true)]
    [Arguments("updatedAt:asc", SortType.UpdatedAt, false)]
    [Arguments("duration:desc", SortType.Duration, true)]
    [Arguments("mediaSize:asc", SortType.MediaSize, false)]
    public void ShouldResolveCanonicalWireSort_WhenParsedThroughSharedOptions(
        string value,
        SortType expectedField,
        bool expectedDescending
    )
    {
        // Arrange
        var options = QueryOptionsParser.Parse(new FlexQueryParameters { Sort = value });

        // Act
        var result = options.ResolveSort();

        // Assert
        result.ShouldBe((expectedField, expectedDescending));
    }

    [Test]
    public void ShouldRejectUnsupportedSortValuesAtTheSharedBoundary()
    {
        // Arrange

        // Act
        var unknown = "unknown:asc".IsSupportedSort(SortType.Title, SortType.AddedAt);
        var unsupported = "year:asc".IsSupportedSort(SortType.Title, SortType.AddedAt);
        var multiple = "title:asc,addedAt:desc".IsSupportedSort(SortType.Title, SortType.AddedAt);

        // Assert
        unknown.ShouldBeFalse();
        unsupported.ShouldBeFalse();
        multiple.ShouldBeFalse();
    }


    [Test]
    public void ShouldReturnNull_WhenRecognizedSortTypeHasNoOverviewRank()
    {
        // Arrange
        var options = new QueryOptions();
        options.Sort.Add(
            new SortNode
            {
                Field = nameof(BasePlexMedia.ComparisonState),
                Descending = true,
            }
        );

        // Act
        var result = options.ResolveSort();

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public void ShouldReturnNull_WhenMultipleSortsAreRequested()
    {
        // Arrange
        var options = new QueryOptions();
        options.Sort.Add(new SortNode { Field = "sortIndex", Descending = false });
        options.Sort.Add(new SortNode { Field = "year", Descending = true });

        // Act
        var result = options.ResolveSort();

        // Assert
        result.ShouldBeNull();
    }
}
