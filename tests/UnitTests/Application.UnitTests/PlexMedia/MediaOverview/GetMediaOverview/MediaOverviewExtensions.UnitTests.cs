using FlexQuery.NET.Models;

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
