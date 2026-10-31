namespace Reaparr.Application.UnitTests;

public class GetPhotoDownloadPreviewCommandUnitTests : BaseCommandUnitTest<GetPhotoDownloadPreviewCommand>
{
    [Test]
    public async Task ShouldBuildPhotoFolderAndFilePreview_WhenDirectAssetIsSelected()
    {
        // Arrange
        await SetupDatabase(
            62401,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    CreatePhotoSelection(PhotoItemKind.Asset, (media.PlexServerId, media.PlexLibraryId), [media.Id]),
                ],
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.Count().ShouldBe(1);
        var rootPreview = result.Value.Single();
        rootPreview.MediaType.ShouldBe(PlexMediaType.PhotoAlbum);
        rootPreview.Size.ShouldBe(media.Size);
        rootPreview.ChildCount.ShouldBe(1);
        rootPreview.Children.Count.ShouldBe(1);
        var filePreview = rootPreview.Children.Single();
        filePreview.MediaType.ShouldBe(PlexMediaType.PhotoImage);
        filePreview.Id.ShouldBe(media.Id);
        filePreview.Title.ShouldBe(media.OriginalFilename.GetFileName());
        filePreview.Size.ShouldBe(media.Size);
        filePreview.ChildCount.ShouldBe(0);
        filePreview.Qualities.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldPreviewUnmappedUnalbumedAssetAtLibraryRoot_WhenNoCatalogRootIsActive()
    {
        // Arrange
        await SetupDatabase(
            62411,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext
            .PlexPhotoFolders.AsTracking()
            .SingleAsync(x => x.ParentId == null, CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.AsTracking().SingleAsync(CancellationToken);
        var server = await dbContext.PlexServers.AsTracking().SingleAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        root.IsActive = false;
        media.PlexPhotoFolderId = null;
        await dbContext
            .PlexServers.Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "Home Plex"), CancellationToken);
        library.Title = "Photos";
        await dbContext.SaveChangesAsync(CancellationToken);
        media.PlexPhotoFolderId.ShouldBeNull();
        (
            await dbContext.PlexPhotoFolders.CountAsync(
                x => x.ParentId == null && x.IsActive == true,
                CancellationToken
            )
        ).ShouldBe(0);
        (await dbContext.PlexPhotoAlbumMemberships.CountAsync(CancellationToken)).ShouldBe(0);
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    CreatePhotoSelection(PhotoItemKind.Asset, (media.PlexServerId, media.PlexLibraryId), [media.Id]),
                ],
                CustomDestinationFolderPath = "/unmapped-preview",
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var preview = result.Value.Single();
        preview.MediaType.ShouldBe(PlexMediaType.PhotoAlbum);
        preview.ChildCount.ShouldBe(1);
        preview.Children.Count.ShouldBe(1);
        preview.Children.Single().MediaType.ShouldBe(PlexMediaType.PhotoImage);
        preview.Children.Single().Id.ShouldBe(media.Id);
        preview.Children.Single().Children.ShouldBeEmpty();
        $"{preview.Title}/{preview.Children.Single().Title}".ShouldBe(
            $"Home Plex - Photos/{media.OriginalFilename.GetFileName()}"
        );
    }

