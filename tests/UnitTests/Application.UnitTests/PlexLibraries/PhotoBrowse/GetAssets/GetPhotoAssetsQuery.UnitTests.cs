using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaturalSort.Extension;
using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoAssetsQueryUnitTests : BaseCommandUnitTest<GetPhotoAssetsQuery>
{
    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(3, 1)]
    [Arguments(3, 2)]
    public async Task ShouldReturnScopedTotalsInOneAggregate_WhenPagingUnmappedAssets(int assetCount, int page)
    {
        // Arrange
        await SetupDatabase(
            71020,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 3;
            }
        );
        var dbContext = IDbContext;
        var libraryId = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        var assets = await dbContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == libraryId)
            .AsTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        assets.Count.ShouldBe(3);
        for (var index = 0; index < assets.Count; index++)
        {
            assets[index].Title = "Same title";
            await dbContext
                .PlexPhotoMediaData.Where(x => x.Id == assets[index].Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Size, index * 100L), CancellationToken);
            if (index < assetCount)
                assets[index].PlexPhotoFolderId = null;
        }
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedAssets = assets.Take(assetCount).ToList();
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener
            .AllListeners.Where(listener => listener.Name == "Microsoft.EntityFrameworkCore")
            .Subscribe(listener =>
                commandSubscriptions.Add(
                    listener
                        .Select(entry => entry.Value!)
                        .OfType<CommandExecutedEventData>()
                        .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                        .Subscribe(command => commands.Enqueue(command.Command.CommandText))
                )
            );

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(libraryId, page, 2, UnmappedOnly: true)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Page.ShouldBe(page);
        result.Value.PageSize.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(assetCount);
        result.Value.TotalBytes.ShouldBe(Enumerable.Range(0, assetCount).Sum(index => index * 100L));
        result.Value.Items.Select(x => x.Id).ShouldBe(expectedAssets.Skip((page - 1) * 2).Take(2).Select(x => x.Id));
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == libraryId && x.RelativeFolderPath == null);
        var aggregateCommands = commands
            .Where(sql => sql.Contains("\"PlexPhotoMediaData\"") && (sql.Contains("COUNT(") || sql.Contains("SUM(")))
            .ToList();
        aggregateCommands.Count.ShouldBe(1);
        aggregateCommands[0].ShouldContain("COUNT(");
        aggregateCommands[0].ShouldContain("SUM(");
    }

    [Test]
    public async Task ShouldReturnUniqueAggregateAssetsIncludingUnmappedFiles()
    {
        // Arrange
        var fixture = await ArrangeUniqueAssetFixtureAsync();
        var dbContext = IDbContext;
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener
            .AllListeners.Where(listener => listener.Name == "Microsoft.EntityFrameworkCore")
            .Subscribe(listener =>
                commandSubscriptions.Add(
                    listener
                        .Select(entry => entry.Value!)
                        .OfType<CommandExecutedEventData>()
                        .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                        .Subscribe(command => commands.Enqueue(command.Command.CommandText))
                )
            );

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
        var libraryAssets = fixture.Assets.Where(x => x.PlexLibraryId == fixture.LibraryId).ToList();
        scopedResult.Value.TotalCount.ShouldBe(libraryAssets.Count);
        scopedResult.Value.TotalBytes.ShouldBe(libraryAssets.Sum(x => x.Size));
        result.Value.Items.Select(x => x.Id).ShouldBe(fixture.Assets.Select(x => x.Id));
        result.Value.Items.Select(x => x.Id).Distinct().Count().ShouldBe(result.Value.Items.Count);
        result.Value.Items.Single(x => x.Id == fixture.UnmappedAsset.Id).RelativeFolderPath.ShouldBeNull();
        result.Value.Items.Single(x => x.Id == fixture.UnmappedAsset.Id).SourceRootTitle.ShouldBeNull();
        result
            .Value.Items.Single(x => x.Id == fixture.InactiveAsset.Id)
            .SourceRootTitle.ShouldBe(fixture.InactiveRoot.Title);
        scopedResult
            .Value.Items.Single(x => x.Id == fixture.InactiveAsset.Id)
            .SourceRootTitle.ShouldBe(fixture.InactiveRoot.Title);
        result
            .Value.Items.Select(x => (x.PlexServerId, x.PlexLibraryId, x.Id))
            .Distinct()
            .Count()
            .ShouldBe(result.Value.Items.Count);
        commands
            .Where(sql => sql.Contains("\"PlexPhotoMediaData\""))
            .ShouldAllBe(sql => !sql.Contains("COUNT(") && !sql.Contains("SUM("));
        commands.Count(sql => sql.Contains("\"PlexLibraries\"") && sql.Contains("SUM(")).ShouldBe(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldUseOnlyEligibleLibraryMetrics_WhenOtherLibrariesAreDisabledOrUnlinked(bool scoped)
    {
        // Arrange
        var (libraryId, expected) = await ArrangeEligibleLibraryMetricsAsync();
        expected.Count.ShouldBe(3);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAssetsQuery(scoped ? libraryId : null, 2, 2)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Page.ShouldBe(2);
        result.Value.PageSize.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(3);
        result.Value.TotalBytes.ShouldBe(expected.Sum(x => x.Size));
        result.Value.Items.Select(x => x.Id).ShouldBe(expected.Skip(2).Select(x => x.Id));
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(2, 0)]
    [Arguments(1, 1)]
    public async Task ShouldReturnStoredCountsAndZeroBytes_WhenLibraryIsEmptyOrHasZeroByteAssets(
        int imageCount,
        int clipCount
    )
    {
        // Arrange
        var (libraryId, expectedIds) = await ArrangeZeroByteLibraryAsync(imageCount, clipCount);
        expectedIds.Count.ShouldBe(imageCount + clipCount);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(new GetPhotoAssetsQuery(libraryId));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(imageCount + clipCount);
        result.Value.TotalBytes.ShouldBe(0L);
        result.Value.Items.Select(x => x.Id).ShouldBe(expectedIds);
    }

    private async Task<(int LibraryId, List<PlexPhotoMediaData> Assets)> ArrangeEligibleLibraryMetricsAsync()
    {
        await SetupDatabase(
            71023,
            config =>
            {
                config.PlexAccountCount = 1;
                config.PlexPhotoLibraryCount = 3;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 2;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(3);
        await dbContext
            .PlexLibraries.Where(x => x.Id == libraries[1].Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsEnabled, false), CancellationToken);
        var removedLinks = await dbContext
            .PlexAccountLibraries.Where(x => x.PlexLibraryId == libraries[2].Id)
            .ExecuteDeleteAsync(CancellationToken);
        removedLinks.ShouldBe(1);
        var expected = await dbContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == libraries[0].Id)
            .OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        return (libraries[0].Id, expected);
    }

    private async Task<(int LibraryId, List<int> AssetIds)> ArrangeZeroByteLibraryAsync(int imageCount, int clipCount)
    {
        await SetupDatabase(
            71024,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = imageCount;
                config.PhotoClipCount = clipCount;
            }
        );
        var dbContext = IDbContext;
        var libraryId = await dbContext.PlexLibraries.Select(x => x.Id).SingleAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData.ExecuteUpdateAsync(
            setters => setters.SetProperty(x => x.Size, 0L),
            CancellationToken
        );
        await dbContext.SetPhotoMediaMetrics(libraryId, 1, imageCount, clipCount, 0L, CancellationToken);
        var expectedIds = await dbContext
            .PlexPhotoMediaData.OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);
        return (libraryId, expectedIds);
    }

    private async Task<UniqueAssetFixture> ArrangeUniqueAssetFixtureAsync()
    {
        await SetupDatabase(
            71002,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 3;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var assets = await dbContext
            .PlexPhotoMediaData.OrderBy(x => x.Title)
            .ThenBy(x => x.Id)
            .ToListAsync(CancellationToken);
        assets.Count.ShouldBeGreaterThan(0);
        var library = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
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
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == inactiveAsset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, inactiveChild.Id), CancellationToken);
        inactiveAsset.PlexPhotoFolderId = inactiveChild.Id;
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == unmappedAsset.Id)
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
        await SetupDatabase(
            71009,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 4;
            }
        );
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
        await SetupDatabase(
            71012,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 4;
            }
        );
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
            await dbContext
                .PlexPhotoMediaData.Where(x => x.Id == assets[index].Id)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(x => x.Title, titles[index])
                            .SetProperty(x => x.Duration, value.Duration)
                            .SetProperty(x => x.Size, value.Size)
                            .SetProperty(x => x.AddedAt, value.AddedAt)
                            .SetProperty(x => x.UpdatedAt, value.UpdatedAt),
                    CancellationToken
                );
        }
        await dbContext.SetPhotoMediaMetrics(
            assets[0].PlexLibraryId,
            1,
            assets.Count,
            0,
            values.Sum(x => x.Size),
            CancellationToken
        );

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
        firstPage.Value.TotalBytes.ShouldBe(values.Sum(x => x.Size));
        secondPage.Value.TotalBytes.ShouldBe(values.Sum(x => x.Size));
        firstPage.Value.Items.Select(x => x.Id).ShouldBe(expectedOrder.Take(2).Select(index => assets[index].Id));
        secondPage.Value.Items.Select(x => x.Id).ShouldBe(expectedOrder.Skip(2).Select(index => assets[index].Id));
    }

    [Test]
    public async Task ShouldExcludeDisabledServersFromAggregateAndRejectExplicitRequests()
    {
        // Arrange
        await SetupDatabase(
            71010,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        await dbContext
            .PlexServers.IgnoreIsEnabledFilter()
            .Where(x => x.Id == library.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsEnabled, false), CancellationToken);

        // Act
        var aggregate = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(new GetPhotoAssetsQuery(null));
        var explicitRequest = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(new GetPhotoAssetsQuery(library.Id));

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
        await SetupDatabase(
            71011,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 3;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var root = await dbContext.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var assets = await dbContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == library.Id)
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
