using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoFolderChildrenQueryUnitTests : BaseCommandUnitTest<GetPhotoFolderChildrenQuery>
{
    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(3, 1)]
    [Arguments(3, 2)]
    public async Task ShouldReturnDirectTotalsInOneAggregate_WhenPagingFolderAssets(int assetCount, int page)
    {
        // Arrange
        await SetupDatabase(
            71022,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 3;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext
            .PlexPhotoFolders.Where(x => x.ParentId == null)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var assets = await dbContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == root.PlexLibraryId)
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
            assets[index].PlexPhotoFolderId = index < assetCount ? root.Id : null;
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
        var result = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(root.PlexLibraryId, root.Id, AssetPage: page, AssetPageSize: 2)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Folder.Id.ShouldBe(root.Id);
        result.Value.Folder.RecursiveAssetCount.ShouldBe(assetCount);
        result.Value.Folder.RecursiveAssetBytes.ShouldBe(Enumerable.Range(0, assetCount).Sum(index => index * 100L));
        result.Value.Assets.Page.ShouldBe(page);
        result.Value.Assets.PageSize.ShouldBe(2);
        result.Value.Assets.TotalCount.ShouldBe(assetCount);
        result.Value.Assets.TotalBytes.ShouldBe(Enumerable.Range(0, assetCount).Sum(index => index * 100L));
        result
            .Value.Assets.Items.Select(x => x.Id)
            .ShouldBe(expectedAssets.Skip((page - 1) * 2).Take(2).Select(x => x.Id));
        result.Value.Assets.Items.ShouldAllBe(x => x.PlexLibraryId == root.PlexLibraryId);
        result.Value.Folders.Items.ShouldBeEmpty();
        result.Value.Breadcrumbs.Select(x => x.Id).ShouldBe([root.Id]);
        var aggregateCommands = commands
            .Where(sql =>
                !sql.Contains("WITH RECURSIVE")
                && sql.Contains("\"PlexPhotoMediaData\"")
                && (sql.Contains("COUNT(") || sql.Contains("SUM("))
            )
            .ToList();
        aggregateCommands.Count.ShouldBe(1);
        aggregateCommands[0].ShouldContain("COUNT(");
        aggregateCommands[0].ShouldContain("SUM(");
    }

    [Test]
    public async Task ShouldPageDirectFoldersAndAssetsIndependentlyWithCompleteBreadcrumbs()
    {
        // Arrange
        await SetupDatabase(
            71004,
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
        var asset = await dbContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
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
            new GetPhotoFolderChildrenQuery(fixture.LibraryId, fixture.Root.Id, 1, 100, 1, 100, (SortType)999)
        );

        // Assert
        deepResult.IsSuccess.ShouldBeTrue();
        deepResult.Errors.Count.ShouldBe(0);
        deepResult.Value.Breadcrumbs.Select(x => x.Id).ShouldBe([fixture.Root.Id, .. fixture.Chain.Select(x => x.Id)]);
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
        descendingPage
            .Value.Folders.Items.Select(x => x.Id)
            .ShouldBe(expectedFolderIds.OrderByDescending(x => x).Take(100));
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

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task ShouldBatchCurrentAndVisibleChildTotals_WhenFolderPageExcludesOtherDescendants(int page)
    {
        // Arrange
        var fixture = await ArrangeBatchedTotalsFixtureAsync();
        var dbContext = IDbContext;
        var expectedChildren = new[]
        {
            (fixture.ChildIds[0], 1, 200L),
            (fixture.ChildIds[1], 1, 300L),
            (fixture.ChildIds[2], 1, 0L),
            (fixture.ChildIds[3], 0, 0L),
        };
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners
            .Where(listener => listener.Name == "Microsoft.EntityFrameworkCore")
            .Subscribe(listener => commandSubscriptions.Add(listener
                .Select(entry => entry.Value!).OfType<CommandExecutedEventData>()
                .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                .Subscribe(command => commands.Enqueue(command.Command.CommandText))));

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(fixture.Root.PlexLibraryId, fixture.Root.Id,
                FolderPage: page, FolderPageSize: 1, AssetPageSize: 1));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var response = result.Value;
        (response.Folder.Id, response.Folder.RecursiveAssetCount, response.Folder.RecursiveAssetBytes)
            .ShouldBe((fixture.Root.Id, 4, 600L));
        response.Breadcrumbs.Select(x => x.Id).ShouldBe([fixture.Root.Id]);
        (response.Folders.Page, response.Folders.PageSize, response.Folders.TotalCount).ShouldBe((page, 1, 4));
        response.Folders.Items.Select(x => (x.Id, x.RecursiveAssetCount, x.RecursiveAssetBytes))
            .ShouldBe(expectedChildren.Skip(page - 1).Take(1));
        response.Folders.Items.ShouldAllBe(x =>
            x.PlexLibraryId == fixture.Root.PlexLibraryId && x.PlexServerId == fixture.Root.PlexServerId);
        (response.Assets.TotalCount, response.Assets.TotalBytes).ShouldBe((1, 100L));
        response.Assets.Items.Select(x => x.Id).ShouldBe([fixture.DirectAssetId]);
        commands.Count(sql => sql.Contains("WITH RECURSIVE descendants AS")).ShouldBe(1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldRejectForeignFolderScope_WhenRequestedLibraryBelongsToSameOrAnotherServer(bool sameServer)
    {
        // Arrange
        var fixture = await ArrangeBatchedTotalsFixtureAsync();
        var foreignLibraryId = sameServer ? fixture.OtherLibraryId : fixture.OtherServerLibraryId;

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderChildrenDTO>(
            new GetPhotoFolderChildrenQuery(foreignLibraryId, fixture.Root.Id));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.Has404NotFoundError().ShouldBeTrue();
    }

    private async Task<BatchedTotalsFixture> ArrangeBatchedTotalsFixtureAsync()
    {
        await SetupDatabase(71025, config =>
        {
            config.PlexServerCount = 2;
            config.PlexPhotoLibraryCount = 2;
            config.PhotoAlbumCount = 1;
            config.PhotoCount = 4;
        });
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.Id).ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(4);
        var root = await dbContext.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == libraries[0].Id && x.ParentId == null, CancellationToken);
        var children = new[] { "A", "B", "C", "D" }.Select(title => CreateChildFolder(root, title, title)).ToList();
        dbContext.PlexPhotoFolders.AddRange(children);
        await dbContext.SaveChangesAsync(CancellationToken);
        var deep = CreateChildFolder(children[0], "A/Deep", "Deep");
        dbContext.PlexPhotoFolders.Add(deep);
        await dbContext.SaveChangesAsync(CancellationToken);
        var leaf = CreateChildFolder(deep, "A/Deep/Leaf", "Leaf");
        dbContext.PlexPhotoFolders.Add(leaf);
        await dbContext.SaveChangesAsync(CancellationToken);
        var assets = await dbContext.PlexPhotoMediaData.Where(x => x.PlexLibraryId == root.PlexLibraryId)
            .OrderBy(x => x.Id).ToListAsync(CancellationToken);
        assets.Count.ShouldBe(4);
        var folderIds = new[] { root.Id, leaf.Id, children[1].Id, children[2].Id };
        var sizes = new[] { 100L, 200L, 300L, 0L };
        for (var index = 0; index < assets.Count; index++)
            await dbContext.PlexPhotoMediaData.Where(x => x.Id == assets[index].Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.PlexPhotoFolderId, folderIds[index])
                    .SetProperty(x => x.Size, sizes[index]), CancellationToken);
        return new BatchedTotalsFixture(root, children.Select(x => x.Id).ToArray(), assets[0].Id,
            libraries.Single(x => x.PlexServerId == root.PlexServerId && x.Id != root.PlexLibraryId).Id,
            libraries.First(x => x.PlexServerId != root.PlexServerId).Id);
    }

    private static PlexPhotoFolder CreateChildFolder(PlexPhotoFolder parent, string path, string title) => new()
    {
        PlexServerId = parent.PlexServerId,
        PlexLibraryId = parent.PlexLibraryId,
        ParentId = parent.Id,
        RelativePath = path,
        Title = title,
    };

    private sealed record BatchedTotalsFixture(
        PlexPhotoFolder Root,
        int[] ChildIds,
        int DirectAssetId,
        int OtherLibraryId,
        int OtherServerLibraryId
    );

    private async Task<ScaledFolderFixture> ArrangeScaledFolderFixtureAsync()
    {
        await SetupDatabase(
            71011,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
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
        var siblings = Enumerable
            .Range(1, 120)
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
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == asset.Id)
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
