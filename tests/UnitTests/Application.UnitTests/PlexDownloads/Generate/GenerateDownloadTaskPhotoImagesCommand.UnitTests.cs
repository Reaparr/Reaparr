using Reaparr.Data;

namespace Reaparr.Application.UnitTests;

public class GenerateDownloadTaskPhotoImagesCommandUnitTests
    : BaseCommandUnitTest<GenerateDownloadTaskPhotoImagesCommand>
{
    [Test]
    public async Task ShouldQueueOneFolderAndOneFile_WhenDirectMergedAssetIsSelected()
    {
        // Arrange
        await SetupDatabase(
            62304,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var destination = "/destination";
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            PlexServerId = media.PlexServerId,
            PlexLibraryId = media.PlexLibraryId,
            MediaIds = [media.Id],
            Qualities = [],
            KeepCompletedInDownloadFolder = true,
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest([selection], customDestinationFolderPath: destination)
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 1 });
        var folder = await dbContext.DownloadTaskPhotoFolders
            .Include(x => x.Files)
            .SingleAsync(CancellationToken);
        var file = folder.Files.Single();
        folder.ParentId.ShouldBeNull();
        folder.PlexServerId.ShouldBe(media.PlexServerId);
        folder.PlexLibraryId.ShouldBe(media.PlexLibraryId);
        folder.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        file.ParentId.ShouldBe(folder.Id);
        file.PlexApiRatingKey.ShouldBe(media.PlexApiRatingKey);
        file.PlexApiMediaId.ShouldBe(media.PlexApiMediaId);
        file.PlexApiPartId.ShouldBe(media.PlexApiPartId);
        file.AssetKind.ShouldBe(media.AssetKind);
        file.DataTotal.ShouldBe(media.Size);
        file.FileName.ShouldBe(media.OriginalFilename.GetFileName());
        file.FileLocationUrl.ShouldBe(media.Key);
        file.DirectoryMeta.DestinationRootPath.ShouldBe(destination);
        file.DirectoryMeta.PhotoFolder.ShouldStartWith(folder.Title);
        file.DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeTrue();
        var logs = await dbContext.DownloadTaskPhotoImageFileLogs.ToListAsync(CancellationToken);
        logs.Count.ShouldBe(1);
        logs.Single().DownloadTaskFileId.ShouldBe(file.Id);
        logs.Single().Status.ShouldBe(DownloadStatus.Queued);
    }

    [Test]
    public async Task ShouldQueueUnmappedUnalbumedAssetAtLibraryRoot_WithoutCreatingCatalogFolders()
    {
        // Arrange
        await SetupDatabase(
            62319,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.AsTracking().SingleAsync(CancellationToken);
        var server = await dbContext.PlexServers.AsTracking().SingleAsync(CancellationToken);
        var library = await dbContext.PlexLibraries.AsTracking().SingleAsync(CancellationToken);
        await dbContext.PlexServers.Where(x => x.Id == server.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Name, "Home Plex"), CancellationToken);
        library.Title = "Photos";
        media.PlexPhotoFolderId = null;
        await dbContext.SaveChangesAsync(CancellationToken);
        media.PlexPhotoFolderId.ShouldBeNull();
        var expectedRootName = "Home Plex - Photos";
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            PlexServerId = media.PlexServerId,
            PlexLibraryId = media.PlexLibraryId,
            MediaIds = [media.Id],
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/unmapped-target")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 1 });
        (await dbContext.PlexPhotoAlbumMemberships.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.PlexPhotoFolders.Select(x => x.RelativePath).ToListAsync(CancellationToken))
            .ShouldBe([""]);
        var folder = await dbContext.DownloadTaskPhotoFolders.Include(x => x.Files).SingleAsync(CancellationToken);
        folder.ParentId.ShouldBeNull();
        folder.RelativePath.ShouldBeEmpty();
        folder.Title.ShouldBe(expectedRootName);
        folder.Files.Count.ShouldBe(1);
        var file = folder.Files.Single();
        file.ParentId.ShouldBe(folder.Id);
        file.FileName.ShouldBe(media.OriginalFilename.GetFileName());
        file.DirectoryMeta.PhotoFolder.ShouldBe(expectedRootName);
    }

    [Test]
    public async Task ShouldQueueMappedAndUnmappedAssetsAtLibraryRoot_WhenAllCatalogRootsAreInactive()
    {
        // Arrange
        await SetupDatabase(
            62320,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var root = await dbContext.PlexPhotoFolders.AsTracking().SingleAsync(x => x.ParentId == null, CancellationToken);
        var files = await dbContext.PlexPhotoMediaData.AsTracking().OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var inactiveChild = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Old",
            Title = "Old",
        };
        dbContext.PlexPhotoFolders.Add(inactiveChild);
        await dbContext.SaveChangesAsync(CancellationToken);
        root.IsActive = false;
        files[0].PlexPhotoFolderId = inactiveChild.Id;
        files[1].PlexPhotoFolderId = null;
        await dbContext.SaveChangesAsync(CancellationToken);
        files[0].PlexPhotoFolderId.ShouldBe(inactiveChild.Id);
        files[1].PlexPhotoFolderId.ShouldBeNull();
        (await dbContext.PlexPhotoFolders.CountAsync(x => x.ParentId == null && x.IsActive == true, CancellationToken))
            .ShouldBe(0);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            PlexServerId = files[0].PlexServerId,
            PlexLibraryId = files[0].PlexLibraryId,
            MediaIds = files.Select(x => x.Id).ToList(),
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/inactive-target")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 2 });
        (await dbContext.PlexPhotoFolders.Select(x => x.RelativePath).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(["", "Old"]);
        var folder = await dbContext.DownloadTaskPhotoFolders.Include(x => x.Files).SingleAsync(CancellationToken);
        folder.RelativePath.ShouldBeEmpty();
        folder.Files.Select(x => (x.PlexApiPartId, x.DirectoryMeta.PhotoFolder, x.FileName))
            .OrderBy(x => x.PlexApiPartId)
            .ShouldBe(
                files.Select(x => (x.PlexApiPartId, folder.Title, x.OriginalFilename.GetFileName()))
                    .OrderBy(x => x.PlexApiPartId)
            );
    }

    [Test]
    public async Task ShouldQueueMappedAndUnmappedAlbumMembersAtTheirExpectedPaths()
    {
        // Arrange
        var (album, child, files) = await CreateMappedAndUnmappedAlbumAsync();
        var dbContext = IDbContext;
        files[0].PlexPhotoFolderId.ShouldBe(child.Id);
        files[1].PlexPhotoFolderId.ShouldBeNull();
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoAlbum,
            SelectionKind = PhotoItemKind.Album,
            PlexServerId = album.PlexServerId,
            PlexLibraryId = album.PlexLibraryId,
            MediaIds = [album.Id],
            Qualities = [],
        };
        (await dbContext.PlexPhotoAlbumMemberships.CountAsync(x => x.PlexPhotoAlbumId == album.Id, CancellationToken))
            .ShouldBe(2);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/album-target")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 2, PhotoImages = 2 });
        (await dbContext.PlexPhotoFolders.Select(x => x.RelativePath).OrderBy(x => x).ToListAsync(CancellationToken))
            .ShouldBe(["", "Trips"]);
        var taskFolders = await dbContext.DownloadTaskPhotoFolders.Include(x => x.Files).ToListAsync(CancellationToken);
        taskFolders.Select(x => x.RelativePath).OrderBy(x => x).ShouldBe(["", "Trips"]);
        var expectedPaths = new[]
        {
            ($"Home Plex - Photos/Trips/{files[0].OriginalFilename.GetFileName()}", files[0].Size),
            ($"Home Plex - Photos/{files[1].OriginalFilename.GetFileName()}", files[1].Size),
        };
        taskFolders.SelectMany(x => x.Files)
            .Select(x => ($"{x.DirectoryMeta.PhotoFolder}/{x.FileName}", x.DataTotal))
            .OrderBy(x => x.Item1)
            .ShouldBe(expectedPaths.OrderBy(x => x.Item1));
    }

    [Test]
    public async Task ShouldRejectMissingOrForeignDirectFileIds_WithoutCreatingPartialTasks()
    {
        // Arrange
        await SetupDatabase(
            62316,
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
        var command = new GenerateDownloadTaskPhotoImagesCommand(
            new CreateDownloadTasksRequest(
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.PhotoImage,
                        SelectionKind = PhotoItemKind.Asset,
                        PlexServerId = target.PlexServerId,
                        PlexLibraryId = target.PlexLibraryId,
                        MediaIds = [target.Id, foreign.Id, int.MaxValue],
                        Qualities = [],
                    },
                ]
            )
        );

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(command);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        result.ValueOrDefault.ShouldBeNull();
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldExpandAlbumMembershipsAndDeduplicateOverlap_WhenAlbumAndFileAreSelected()
    {
        // Arrange
        await SetupDatabase(
            62305,
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
        var files = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        dbContext.PlexPhotoAlbumMemberships.Add(
            new PlexPhotoAlbumMembership { PlexPhotoAlbumId = albums[1].Id, PlexPhotoMediaDataId = files[0].Id }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var selections = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.PhotoAlbum,
                SelectionKind = PhotoItemKind.Album,
                PlexServerId = albums[0].PlexServerId,
                PlexLibraryId = albums[0].PlexLibraryId,
                MediaIds = [albums[0].Id, albums[1].Id],
                Qualities = [],
            },
            new()
            {
                Type = PlexMediaType.PhotoImage,
                SelectionKind = PhotoItemKind.Asset,
                PlexServerId = files[0].PlexServerId,
                PlexLibraryId = files[0].PlexLibraryId,
                MediaIds = [files[0].Id],
                Qualities = [],
                KeepCompletedInDownloadFolder = true,
            },
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest(selections))
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.PhotoImages.ShouldBe(2);
        var queuedFiles = await dbContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken);
        queuedFiles.Select(x => x.PlexApiRatingKey).Order().ShouldBe(files.Select(x => x.PlexApiRatingKey).Order());
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(2);
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(1);
        queuedFiles.Single(x => x.PlexApiRatingKey == files[0].PlexApiRatingKey)
            .DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeTrue();
        queuedFiles.Single(x => x.PlexApiRatingKey == files[1].PlexApiRatingKey)
            .DirectoryMeta.KeepCompletedInDownloadFolder.ShouldBeFalse();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ShouldResolveOverlappingAlbumOptions_AccordingToDirectSelectionPrecedence(
        bool includeDirectSelection, bool reverseSelections)
    {
        var (selections, files) = await CreateOverlappingAlbumSelectionsAsync(includeDirectSelection);
        var dbContext = IDbContext;
        if (reverseSelections)
            selections.Reverse();
        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(new CreateDownloadTasksRequest(selections))
        );

        // Assert
        if (includeDirectSelection)
        {
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = files.Count });
            var queued = await dbContext.DownloadTaskPhotoImageFiles.OrderBy(x => x.PlexApiRatingKey)
                .ToListAsync(CancellationToken);
            queued.Select(x => (x.PlexApiRatingKey, x.DirectoryMeta.KeepCompletedInDownloadFolder))
                .ShouldBe(files.OrderBy(x => x.PlexApiRatingKey).Select(x => (x.PlexApiRatingKey, false)));
            var folder = await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken);
            queued.Select(x => x.ParentId).Distinct().ShouldBe([folder.Id]);
            var logs = await dbContext.DownloadTaskPhotoImageFileLogs.ToListAsync(CancellationToken);
            logs.Select(x => x.DownloadTaskFileId).Order().ShouldBe(queued.Select(x => x.Id).Order());
        }
        else
        {
            result.IsFailed.ShouldBeTrue();
            result.Errors.Count.ShouldBe(1);
            (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
            (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
            (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
        }
    }

    [Test]
    public async Task ShouldRejectPhotoQualitySelectors_WithoutAffectingOtherFamilyValidation()
    {
        // Arrange
        var validator = new GenerateDownloadTaskPhotoImagesCommandValidator();
        var command = new GenerateDownloadTaskPhotoImagesCommand(
            new CreateDownloadTasksRequest(
                [
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.PhotoImage,
                        SelectionKind = PhotoItemKind.Asset,
                        PlexServerId = 1,
                        PlexLibraryId = 2,
                        MediaIds = [3],
                        Qualities =
                        [
                            new PlexMediaQualityDTO
                            {
                                MediaId = 3,
                                DataId = 4,
                                MediaDataType = PlexMediaType.PhotoImage,
                                VideoQuality = VideoQuality.Unknown,
                            },
                        ],
                    },
                    new DownloadMediaDTO
                    {
                        Type = PlexMediaType.Movie,
                        SelectionKind = null,
                        PlexServerId = 1,
                        PlexLibraryId = 2,
                        MediaIds = [5],
                        Qualities = [],
                    },
                ]
            )
        );

        // Act
        var result = await validator.ValidateAsync(command, CancellationToken);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors.Single().ErrorMessage.ShouldBe("Photo selections must not include quality or part selectors.");
    }

    [Test]
    public async Task ShouldReuseCapturedRootAndSkipExactDuplicate_WhenRequestIsRetried()
    {
        // Arrange
        await SetupDatabase(
            62307,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var media = await IDbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest(
            [
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    SelectionKind = PhotoItemKind.Asset,
                    PlexServerId = media.PlexServerId,
                    PlexLibraryId = media.PlexLibraryId,
                    MediaIds = [media.Id],
                    Qualities = [],
                },
            ],
            customDestinationFolderPath: "/retry-target"
        );

        // Act
        var first = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(request)
        );
        var rootTitle = (await IDbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).Title;
        var second = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(request)
        );

        // Assert
        first.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 1 });
        second.Value.ShouldBe(new DownloadTaskCreationReport());
        (await IDbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(1);
        (await IDbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(1);
        (await IDbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(1);
        (await IDbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).Title.ShouldBe(rootTitle);
    }

    [Test]
    public async Task ShouldDeduplicateEquivalentDestinationRoots_WhenRequestIsRetried()
    {
        // Arrange
        await SetupDatabase(
            62313,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var downloadFolder = await dbContext.FolderPaths.AsTracking().SingleAsync(
            x => x.Id == FolderTypeDefaults.DefaultDownloadFolderId,
            CancellationToken
        );
        downloadFolder.DirectoryPath = "/equivalent-downloads/";
        await dbContext.SaveChangesAsync(CancellationToken);

        async Task<Result<DownloadTaskCreationReport>> Queue(string destination)
        {
            return await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
                new GenerateDownloadTaskPhotoImagesCommand(
                    new CreateDownloadTasksRequest(
                        [
                            new DownloadMediaDTO
                            {
                                Type = PlexMediaType.PhotoImage,
                                SelectionKind = PhotoItemKind.Asset,
                                PlexServerId = media.PlexServerId,
                                PlexLibraryId = media.PlexLibraryId,
                                MediaIds = [media.Id],
                                Qualities = [],
                            },
                        ],
                        customDestinationFolderPath: destination
                    )
                )
            );
        }

        // Act
        var first = await Queue("/equivalent-target/");
        downloadFolder.DirectoryPath = "/equivalent-downloads/./";
        await dbContext.SaveChangesAsync(CancellationToken);
        var second = await Queue("/equivalent-target/./");

        // Assert
        first.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 1 });
        second.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(1);
        var file = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        file.DirectoryMeta.DestinationRootPath.ShouldBe(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath("/equivalent-target"))
        );
        file.DirectoryMeta.DownloadRootPath.ShouldBe(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath("/equivalent-downloads"))
        );
    }

    [Test]
    public async Task ShouldNamespaceFilesByCatalogRootId_WhenMultipleActiveRootsShareALibrary()
    {
        // Arrange
        await SetupDatabase(
            62308,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var sourceRoot = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var media = await dbContext.PlexPhotoMediaData.AsTracking().SingleAsync(CancellationToken);
        var secondRoot = new PlexPhotoFolder
        {
            PlexServerId = sourceRoot.PlexServerId,
            PlexLibraryId = sourceRoot.PlexLibraryId,
            ParentId = null,
            RelativePath = string.Empty,
            Title = "Second root",
            RootIdentityKey = "fixture:second-root",
            SourceRootPath = "/fixture/photos-second",
            PlexApiLocationId = 2,
            IsActive = true,
        };
        dbContext.PlexPhotoFolders.Add(secondRoot);
        await dbContext.SaveChangesAsync(CancellationToken);
        media.PlexPhotoFolderId = secondRoot.Id;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest(
                    [
                        new DownloadMediaDTO
                        {
                            Type = PlexMediaType.PhotoImage,
                            SelectionKind = PhotoItemKind.Asset,
                            PlexServerId = media.PlexServerId,
                            PlexLibraryId = media.PlexLibraryId,
                            MediaIds = [media.Id],
                            Qualities = [],
                        },
                    ],
                    customDestinationFolderPath: "/roots-target"
                )
            )
        );

        // Assert
        result.Value.PhotoFolders.ShouldBe(2);
        result.Value.PhotoImages.ShouldBe(1);
        var file = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        file.DirectoryMeta.PhotoFolder.ShouldContain($"root-{secondRoot.Id}");
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(2);
    }

    [Test]
    public async Task ShouldExcludeEveryNewCandidate_WhenDistinctFilesCollideAtOnePath()
    {
        // Arrange
        await SetupDatabase(
            62309,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var duplicate = new PlexPhotoMediaData
        {
            PlexServerId = media.PlexServerId,
            PlexLibraryId = media.PlexLibraryId,
            PlexApiRatingKey = media.PlexApiRatingKey + 1,
            PlexApiMediaId = media.PlexApiMediaId + 1,
            PlexApiPartId = media.PlexApiPartId + 1,
            AssetKind = media.AssetKind,
            Title = $"{media.Title} duplicate",
            SearchTitle = $"{media.Title} duplicate".ToSearchTitle(),
            Summary = media.Summary,
            HasThumb = media.HasThumb,
            AddedAt = media.AddedAt,
            UpdatedAt = media.UpdatedAt,
            OriginallyAvailableAt = media.OriginallyAvailableAt,
            SourcePath = $"/fixture/photos/duplicate-{media.Id}.jpg",
            OriginalFilename = media.OriginalFilename,
            Key = $"{media.Key}-duplicate",
            Size = media.Size,
            Container = media.Container,
            Width = media.Width,
            Height = media.Height,
            Duration = media.Duration,
            VideoCodec = media.VideoCodec,
            AudioCodec = media.AudioCodec,
            PlexPhotoFolderId = media.PlexPhotoFolderId,
        };
        dbContext.PlexPhotoMediaData.Add(duplicate);
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest(
                    [
                        new DownloadMediaDTO
                        {
                            Type = PlexMediaType.PhotoImage,
                            SelectionKind = PhotoItemKind.Asset,
                            PlexServerId = media.PlexServerId,
                            PlexLibraryId = media.PlexLibraryId,
                            MediaIds = [media.Id, duplicate.Id],
                            Qualities = [],
                        },
                    ],
                    customDestinationFolderPath: "/collision-target"
                )
            )
        );

        // Assert
        result.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await dbContext.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldHonorHostFilesystemCaseSemantics_WhenPhotoNamesDifferOnlyByCase()
    {
        // Arrange
        await SetupDatabase(
            62315,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var files = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        await dbContext.PlexPhotoMediaData.Where(x => x.Id == files[0].Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OriginalFilename, "a.jpg"), CancellationToken);
        await dbContext.PlexPhotoMediaData.Where(x => x.Id == files[1].Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OriginalFilename, "A.jpg"), CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            PlexServerId = files[0].PlexServerId,
            PlexLibraryId = files[0].PlexLibraryId,
            MediaIds = files.Select(x => x.Id).ToList(),
            Qualities = [],
        };

        // Act
        var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(
                new CreateDownloadTasksRequest([selection], customDestinationFolderPath: "/case-target")
            )
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        if (OperatingSystem.IsWindows())
        {
            result.Value.ShouldBe(new DownloadTaskCreationReport());
            (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
            (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        }
        else
        {
            result.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 2 });
            (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.FileName)
                .ToListAsync(CancellationToken))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ShouldBe(["A.jpg", "a.jpg"]);
        }
    }

    [Test]
    public async Task ShouldRetainCapturedTaskPath_WhenCatalogFolderChanges()
    {
        // Arrange
        await SetupDatabase(
            62310,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest(
            [
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    SelectionKind = PhotoItemKind.Asset,
                    PlexServerId = media.PlexServerId,
                    PlexLibraryId = media.PlexLibraryId,
                    MediaIds = [media.Id],
                    Qualities = [],
                },
            ],
            customDestinationFolderPath: "/captured-target"
        );
        var first = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(request)
        );
        var capturedPath = (await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken))
            .DirectoryMeta.PhotoFolder;
        var root = await dbContext.PlexPhotoFolders.SingleAsync(x => x.ParentId == null, CancellationToken);
        var movedFolder = new PlexPhotoFolder
        {
            PlexServerId = root.PlexServerId,
            PlexLibraryId = root.PlexLibraryId,
            ParentId = root.Id,
            RelativePath = "Moved",
            Title = "Moved",
        };
        dbContext.PlexPhotoFolders.Add(movedFolder);
        await dbContext.SaveChangesAsync(CancellationToken);
        media.PlexPhotoFolderId = movedFolder.Id;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var second = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(request)
        );

        // Assert
        first.Value.ShouldBe(new DownloadTaskCreationReport { PhotoFolders = 1, PhotoImages = 1 });
        second.Value.ShouldBe(new DownloadTaskCreationReport());
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken))
            .DirectoryMeta.PhotoFolder.ShouldBe(capturedPath);
    }

    [Test]
    public async Task ShouldSuffixRootName_WhenEqualNamedLibrariesAreQueuedWithDifferentDownloadRoots()
    {
        // Arrange
        await SetupDatabase(
            62311,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 2;
                config.PhotoAlbumCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.AsTracking().Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        libraries.Count.ShouldBe(2);
        libraries[0].Title = libraries[1].Title = "Shared Photos";
        await dbContext.SaveChangesAsync(CancellationToken);
        var files = await dbContext.PlexPhotoMediaData.OrderBy(x => x.PlexLibraryId).ToListAsync(CancellationToken);
        var destination = "/equal-name-target";

        async Task<DownloadTaskCreationReport> Queue(PlexPhotoMediaData file)
        {
            var result = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
                new GenerateDownloadTaskPhotoImagesCommand(
                    new CreateDownloadTasksRequest(
                        [
                            new DownloadMediaDTO
                            {
                                Type = PlexMediaType.PhotoImage,
                                SelectionKind = PhotoItemKind.Asset,
                                PlexServerId = file.PlexServerId,
                                PlexLibraryId = file.PlexLibraryId,
                                MediaIds = [file.Id],
                                Qualities = [],
                            },
                        ],
                        customDestinationFolderPath: destination
                    )
                )
            );
            return result.Value;
        }

        // Act
        var first = await Queue(files[0]);
        var downloadFolder = await dbContext.FolderPaths.AsTracking().SingleAsync(
            x => x.Id == FolderTypeDefaults.DefaultDownloadFolderId,
            CancellationToken
        );
        downloadFolder.DirectoryPath = "/fixture/downloads/different";
        await dbContext.SaveChangesAsync(CancellationToken);

        var second = await Queue(files[^1]);

        // Assert
        first.PhotoFolders.ShouldBe(1);
        second.PhotoFolders.ShouldBe(1);
        var roots = await dbContext
            .DownloadTaskPhotoFolders.Where(x => x.ParentId == null)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(CancellationToken);
        roots.Count.ShouldBe(2);
        roots[0].Title.ShouldNotBe(roots[1].Title);
        roots[1].Title.ShouldContain($"[{files[^1].PlexServerId}-{files[^1].PlexLibraryId}]");
        roots[0].DestinationRootPath.ShouldBe(roots[1].DestinationRootPath);
        roots[0].DownloadRootPath.ShouldNotBe(roots[1].DownloadRootPath);
    }

    [Test]
    public async Task ShouldCreateNewContext_WhenDownloadRootChanges()
    {
        // Arrange
        await SetupDatabase(
            62312,
            config =>
            {
                config.PlexServerCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoCount = 1;
            }
        );
        var dbContext = IDbContext;
        var media = await dbContext.PlexPhotoMediaData.SingleAsync(CancellationToken);
        var request = new CreateDownloadTasksRequest(
            [
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    SelectionKind = PhotoItemKind.Asset,
                    PlexServerId = media.PlexServerId,
                    PlexLibraryId = media.PlexLibraryId,
                    MediaIds = [media.Id],
                    Qualities = [],
                },
            ],
            customDestinationFolderPath: "/context-target"
        );
        var first = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(request)
        );
        var downloadFolder = await dbContext.FolderPaths.AsTracking().SingleAsync(
            x => x.Id == FolderTypeDefaults.DefaultDownloadFolderId,
            CancellationToken
        );
        var firstDownloadRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(downloadFolder.DirectoryPath));
        downloadFolder.DirectoryPath = "/fixture/downloads/second";
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var second = await TestHandlerExecuteAsync<DownloadTaskCreationReport>(
            new GenerateDownloadTaskPhotoImagesCommand(request)
        );

        // Assert
        first.Value.PhotoImages.ShouldBe(1);
        second.Value.PhotoImages.ShouldBe(1);
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(2);
        (await dbContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(2);
        var directoryMetadata = await dbContext.DownloadTaskPhotoImageFiles
            .Select(x => x.DirectoryMeta).ToListAsync(CancellationToken);
        directoryMetadata.Select(x => x.DownloadRootPath).Order().ShouldBe(
            new[] { firstDownloadRoot, Path.TrimEndingDirectorySeparator(Path.GetFullPath("/fixture/downloads/second")) }.Order()
        );
    }

    private async Task<(PlexPhotoAlbum Album, PlexPhotoFolder Child, List<PlexPhotoMediaData> Files)> CreateMappedAndUnmappedAlbumAsync()
    {
        await SetupDatabase(
            62321,
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
        await dbContext.PlexServers.Where(x => x.Id == server.Id)
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
        return (album, child, files);
    }

    private async Task<(List<DownloadMediaDTO> Selections, List<PlexPhotoMediaData> Files)> CreateOverlappingAlbumSelectionsAsync(
        bool includeDirectSelection
    )
    {
        await SetupDatabase(
            62306,
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
        var files = await dbContext.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var file = files[0];
        dbContext.PlexPhotoAlbumMemberships.Add(
            new PlexPhotoAlbumMembership { PlexPhotoAlbumId = albums[1].Id, PlexPhotoMediaDataId = file.Id }
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var selections = albums
            .Select(
                (album, index) => new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoAlbum,
                    SelectionKind = PhotoItemKind.Album,
                    PlexServerId = album.PlexServerId,
                    PlexLibraryId = album.PlexLibraryId,
                    MediaIds = [album.Id],
                    Qualities = [],
                    KeepCompletedInDownloadFolder = index == 0,
                }
            )
            .ToList();
        if (includeDirectSelection)
            selections.Add(
                new DownloadMediaDTO
                {
                    Type = PlexMediaType.PhotoImage,
                    SelectionKind = PhotoItemKind.Asset,
                    PlexServerId = file.PlexServerId,
                    PlexLibraryId = file.PlexLibraryId,
                    MediaIds = [file.Id],
                    Qualities = [],
                    KeepCompletedInDownloadFolder = false,
                }
            );
        return (selections, files);
    }

}
