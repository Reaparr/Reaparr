namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTasksPhotoUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldResolvePhotoKeysAndDetailsThroughNestedFolders_WhenSelectingAnyLevel()
    {
        // Arrange
        var seed = await SetupDatabase(62401, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: true
        );
        var root = folders.Single(x => x.ParentId is null);
        var child = folders.Single(x => x.ParentId == root.Id);
        var rootFile = root.Files.Single();
        var childFile = child.Files.Single();
        var keys = new[] { root.ToKey(), child.ToKey(), rootFile.ToKey(), childFile.ToKey() };
        var expectedParentIds = new Dictionary<Guid, Guid>
        {
            [root.Id] = Guid.Empty,
            [child.Id] = root.Id,
            [rootFile.Id] = root.Id,
            [childFile.Id] = child.Id,
        };

        // Act
        var found = await dbContext.GetDownloadTaskKeysAsync(
            [root.Id, child.Id, rootFile.Id, childFile.Id, Guid.Empty],
            CancellationToken
        );

        // Assert
        found.OrderBy(x => x.Type).ThenBy(x => x.Id).ShouldBe(keys.OrderBy(x => x.Type).ThenBy(x => x.Id));
        foreach (var key in keys)
        {
            (await dbContext.GetDownloadTaskKeyAsync(key.Id, CancellationToken)).ShouldBe(key);
            (await dbContext.GetDownloadTaskTypeAsync(key.Id, CancellationToken)).ShouldBe(key.Type);
            (await dbContext.GetRootDownloadTaskKeyAsync(key, cancellationToken: CancellationToken)).ShouldBe(
                root.ToKey()
            );

            var inferred = await dbContext.GetDownloadTaskAsync(key.Id, cancellationToken: CancellationToken);
            inferred.ShouldNotBeNull();
            inferred.Id.ShouldBe(key.Id);
            inferred.DownloadTaskType.ShouldBe(key.Type);
            inferred.ParentId.ShouldBe(expectedParentIds[key.Id]);

            var expectedDownloadableKeys = key.Id switch
            {
                var id when id == root.Id => new[] { rootFile.ToKey(), childFile.ToKey() },
                var id when id == child.Id => new[] { childFile.ToKey() },
                _ => new[] { key },
            };
            (await dbContext.GetDownloadableChildTaskKeys(key, CancellationToken))
                .OrderBy(x => x.Id)
                .ShouldBe(expectedDownloadableKeys.OrderBy(x => x.Id));
        }

        root.ToParentKey().ShouldBeNull();
        child.ToParentKey().ShouldBe(root.ToKey());
        rootFile.ToParentKey().ShouldBe(root.ToKey());
        childFile.ToParentKey().ShouldBe(child.ToKey());
        DownloadTaskType.PhotoFolder.ToPlexMediaType().ShouldBe(PlexMediaType.PhotoAlbum);
        DownloadTaskType.PhotoImage.ToPlexMediaType().ShouldBe(PlexMediaType.PhotoImage);
        DownloadTaskType.PhotoFolder.ToDownloadTaskString().ToDownloadTaskType().ShouldBe(DownloadTaskType.PhotoFolder);
        DownloadTaskType.PhotoImage.ToDownloadTaskString().ToDownloadTaskType().ShouldBe(DownloadTaskType.PhotoImage);

        var detail = await dbContext.GetDownloadTaskAsync(root.ToKey(), CancellationToken);
        detail.ShouldNotBeNull();
        detail.ParentId.ShouldBe(Guid.Empty);
        detail.MediaType.ShouldBe(PlexMediaType.PhotoAlbum);
        detail.DownloadTaskType.ShouldBe(DownloadTaskType.PhotoFolder);
        detail.IsDownloadable.ShouldBeFalse();
        detail
            .Children.Select(x => (x.Id, x.ParentId, x.IsDownloadable))
            .OrderBy(x => x.Id)
            .ShouldBe(
                new (Guid Id, Guid ParentId, bool IsDownloadable)[]
                {
                    (child.Id, root.Id, false),
                    (rootFile.Id, root.Id, true),
                }.OrderBy(x => x.Id)
            );
        detail
            .Children.Single(x => x.Id == child.Id)
            .Children.Select(x => (x.Id, x.ParentId, x.IsDownloadable))
            .ShouldBe([(childFile.Id, child.Id, true)]);
        detail.Children.Single(x => x.Id == rootFile.Id).Children.ShouldBeEmpty();
        (
            await dbContext.GetAffectedRootDownloadTaskIdsAsync(
                [root.Id, child.Id, rootFile.Id, childFile.Id],
                CancellationToken
            )
        ).ShouldBe([root.Id]);
    }

    [Test]
    public async Task ShouldRetainDisabledPhotoLibraryGraph_WhenReadingTasksProgressStatusAndLogs()
    {
        // Arrange
        var seed = await SetupDatabase(
            62407,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexPhotoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        var targetFolders = await AddPhotoFolders(
            dbContext,
            libraries[0],
            seed,
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: true
        );
        var controlFolders = await AddPhotoFolders(
            dbContext,
            libraries[1],
            new Seed(seed.Next()),
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: false
        );
        var targetRoot = targetFolders.Single(x => x.ParentId is null);
        var targetChild = targetFolders.Single(x => x.ParentId == targetRoot.Id);
        var targetRootFile = targetRoot.Files.Single();
        var targetChildFile = targetChild.Files.Single();
        var controlRoot = controlFolders.Single(x => x.ParentId is null);
        var controlFile = controlRoot.Files.Single();

        foreach (var file in new[] { targetRootFile, targetChildFile })
        {
            file.DataTotal = 1000;
            file.DataReceived = 250;
            file.Percentage = 25;
            file.DownloadStatus = DownloadStatus.Downloading;
        }
        await dbContext.CreateDownloadClientLog(
            targetChildFile.ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Downloading,
            "disabled-photo"
        );
        await dbContext.CreateDownloadClientLog(
            controlFile.ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Queued,
            "enabled-control"
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var disabledCount = await dbContext
            .PlexLibraries.Where(x => x.Id == libraries[0].Id)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.IsEnabled, false), CancellationToken);
        disabledCount.ShouldBe(1);
        var disabledDbContext = IDbContext;
        (
            await disabledDbContext
                .PlexLibraries.IgnoreQueryFilters()
                .Where(x => x.Id == libraries[0].Id)
                .Select(x => x.IsEnabled)
                .SingleAsync(CancellationToken)
        ).ShouldBeFalse();

        var targetRootKey = targetRoot.ToKey();
        var targetChildKey = targetChild.ToKey();
        var targetRootFileKey = targetRootFile.ToKey();
        var targetChildFileKey = targetChildFile.ToKey();
        var controlRootKey = controlRoot.ToKey();
        var targetKeys = await disabledDbContext.GetDownloadTaskKeysAsync(
            [targetRoot.Id, targetChild.Id, targetRootFile.Id, targetChildFile.Id],
            CancellationToken
        );
        var targetDetails = await disabledDbContext.GetDownloadTaskAsync(targetRootKey, CancellationToken);
        var targetFileDetails = await disabledDbContext.GetDownloadTaskAsync(targetChildFileKey, CancellationToken);
        var targetTransfer = await disabledDbContext.GetDownloadTaskFileAsync(targetChildFileKey, CancellationToken);
        var targetTransfers = await disabledDbContext.GetDownloadTaskFilesAsync(
            [targetRootFileKey, targetChildFileKey],
            CancellationToken
        );
        var interruptedTransfers = await disabledDbContext.GetDownloadTaskFilesByStatusAsync(
            [DownloadStatus.Downloading],
            CancellationToken
        );
        var trackedRoots = await disabledDbContext.GetAllDownloadTasksByServerAsync(
            libraries[0].PlexServerId,
            asTracking: true,
            cancellationToken: CancellationToken
        );
        var targetRoots = await disabledDbContext.GetAllDownloadTasksByServerAsync(
            libraries[0].PlexServerId,
            cancellationToken: CancellationToken
        );
        var targetProgress = await disabledDbContext.GetDownloadProgressTasksByServerAsync(
            libraries[0].PlexServerId,
            CancellationToken
        );
        var controlProgress = await disabledDbContext.GetDownloadProgressTasksByServerAsync(
            libraries[1].PlexServerId,
            CancellationToken
        );
        var targetStatus = await disabledDbContext.GetDownloadStatusAsync(targetChildFileKey);
        var targetRootStatusBefore = await disabledDbContext.GetDownloadTaskStatusAsync(
            targetRootKey,
            CancellationToken
        );
        var changedKeys = await disabledDbContext.DetermineDownloadStatus(targetChildFileKey, CancellationToken);
        var targetRootStatusAfter = await disabledDbContext.GetDownloadTaskStatusAsync(
            targetRootKey,
            CancellationToken
        );
        var rootLogs = await disabledDbContext.GetDownloadTaskLogsAsync(targetRootKey, null, null, CancellationToken);
        var fileLogs = await disabledDbContext.GetDownloadTaskLogsAsync(
            targetChildFileKey,
            null,
            null,
            CancellationToken
        );
        var rootKey = await disabledDbContext.GetRootDownloadTaskKeyAsync(
            targetChildFileKey,
            cancellationToken: CancellationToken
        );

        // Assert
        targetKeys
            .OrderBy(x => x.Id)
            .ShouldBe(
                new[] { targetRootKey, targetChildKey, targetRootFileKey, targetChildFileKey }.OrderBy(x => x.Id)
            );
        (await disabledDbContext.GetDownloadTaskKeyAsync(targetRoot.Id, CancellationToken)).ShouldBe(targetRootKey);
        (await disabledDbContext.GetDownloadTaskKeyAsync(targetChildFile.Id, CancellationToken)).ShouldBe(
            targetChildFileKey
        );
        targetDetails.ShouldNotBeNull();
        targetDetails.Id.ShouldBe(targetRoot.Id);
        targetDetails.ParentId.ShouldBe(Guid.Empty);
        targetDetails
            .Children.Select(x => (x.Id, x.ParentId, x.IsDownloadable))
            .OrderBy(x => x.Id)
            .ShouldBe(
                new (Guid Id, Guid ParentId, bool IsDownloadable)[]
                {
                    (targetChild.Id, targetRoot.Id, false),
                    (targetRootFile.Id, targetRoot.Id, true),
                }.OrderBy(x => x.Id)
            );
        targetDetails
            .Children.Single(x => x.Id == targetChild.Id)
            .Children.Select(x => (x.Id, x.ParentId))
            .ShouldBe([(targetChildFile.Id, targetChild.Id)]);
        targetFileDetails.ShouldNotBeNull();
        targetFileDetails.Id.ShouldBe(targetChildFile.Id);
        targetFileDetails.ParentId.ShouldBe(targetChild.Id);
        targetFileDetails.DownloadDirectory.ShouldBe(targetChildFile.DownloadDirectory);
        targetFileDetails.DestinationDirectory.ShouldBe(targetChildFile.DestinationDirectory);
        targetFileDetails.FileName.ShouldBe(targetChildFile.FileName);
        targetTransfer.ShouldNotBeNull();
        (
            targetTransfer.Id,
            targetTransfer.ToParentKey()!.Id,
            targetTransfer.DownloadDirectory,
            targetTransfer.DestinationDirectory
        ).ShouldBe(
            (
                targetChildFile.Id,
                targetChild.Id,
                targetChildFile.DownloadDirectory,
                targetChildFile.DestinationDirectory
            )
        );
        targetTransfers.Select(x => x.Id).Order().ShouldBe(new[] { targetRootFile.Id, targetChildFile.Id }.Order());
        interruptedTransfers
            .Select(x => x.Id)
            .Order()
            .ShouldBe(new[] { targetRootFile.Id, targetChildFile.Id }.Order());
        trackedRoots.Select(x => x.Id).ShouldBe([targetRoot.Id]);
        targetRoots.Select(x => x.Id).ShouldBe([targetRoot.Id]);
        targetRoots
            .Single()
            .Children.Select(x => x.Id)
            .Order()
            .ShouldBe(targetDetails.Children.Select(x => x.Id).Order());
        targetProgress.Select(x => x.Id).ShouldBe([targetRoot.Id]);
        targetProgress
            .Single()
            .Children.Select(x => (x.Id, x.ParentId))
            .OrderBy(x => x.Id)
            .ShouldBe(
                new (Guid Id, Guid ParentId)[]
                {
                    (targetChild.Id, targetRoot.Id),
                    (targetRootFile.Id, targetRoot.Id),
                }.OrderBy(x => x.Id)
            );
        targetProgress.Single().DataTotal.ShouldBe(2000);
        targetProgress.Single().DataReceived.ShouldBe(500);
        targetProgress.Single().Percentage.ShouldBe(25);
        targetProgress.Single().DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        controlProgress.Select(x => x.Id).ShouldBe([controlRoot.Id]);
        controlProgress.Single().Children.Select(x => x.Id).ShouldBe([controlFile.Id]);
        targetStatus.ShouldBe(DownloadStatus.Downloading);
        targetRootStatusBefore.ShouldBe(DownloadStatus.Queued);
        targetRootStatusAfter.ShouldBe(DownloadStatus.Downloading);
        changedKeys.ShouldBe([targetChildKey, targetRootKey]);
        rootKey.ShouldBe(targetRootKey);
        rootLogs.IsSuccess.ShouldBeTrue();
        rootLogs.Errors.Count.ShouldBe(0);
        rootLogs.Value.Select(x => x.Message).ShouldBe(["disabled-photo"]);
        fileLogs.IsSuccess.ShouldBeTrue();
        fileLogs.Errors.Count.ShouldBe(0);
        fileLogs.Value.Select(x => x.Message).ShouldBe(["disabled-photo"]);
        (await disabledDbContext.GetDownloadTaskLogsAsync(controlRootKey, null, null, CancellationToken))
            .Value.Select(x => x.Message)
            .ShouldBe(["enabled-control"]);
    }

    [Test]
    public async Task ShouldReturnOnlyPhotoFolderRootsWithNestedProgress_WhenFilteringByServer()
    {
        // Arrange
        var seed = await SetupDatabase(
            62402,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexPhotoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        var targetFolders = await AddPhotoFolders(
            dbContext,
            libraries[0],
            seed,
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: true
        );
        var otherFolders = await AddPhotoFolders(
            dbContext,
            libraries[1],
            new Seed(seed.Next()),
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: false
        );
        var targetRoot = targetFolders.Single(x => x.ParentId is null);
        var targetChild = targetFolders.Single(x => x.ParentId == targetRoot.Id);
        var rootFile = targetRoot.Files.Single();
        var childFile = targetChild.Files.Single();
        rootFile.DataTotal = 1000;
        rootFile.DataReceived = 250;
        rootFile.Percentage = 25;
        rootFile.DownloadStatus = DownloadStatus.Downloading;
        childFile.DataTotal = 2000;
        childFile.DataReceived = 2000;
        childFile.Percentage = 100;
        childFile.DownloadStatus = DownloadStatus.Completed;
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var progress = await dbContext.GetDownloadProgressTasksByServerAsync(
            libraries[0].PlexServerId,
            CancellationToken
        );

        // Assert
        var root = progress.Single();
        root.Id.ShouldBe(targetRoot.Id);
        root.ParentId.ShouldBe(Guid.Empty);
        root.DownloadTaskType.ShouldBe(DownloadTaskType.PhotoFolder);
        root.DataTotal.ShouldBe(3000);
        root.DataReceived.ShouldBe(2250);
        root.Percentage.ShouldBe(62.5m);
        root.DownloadStatus.ShouldBe(DownloadStatus.Downloading);
        root.Children.Select(x => (x.Id, x.ParentId, x.IsDownloadable))
            .OrderBy(x => x.Id)
            .ShouldBe(
                new (Guid Id, Guid ParentId, bool IsDownloadable)[]
                {
                    (targetChild.Id, targetRoot.Id, false),
                    (rootFile.Id, targetRoot.Id, true),
                }.OrderBy(x => x.Id)
            );
        var child = root.Children.Single(x => x.Id == targetChild.Id);
        child
            .Children.Select(x => (x.Id, x.ParentId, x.IsDownloadable))
            .ShouldBe([(childFile.Id, targetChild.Id, true)]);
        child.DataTotal.ShouldBe(2000);
        child.DataReceived.ShouldBe(2000);
        child.Percentage.ShouldBe(100);
        child.DownloadStatus.ShouldBe(DownloadStatus.Completed);
        child.Children.Single().Id.ShouldBe(childFile.Id);
        child.Children.Single().ParentId.ShouldBe(targetChild.Id);
        child.Children.Single().IsDownloadable.ShouldBeTrue();

        var details = await dbContext.GetAllDownloadTasksByServerAsync(
            libraries[0].PlexServerId,
            cancellationToken: CancellationToken
        );
        details.Select(x => x.Id).ShouldBe([targetRoot.Id]);
        details.Single().DataTotal.ShouldBe(root.DataTotal);
        details.Single().Children.Select(x => x.Id).Order().ShouldBe(root.Children.Select(x => x.Id).Order());

        var otherProgress = await dbContext.GetDownloadProgressTasksByServerAsync(
            libraries[1].PlexServerId,
            CancellationToken
        );
        otherProgress.Select(x => x.Id).ShouldBe([otherFolders.Single(x => x.ParentId is null).Id]);
        otherProgress.Select(x => x.Id).Intersect(progress.Select(x => x.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldAggregateFileStatusThroughNestedFolders_WithoutChangingOtherRoots()
    {
        // Arrange
        var seed = await SetupDatabase(62403, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 2,
            filesPerFolder: 1,
            includeNested: true
        );
        var roots = folders.Where(x => x.ParentId is null).OrderBy(x => x.Id).ToArray();
        var targetRoot = roots[0];
        var targetChild = folders.Single(x => x.ParentId == targetRoot.Id);
        var targetFile = targetChild.Files.Single();
        var controlRoot = roots[1];
        var controlFile = controlRoot.Files.Single();

        // Act
        await dbContext.SetDownloadStatus(targetFile.ToKey(), DownloadStatus.Error);
        var changed = await dbContext.DetermineDownloadStatus(targetFile.ToKey(), CancellationToken);

        // Assert
        changed.ShouldBe([targetChild.ToKey(), targetRoot.ToKey()]);
        (await dbContext.GetDownloadStatusAsync(targetFile.ToKey())).ShouldBe(DownloadStatus.Error);
        (await dbContext.GetDownloadTaskStatusAsync(targetChild.ToKey(), CancellationToken)).ShouldBe(
            DownloadStatus.Error
        );
        (await dbContext.GetDownloadTaskStatusAsync(targetRoot.ToKey(), CancellationToken)).ShouldBe(
            DownloadStatus.Error
        );
        (await dbContext.GetDownloadStatusAsync(controlRoot.ToKey())).ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadStatusAsync(controlFile.ToKey())).ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.GetRootDownloadTaskKeyAsync(targetFile.ToKey(), cancellationToken: CancellationToken)
        ).ShouldBe(targetRoot.ToKey());
        (
            await dbContext.GetDownloadTaskKeysByStatusAsync(
                [targetRoot.ToKey(), targetChild.ToKey(), controlRoot.ToKey(), targetFile.ToKey(), controlFile.ToKey()],
                [DownloadStatus.Error],
                CancellationToken
            )
        )
            .OrderBy(x => x.Id)
            .ShouldBe(new[] { targetRoot.ToKey(), targetChild.ToKey(), targetFile.ToKey() }.OrderBy(x => x.Id));
    }

    [Test]
    public async Task ShouldSelectOnlyCompletedPhotoFoldersAndFiles_WhenFilteringClearCandidates()
    {
        // Arrange
        var seed = await SetupDatabase(62431, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 2,
            filesPerFolder: 1,
            includeNested: true
        );
        var roots = folders.Where(x => x.ParentId is null).OrderBy(x => x.Id).ToArray();
        var targetRoot = roots[0];
        var targetChild = folders.Single(x => x.ParentId == targetRoot.Id);
        var controlRoot = roots[1];
        var targetNodes = new DownloadTaskBase[]
        {
            targetRoot,
            targetChild,
            targetRoot.Files.Single(),
            targetChild.Files.Single(),
        };
        foreach (var node in targetNodes)
            await dbContext.SetDownloadStatus(node.ToKey(), DownloadStatus.Completed);
        var requestedKeys = targetNodes
            .Concat(new DownloadTaskBase[] { controlRoot, controlRoot.Files.Single() })
            .Select(x => x.ToKey())
            .ToArray();

        // Act
        var completedKeys = await dbContext.GetDownloadTaskKeysByStatusAsync(
            requestedKeys,
            [DownloadStatus.Completed],
            CancellationToken
        );

        // Assert
        completedKeys.OrderBy(x => x.Id).ShouldBe(targetNodes.Select(x => x.ToKey()).OrderBy(x => x.Id));
        (await dbContext.GetDownloadStatusAsync(controlRoot.ToKey())).ShouldBe(DownloadStatus.Queued);
        (await dbContext.GetDownloadStatusAsync(controlRoot.Files.Single().ToKey())).ShouldBe(DownloadStatus.Queued);
    }

    [Test]
    public async Task ShouldPersistTransferAndResetPhotoProgress_WithoutLosingDirectParentOrPath()
    {
        // Arrange
        var seed = await SetupDatabase(62404, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: false
        );
        var root = folders.Single(x => x.ParentId is null);
        var file = root.Files.Single();
        file.DataTotal = 1000;
        file.DataReceived = 250;
        file.DownloadSpeed = 50;
        file.Percentage = 25;
        file.TimeRemaining = 15;
        file.FileTransferSpeed = 100;
        file.FileDataTransferred = 500;
        file.CurrentFileTransferBytesOffset = 500;
        var key = file.ToKey();

        // Act
        await dbContext.UpdateDownloadProgress(key, file, cancellationToken: CancellationToken);
        await dbContext.UpdateDownloadFileTransferProgress(key, file, CancellationToken);

        // Assert
        var persisted = await IDbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        persisted.DataReceived.ShouldBe(250);
        persisted.DataTotal.ShouldBe(1000);
        persisted.DownloadSpeed.ShouldBe(50);
        persisted.FileDataTransferred.ShouldBe(500);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(500);
        persisted.Percentage.ShouldBe(50);

        await dbContext.ClearDownloadSpeed(key, CancellationToken);
        persisted = await IDbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        persisted.DownloadSpeed.ShouldBe(0);
        persisted.TimeRemaining.ShouldBe(0);
        persisted.FileTransferSpeed.ShouldBe(0);

        var reset = await dbContext.ResetDownloadTaskProgress(key, DownloadStatus.Stopped, CancellationToken);
        reset.IsSuccess.ShouldBeTrue();
        reset.Errors.Count.ShouldBe(0);
        persisted = await IDbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        persisted.DataReceived.ShouldBe(0);
        persisted.FileDataTransferred.ShouldBe(0);
        persisted.CurrentFileTransferBytesOffset.ShouldBe(0);
        persisted.FileTransferSpeed.ShouldBe(0);
        persisted.Percentage.ShouldBe(0);
        persisted.DownloadStatus.ShouldBe(DownloadStatus.Stopped);
        persisted.DataTotal.ShouldBe(1000);
        persisted.ParentId.ShouldBe(file.ParentId);
        persisted.FileLocationUrl.ShouldBe(file.FileLocationUrl);
        persisted.FileName.ShouldBe(file.FileName);
        persisted.DirectoryMeta.PhotoFolder.ShouldBe(file.DirectoryMeta.PhotoFolder);
    }

    [Test]
    public async Task ShouldRemoveOnlyScopedEmptyPhotoFolders_WhenCleaningOrphans()
    {
        // Arrange
        var seed = await SetupDatabase(
            62405,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexPhotoLibraryCount = 1;
            }
        );
        var dbContext = IDbContext;
        var libraries = await dbContext
            .PlexLibraries.Where(x => x.Type == PlexMediaType.PhotoAlbum)
            .OrderBy(x => x.PlexServerId)
            .ToListAsync(CancellationToken);
        var targetFolders = await AddPhotoFolders(
            dbContext,
            libraries[0],
            seed,
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: true
        );
        var controlFolders = await AddPhotoFolders(
            dbContext,
            libraries[0],
            new Seed(seed.Next()),
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: false
        );
        var otherServerFolders = await AddPhotoFolders(
            dbContext,
            libraries[1],
            new Seed(seed.Next()),
            rootCount: 1,
            filesPerFolder: 1,
            includeNested: false
        );
        var targetRoot = targetFolders.Single(x => x.ParentId is null);
        var controlRootId = controlFolders.Single(x => x.ParentId is null).Id;
        var otherServerRootId = otherServerFolders.Single(x => x.ParentId is null).Id;
        var targetFileIds = targetFolders.SelectMany(x => x.Files).Select(x => x.Id).ToArray();
        await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => targetFileIds.Contains(x.Id))
            .ExecuteDeleteAsync(CancellationToken);

        // Act
        (await dbContext.DeleteOrphanedParentTasksByRootIdsAsync([targetRoot.Id], CancellationToken)).ShouldBe(2);

        // Assert
        (await dbContext.DownloadTaskPhotoFolders.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(
                new[]
                {
                    controlFolders.Single(x => x.ParentId is null).Id,
                    otherServerFolders.Single(x => x.ParentId is null).Id,
                }.Order()
            );
        (
            await dbContext.DeleteOrphanedParentTasksByServerIdAsync(libraries[0].PlexServerId, CancellationToken)
        ).ShouldBe(0);

        await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => x.ParentId == controlRootId)
            .ExecuteDeleteAsync(CancellationToken);
        (
            await dbContext.DeleteOrphanedParentTasksByServerIdAsync(libraries[0].PlexServerId, CancellationToken)
        ).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoFolders.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBe([
            otherServerFolders.Single(x => x.ParentId is null).Id,
        ]);

        await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => x.ParentId == otherServerRootId)
            .ExecuteDeleteAsync(CancellationToken);
        (await dbContext.DeleteOrphanedParentTasksAsync(CancellationToken)).ShouldBe(1);
        (await dbContext.DownloadTaskPhotoFolders.Select(x => x.Id).ToListAsync(CancellationToken)).ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldCascadePhotoFolderDeletionThroughNestedFilesAndLogs_WithoutDeletingOtherRoots()
    {
        // Arrange
        var seed = await SetupDatabase(62406, config => config.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var folders = await AddPhotoFolders(
            dbContext,
            library,
            seed,
            rootCount: 2,
            filesPerFolder: 1,
            includeNested: true
        );
        var roots = folders.Where(x => x.ParentId is null).OrderBy(x => x.Id).ToArray();
        var targetRoot = roots[0];
        var retainedRoot = roots[1];
        var files = folders.SelectMany(x => x.Files).ToList();
        foreach (var file in files)
            await dbContext.CreateDownloadClientLog(
                file.ToKey(),
                NotificationLevel.Information,
                DownloadStatus.Queued,
                $"file-{file.Id}"
            );

        // Act
        await dbContext
            .DownloadTaskPhotoFolders.Where(x => x.Id == targetRoot.Id)
            .ExecuteDeleteAsync(CancellationToken);

        // Assert
        var retainedFolders = folders
            .Where(x => x.Id == retainedRoot.Id || x.ParentId == retainedRoot.Id)
            .Select(x => x.Id)
            .Order();
        (await dbContext.DownloadTaskPhotoFolders.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(retainedFolders);
        var retainedFileIds = retainedRoot
            .Files.Concat(retainedRoot.Children.SelectMany(x => x.Files))
            .Select(x => x.Id)
            .Order();
        (await dbContext.DownloadTaskPhotoImageFiles.Select(x => x.Id).ToListAsync(CancellationToken))
            .Order()
            .ShouldBe(retainedFileIds);
        (
            await dbContext
                .DownloadTaskPhotoImageFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message })
                .ToListAsync(CancellationToken)
        )
            .OrderBy(x => x.DownloadTaskFileId)
            .ShouldBe(
                retainedRoot
                    .Files.Concat(retainedRoot.Children.SelectMany(x => x.Files))
                    .Select(x => new { DownloadTaskFileId = x.Id, Message = $"file-{x.Id}" })
                    .OrderBy(x => x.DownloadTaskFileId)
            );
    }

    private static async Task<List<DownloadTaskPhotoFolder>> AddPhotoFolders(
        IReaparrDbContext dbContext,
        PlexLibrary library,
        Seed seed,
        int rootCount,
        int filesPerFolder,
        bool includeNested
    )
    {
        var roots = FakeData
            .GetDownloadTaskPhotoFolder(seed, config => config.PhotoImageDownloadTasksCount = filesPerFolder)
            .RuleFor(x => x.Id, faker => faker.Random.Guid())
            .Generate(rootCount)
            .ToList();
        var folders = new List<DownloadTaskPhotoFolder>();
        for (var index = 0; index < roots.Count; index++)
        {
            var root = roots[index];
            root.RelativePath = string.Empty;
            root.DownloadRootPath = "/downloads";
            root.DestinationRootPath = $"/destination/{root.Id:N}";
            foreach (var file in root.Files)
            {
                file.DirectoryMeta.DownloadRootPath = root.DownloadRootPath;
                file.DirectoryMeta.DestinationRootPath = root.DestinationRootPath;
                file.DirectoryMeta.PhotoFolder = $"root-{index}";
            }
            folders.Add(root);
            if (!includeNested)
                continue;

            var child = FakeData
                .GetDownloadTaskPhotoFolder(
                    new Seed(seed.Next()),
                    config => config.PhotoImageDownloadTasksCount = filesPerFolder
                )
                .Generate();
            child.Parent = root;
            child.ParentId = root.Id;
            child.RelativePath = $"nested-{index}";
            child.DownloadRootPath = root.DownloadRootPath;
            child.DestinationRootPath = root.DestinationRootPath;
            foreach (var file in child.Files)
            {
                file.DirectoryMeta.DownloadRootPath = child.DownloadRootPath;
                file.DirectoryMeta.DestinationRootPath = child.DestinationRootPath;
                file.DirectoryMeta.PhotoFolder = $"root-{index}/nested-{index}";
            }
            root.Children.Add(child);
            folders.Add(child);
        }

        folders.SetRelationshipIds(library.PlexServerId, library.Id);
        dbContext.DownloadTaskPhotoFolders.AddRange(roots);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return folders;
    }
}
