using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoFolderChildrenQueryUnitTests : BaseCommandUnitTest<GetPhotoFolderChildrenQuery>
{
    [Test]
    public async Task ShouldPageDirectFoldersAndAssetsIndependentlyWithCompleteBreadcrumbs()
    {
        // Arrange
        await SetupDatabase(71004, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 2;
        });
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum, CancellationToken);
        var root = await dbContext.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var child = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "nested",
            Title = "Nested",
            RootIdentityKey = null,
            SourceRootPath = null,
            PlexApiLocationId = null,
            IsActive = null,
        };
        dbContext.PlexPhotoFolders.Add(child);
        await dbContext.SaveChangesAsync(CancellationToken);
        var asset = await dbContext.PlexPhotoMediaData
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, child.Id), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(library.Id, root.Id, 1, 1, 1, 1, SortType.Title)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Folder.Id.ShouldBe(root.Id);
        result.Value.Folders.TotalCount.ShouldBe(1);
        result.Value.Folders.Items.Select(x => x.Id).ShouldBe([child.Id]);
        result.Value.Assets.Page.ShouldBe(1);
        result.Value.Assets.PageSize.ShouldBe(1);
        result.Value.Assets.TotalCount.ShouldBe(
            await dbContext.PlexPhotoMediaData.CountAsync(x => x.PlexPhotoFolderId == root.Id, CancellationToken)
        );
        result.Value.Breadcrumbs.Select(x => x.Id).ShouldBe([root.Id]);
        result.Value.Folders.Items[0].RecursiveAssetCount.ShouldBe(1);
        result.Value.Folders.Items[0].RecursiveAssetBytes.ShouldBe(asset.Size);

        // Act on the child page to prove direct assets and ancestry are separate from recursive totals.
        var childResult = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(library.Id, child.Id, 1, 1, 1, 1, SortType.Title)
        );

        // Assert
        childResult.IsSuccess.ShouldBeTrue();
        childResult.Errors.Count.ShouldBe(0);
        childResult.Value.Breadcrumbs.Select(x => x.Id).ShouldBe([root.Id, child.Id]);
        childResult.Value.Folders.TotalCount.ShouldBe(0);
        childResult.Value.Assets.TotalCount.ShouldBe(1);
        childResult.Value.Assets.TotalBytes.ShouldBe(asset.Size);
        childResult.Value.Assets.Items.Select(x => x.Id).ShouldBe([asset.Id]);
    }
 
    [Test]
    public async Task ShouldBoundFolderPagingAndResolveDeepAncestryAtScale()
    {
        // Arrange
        var fixture = await ArrangeScaledFolderFixtureAsync();
        var expectedFolderIds = await IDbContext
            .PlexPhotoFolders.Where(x => x.ParentId == fixture.Root.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        // Act
        var deepResult = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(fixture.LibraryId, fixture.Chain[^1].Id, 1, 100, 1, 100)
        );
        var firstPage = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(fixture.LibraryId, fixture.Root.Id, 1, 100, 1, 100)
        );
        var secondPage = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(fixture.LibraryId, fixture.Root.Id, 2, 100, 1, 100)
        );
        var descendingPage = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(
                fixture.LibraryId,
                fixture.Root.Id,
                1,
                100,
                1,
                100,
                SortType.Title,
                SortDirection.Desc
            )
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(
                fixture.LibraryId,
                fixture.Root.Id,
                1,
                100,
                1,
                100,
                (SortType)999
            )
        );

        // Assert
        deepResult.IsSuccess.ShouldBeTrue();
        deepResult.Errors.Count.ShouldBe(0);
        deepResult.Value.Breadcrumbs.Select(x => x.Id).ShouldBe(
            [fixture.Root.Id, .. fixture.Chain.Select(x => x.Id)]
        );
        deepResult.Value.Folder.RecursiveAssetCount.ShouldBe(1);
        deepResult.Value.Folder.RecursiveAssetBytes.ShouldBe(fixture.Asset.Size);
        deepResult.Value.Assets.TotalCount.ShouldBe(1);
        deepResult.Value.Assets.Items.Select(x => x.Id).ShouldBe([fixture.Asset.Id]);
        firstPage.IsSuccess.ShouldBeTrue();
        firstPage.Errors.Count.ShouldBe(0);
        firstPage.Value.Folders.TotalCount.ShouldBe(121);
        firstPage.Value.Folders.Items.Count.ShouldBe(100);
        firstPage.Value.Folders.Items.Select(x => x.Id).ShouldBe(expectedFolderIds.Take(100));
        secondPage.IsSuccess.ShouldBeTrue();
        secondPage.Errors.Count.ShouldBe(0);
        secondPage.Value.Folders.TotalCount.ShouldBe(121);
        secondPage.Value.Folders.Items.Count.ShouldBe(21);
        descendingPage.IsSuccess.ShouldBeTrue();
        descendingPage.Errors.Count.ShouldBe(0);
        descendingPage.Value.Folders.Items.Select(x => x.Id).ShouldBe(expectedFolderIds.OrderByDescending(x => x).Take(100));
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldRejectUndefinedDirectionBeforeLoadingFolderChildren()
    {
        // Arrange
        var query = new GetPhotoFolderChildrenQuery(1, 1, Direction: (SortDirection)999);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Has400BadRequestError().ShouldBeTrue();
    }

    private async Task<ScaledFolderFixture> ArrangeScaledFolderFixtureAsync()
    {
        await SetupDatabase(71011, config =>
        {
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 1;
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

        var chain = new List<PlexPhotoFolder>();
        var parentId = root.Id;
        for (var depth = 1; depth <= 3; depth++)
        {
            var folder = new PlexPhotoFolder
            {
                PlexServerId = root.PlexServerId,
                PlexLibraryId = root.PlexLibraryId,
                ParentId = parentId,
                RelativePath = $"level-{depth}",
                Title = "Sibling",
                RootIdentityKey = null,
                SourceRootPath = null,
                PlexApiLocationId = null,
                IsActive = null,
            };
            dbContext.PlexPhotoFolders.Add(folder);
            await dbContext.SaveChangesAsync(CancellationToken);
            chain.Add(folder);
            parentId = folder.Id;
        }
        var siblings = Enumerable.Range(1, 120)
            .Select(index => new PlexPhotoFolder
            {
                PlexServerId = root.PlexServerId,
                PlexLibraryId = root.PlexLibraryId,
                ParentId = root.Id,
                RelativePath = $"sibling-{index}",
                Title = "Sibling",
                RootIdentityKey = null,
                SourceRootPath = null,
                PlexApiLocationId = null,
                IsActive = null,
            })
            .ToList();
        dbContext.PlexPhotoFolders.AddRange(siblings);
        await dbContext.SaveChangesAsync(CancellationToken);
        var asset = await dbContext.PlexPhotoMediaData.FirstAsync(CancellationToken);
        await dbContext.PlexPhotoMediaData
            .Where(x => x.Id == asset.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, chain[^1].Id), CancellationToken);

        return new ScaledFolderFixture(library.Id, root, chain, asset);
    }

    private sealed record ScaledFolderFixture(
        int LibraryId,
        PlexPhotoFolder Root,
        IReadOnlyList<PlexPhotoFolder> Chain,
        PlexPhotoMediaData Asset
    );
}
