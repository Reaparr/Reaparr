using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SortDirection = Reaparr.Domain.SortDirection;

namespace Reaparr.Application.UnitTests;

public class GetPhotoAlbumMembersQueryUnitTests : BaseCommandUnitTest<GetPhotoAlbumMembersQuery>
{
    [Test]
    [Arguments(0, 1)]
    [Arguments(1, 1)]
    [Arguments(3, 1)]
    [Arguments(3, 2)]
    public async Task ShouldReturnUniqueTotalsInOneAggregate_WhenPagingAlbumMembers(int assetCount, int page)
    {
        // Arrange
        await SetupDatabase(71021, config =>
        {
            config.PlexPhotoLibraryCount = 2;
            config.PhotoAlbumCount = 2;
            config.PhotoCount = 3;
        });
        var dbContext = IDbContext;
        var album = await dbContext.PlexPhotoAlbums.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        var assets = await dbContext.PlexPhotoMediaData
            .Where(x => x.AlbumMemberships.Any(membership => membership.PlexPhotoAlbumId == album.Id))
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        assets.Count.ShouldBe(3);
        for (var index = 0; index < assets.Count; index++)
        {
            await dbContext.PlexPhotoMediaData.Where(x => x.Id == assets[index].Id).ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Size, index * 100L).SetProperty(x => x.Title, "Same title"),
                CancellationToken
            );
        }
        var expectedAssets = assets.Take(assetCount).ToList();
        var excludedIds = assets.Skip(assetCount).Select(x => x.Id).ToList();
        await dbContext.PlexPhotoAlbumMemberships
            .Where(x => x.PlexPhotoAlbumId == album.Id && excludedIds.Contains(x.PlexPhotoMediaDataId))
            .ExecuteDeleteAsync(CancellationToken);
        var otherAlbumId = await dbContext.PlexPhotoAlbums
            .Where(x => x.PlexLibraryId == album.PlexLibraryId && x.Id != album.Id)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.Add(new PlexPhotoAlbumMembership
        {
            PlexPhotoAlbumId = otherAlbumId,
            PlexPhotoMediaDataId = assets[0].Id,
        });
        await dbContext.SaveChangesAsync(CancellationToken);
        var connectionString = ((DbContext)dbContext).Database.GetConnectionString();
        var commands = new ConcurrentQueue<string>();
        using var commandSubscriptions = new System.Reactive.Disposables.CompositeDisposable();
        using var listeners = DiagnosticListener.AllListeners
            .Where(listener => listener.Name == "Microsoft.EntityFrameworkCore")
            .Subscribe(listener => commandSubscriptions.Add(listener
                .Select(entry => entry.Value!)
                .OfType<CommandExecutedEventData>()
                .Where(command => command.Context?.Database.GetConnectionString() == connectionString)
                .Subscribe(command => commands.Enqueue(command.Command.CommandText))));

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(album.PlexLibraryId, album.Id, page, 2)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Page.ShouldBe(page);
        result.Value.PageSize.ShouldBe(2);
        result.Value.TotalCount.ShouldBe(assetCount);
        result.Value.TotalBytes.ShouldBe(Enumerable.Range(0, assetCount).Sum(index => index * 100L));
        result.Value.Items.Select(x => x.Id).ShouldBe(
            expectedAssets.Skip((page - 1) * 2).Take(2).Select(x => x.Id)
        );
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == album.PlexLibraryId);
        var aggregateCommands = commands
            .Where(sql => sql.Contains("\"PlexPhotoMediaData\"")
                && (sql.Contains("COUNT(") || sql.Contains("SUM(")))
            .ToList();
        aggregateCommands.Count.ShouldBe(1);
        aggregateCommands[0].ShouldContain("COUNT(");
        aggregateCommands[0].ShouldContain("SUM(");
    }

    [Test]
    public async Task ShouldReturnOnlyDistinctMembersForTheRequestedAlbumAndLibrary()
    {
        // Arrange
        await SetupDatabase(
            71005,
            config =>
            {
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 3;
                config.PhotoClipCount = 1;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var album = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var expectedIds = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbumId == album.Id)
            .Select(x => x.PlexPhotoMediaDataId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(CancellationToken);
        expectedIds.Count.ShouldBeGreaterThan(0);
        await dbContext.PlexPhotoMediaData
            .Where(x => expectedIds.Contains(x.Id))
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Title, "Album member"), CancellationToken);
        var inactiveAssetId = expectedIds[0];
        var inactiveRoot = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            RelativePath = string.Empty,
            Title = "Album inactive",
            RootIdentityKey = "path:album-inactive",
            SourceRootPath = "/album-inactive",
            PlexApiLocationId = 9201,
            IsActive = false,
        };
        dbContext.PlexPhotoFolders.Add(inactiveRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        var inactiveChild = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            ParentId = inactiveRoot.Id,
            RelativePath = "album-inactive/child",
            Title = "Album inactive child",
        };
        dbContext.PlexPhotoFolders.Add(inactiveChild);
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == inactiveAssetId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, inactiveChild.Id), CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(library.Id, album.Id, 1, 100, SortType.Title)
        );
        var descending = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(library.Id, album.Id, 1, 100, SortType.Title, SortDirection.Desc)
        );
        var invalidSort = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(library.Id, album.Id, 1, 100, (SortType)999)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        descending.IsSuccess.ShouldBeTrue();
        descending.Errors.Count.ShouldBe(0);
        result.Value.TotalCount.ShouldBe(expectedIds.Count);
        result.Value.Items.Select(x => x.Id).Distinct().Count().ShouldBe(result.Value.Items.Count);
        result.Value.Items.ShouldAllBe(x => x.PlexLibraryId == library.Id);
        result
            .Value.Items.Select(x => x.Id)
            .ShouldBe(
                result
                    .Value.Items.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(x => x.Id)
                    .Select(x => x.Id)
            );
        descending.Value.Items.Select(x => x.Id).ShouldBe(expectedIds.OrderByDescending(x => x));
        result.Value.Items.Select(x => x.Id).ShouldAllBe(id => expectedIds.Contains(id));
        result.Value.Items.Single(x => x.Id == inactiveAssetId).SourceRootTitle.ShouldBe(inactiveRoot.Title);
        invalidSort.IsFailed.ShouldBeTrue();
        invalidSort.Errors.Count.ShouldBe(2);
        invalidSort.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldRejectUndefinedDirectionBeforeLoadingAlbumMembers()
    {
        // Arrange
        var query = new GetPhotoAlbumMembersQuery(1, 1, Direction: (SortDirection)999);

        // Act
        var result = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(2);
        result.Has400BadRequestError().ShouldBeTrue();
    }

    [Test]
    public async Task ShouldPageOverlappingMembershipsAndRejectForeignAlbumScope()
    {
        // Arrange
        await SetupDatabase(
            71013,
            config =>
            {
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext.PlexLibraries.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var album = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id)
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);
        var otherAlbumId = await dbContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == libraries[0].Id && x.Id != album.Id)
            .Select(x => x.Id)
            .FirstAsync(CancellationToken);
        var overlapAssetId = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbumId == otherAlbumId)
            .Select(x => x.PlexPhotoMediaDataId)
            .FirstAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.Add(
            new PlexPhotoAlbumMembership { PlexPhotoAlbumId = album.Id, PlexPhotoMediaDataId = overlapAssetId }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        await dbContext
            .PlexPhotoMediaData.Where(x => x.Id == overlapAssetId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.PlexPhotoFolderId, (int?)null), CancellationToken);
        var expectedIds = await dbContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbumId == album.Id)
            .Select(x => x.PlexPhotoMediaDataId)
            .Distinct()
            .ToListAsync(CancellationToken);

        // Act
        var firstPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[0].Id, album.Id, 1, 1)
        );
        var secondPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[0].Id, album.Id, 2, 1)
        );
        var thirdPage = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[0].Id, album.Id, 3, 1)
        );
        var foreignAlbum = await TestHandlerExecuteAsync<PhotoAssetPageDTO>(
            new GetPhotoAlbumMembersQuery(libraries[1].Id, album.Id)
        );

        // Assert
        firstPage.IsSuccess.ShouldBeTrue();
        secondPage.IsSuccess.ShouldBeTrue();
        thirdPage.IsSuccess.ShouldBeTrue();
        firstPage.Value.TotalCount.ShouldBe(expectedIds.Count);
        secondPage.Value.TotalCount.ShouldBe(expectedIds.Count);
        thirdPage.Value.TotalCount.ShouldBe(expectedIds.Count);
        firstPage.Value.Items.Count.ShouldBe(1);
        secondPage.Value.Items.Count.ShouldBe(1);
        thirdPage.Value.Items.Count.ShouldBe(1);
        var pagedItems = firstPage.Value.Items.Concat(secondPage.Value.Items).Concat(thirdPage.Value.Items).ToList();
        pagedItems.Select(x => x.Id).Distinct().Count().ShouldBe(expectedIds.Count);
        pagedItems.ShouldAllBe(x => expectedIds.Contains(x.Id));
        pagedItems.First(x => x.Id == overlapAssetId).RelativeFolderPath.ShouldBeNull();
        pagedItems.First(x => x.Id == overlapAssetId).SourceRootTitle.ShouldBeNull();
        foreignAlbum.Has404NotFoundError().ShouldBeTrue();
    }
}
