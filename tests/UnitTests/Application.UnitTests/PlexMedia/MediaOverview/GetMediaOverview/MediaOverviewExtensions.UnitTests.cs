using FlexQuery.NET.Models;
using FlexQuery.NET.Parsers;
using SortDirection = Reaparr.Domain.SortDirection;

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
        result.Value.ShouldBe(new Sort(SortType.Title, SortDirection.Asc));
    }

    [Test]
    [Arguments("sortIndex", SortType.Title)]
    [Arguments(" searchTitle ", SortType.Title)]
    [Arguments("title", SortType.Title)]
    public void ShouldResolveCanonicalAndInternalTitleNames_WhenOneSortIsRequested(string field, SortType expectedField)
    {
        // Arrange
        var options = new QueryOptions();
        options.Sort.Add(new SortNode { Field = field, Descending = true });

        // Act
        var result = options.ResolveSort();

        result.ShouldNotBeNull();
        result.Value.ShouldBe(new Sort(expectedField, SortDirection.Desc));
    }

    [Test]
    public void ShouldThrow_WhenSortFieldIsUnsupported()
    {
        // Arrange
        var options = new QueryOptions();
        options.Sort.Add(new SortNode { Field = "unsupported", Descending = false });

        // Act
        Action act = () => options.ResolveSort();

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("value");
    }

    [Test]
    [Arguments("sortIndex:asc", SortType.Title, SortDirection.Asc)]
    [Arguments("addedAt:desc", SortType.AddedAt, SortDirection.Desc)]
    [Arguments("updatedAt:asc", SortType.UpdatedAt, SortDirection.Asc)]
    [Arguments("duration:desc", SortType.Duration, SortDirection.Desc)]
    [Arguments("mediaSize:asc", SortType.MediaSize, SortDirection.Asc)]
    public void ShouldResolveCanonicalWireSort_WhenParsedThroughSharedOptions(
        string value,
        SortType expectedField,
        SortDirection expectedDirection
    )
    {
        // Arrange
        var options = QueryOptionsParser.Parse(new FlexQueryParameters { Sort = value });

        // Act
        var result = options.ResolveSort();

        // Assert
        result.ShouldBe(new Sort(expectedField, expectedDirection));
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
