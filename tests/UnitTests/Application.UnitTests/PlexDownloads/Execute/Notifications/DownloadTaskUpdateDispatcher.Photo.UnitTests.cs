namespace Reaparr.Application.UnitTests;

public class DownloadTaskUpdateDispatcherPhotoUnitTests : BaseUnitTest<DownloadTaskUpdateDispatcher>
{
    [Test]
    [Arguments(false, DownloadStatus.Downloading)]
    [Arguments(false, DownloadStatus.MovePaused)]
    [Arguments(true, DownloadStatus.Queued)]
    [Arguments(true, DownloadStatus.Paused)]
    public async Task ShouldPersistPhotoStateAndPatchOnlyItsAncestors_WhenFileChanges(
        bool progressOnly,
        DownloadStatus status
    )
    {
        // Arrange
        await SetupDatabase(88001, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88001))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(88002)).Generate(2))
            .Generate();
        var images = album.Children.ToList();
        var files = images.SelectMany(x => x.Children).ToList();
        foreach (
            var node in new DownloadTaskBase[] { album }
                .Concat(images)
                .Concat(files)
        )
        {
            node.PlexServerId = library.PlexServerId;
            node.PlexLibraryId = library.Id;
        }
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        var target = files[0];
        var sibling = files[1];

        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsServerDownloading(target.PlexServerId))
            .ReturnsAsync(true);
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.IsAny<UpdateScheduledDownloadLimitsCommand>(), CancellationToken.None))
            .ReturnsAsync(Result.Ok());
        (await dbContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(2);
        var expectedIds = new[] { album.Id, images[0].Id, target.Id }.Order().ToArray();
        var patchReceived = new TaskCompletionSource<IReadOnlyCollection<DownloadPatchDTO>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Mock.Mock<IDownloadHubService>()
            .Setup(x =>
                x.SendDownloadPatchAsync(
                    library.PlexServerId,
                    It.IsAny<long>(),
                    It.Is<IReadOnlyCollection<DownloadPatchDTO>>(p => p.Count == 3),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<int, long, IReadOnlyCollection<DownloadPatchDTO>, IReadOnlyCollection<Guid>?, CancellationToken>(
                (_, _, patches, _, _) => patchReceived.TrySetResult(patches)
            )
            .Returns(Task.CompletedTask);
        var sut = Sut;

        // Act
        if (progressOnly)
            sut.OnProgressUpdated(
                target.ToKey(),
                new DownloadTaskProgress
                {
                    DataTotal = target.DataTotal,
                    DataReceived = 500,
                    DownloadSpeed = 100,
                    Percentage = 500m / target.DataTotal * 100,
                    TimeRemaining = 10,
                }
            );
        if (!progressOnly || status == DownloadStatus.Paused)
            await sut.OnStatusChangedAsync(target.ToKey(), status, CancellationToken);
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

        // Assert
        patches.Select(x => x.Id).Order().ShouldBe(expectedIds);
        patches.Single(x => x.Id == target.Id).ParentId.ShouldBe(images[0].Id);
        patches.Single(x => x.Id == images[0].Id).ParentId.ShouldBe(album.Id);
        patches.Select(x => x.Status).ShouldAllBe(x => x == status);
        patches.Single(x => x.Id == target.Id).DataReceived.ShouldBe(progressOnly ? 500 : 0);
        var persisted = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == target.Id,
            CancellationToken
        );
        persisted.DownloadStatus.ShouldBe(status);
        persisted.DataReceived.ShouldBe(progressOnly ? 500 : 0);
        (
            await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == sibling.Id, CancellationToken)
        ).DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoImages.SingleAsync(x => x.Id == images[0].Id, CancellationToken)
        ).DownloadStatus.ShouldBe(status);
        (await dbContext.DownloadTaskPhotoAlbums.SingleAsync(CancellationToken)).DownloadStatus.ShouldBe(status);
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
                Times.AtLeastOnce()
            );
    }

}
