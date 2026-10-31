using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoSourceRootsQueryUnitTests : BaseCommandUnitTest<GetPhotoSourceRootsQueryCommand>
{
    [Test]
    public async Task ShouldListOnlyActiveRootsAndKeepRecursiveTotalsOnTheFolderItems()
    {
        // Arrange
        await SetupDatabase(
            71003,
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
        root.IsActive.ShouldBe(true);
        var inactiveRoot = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = null,
            RelativePath = string.Empty,
            Title = "Inactive root",
            RootIdentityKey = "path:posix:/inactive-root",
            SourceRootPath = "/inactive-root",
            PlexApiLocationId = null,
            IsActive = false,
        };
        dbContext.PlexPhotoFolders.Add(inactiveRoot);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(new GetPhotoSourceRootsQueryCommand(library.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(1);
        result.Value.Items.Select(x => x.Id).ShouldBe([root.Id]);
        result.Value.Items[0].IsRoot.ShouldBeTrue();
        result.Value.Items[0].IsActive.ShouldBe(true);
        result
            .Value.Items[0]
            .RecursiveAssetCount.ShouldBe(
                await dbContext.PlexPhotoMediaData.CountAsync(x => x.PlexPhotoFolderId == root.Id, CancellationToken)
            );
        result
            .Value.Items[0]
            .RecursiveAssetBytes.ShouldBe(
                await dbContext
                    .PlexPhotoMediaData.Where(x => x.PlexPhotoFolderId == root.Id)
                    .SumAsync(x => x.Size, CancellationToken)
            );
    }

    [Test]
    public async Task ShouldReturnAValidEmptyPageWhenNoActiveRootsRemain()
    {
        // Arrange
        await SetupDatabase(
            71012,
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
        await dbContext
            .PlexPhotoFolders.Where(x => x.PlexLibraryId == library.Id && x.ParentId == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsActive, false), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(new GetPhotoSourceRootsQueryCommand(library.Id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Items.ShouldBeEmpty();
        result.Value.TotalCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldBoundLargeRootPagesAndBatchRecursiveTotals()
    {
        // Arrange
        await SetupDatabase(
            71016,
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
        var serverId = library.PlexServerId;
        await dbContext
            .PlexPhotoFolders.Where(x => x.PlexLibraryId == library.Id && x.ParentId == null)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Title, "Scale root"), CancellationToken);
        var roots = Enumerable
            .Range(1, 120)
            .Select(index => new PlexPhotoFolder
            {
                PlexServerId = serverId,
                PlexLibraryId = library.Id,
                ParentId = null,
                RelativePath = string.Empty,
                Title = "Scale root",
                RootIdentityKey = $"id:scale-{index}",
                SourceRootPath = $"/fixture/scale-{index}",
                PlexApiLocationId = index + 1L,
                IsActive = true,
            })
            .ToList();
        dbContext.PlexPhotoFolders.AddRange(roots);
        await dbContext.SaveChangesAsync(CancellationToken);
        var expectedRootIds = await dbContext
            .PlexPhotoFolders.Where(x => x.PlexLibraryId == library.Id && x.ParentId == null && x.IsActive == true)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(CancellationToken);

        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners.Subscribe(listener =>
        {
            if (listener.Name != "Microsoft.EntityFrameworkCore")
                return;
            commandSubscriptions.Add(
                listener.Subscribe(entry =>
                {
                    if (
                        entry.Value is CommandExecutedEventData command
                        && command.Context?.Database.GetConnectionString() == connectionString
                    )
                        commands.Enqueue(command.Command.CommandText);
                })
            );
        });

        // Act
        var smallPage = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(
            new GetPhotoSourceRootsQueryCommand(library.Id, 1, 1, SortType.Title)
        );
        var smallPageQueryCount = commands.Count;
        commands.Clear();
        var firstPage = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(
            new GetPhotoSourceRootsQueryCommand(library.Id, 1, 100, SortType.Title)
        );
        var largePageQueryCount = commands.Count;
        var secondPage = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(
            new GetPhotoSourceRootsQueryCommand(library.Id, 2, 100, SortType.Title)
        );
        var descendingPage = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(
            new GetPhotoSourceRootsQueryCommand(library.Id, 1, 100, SortType.Title, SortDirection.Desc)
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(
            new GetPhotoSourceRootsQueryCommand(library.Id, 1, 100, (SortType)999)
        );

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        secondPage.IsSuccess.ShouldBeTrue();
        smallPage.IsSuccess.ShouldBeTrue();
        descendingPage.IsSuccess.ShouldBeTrue();
        smallPage.Errors.Count.ShouldBe(0);
        firstPage.Errors.Count.ShouldBe(0);
        secondPage.Errors.Count.ShouldBe(0);
        descendingPage.Errors.Count.ShouldBe(0);
        smallPage.Value.Items.Count.ShouldBe(1);
        smallPageQueryCount.ShouldBe(8);
        largePageQueryCount.ShouldBe(smallPageQueryCount);
        firstPage.Value.TotalCount.ShouldBe(121);
        secondPage.Value.TotalCount.ShouldBe(121);
        firstPage.Value.Items.Count.ShouldBe(100);
        secondPage.Value.Items.Count.ShouldBe(21);
        firstPage.Value.Items.Select(x => x.Id).ShouldBe(expectedRootIds.Take(100));
        descendingPage.Value.Items.Select(x => x.Id).ShouldBe(expectedRootIds.OrderByDescending(x => x).Take(100));
        firstPage.Value.Items.ShouldAllBe(x => x.RecursiveAssetCount >= 0 && x.RecursiveAssetBytes >= 0);
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldRejectUndefinedDirectionBeforeLoadingSourceRoots()
    {
        // Arrange
        var query = new GetPhotoSourceRootsQueryCommand(1, Direction: (SortDirection)999);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoFolderPageDTO>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Has400BadRequestError().ShouldBeTrue();
    }
}
