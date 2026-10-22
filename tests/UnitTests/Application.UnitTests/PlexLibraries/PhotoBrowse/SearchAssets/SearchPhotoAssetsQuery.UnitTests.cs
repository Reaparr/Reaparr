using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class SearchPhotoAssetsQueryUnitTests : BaseCommandUnitTest<SearchPhotoAssetsQuery>
{
    [Test]
    public async Task ShouldSearchAcrossAllEligibleLibrariesWithoutDuplicateRows()
    {
        // Arrange
        await SetupDatabase(71007, config =>
        {
            config.PlexPhotoLibraryCount = 2;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 1;
            config.PhotoClipCount = 1;
        });
        var dbContext = IDbContext;
        var assets = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        assets.Count.ShouldBeGreaterThan(1);
        var selectedIds = assets
            .GroupBy(x => x.PlexLibraryId)
            .Select(group => group.First().Id)
            .Take(2)
            .ToArray();
        var searchTitle = "cross-library-search".ToSearchTitle();
        await dbContext.PlexPhotoMediaData
            .Where(x => selectedIds.Contains(x.Id))
            .ExecuteUpdateAsync(
                x => x.SetProperty(y => y.SearchTitle, searchTitle).SetProperty(y => y.Title, "Cross library"),
                CancellationToken
            );
        var inactiveAsset = assets.Single(x => x.Id == selectedIds[0]);
        var inactiveRoot = new PlexPhotoFolder
        {
            PlexServerId = inactiveAsset.PlexServerId,
            PlexLibraryId = inactiveAsset.PlexLibraryId,
            RelativePath = string.Empty,
            Title = "Search inactive",
            RootIdentityKey = "path:search-inactive",
            SourceRootPath = "/search-inactive",
            PlexApiLocationId = 9101,
            IsActive = false,
        };
        dbContext.PlexPhotoFolders.Add(inactiveRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        var inactiveChild = new PlexPhotoFolder
        {
            PlexServerId = inactiveAsset.PlexServerId,
            PlexLibraryId = inactiveAsset.PlexLibraryId,
            ParentId = inactiveRoot.Id,
            RelativePath = "search-inactive/child",
            Title = "Search inactive child",
        };
        dbContext.PlexPhotoFolders.Add(inactiveChild);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == inactiveAsset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, inactiveChild.Id), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("cross-library-search", 1, 100, SortType.Title)
        );
        var descending = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("cross-library-search", 1, 100, SortType.Title, SortDirection.Desc)
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("cross-library-search", 1, 100, (SortType)999)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        descending.IsSuccess.ShouldBeTrue();
        descending.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(2);
        result.Value.Items.Select(x => x.Id).Distinct().Count().ShouldBe(2);
        result.Value.Items.Select(x => x.Id).ShouldBe(selectedIds.OrderBy(x => x));
        descending.Value.Items.Select(x => x.Id).ShouldBe(selectedIds.OrderByDescending(x => x));
        result.Value.Items.Select(x => x.PlexLibraryId).Distinct().Count().ShouldBeGreaterThan(1);
        result.Value.Items.Single(x => x.Id == inactiveAsset.Id).SourceRootTitle.ShouldBe(inactiveRoot.Title);
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldRejectUndefinedDirectionBeforeSearchingAssets()
    {
        // Arrange
        var query = new SearchPhotoAssetsQuery("photo", Direction: (SortDirection)999);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Has400BadRequestError().ShouldBeTrue();
    }
 
    [Test]
    public async Task ShouldNormalizeUnicodePunctuationAndRejectQueriesWithOnlyShortTerms()
    {
        // Arrange
        await SetupDatabase(71015, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 3;
        });
        var dbContext = IDbContext;
        var assets = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var matchingIds = assets.Take(2).Select(x => x.Id).ToArray();
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == matchingIds[0])
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "cafe resume"), CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == matchingIds[1])
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "cafe resume extra"), CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == assets[2].Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, "unrelated"), CancellationToken);

        // Act
        var normalized = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("Café, résumé!", 1, 100)
        );
        var accentTerm = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("résumé", 1, 100)
        );
        var shortTerm = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("a", 1, 100)
        );

        // Assert
        normalized.IsSuccess.ShouldBeTrue();
        normalized.Value.TotalCount.ShouldBe(2);
        normalized.Value.Items.Select(x => x.Id).ShouldBe(matchingIds.OrderBy(x => x));
        accentTerm.IsSuccess.ShouldBeTrue();
        accentTerm.Value.TotalCount.ShouldBe(2);
        shortTerm.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldPreserveSubstringAllTermAndTwoCharacterSearchSemantics()
    {
        // Arrange
        await SetupDatabase(71016, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 4;
        });
        var dbContext = IDbContext;
        var assets = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var searchTitles = new[] { "alpha beta gamma", "alphabet betamax", "alpha only", "cafe resume" };
        for (var index = 0; index < searchTitles.Length; index++)
        {
            var assetId = assets[index].Id;
            var searchTitle = searchTitles[index];
            await dbContext.PlexPhotoMediaData
                .Where(x => x.Id == assetId)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.SearchTitle, searchTitle), CancellationToken);
        }

        // Act
        var substring = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("pha", 1, 100)
        );
        var allTerms = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("alpha beta", 1, 100)
        );
        var twoCharacter = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("ha", 1, 100)
        );
        var punctuation = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new SearchPhotoAssetsQuery("Café, résumé!", 1, 100)
        );

        // Assert
        substring.IsSuccess.ShouldBeTrue();
        substring.Errors.Count.ShouldBe(0);
        substring.Value.TotalCount.ShouldBe(3);
        substring.Value.TotalBytes.ShouldBe(assets.Take(3).Sum(x => x.Size));
        substring.Value.Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(assets.Take(3).Select(x => x.Id).OrderBy(x => x));

        allTerms.IsSuccess.ShouldBeTrue();
        allTerms.Errors.Count.ShouldBe(0);
        allTerms.Value.TotalCount.ShouldBe(2);
        allTerms.Value.Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(assets.Take(2).Select(x => x.Id).OrderBy(x => x));

        twoCharacter.IsSuccess.ShouldBeTrue();
        twoCharacter.Errors.Count.ShouldBe(0);
        twoCharacter.Value.TotalCount.ShouldBe(3);
        twoCharacter.Value.Items.Select(x => x.Id).OrderBy(x => x).ShouldBe(assets.Take(3).Select(x => x.Id).OrderBy(x => x));

        punctuation.IsSuccess.ShouldBeTrue();
        punctuation.Errors.Count.ShouldBe(0);
        punctuation.Value.TotalCount.ShouldBe(1);
        punctuation.Value.Items.Select(x => x.Id).ShouldBe([assets[3].Id]);
    }
}
