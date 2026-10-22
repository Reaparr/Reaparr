using NaturalSort.Extension;
using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoAssetsQueryUnitTests : BaseCommandUnitTest<GetPhotoAssetsQuery>
{
    [Test]
    public async Task ShouldReturnUniqueAggregateAssetsIncludingUnmappedFiles()
    {
        // Arrange
        var fixture = await ArrangeUniqueAssetFixtureAsync();

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 100, SortType.Title)
        );
        var scopedResult = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(fixture.LibraryId, 1, 100, SortType.Title)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        scopedResult.IsSuccess.ShouldBeTrue();
        scopedResult.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(fixture.Assets.Count);
        result.Value.TotalBytes.ShouldBe(fixture.ExpectedBytes);
        result.Value.Items.Select(x => x.Id).ShouldBe(fixture.Assets.Select(x => x.Id));
        result.Value.Items.Select(x => x.Id).Distinct().Count().ShouldBe(result.Value.Items.Count);
        result.Value.Items.Single(x => x.Id == fixture.UnmappedAsset.Id).RelativeFolderPath.ShouldBeNull();
        result.Value.Items.Single(x => x.Id == fixture.UnmappedAsset.Id).SourceRootTitle.ShouldBeNull();
        result.Value.Items.Single(x => x.Id == fixture.InactiveAsset.Id).SourceRootTitle.ShouldBe(
            fixture.InactiveRoot.Title
        );
        scopedResult.Value.Items.Single(x => x.Id == fixture.InactiveAsset.Id).SourceRootTitle.ShouldBe(
            fixture.InactiveRoot.Title
        );
        result.Value.Items.Select(x => (x.PlexServerId, x.PlexLibraryId, x.Id)).Distinct().Count().ShouldBe(
            result.Value.Items.Count
        );
    }
 
    private async Task<UniqueAssetFixture> ArrangeUniqueAssetFixtureAsync()
    {
        await SetupDatabase(71002, config =>
        {
            config.PlexPhotoLibraryCount = 2;
            config.PhotoAlbumCount = 2;
            config.PhotoCount = 3;
            config.PhotoClipCount = 1;
        });
        var dbContext = IDbContext;
        var assets = await dbContext.PlexPhotoMediaData
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        assets.Count.ShouldBeGreaterThan(0);
        var library = await dbContext.PlexLibraries
            .Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var libraryAssets = assets.Where(x => x.PlexLibraryId == library.Id).ToList();
        libraryAssets.Count.ShouldBeGreaterThan(1);
        var unmappedAsset = libraryAssets[0];
        var inactiveAsset = libraryAssets[1];
        var inactiveRoot = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            RelativePath = string.Empty,
            Title = "Inactive root",
            RootIdentityKey = "path:inactive",
            SourceRootPath = "/inactive",
            PlexApiLocationId = 9001,
            IsActive = false,
        };
        dbContext.PlexPhotoFolders.Add(inactiveRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        var inactiveChild = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            ParentId = inactiveRoot.Id,
            RelativePath = "inactive-root/child",
            Title = "Inactive child",
        };
        dbContext.PlexPhotoFolders.Add(inactiveChild);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == inactiveAsset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, inactiveChild.Id), CancellationToken);
        inactiveAsset.PlexPhotoFolderId = inactiveChild.Id;
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == unmappedAsset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, (int?)null), CancellationToken);
        unmappedAsset.PlexPhotoFolderId = null;

        return new UniqueAssetFixture(
            library.Id,
            assets,
            assets.Sum(x => x.Size),
            unmappedAsset,
            inactiveAsset,
            inactiveRoot
        );
    }

    private sealed record UniqueAssetFixture(
        int LibraryId,
        IReadOnlyList<PlexPhotoMediaData> Assets,
        long ExpectedBytes,
        PlexPhotoMediaData UnmappedAsset,
        PlexPhotoMediaData InactiveAsset,
        PlexPhotoFolder InactiveRoot
    );

    [Test]
    public async Task ShouldSortNaturalTiesInBothDirectionsAndReturnEmptyBoundedPages()
    {
        // Arrange
        await SetupDatabase(71009, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 4;
        });
        var dbContext = IDbContext;
        await dbContext.PlexPhotoMediaData.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.Title, "Same title"),
            CancellationToken
        );
        var assets = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);

        // Act
        var ascending = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 1, SortType.Title)
        );
        var descending = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 1, SortType.Title, SortDirection.Desc)
        );
        var emptyPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 100, 2, SortType.Title)
        );
        var invalidSize = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, MediaQueryFilter.MaximumPageSize + 1, SortType.Title)
        );
        var overflowPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, int.MaxValue, MediaQueryFilter.MaximumPageSize, SortType.Title)
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 1, (SortType)999)
        );
        var invalidDirection = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 1, SortType.Title, (SortDirection)999)
        );

        // Assert
        ascending.IsSuccess.ShouldBeTrue();
        ascending.Errors.Count.ShouldBe(0);
        descending.IsSuccess.ShouldBeTrue();
        descending.Errors.Count.ShouldBe(0);
        ascending.Value.Items.Select(x => x.Id).ShouldBe([assets[0].Id]);
        descending.Value.Items.Select(x => x.Id).ShouldBe([assets[^1].Id]);
        emptyPage.IsSuccess.ShouldBeTrue();
        emptyPage.Errors.Count.ShouldBe(0);
        emptyPage.Value.Items.ShouldBeEmpty();
        emptyPage.Value.TotalCount.ShouldBe(assets.Count);
        invalidSize.Has400BadRequestError().ShouldBeTrue();
        overflowPage.Has400BadRequestError().ShouldBeTrue();
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
        invalidDirection.IsFailed.ShouldBeTrue();
        invalidDirection.Errors.Count.ShouldBe(2);
        invalidDirection.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    [Arguments(SortType.Title, SortDirection.Asc, new[] { 1, 3, 2, 0 })]
    [Arguments(SortType.Title, SortDirection.Desc, new[] { 0, 2, 3, 1 })]
    [Arguments(SortType.AddedAt, SortDirection.Asc, new[] { 1, 0, 3, 2 })]
    [Arguments(SortType.AddedAt, SortDirection.Desc, new[] { 2, 3, 0, 1 })]
    [Arguments(SortType.UpdatedAt, SortDirection.Asc, new[] { 0, 2, 1, 3 })]
    [Arguments(SortType.UpdatedAt, SortDirection.Desc, new[] { 3, 1, 2, 0 })]
    [Arguments(SortType.Duration, SortDirection.Asc, new[] { 0, 3, 1, 2 })]
    [Arguments(SortType.Duration, SortDirection.Desc, new[] { 2, 1, 3, 0 })]
    [Arguments(SortType.MediaSize, SortDirection.Asc, new[] { 2, 0, 3, 1 })]
    [Arguments(SortType.MediaSize, SortDirection.Desc, new[] { 1, 3, 0, 2 })]
    [Arguments(SortType.MediaSize, SortDirection.NoSort, new[] { 2, 0, 3, 1 })]
    public async Task ShouldApplySharedSortFieldsBeforePagingWithNullsAndTies(
        SortType sort,
        SortDirection direction,
        int[] expectedOrder
    )
    {
        // Arrange
        await SetupDatabase(71012, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 4;
        });
        var dbContext = IDbContext;
        var assets = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).Take(4).ToListAsync(CancellationToken);
        var date = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var values = new (int? Duration, long Size, DateTime AddedAt, DateTime? UpdatedAt)[]
        {
            (null, 200, date.AddDays(1), null),
            (10, 400, date, date.AddDays(1)),
            (10, 100, date.AddDays(3), date),
            (null, 300, date.AddDays(2), date.AddDays(2)),
        };
        var titles = new[] { "Zulu", "Alpha", "Charlie", "Bravo" };
        for (var index = 0; index < assets.Count; index++)
        {
            var value = values[index];
            await dbContext.PlexPhotoMediaData
                .Where(x => x.Id == assets[index].Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Title, titles[index])
                        .SetProperty(x => x.Duration, value.Duration)
                        .SetProperty(x => x.Size, value.Size)
                        .SetProperty(x => x.AddedAt, value.AddedAt)
                        .SetProperty(x => x.UpdatedAt, value.UpdatedAt),
                    CancellationToken
                );
        }

        // Act
        var firstPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 2, sort, direction)
        );
        var secondPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 2, 2, sort, direction)
        );

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Errors.Count.ShouldBe(0);
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Errors.Count.ShouldBe(0);
        firstPage.Value.TotalCount.ShouldBe(4);
        secondPage.Value.TotalCount.ShouldBe(4);
        firstPage.Value.Items.Select(x => x.Id).ShouldBe(expectedOrder.Take(2).Select(index => assets[index].Id));
        secondPage.Value.Items.Select(x => x.Id).ShouldBe(expectedOrder.Skip(2).Select(index => assets[index].Id));
    }

    [Test]
    public async Task ShouldExcludeDisabledServersFromAggregateAndRejectExplicitRequests()
    {
        // Arrange
        await SetupDatabase(71010, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 2;
        });
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum, CancellationToken);
        await dbContext.PlexServers
            .IgnoreIsEnabledFilter()
            .Where(x => x.Id == library.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        // Act
        var aggregate = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(new GetPhotoAssetsQuery(null));
        var explicitRequest = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(library.Id)
        );

        // Assert
        aggregate.IsSuccess.ShouldBeTrue();
        aggregate.Value.TotalCount.ShouldBe(0);
        aggregate.Value.TotalBytes.ShouldBe(0);
        explicitRequest.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnOnlyNullFolderAssets_WhenUnmappedOnlyIsScopedToLibrary()
    {
        // Arrange
        await SetupDatabase(71011, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 3;
        });
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var root = await dbContext.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var assets = await dbContext.PlexPhotoMediaData
            .Where(x => x.PlexLibraryId == library.Id)
            .AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        assets.Count.ShouldBe(3);
        assets[0].PlexPhotoFolderId = root.Id;
        assets[1].PlexPhotoFolderId = null;
        assets[2].PlexPhotoFolderId = null;
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedUnmapped = assets
            .Where(x => x.PlexPhotoFolderId is null)
            .OrderBy(x => x.Title, StringComparison.OrdinalIgnoreCase.WithNaturalSort())
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        // Act
        var scoped = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(library.Id, 1, 100, SortType.Title, SortDirection.Asc, true)
        );
        var aggregate = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 100, SortType.Title)
        );
        var invalidAggregate = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(null, 1, 100, SortType.Title, SortDirection.Asc, true)
        );

        // Assert
        scoped.IsSuccess.ShouldBeTrue();
        scoped.Errors.Count.ShouldBe(0);
        scoped.Value.Items.Select(x => x.Id).ShouldBe(expectedUnmapped);
        scoped.Value.TotalCount.ShouldBe(expectedUnmapped.Count);
        scoped.Value.TotalBytes.ShouldBe(assets.Where(x => x.PlexPhotoFolderId is null).Sum(x => x.Size));
        aggregate.IsSuccess.ShouldBeTrue();
        aggregate.Value.TotalCount.ShouldBe(assets.Count);
        aggregate.Value.Items.Select(x => x.Id).ShouldContain(assets[0].Id);
        invalidAggregate.IsFailed.ShouldBeTrue();
        invalidAggregate.Errors.Count.ShouldBe(2);
        invalidAggregate.Has400BadRequestError().ShouldBeTrue();
    }
}