    [Test]
    public async Task ShouldPreviewMappedAndUnmappedAlbumMembersAtTheirExpectedPaths()
    {
        // Arrange
        await SetupDatabase(
            62412,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.AsTracking().SingleAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "Home Plex"), CancellationToken);
        library.Title = "Photos";
        var root = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var child = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Trips",
            Title = "Trips",
        };
        dbContext.PlexPhotoFolders.Add(child);
        var album = await dbContext.PlexPhotoAlbums.SingleAsync(CancellationToken);
        var files = await dbContext.PlexPhotoMediaData.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.SaveChangesAsync(CancellationToken);
        files[0].PlexPhotoFolderId = child.Id;
        files[1].PlexPhotoFolderId = null;
        await dbContext.SaveChangesAsync(CancellationToken);
        files[0].PlexPhotoFolderId.ShouldBe(child.Id);
        files[1].PlexPhotoFolderId.ShouldBeNull();
        (
            await dbContext.PlexPhotoAlbumMemberships.CountAsync(x => x.PlexPhotoAlbumId == album.Id, CancellationToken)
        ).ShouldBe(2);
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    CreatePhotoSelection(PhotoItemKind.Album, (album.PlexServerId, album.PlexLibraryId), [album.Id]),
                ],
                CustomDestinationFolderPath = "/album-preview",
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var rootPreview = result.Value.Single();
        rootPreview.Title.ShouldBe("Home Plex - Photos");
        rootPreview.ChildCount.ShouldBe(2);
        rootPreview.Children.Count.ShouldBe(2);
        var actualPaths = new List<string>();
        void CollectPaths(DownloadPreview node, string parentPath)
        {
            var path = string.IsNullOrEmpty(parentPath) ? node.Title : $"{parentPath}/{node.Title}";
            if (node.MediaType == PlexMediaType.PhotoImage)
                actualPaths.Add(path);
            foreach (var childNode in node.Children)
                CollectPaths(childNode, path);
        }
        CollectPaths(rootPreview, string.Empty);
        actualPaths
            .Order(StringComparer.Ordinal)
            .ShouldBe(
                new[]
                {
                    $"Home Plex - Photos/Trips/{files[0].OriginalFilename.GetFileName()}",
                    $"Home Plex - Photos/{files[1].OriginalFilename.GetFileName()}",
                }.Order(StringComparer.Ordinal)
            );
        rootPreview
            .Children.Single(x => x.MediaType == PlexMediaType.PhotoImage)
            .Title.ShouldBe(files[1].OriginalFilename.GetFileName());
        rootPreview.Children.Single(x => x.MediaType == PlexMediaType.PhotoAlbum).Title.ShouldBe("Trips");
    }

    [Test]
    public async Task ShouldRejectMissingOrForeignDirectFileIds_BeforeBuildingPreview()
    {
        // Arrange
        await SetupDatabase(
            62408,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var files = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        var target = files[0];
        var foreign = files[1];
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    CreatePhotoSelection(
                        PhotoItemKind.Asset,
                        (target.PlexServerId, target.PlexLibraryId),
                        [target.Id, foreign.Id, int.MaxValue]
                    ),
                ],
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.ValueOrDefault.ShouldBeNull();
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldPreservePhysicalFolderHierarchy_WhenAssetIsMappedToDescendantFolder()
    {
        // Arrange
        await SetupDatabase(
            62402,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.AsTracking().SingleAsync(CancellationToken);
        var child = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Trips",
            Title = "Trips",
        };
        dbContext.PlexPhotoFolders.Add(child);
        media.PlexPhotoFolder = child;
        await dbContext.SaveChangesAsync(CancellationToken);
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    CreatePhotoSelection(PhotoItemKind.Asset, (media.PlexServerId, media.PlexLibraryId), [media.Id]),
                ],
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var folderPreview = result.Value.Single().Children.Single();
        folderPreview.MediaType.ShouldBe(PlexMediaType.PhotoAlbum);
        folderPreview.Title.ShouldBe("Trips");
        folderPreview.Size.ShouldBe(media.Size);
        folderPreview.ChildCount.ShouldBe(1);
        folderPreview.Children.Single().Title.ShouldBe(media.OriginalFilename.GetFileName());
    }

    [Test]
    public async Task ShouldExpandAlbumAndDirectSelectionToOnePhysicalFilePreview()
    {
        // Arrange
        await SetupDatabase(
            62403,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var album = await dbContext.PlexPhotoAlbums.SingleAsync(CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias =
                [
                    CreatePhotoSelection(PhotoItemKind.Album, (album.PlexServerId, album.PlexLibraryId), [album.Id]),
                    CreatePhotoSelection(PhotoItemKind.Asset, (media.PlexServerId, media.PlexLibraryId), [media.Id]),
                ],
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var root = result.Value.Single();
        root.Size.ShouldBe(media.Size);
        root.ChildCount.ShouldBe(1);
        root.Children.Select(x => (x.Id, x.Title, x.MediaType, x.Size))
            .ShouldBe([(media.Id, media.OriginalFilename.GetFileName(), PlexMediaType.PhotoImage, media.Size)]);
        root.Children.Single().Children.ShouldBeEmpty();
        root.Children.Single().Qualities.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldRejectInheritedKeepCompletedConflict_BeforeBuildingPreview()
    {
        // Arrange
        await SetupDatabase(
            62404,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var albums = await dbContext.PlexPhotoAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).FirstAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.Add(
            new PlexPhotoAlbumMembership { PlexPhotoAlbumId = albums[1].Id, PlexPhotoMediaDataId = media.Id }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var query = new GetPhotoDownloadPreviewCommand(
            new DownloadPreviewRequest
            {
                DownloadMedias = albums
                    .Select(
                        (album, index) =>
                            CreatePhotoSelection(
                                PhotoItemKind.Album,
                                (album.PlexServerId, album.PlexLibraryId),
                                [album.Id],
                                index == 0
                            )
                    )
                    .ToList(),
            }
        );

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(query);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.ValueOrDefault.ShouldBeNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldMatchCreatedPathsAndBytes_WhenAlbumsOverlapAcrossSourceRoots(bool reverseSelections)
    {
        // Arrange
        await SetupDatabase(
            62405,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 2;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.AsTracking().SingleAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "Home Plex"), CancellationToken);
        library.Title = "Photos";
        var albums = await dbContext.PlexPhotoAlbums.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        media.Count.ShouldBe(2);
        var originalRoot = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var secondRoot = new PlexPhotoFolder
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            ParentId = null,
            RelativePath = string.Empty,
            Title = "Second source",
            RootIdentityKey = "fixture:second-source",
            SourceRootPath = "/second-source",
            IsActive = true,
        };
        var trips = new PlexPhotoFolder
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            Parent = secondRoot,
            RelativePath = "Trips",
            Title = "Trips",
        };
        var day = new PlexPhotoFolder
        {
            PlexServerId = server.Id,
            PlexLibraryId = library.Id,
            Parent = trips,
            RelativePath = "Trips/Day 2",
            Title = "Day 2",
        };
        dbContext.PlexPhotoFolders.AddRange(secondRoot, trips, day);
        media[0].PlexPhotoFolder = day;
        dbContext.PlexPhotoAlbumMemberships.Add(
            new PlexPhotoAlbumMembership { PlexPhotoAlbumId = albums[1].Id, PlexPhotoMediaDataId = media[0].Id }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var selections = albums
            .Select(
                (album, index) =>
                    CreatePhotoSelection(PhotoItemKind.Album, (server.Id, library.Id), [album.Id], index == 0)
            )
            .ToList();
        selections.Add(CreatePhotoSelection(PhotoItemKind.Asset, (server.Id, library.Id), [media[0].Id]));
        if (reverseSelections)
            selections.Reverse();
        var creationCommand = new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest(selections));
        var validation = await new GenerateDownloadTaskPhotoImagesCommandValidator().ValidateAsync(
            creationCommand,
            CancellationToken
        );
        validation.IsValid.ShouldBeTrue();
        var creator = Mock.Create<GenerateDownloadTaskPhotoImagesCommandHandler>();

        // Act
        var preview = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(
            new(new DownloadPreviewRequest { DownloadMedias = selections })
        );
        var created = await creator.ExecuteAsync(creationCommand, CancellationToken);

        // Assert
        preview.IsSuccess.ShouldBeTrue();
        preview.Errors.Count.ShouldBe(0);
        created.IsSuccess.ShouldBeTrue();
        created.Errors.Count.ShouldBe(0);
        created.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 5, PhotoImages = 2 });
        var root = preview.Value.Single();
        root.Title.ShouldBe("Home Plex - Photos");
        root.Size.ShouldBe(media.Sum(x => x.Size));
        root.ChildCount.ShouldBe(2);
        var leaves = new List<(int Id, string Path, long Size)>();
        void CollectFiles(DownloadPreview node, string parentPath)
        {
            var path = string.IsNullOrEmpty(parentPath) ? node.Title : $"{parentPath}/{node.Title}";
            if (node.MediaType == PlexMediaType.PhotoImage)
                leaves.Add((node.Id, path, node.Size));
            foreach (var child in node.Children)
                CollectFiles(child, path);
        }
        CollectFiles(root, "");
        var expected = new[]
        {
            (
                media[0].Id,
                $"Home Plex - Photos/root-{secondRoot.Id}/Trips/Day 2/{media[0].OriginalFilename.GetFileName()}",
                media[0].Size
            ),
            (
                media[1].Id,
                $"Home Plex - Photos/root-{originalRoot.Id}/{media[1].OriginalFilename.GetFileName()}",
                media[1].Size
            ),
        };
        leaves.OrderBy(x => x.Id).ShouldBe(expected.OrderBy(x => x.Id));
        var queued = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.PlexApiRatingKey)
            .ToListAsync(CancellationToken);
        queued
            .Select(x => ($"{x.DirectoryMeta.PhotoFolder}/{x.FileName}", x.DataTotal))
            .OrderBy(x => x.Item1)
            .ShouldBe(expected.OrderBy(x => x.Item2).Select(x => (x.Item2, x.Item3)));
        queued.Select(x => x.DirectoryMeta.KeepCompletedInDownloadFolder).ShouldBe([false, false]);
        var folders = await dbContext.DownloadTaskPhotoFolders.ToListAsync(CancellationToken);
        folders
            .OrderBy(x => x.RelativePath)
            .Select(x =>
                (
                    x.RelativePath,
                    ParentPath: x.ParentId is Guid parentId
                        ? folders.Single(parent => parent.Id == parentId).RelativePath
                        : null
                )
            )
            .ShouldBe(
                new[]
                {
                    ("", (string?)null),
                    ($"root-{originalRoot.Id}", ""),
                    ($"root-{secondRoot.Id}", ""),
                    ($"root-{secondRoot.Id}/Trips", $"root-{secondRoot.Id}"),
                    ($"root-{secondRoot.Id}/Trips/Day 2", $"root-{secondRoot.Id}/Trips"),
                }.OrderBy(x => x.Item1)
            );
        var logs = await dbContext.DownloadTaskPhotoImageFileLogs.ToListAsync(CancellationToken);
        logs.Select(x => x.DownloadTaskFileId).Order().ShouldBe(queued.Select(x => x.Id).Order());
    }

    [Test]
    [Arguments(false, "CON.jpg")]
    [Arguments(true, "CON.jpg")]
    [Arguments(false, "back\\slash.jpg")]
    [Arguments(true, "back\\slash.jpg")]
    public async Task ShouldReuseCapturedRootAndExcludeQueuedOrConflictingFiles_WithoutChangingTheQueue(
        bool queueCollisionOwner,
        string invalidFileName
    )
    {
        // Arrange
        await SetupDatabase(
            62406,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 3;
            }
        );
        var dbContext = IDbContext;
        var server = await dbContext.PlexServers.AsTracking().SingleAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        await dbContext
            .PlexServers.Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "Home Plex"), CancellationToken);
        library.Title = "Photos";
        await dbContext.SaveChangesAsync(CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        media.Count.ShouldBe(3);
        var firstSelection = CreatePhotoSelection(
            PhotoItemKind.Asset,
            (server.Id, library.Id),
            queueCollisionOwner ? [media[0].Id, media[1].Id] : [media[0].Id]
        );
        var initialCommand = new GenerateDownloadTaskPhotoImagesCommand([firstSelection]);
        (
            await new GenerateDownloadTaskPhotoImagesCommandValidator().ValidateAsync(initialCommand, CancellationToken)
        ).IsValid.ShouldBeTrue();
        var initial = await Mock.Create<GenerateDownloadTaskPhotoImagesCommandHandler>()
            .ExecuteAsync(initialCommand, CancellationToken);
        initial.IsSuccess.ShouldBeTrue();
        initial.Errors.Count.ShouldBe(0);
        initial.Value.ShouldBe(
            new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = queueCollisionOwner ? 2 : 1 }
        );
        var queued = await dbContext.DownloadTaskPhotoImageFiles.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var beforeFiles = queued
            .Select(x => (x.Id, x.PlexApiRatingKey, x.DirectoryMeta.PhotoFolder, x.DataTotal, x.DownloadStatus))
            .ToArray();
        var queueRoot = await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken);
        queueRoot.Title.ShouldBe("Home Plex - Photos");
        var beforeRoot = (
            queueRoot.Id,
            queueRoot.Title,
            queueRoot.DownloadStatus,
            queueRoot.RelativePath,
            queueRoot.DownloadRootPath,
            queueRoot.DestinationRootPath
        );
        var originalLogs = await dbContext
            .DownloadTaskPhotoImageFileLogs.Select(x => x.Id)
            .ToListAsync(CancellationToken);
        library.Title = "Renamed library";
        PlexPhotoMediaData AdditionalFile(int offset, string filename) =>
            new()
            {
                PlexServerId = server.Id,
                PlexLibraryId = library.Id,
                PlexApiRatingKey = media[1].PlexApiRatingKey + offset,
                PlexApiMediaId = media[1].PlexApiMediaId + offset,
                PlexApiPartId = media[1].PlexApiPartId + offset,
                Title = filename,
                SearchTitle = filename.ToSearchTitle(),
                HasThumb = false,
                AddedAt = media[1].AddedAt,
                OriginalFilename = filename,
                Key = $"/library/parts/{media[1].PlexApiPartId + offset}/file",
                Size = media[1].Size,
                Container = media[1].Container,
                PlexPhotoFolderId = media[1].PlexPhotoFolderId,
            };
        var collision = AdditionalFile(10000, media[1].OriginalFilename);
        var invalid = AdditionalFile(20000, invalidFileName);
        dbContext.PlexPhotoMediaData.AddRange(collision, invalid);
        await dbContext.SaveChangesAsync(CancellationToken);
        var selection = firstSelection with { MediaIds = [.. media.Select(x => x.Id), collision.Id, invalid.Id] };

        // Act
        var result = await TestHandlerExecuteAsync<IEnumerable<DownloadPreview>>(
            new(new DownloadPreviewRequest { DownloadMedias = [selection] })
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var root = result.Value.Single();
        root.Title.ShouldBe("Home Plex - Photos");
        root.Size.ShouldBe(media[2].Size);
        root.ChildCount.ShouldBe(1);
        root.Children.Select(x => (x.Id, x.Title, x.Size, x.MediaType))
            .ShouldBe([
                (media[2].Id, media[2].OriginalFilename.GetFileName(), media[2].Size, PlexMediaType.PhotoImage),
            ]);
        var afterFiles = await dbContext.DownloadTaskPhotoImageFiles.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        afterFiles
            .Select(x => (x.Id, x.PlexApiRatingKey, x.DirectoryMeta.PhotoFolder, x.DataTotal, x.DownloadStatus))
            .ShouldBe(beforeFiles);
        var afterFolders = await dbContext.DownloadTaskPhotoFolders.ToListAsync(CancellationToken);
        afterFolders
            .Select(x => (x.Id, x.Title, x.DownloadStatus, x.RelativePath, x.DownloadRootPath, x.DestinationRootPath))
            .ShouldBe([beforeRoot]);
        (await dbContext.DownloadTaskPhotoImageFileLogs.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(originalLogs.Order());
    }

    private static DownloadMediaDTO CreatePhotoSelection(
        PhotoItemKind selectionKind,
        (int ServerId, int LibraryId) scope,
        IEnumerable<int> mediaIds,
        bool keepCompleted = false
    ) =>
        new()
        {
            Type = selectionKind == PhotoItemKind.Album ? PlexMediaType.PhotoAlbum : PlexMediaType.PhotoImage,
            SelectionKind = selectionKind,
            PlexServerId = scope.ServerId,
            PlexLibraryId = scope.LibraryId,
            MediaIds = mediaIds.ToList(),
            Qualities = [],
            KeepCompletedInDownloadFolder = keepCompleted,
        };
}
