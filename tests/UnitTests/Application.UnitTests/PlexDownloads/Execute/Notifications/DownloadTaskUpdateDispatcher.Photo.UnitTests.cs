using System.Collections.Concurrent;

namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherPhotoUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    [Arguments(false, DownloadStatus.Downloading)]
    [Arguments(false, DownloadStatus.MovePaused)]
    [Arguments(true, DownloadStatus.Queued)]
    [Arguments(true, DownloadStatus.Paused)]
    public async Task ShouldPersistPhotoStateAndPatchOnlyFileAndFolderAncestors_WhenFileChanges(
        bool progressOnly,
        DownloadStatus status
    )
    {
        // Arrange
        await SetupDatabase(88001, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData.GetDownloadTaskPhotoFolder(new Seed(88001)).Generate();
        var nested = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88002))
            .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88003)).Generate(2))
            .Generate();
        nested.ParentId = root.Id;
        nested.Parent = root;
        nested.Title = "Trips";
        nested.RelativePath = "Trips";
        nested.FullTitle = $"{root.FullTitle}/Trips";
        root.Children.Add(nested);
        new List<DownloadTaskPhotoFolder> { root }.SetRelationshipIds(library.PlexServerId, library.Id);
        var target = nested.Files.First();
        var sibling = nested.Files.Last();
        var directRootFile = root.Files.Single();
        root.DownloadStatus = DownloadStatus.Queued;
        nested.DownloadStatus = DownloadStatus.Queued;
        target.DownloadStatus = DownloadStatus.Queued;
        sibling.DownloadStatus = DownloadStatus.Queued;
        directRootFile.DownloadStatus = DownloadStatus.Queued;
        foreach (var file in nested.Files)
        {
            file.DataTotal = 1_000;
            file.DirectoryMeta.PhotoFolder = "Trips";
        }
        directRootFile.DataTotal = 1_000;
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);
        if (status == DownloadStatus.MovePaused)
        {
            await dbContext
                .DownloadTaskPhotoImageFiles.Where(x => x.Id == target.Id)
                .ExecuteUpdateAsync(
                    p => p.SetProperty(x => x.FileDataTransferred, 250).SetProperty(x => x.Percentage, 25),
                    CancellationToken
                );
        }
        if (!progressOnly && status == DownloadStatus.Downloading)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsServerDownloading(target.PlexServerId))
                .ReturnsAsync(true)
                .Verifiable(Times.Once());
            Mock.Mock<ICommandExecutor>()
                .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
                .ReturnsAsync(Result.Ok())
                .Verifiable(Times.Once());
        }
        var expectedIds = new[] { root.Id, nested.Id, target.Id }.Order().ToArray();
        var received = new ConcurrentQueue<(long Sequence, DownloadPatchDTO[] Patches)>();
        var patchReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    library.PlexServerId,
                    It.IsAny<long>(),
                    It.Is<IReadOnlyCollection<DownloadPatchDTO>>(p =>
                        p.Count == expectedIds.Length && p.Select(n => n.Id).Order().SequenceEqual(expectedIds)
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, sequence, patches, _, _) =>
                {
                    received.Enqueue((sequence, patches.ToArray()));
                    patchReceived.TrySetResult(true);
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        var sut = Sut;

        // Act
        if (progressOnly)
            sut.OnProgressUpdated(
                target.ToKey(),
                new DownloadTaskProgress
                {
                    DataTotal = 1_000,
                    DataReceived = 500,
                    DownloadSpeed = 100,
                    Percentage = 50,
                    TimeRemaining = 10,
                }
            );
        if (!progressOnly || status == DownloadStatus.Paused)
            await sut.OnStatusChangedAsync(target.ToKey(), status, CancellationToken);
        await sut.StartAsync(CancellationToken.None);
        try
        {
            await patchReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
        dbContext.ClearChangeTracker();

        // Assert
        var expectedReceived = progressOnly ? 500L : 0L;
        var persisted = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == target.Id,
            CancellationToken
        );
        persisted.ParentId.ShouldBe(nested.Id);
        persisted.DownloadStatus.ShouldBe(status);
        persisted.DataReceived.ShouldBe(expectedReceived);
        persisted.DataTotal.ShouldBe(1_000);
        persisted.FileDataTransferred.ShouldBe(status == DownloadStatus.MovePaused ? 250 : 0);
        persisted.DownloadSpeed.ShouldBe(progressOnly && status != DownloadStatus.Paused ? 100 : 0);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == sibling.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == directRootFile.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == nested.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(status);
        (
            await dbContext.DownloadTaskPhotoFolders.SingleAsync(x => x.Id == root.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(status);

        var dispatched = received.ToArray();
        dispatched.ShouldNotBeEmpty();
        dispatched
            .Select(x => x.Sequence)
            .Order()
            .ShouldBe(Enumerable.Range(1, dispatched.Length).Select(x => (long)x));
        foreach (var (_, patches) in dispatched)
        {
            patches.Select(x => x.Id).Order().ShouldBe(expectedIds);
            patches.Select(x => x.Status).ShouldAllBe(x => x == status);
            var leaf = patches.Single(x => x.Id == target.Id);
            leaf.ParentId.ShouldBe(nested.Id);
            leaf.Status.ShouldBe(status);
            leaf.DataReceived.ShouldBe(expectedReceived);
            leaf.DataTotal.ShouldBe(1_000);
            leaf.Percentage.ShouldBe(
                status == DownloadStatus.MovePaused ? 25
                : progressOnly ? 50
                : 0
            );
            var nestedPatch = patches.Single(x => x.Id == nested.Id);
            nestedPatch.ParentId.ShouldBe(root.Id);
            nestedPatch.DataTotal.ShouldBe(2_000);
            nestedPatch.DataReceived.ShouldBe(expectedReceived);
            nestedPatch.DownloadSpeed.ShouldBe(progressOnly && status != DownloadStatus.Paused ? 100 : 0);
            nestedPatch.Percentage.ShouldBe(
                status == DownloadStatus.MovePaused ? 12.5m
                : progressOnly ? 25m
                : 0m
            );
            var rootPatch = patches.Single(x => x.Id == root.Id);
            rootPatch.ParentId.ShouldBe(Guid.Empty);
            rootPatch.DataTotal.ShouldBe(3_000);
            rootPatch.DataReceived.ShouldBe(expectedReceived);
            rootPatch.DownloadSpeed.ShouldBe(progressOnly && status != DownloadStatus.Paused ? 100 : 0);
            rootPatch.Percentage.ShouldBe(
                status == DownloadStatus.MovePaused ? 6.25m
                : progressOnly ? 12.5m
                : 0m
            );
        }
        var logs = await dbContext.DownloadTaskPhotoImageFileLogs.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var targetLogs = logs.Where(x => x.DownloadTaskFileId == target.Id).ToList();
        if (!progressOnly || status == DownloadStatus.Paused)
            targetLogs.ShouldContain(x => x.Status == status);
        logs.ShouldNotContain(x => x.DownloadTaskFileId == sibling.Id);
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadHubService>().Verify();
        Mock.Mock<IDownloadHubService>()
            .Verify(
                x =>
                    x.SendDownloadPatchAsync(
                        library.PlexServerId,
                        It.IsAny<long>(),
                        It.Is<IReadOnlyCollection<DownloadPatchDTO>>(p =>
                            p.Select(n => n.Id).Order().SequenceEqual(expectedIds)
                        ),
                        null,
                        It.IsAny<CancellationToken>()
                    ),
                Times.Exactly(dispatched.Length)
            );
        Mock.Mock<IDownloadHubService>()
            .Verify(
                x =>
                    x.SendDownloadPatchAsync(
                        It.Is<int>(id => id != library.PlexServerId),
                        It.IsAny<long>(),
                        It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                        It.IsAny<IReadOnlyCollection<Guid>?>(),
                        It.IsAny<CancellationToken>()
                    ),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldPatchDirectRootPhotoFileAndFolderWithoutPhantomAssetAncestor()
    {
        // Arrange
        await SetupDatabase(88105, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88105)).Generate();
        new List<DownloadTaskPhotoFolder> { folder }.SetRelationshipIds(library.PlexServerId, library.Id);
        folder.DownloadStatus = DownloadStatus.Queued;
        var file = folder.Files.Single();
        file.DownloadStatus = DownloadStatus.Queued;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        var patchReceived = new TaskCompletionSource<IReadOnlyCollection<DownloadPatchDTO>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    library.PlexServerId,
                    It.IsAny<long>(),
                    It.Is<IReadOnlyCollection<DownloadPatchDTO>>(p =>
                        p.Count == 2 && p.Select(n => n.Id).Order().SequenceEqual(new[] { folder.Id, file.Id }.Order())
                    ),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, patches, _, _) => patchReceived.TrySetResult(patches)
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
        var sut = Sut;

        // Act
        await sut.OnStatusChangedAsync(file.ToKey(), DownloadStatus.Paused, CancellationToken);
        await sut.StartAsync(CancellationToken.None);
        IReadOnlyCollection<DownloadPatchDTO> patches;
        try
        {
            patches = await patchReceived.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        }
        finally
        {
            await sut.StopAsync(CancellationToken.None);
        }
        dbContext.ClearChangeTracker();

        // Assert
        patches.Select(x => x.Id).Order().ShouldBe(new[] { folder.Id, file.Id }.Order());
        patches.Single(x => x.Id == folder.Id).ParentId.ShouldBe(Guid.Empty);
        patches.Single(x => x.Id == folder.Id).DataTotal.ShouldBe(file.DataTotal);
        patches.Single(x => x.Id == folder.Id).DataReceived.ShouldBe(0);
        patches.Single(x => x.Id == folder.Id).Percentage.ShouldBe(0);
        (await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Paused
        );
        (await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(
            DownloadStatus.Paused
        );
        patches.Single(x => x.Id == file.Id).ParentId.ShouldBe(folder.Id);
        patches.Select(x => x.Status).ShouldAllBe(x => x == DownloadStatus.Paused);
        (await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken)).ParentId.ShouldBeNull();
        (await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken)).ParentId.ShouldBe(folder.Id);
        Mock.Mock<IDownloadHubService>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldSendDeletedPhotoFileIdWithoutLoadingAWrapper()
    {
        // Arrange
        await SetupDatabase(88106, c => c.PlexPhotoLibraryCount = 1);
        var key = new DownloadTaskKey
        {
            Id = Guid.NewGuid(),
            PlexServerId = 42,
            PlexLibraryId = 84,
            Type = DownloadTaskType.PhotoImage,
        };
        IReadOnlyCollection<Guid>? deletedIds = null;
        IReadOnlyCollection<DownloadPatchDTO>? upserts = null;
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    key.PlexServerId,
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<DownloadPatchDTO>>(),
                    It.IsAny<IReadOnlyCollection<Guid>?>(),
                    CancellationToken
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, patches, deleted, _) =>
                {
                    upserts = patches;
                    deletedIds = deleted;
                }
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        await Sut.OnStatusChangedAsync(key, DownloadStatus.Deleted, CancellationToken);

        // Assert
        upserts.ShouldNotBeNull();
        upserts.ShouldBeEmpty();
        deletedIds.ShouldBe([key.Id]);
        Mock.Mock<IDownloadHubService>().Verify();
        Mock.Mock<ICommandExecutor>()
            .Verify(
                x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
