namespace Reaparr.Application.UnitTests;

public class SyncPlexPhotosCommandUnitTests : BaseCommandUnitTest<SyncPlexPhotosCommand>
{
    [Test]
    public async Task ShouldPersistOverlappingMembershipsAndRetainStableIdsAcrossFolderMove()
    {
        await SetupDatabase(73001, x => x.PlexPhotoLibraryCount = 1);
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var first = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [
                CreateFile(201, 301, "/fixture/photos/Trips/one.jpg"),
                CreateFile(202, 302, "/fixture/photos/two.jpg"),
            ],
            [new(101, "Album A"), new(102, "Album B")],
            [(101, 201), (102, 201), (101, 202)]
        );

        var firstResult = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(first));

        firstResult.IsSuccess.ShouldBeTrue(firstResult.ToString());
        firstResult.Value.CreatedPhotos.ShouldBe(2);
        var firstLibrary = await IDbContext.PlexLibraries.SingleAsync(x => x.Id == library.Id, CancellationToken);
        firstLibrary.SyncedAt.ShouldNotBeNull();
        firstLibrary.SyncedContentChangedAt.ShouldBe(first.PlexLibrary.ContentChangedAt);
        firstLibrary.Outdated.ShouldBeFalse();
        (firstLibrary.PhotoAlbumCount, firstLibrary.PhotoImageCount, firstLibrary.PhotoClipCount).ShouldBe((2, 2, 0));
        var firstFile = await IDbContext.PlexPhotoMediaData.SingleAsync(x => x.PlexApiRatingKey == 201, CancellationToken);
        var secondFile = await IDbContext.PlexPhotoMediaData.SingleAsync(x => x.PlexApiRatingKey == 202, CancellationToken);
        var firstAlbum = await IDbContext.PlexPhotoAlbums.SingleAsync(x => x.PlexApiRatingKey == 101, CancellationToken);
        var secondAlbum = await IDbContext.PlexPhotoAlbums.SingleAsync(x => x.PlexApiRatingKey == 102, CancellationToken);
        var firstTripsFolder = await IDbContext.PlexPhotoFolders.SingleAsync(x => x.RelativePath == "Trips", CancellationToken);
        firstFile.PlexPhotoFolderId.ShouldBe(firstTripsFolder.Id);

        var second = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [
                CreateFile(201, 301, "/fixture/photos/Trips/Renamed/one.jpg"),
                CreateFile(202, 302, "/fixture/photos/two.jpg"),
            ],
            [new(101, "Album A"), new(102, "Album B")],
            [(102, 201), (101, 202)]
        );
        var secondResult = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(second, true));

        secondResult.IsSuccess.ShouldBeTrue(secondResult.ToString());
        secondResult.Value.CreatedPhotos.ShouldBe(0);
        secondResult.Value.UpdatedPhotos.ShouldBe(1);
        secondResult.Value.UnchangedPhotos.ShouldBe(1);
        var stableFile = await IDbContext.PlexPhotoMediaData.SingleAsync(x => x.PlexApiRatingKey == 201, CancellationToken);
        stableFile.Id.ShouldBe(firstFile.Id);
        var renamedFolder = await IDbContext.PlexPhotoFolders.SingleAsync(x => x.RelativePath == "Trips/Renamed", CancellationToken);
        stableFile.PlexPhotoFolderId.ShouldBe(renamedFolder.Id);
        (await IDbContext.PlexPhotoAlbumMemberships
                .OrderBy(x => x.PlexPhotoAlbumId)
                .ThenBy(x => x.PlexPhotoMediaDataId)
                .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
                .ToListAsync(CancellationToken))
            .ShouldBe([
                new { PlexPhotoAlbumId = firstAlbum.Id, PlexPhotoMediaDataId = secondFile.Id },
                new { PlexPhotoAlbumId = secondAlbum.Id, PlexPhotoMediaDataId = stableFile.Id },
            ]);

        var repeatResult = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(second, true));
        repeatResult.IsSuccess.ShouldBeTrue(repeatResult.ToString());
        repeatResult.Value.UpdatedPhotos.ShouldBe(0);
        repeatResult.Value.UnchangedPhotos.ShouldBe(2);
    }

    [Test]
    public async Task ShouldDeactivateRemovedRootsAndKeepUnassignedIncomingFiles()
    {
        await SetupDatabase(73003, x => x.PlexPhotoLibraryCount = 1);
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(220, 320, "/fixture/photos/Folder/file.jpg")],
            [],
            []
        );
        (await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial))).IsSuccess.ShouldBeTrue();
        var rootId = await IDbContext.PlexPhotoFolders.Where(x => x.ParentId == null).Select(x => x.Id).SingleAsync(CancellationToken);

        var outside = CreateFile(220, 320, "/outside/file.jpg");
        var removedRoot = CreateMetadata(library, [], [outside], [], []);
        var result = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(removedRoot));

        result.IsSuccess.ShouldBeTrue(result.ToString());
        var retainedRoot = await IDbContext.PlexPhotoFolders.SingleAsync(x => x.Id == rootId, CancellationToken);
        retainedRoot.IsActive.ShouldBe((bool?)false);
        (await IDbContext.PlexPhotoFolders.CountAsync(x => x.ParentId != null, CancellationToken)).ShouldBe(0);
        (await IDbContext.PlexPhotoMediaData.SingleAsync(CancellationToken)).PlexPhotoFolderId.ShouldBeNull();
    }

    [Test]
    public async Task ShouldRemoveMissingAlbumsFilesAndMembershipsAsOneReconciliation()
    {
        await SetupDatabase(73006, x => x.PlexPhotoLibraryCount = 1);
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(250, 350, "/fixture/photos/one.jpg"), CreateFile(251, 351, "/fixture/photos/two.jpg")],
            [new(150, "Album")],
            [(150, 250), (150, 251)]
        );
        (await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial))).IsSuccess.ShouldBeTrue();

        var reduced = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(250, 350, "/fixture/photos/one.jpg")],
            [],
            []
        );
        var result = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(reduced));

        result.IsSuccess.ShouldBeTrue(result.ToString());
        result.Value.DeletedPhotoAlbums.ShouldBe(1);
        result.Value.DeletedPhotos.ShouldBe(1);
        (await IDbContext.PlexPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await IDbContext.PlexPhotoMediaData.CountAsync(CancellationToken)).ShouldBe(1);
        (await IDbContext.PlexPhotoAlbumMemberships.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldKeepInsertedPhotosSearchable()
    {
        // Arrange
        var (library, searchHandler) = await ArrangePhotoSearchFixtureAsync();
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [
                CreateFile(280, 380, "/fixture/photos/summer.jpg", "Summer Sunset.jpg"),
                CreateFile(281, 381, "/fixture/photos/delete.jpg", "Delete Me.jpg"),
            ],
            [],
            []
        );

        // Act
        var sync = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial));
        var insertedId = await GetPhotoIdAsync(280);
        var deletedId = await GetPhotoIdAsync(281);
        var summerSearch = await SearchPhotoAssetsAsync(searchHandler, "summer");
        var deleteSearch = await SearchPhotoAssetsAsync(searchHandler, "delete");

        // Assert
        sync.IsSuccess.ShouldBeTrue(sync.ToString());
        sync.Errors.Count.ShouldBe(0);
        sync.Value.CreatedPhotos.ShouldBe(2);
        summerSearch.IsSuccess.ShouldBeTrue(summerSearch.ToString());
        summerSearch.Errors.Count.ShouldBe(0);
        (summerSearch.Value.TotalCount, summerSearch.Value.TotalBytes).ShouldBe((1, 20L));
        summerSearch.Value.Items.Select(x => x.Id).ShouldBe([insertedId]);
        deleteSearch.IsSuccess.ShouldBeTrue(deleteSearch.ToString());
        deleteSearch.Errors.Count.ShouldBe(0);
        (deleteSearch.Value.TotalCount, deleteSearch.Value.TotalBytes).ShouldBe((1, 20L));
        deleteSearch.Value.Items.Select(x => x.Id).ShouldBe([deletedId]);
    }

    [Test]
    public async Task ShouldKeepUpdatedPhotoSearchableWithStableIdentity()
    {
        // Arrange
        var (library, searchHandler) = await ArrangePhotoSearchFixtureAsync();
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(280, 380, "/fixture/photos/summer.jpg", "Summer Sunset.jpg")],
            [],
            []
        );

        // Act
        var initialSync = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial));
        var beforeId = await GetPhotoIdAsync(280);
        var beforeSearch = await SearchPhotoAssetsAsync(searchHandler, "summer");
        var updated = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(280, 380, "/fixture/photos/winter.jpg", "Winter Sunset.jpg")],
            [],
            []
        );

        var updateSync = await TestHandlerExecuteAsync<CrudPhotosReport>(
            new SyncPlexPhotosCommand(updated)
        );
        var afterId = await GetPhotoIdAsync(280);
        var oldTitleSearch = await SearchPhotoAssetsAsync(searchHandler, "summer");
        var newTitleSearch = await SearchPhotoAssetsAsync(searchHandler, "winter");

        // Assert
        initialSync.IsSuccess.ShouldBeTrue(initialSync.ToString());
        initialSync.Errors.Count.ShouldBe(0);
        initialSync.Value.CreatedPhotos.ShouldBe(1);
        beforeSearch.IsSuccess.ShouldBeTrue(beforeSearch.ToString());
        beforeSearch.Errors.Count.ShouldBe(0);
        (beforeSearch.Value.TotalCount, beforeSearch.Value.TotalBytes).ShouldBe((1, 20L));
        beforeSearch.Value.Items.Select(x => x.Id).ShouldBe([beforeId]);
        updateSync.IsSuccess.ShouldBeTrue(updateSync.ToString());
        updateSync.Errors.Count.ShouldBe(0);
        updateSync.Value.UpdatedPhotos.ShouldBe(1);
        afterId.ShouldBe(beforeId);
        oldTitleSearch.IsSuccess.ShouldBeTrue(oldTitleSearch.ToString());
        oldTitleSearch.Errors.Count.ShouldBe(0);
        (oldTitleSearch.Value.TotalCount, oldTitleSearch.Value.TotalBytes).ShouldBe((0, 0L));
        oldTitleSearch.Value.Items.ShouldBeEmpty();
        newTitleSearch.IsSuccess.ShouldBeTrue(newTitleSearch.ToString());
        newTitleSearch.Errors.Count.ShouldBe(0);
        (newTitleSearch.Value.TotalCount, newTitleSearch.Value.TotalBytes).ShouldBe((1, 20L));
        newTitleSearch.Value.Items.Select(x => x.Id).ShouldBe([afterId]);
    }

    [Test]
    public async Task ShouldRemoveDeletedPhotoFromSearchResults()
    {
        // Arrange
        var (library, searchHandler) = await ArrangePhotoSearchFixtureAsync();
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(281, 381, "/fixture/photos/delete.jpg", "Delete Me.jpg")],
            [],
            []
        );

        // Act
        var initialSync = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial));
        var deletedId = await GetPhotoIdAsync(281);
        var beforeSearch = await SearchPhotoAssetsAsync(searchHandler, "delete");
        var reduced = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [],
            [],
            []
        );

        var deleteSync = await TestHandlerExecuteAsync<CrudPhotosReport>(
            new SyncPlexPhotosCommand(reduced)
        );
        var afterSearch = await SearchPhotoAssetsAsync(searchHandler, "delete");

        // Assert
        initialSync.IsSuccess.ShouldBeTrue(initialSync.ToString());
        initialSync.Errors.Count.ShouldBe(0);
        initialSync.Value.CreatedPhotos.ShouldBe(1);
        beforeSearch.IsSuccess.ShouldBeTrue(beforeSearch.ToString());
        beforeSearch.Errors.Count.ShouldBe(0);
        (beforeSearch.Value.TotalCount, beforeSearch.Value.TotalBytes).ShouldBe((1, 20L));
        beforeSearch.Value.Items.Select(x => x.Id).ShouldBe([deletedId]);
        deleteSync.IsSuccess.ShouldBeTrue(deleteSync.ToString());
        deleteSync.Errors.Count.ShouldBe(0);
        deleteSync.Value.DeletedPhotos.ShouldBe(1);
        afterSearch.IsSuccess.ShouldBeTrue(afterSearch.ToString());
        afterSearch.Errors.Count.ShouldBe(0);
        (afterSearch.Value.TotalCount, afterSearch.Value.TotalBytes).ShouldBe((0, 0L));
        afterSearch.Value.Items.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPreserveCatalogAndSyncCompletion_WhenPhotoReconciliationFails()
    {
        // Arrange
        await SetupDatabase(73007, x => x.PlexPhotoLibraryCount = 1);
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(260, 360, "/fixture/photos/original.jpg")],
            [new(160, "Original")],
            [(160, 260)]
        );
        var seeded = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial));
        seeded.IsSuccess.ShouldBeTrue(seeded.ToString());

        var originalSyncedAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
        var originalSyncedContentChangedAt = 456_789L;
        await IDbContext.PlexLibraries
            .Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.SyncedAt, (DateTime?)originalSyncedAt)
                        .SetProperty(x => x.SyncedContentChangedAt, (long?)originalSyncedContentChangedAt)
                        .SetProperty(x => x.Outdated, true),
                CancellationToken
            );

        var beforeAlbums = await IDbContext.PlexPhotoAlbums
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.Title })
            .ToListAsync(CancellationToken);
        var beforeFiles = await IDbContext.PlexPhotoMediaData
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexApiPartId, x.PlexPhotoFolderId, x.Title })
            .ToListAsync(CancellationToken);
        var beforeFolders = await IDbContext.PlexPhotoFolders
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ParentId, x.RelativePath, x.IsActive })
            .ToListAsync(CancellationToken);
        var beforeMemberships = await IDbContext.PlexPhotoAlbumMemberships
            .Where(x => x.PlexPhotoAlbum!.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexPhotoAlbumId)
            .ThenBy(x => x.PlexPhotoMediaDataId)
            .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
            .ToListAsync(CancellationToken);
        var beforeLibrary = await IDbContext.PlexLibraries
            .Where(x => x.Id == library.Id)
            .Select(x => new
            {
                x.Title,
                x.ContentChangedAt,
                x.UpdatedAt,
                x.ScannedAt,
                x.PhotoAlbumCount,
                x.PhotoImageCount,
                x.PhotoClipCount,
                x.MediaSize,
                x.SyncedAt,
                x.SyncedContentChangedAt,
                x.Outdated,
            })
            .SingleAsync(CancellationToken);

        var invalid = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [
                CreateFile(261, 361, "/fixture/photos/first.jpg"),
                CreateFile(262, 361, "/fixture/photos/second.jpg"),
            ],
            [],
            []
        );

        // Act
        var result = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(invalid));

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await IDbContext.PlexPhotoAlbums
                .Where(x => x.PlexLibraryId == library.Id)
                .OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey, x.Title })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeAlbums);
        (await IDbContext.PlexPhotoMediaData
                .Where(x => x.PlexLibraryId == library.Id)
                .OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexApiPartId, x.PlexPhotoFolderId, x.Title })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeFiles);
        (await IDbContext.PlexPhotoFolders
                .Where(x => x.PlexLibraryId == library.Id)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.ParentId, x.RelativePath, x.IsActive })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeFolders);
        (await IDbContext.PlexPhotoAlbumMemberships
                .Where(x => x.PlexPhotoAlbum!.PlexLibraryId == library.Id)
                .OrderBy(x => x.PlexPhotoAlbumId)
                .ThenBy(x => x.PlexPhotoMediaDataId)
                .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeMemberships);
        (await IDbContext.PlexLibraries
                .Where(x => x.Id == library.Id)
                .Select(x => new
                {
                    x.Title,
                    x.ContentChangedAt,
                    x.UpdatedAt,
                    x.ScannedAt,
                    x.PhotoAlbumCount,
                    x.PhotoImageCount,
                    x.PhotoClipCount,
                    x.MediaSize,
                    x.SyncedAt,
                    x.SyncedContentChangedAt,
                    x.Outdated,
                })
                .SingleAsync(CancellationToken))
            .ShouldBe(beforeLibrary);
    }

    [Test]
    public async Task ShouldPreserveCatalogAndSyncCompletion_WhenPhotoReconciliationIsCancelled()
    {
        // Arrange
        await SetupDatabase(73008, x => x.PlexPhotoLibraryCount = 1);
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        var initial = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(270, 370, "/fixture/photos/original.jpg")],
            [new(170, "Original")],
            [(170, 270)]
        );
        var seeded = await TestHandlerExecuteAsync<CrudPhotosReport>(new SyncPlexPhotosCommand(initial));
        seeded.IsSuccess.ShouldBeTrue(seeded.ToString());

        var originalSyncedAt = new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc);
        var originalSyncedContentChangedAt = 567_890L;
        await IDbContext.PlexLibraries
            .Where(x => x.Id == library.Id)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(x => x.SyncedAt, (DateTime?)originalSyncedAt)
                        .SetProperty(x => x.SyncedContentChangedAt, (long?)originalSyncedContentChangedAt)
                        .SetProperty(x => x.Outdated, true),
                CancellationToken
            );

        var beforeAlbums = await IDbContext.PlexPhotoAlbums
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.Title })
            .ToListAsync(CancellationToken);
        var beforeFiles = await IDbContext.PlexPhotoMediaData
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexApiPartId, x.PlexPhotoFolderId, x.Title })
            .ToListAsync(CancellationToken);
        var beforeFolders = await IDbContext.PlexPhotoFolders
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ParentId, x.RelativePath, x.IsActive })
            .ToListAsync(CancellationToken);
        var beforeMemberships = await IDbContext.PlexPhotoAlbumMemberships
            .Where(x => x.PlexPhotoAlbum!.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexPhotoAlbumId)
            .ThenBy(x => x.PlexPhotoMediaDataId)
            .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
            .ToListAsync(CancellationToken);
        var beforeLibrary = await IDbContext.PlexLibraries
            .Where(x => x.Id == library.Id)
            .Select(x => new
            {
                x.Title,
                x.ContentChangedAt,
                x.UpdatedAt,
                x.ScannedAt,
                x.PhotoAlbumCount,
                x.PhotoImageCount,
                x.PhotoClipCount,
                x.MediaSize,
                x.SyncedAt,
                x.SyncedContentChangedAt,
                x.Outdated,
            })
            .SingleAsync(CancellationToken);

        var cancelled = CreateMetadata(
            library,
            [new SourceRoot("/fixture/photos", null)],
            [CreateFile(271, 371, "/fixture/photos/replacement.jpg")],
            [],
            []
        );
        var command = new SyncPlexPhotosCommand(cancelled);
        (await new SyncPlexPhotosCommandValidator().ValidateAsync(command, CancellationToken)).IsValid.ShouldBeTrue();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var result = await Mock.Create<SyncPlexPhotosCommandHandler>().ExecuteAsync(command, cancellation.Token);

        // Assert
        result.IsCancelled.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        (await IDbContext.PlexPhotoAlbums
                .Where(x => x.PlexLibraryId == library.Id)
                .OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey, x.Title })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeAlbums);
        (await IDbContext.PlexPhotoMediaData
                .Where(x => x.PlexLibraryId == library.Id)
                .OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexApiPartId, x.PlexPhotoFolderId, x.Title })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeFiles);
        (await IDbContext.PlexPhotoFolders
                .Where(x => x.PlexLibraryId == library.Id)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.ParentId, x.RelativePath, x.IsActive })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeFolders);
        (await IDbContext.PlexPhotoAlbumMemberships
                .Where(x => x.PlexPhotoAlbum!.PlexLibraryId == library.Id)
                .OrderBy(x => x.PlexPhotoAlbumId)
                .ThenBy(x => x.PlexPhotoMediaDataId)
                .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
                .ToListAsync(CancellationToken))
            .ShouldBe(beforeMemberships);
        (await IDbContext.PlexLibraries
                .Where(x => x.Id == library.Id)
                .Select(x => new
                {
                    x.Title,
                    x.ContentChangedAt,
                    x.UpdatedAt,
                    x.ScannedAt,
                    x.PhotoAlbumCount,
                    x.PhotoImageCount,
                    x.PhotoClipCount,
                    x.MediaSize,
                    x.SyncedAt,
                    x.SyncedContentChangedAt,
                    x.Outdated,
                })
                .SingleAsync(CancellationToken))
            .ShouldBe(beforeLibrary);
    }

    private async Task<(PlexLibrary Library, SearchPhotoAssetsQueryHandler SearchHandler)> ArrangePhotoSearchFixtureAsync()
    {
        await SetupDatabase(73010, x => x.PlexPhotoLibraryCount = 1);
        var library = await IDbContext.PlexLibraries.SingleAsync(CancellationToken);
        return (library, Mock.Create<SearchPhotoAssetsQueryHandler>());
    }

    private Task<int> GetPhotoIdAsync(int ratingKey) =>
        IDbContext.PlexPhotoMediaData
            .Where(x => x.PlexApiRatingKey == ratingKey)
            .Select(x => x.Id)
            .SingleAsync(CancellationToken);

    private Task<Result<PhotoAssetPageDTO>> SearchPhotoAssetsAsync(
        SearchPhotoAssetsQueryHandler searchHandler,
        string searchTerm
    ) =>
        searchHandler.ExecuteAsync(
            new SearchPhotoAssetsQuery(searchTerm, 1, 100),
            CancellationToken
        );

    private static InsertMediaMetaDataCommandResponse CreateMetadata(
        PlexLibrary databaseLibrary,
        IReadOnlyList<SourceRoot> roots,
        IReadOnlyList<PlexPhotoMediaData> files,
        IReadOnlyList<(int RatingKey, string Title)> albums,
        IReadOnlyList<(int AlbumRatingKey, int FileRatingKey)> memberships
    )
    {
        var library = FakeData.GetPlexLibrary(new Seed(databaseLibrary.Id + 1), PlexMediaType.PhotoAlbum).Generate();
        library.Id = databaseLibrary.Id;
        library.PlexServerId = databaseLibrary.PlexServerId;
        library.PhotoAlbums.Clear();
        library.PhotoMediaData.Clear();
        library.PhotoFolders.Clear();

        var foldersByPath = new Dictionary<string, PlexPhotoFolder>(StringComparer.Ordinal);
        foreach (var rootInput in roots)
        {
            var root = new PlexPhotoFolder
            {
                Id = 0,
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                ParentId = null,
                RelativePath = string.Empty,
                Title = rootInput.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? rootInput.Path,
                RootIdentityKey = rootInput.LocationId is > 0 ? $"id:{rootInput.LocationId}" : $"path:posix:{rootInput.Path}",
                SourceRootPath = rootInput.Path,
                PlexApiLocationId = rootInput.LocationId,
                IsActive = true,
            };
            foldersByPath[rootInput.Path] = root;
            library.PhotoFolders.Add(root);
        }

        foreach (var file in files)
        {
            var rootInput = roots
                .Where(x => file.SourcePath!.StartsWith(x.Path + "/", StringComparison.Ordinal))
                .OrderByDescending(x => x.Path.Length)
                .FirstOrDefault();
            if (rootInput is not null)
            {
                var root = foldersByPath[rootInput.Path];
                var relative = file.SourcePath![rootInput.Path.Length..].Trim('/');
                var segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var current = root;
                for (var index = 0; index < segments.Length - 1; index++)
                {
                    var path = string.Join('/', segments.Take(index + 1));
                    var absolute = rootInput.Path + "/" + path;
                    if (!foldersByPath.TryGetValue(absolute, out var folder))
                    {
                        folder = new PlexPhotoFolder
                        {
                            Id = 0,
                            PlexServerId = library.PlexServerId,
                            PlexLibraryId = library.Id,
                            Parent = current,
                            RelativePath = path,
                            Title = segments[index],
                            RootIdentityKey = null,
                            SourceRootPath = null,
                            IsActive = null,
                        };
                        current.Children.Add(folder);
                        foldersByPath.Add(absolute, folder);
                        library.PhotoFolders.Add(folder);
                    }
                    current = folder;
                }
                file.PlexPhotoFolder = current;
                current.Files.Add(file);
            }
            library.PhotoMediaData.Add(file);
        }

        foreach (var (ratingKey, title) in albums)
            library.PhotoAlbums.Add(CreateAlbum(ratingKey, title));
        var albumsByKey = library.PhotoAlbums.ToDictionary(x => x.PlexApiRatingKey);
        var filesByKey = library.PhotoMediaData.ToDictionary(x => x.PlexApiRatingKey);
        foreach (var (albumRatingKey, fileRatingKey) in memberships)
        {
            var album = albumsByKey[albumRatingKey];
            var file = filesByKey[fileRatingKey];
            album.Memberships.Add(new PlexPhotoAlbumMembership
            {
                PlexPhotoAlbumId = 0,
                PlexPhotoMediaDataId = 0,
                PlexPhotoAlbum = album,
                PlexPhotoMediaData = file,
            });
        }

        return new InsertMediaMetaDataCommandResponse(library)
        {
            PlexActors = [],
            PlexGenresVideo = [],
            PlexGenresAudio = [],
            PlexCountries = [],
        };
    }

    private static PlexPhotoAlbum CreateAlbum(int ratingKey, string title) =>
        new()
        {
            Id = 0,
            PlexServerId = 1,
            PlexLibraryId = 1,
            PlexApiRatingKey = ratingKey,
            Title = title,
            SearchTitle = title.ToSearchTitle(),
            Summary = null,
            HasThumb = false,
            AddedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = null,
        };

    private static PlexPhotoMediaData CreateFile(int ratingKey, int partId, string sourcePath, string? title = null) =>
        new()
        {
            Id = 0,
            PlexServerId = 1,
            PlexLibraryId = 1,
            PlexApiRatingKey = ratingKey,
            PlexApiMediaId = partId + 1000,
            PlexApiPartId = partId,
            AssetKind = PhotoAssetKind.Image,
            Title = title ?? $"{ratingKey}.jpg",
            SearchTitle = (title ?? $"{ratingKey}.jpg").ToSearchTitle(),
            Summary = null,
            HasThumb = false,
            AddedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = null,
            OriginallyAvailableAt = null,
            SourcePath = sourcePath,
            OriginalFilename = sourcePath.Split('/').Last(),
            Key = $"/library/parts/{partId}/file.jpg",
            Size = 20,
            Container = "jpg",
            Width = 100,
            Height = 100,
            Duration = null,
            VideoCodec = null,
            AudioCodec = null,
            PlexPhotoFolderId = null,
        };

    private sealed record SourceRoot(string Path, long? LocationId);
}
