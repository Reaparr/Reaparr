using FlexQuery.NET.Models;

namespace Reaparr.Data.UnitTests;

public class MediaQueryFilterUnitTests : BaseUnitTest<MediaQueryFilterUnitTests>
{
    [Test]
    public void ShouldNotChangeRequestHash_WhenPageChanges()
    {
        // Arrange
        var original = CreateFilter(page: 1, pageSize: 25, sort: "Year:desc", filter: "Year:gte:2000");
        var updated = CreateFilter(page: 2, pageSize: 25, sort: "Year:desc", filter: "Year:gte:2000");

        // Act
        var originalHash = original.QueryHash;
        var updatedHash = updated.QueryHash;

        // Assert
        originalHash.ShouldBe(updatedHash);
    }

    [Test]
    public void ShouldKeepRequestHashSame_WhenQueryParametersDoNotChange()
    {
        // Arrange
        var original = CreateFilter(page: 1, pageSize: 25, sort: "Year:desc", filter: "Year:gte:2000");
        var duplicate = CreateFilter(page: 1, pageSize: 25, sort: "Year:desc", filter: "Year:gte:2000");

        // Act
        var originalHash = original.QueryHash;
        var duplicateHash = duplicate.QueryHash;

        // Assert
        originalHash.ShouldBe(duplicateHash);
    }

    [Test]
    public void ShouldHaveDifferentHash_WhenSortingChanged()
    {
        // Arrange
        var original = CreateFilter(page: 1, pageSize: 25, sort: "Year:desc", filter: "Year:gte:2000");
        var duplicate = CreateFilter(page: 1, pageSize: 25, sort: "Year:asc", filter: "Year:gte:2000");

        // Act
        var originalHash = original.QueryHash;
        var duplicateHash = duplicate.QueryHash;

        // Assert
        originalHash.ShouldNotBe(duplicateHash);
    }

    [Test]
    public void ShouldHaveDifferentHash_WhenIncludeChanges()
    {
        var withoutInclude = CreateFilter(include: null);
        var withInclude = CreateFilter(include: "MediaDataList");

        withoutInclude.QueryHash.ShouldNotBe(withInclude.QueryHash);
    }

    private static MediaQueryFilter CreateFilter(
        int? page = null,
        int? pageSize = null,
        string? sort = null,
        string? filter = null,
        string? include = null
    ) =>
        new()
        {
            MediaType = PlexMediaType.Movie,
            PlexLibraryId = 7,
            FilterOfflineMedia = true,
            FilterOwnedMedia = false,
            Parameters = new FlexQueryParameters
            {
                Page = page,
                PageSize = pageSize,
                Sort = sort,
                Filter = filter,
                Include = include,
            },
        };
}
