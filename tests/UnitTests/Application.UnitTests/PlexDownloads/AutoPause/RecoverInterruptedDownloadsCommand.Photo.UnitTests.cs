namespace Reaparr.Application.UnitTests;

public class RecoverInterruptedDownloadsCommandPhotoUnitTests : BaseUnitTest<RecoverInterruptedDownloadsCommandHandler>
{
    [Test]
    public async Task ShouldRecoverInterruptedPhotoFilesWithoutChangingIdleSiblings_WhenRecovering()
    {
        // Arrange
        await SetupDatabase(88060, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var root = FakeData
            .GetDownloadTaskPhotoFolder(new Seed(88060))
            .RuleFor(
                x => x.Children,
                _ =>
                    FakeData
                        .GetDownloadTaskPhotoFolder(new Seed(88061))
                        .RuleFor(x => x.Files, _ => FakeData.GetDownloadTaskPhotoImageFile(new Seed(88062)).Generate(1))
                        .Generate(4)
            )
            .Generate();
        new List<DownloadTaskPhotoFolder> { root }.SetRelationshipIds(library.PlexServerId, library.Id);
        var files = root.Children.SelectMany(x => x.Files).ToList();
        var folderIndex = 0;
        foreach (var folder in root.Children)
        {
            folder.RelativePath = $"folder-{folderIndex}";
            folder.Title = folder.RelativePath;
            folder.FullTitle = $"{root.FullTitle}/{folder.RelativePath}";
            folderIndex++;
        }
        files[0].DownloadStatus = DownloadStatus.Downloading;
        files[1].DownloadStatus = DownloadStatus.Moving;
        files[2].DownloadStatus = DownloadStatus.MoveFinished;
        files[3].DownloadStatus = DownloadStatus.Paused;
        foreach (var file in files)
            file.DataReceived = 512;
        dbContext.DownloadTaskPhotoFolders.Add(root);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.SetupCommand(() => new UpdateScheduledDownloadLimitsCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expected = new[]
        {
            DownloadStatus.AutoPaused,
            DownloadStatus.AutoMovePaused,
            DownloadStatus.Completed,
            DownloadStatus.Paused,
        };
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.Where(x => x.ParentId != root.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            files
                .Select(
                    (x, i) =>
                        new
                        {
                            x.Id,
                            x.ParentId,
                            DownloadStatus = expected[i],
                            DataReceived = 512L,
                        }
                )
                .OrderBy(x => x.Id)
        );
        var idleRootFile = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.ParentId == root.Id,
            CancellationToken
        );
        idleRootFile.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        idleRootFile.DataReceived.ShouldBe(0);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }

    [Test]
    public async Task ShouldRecoverInterruptedPhotoFileInRootFolder()
    {
        // Arrange
        await SetupDatabase(88104, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var folder = FakeData.GetDownloadTaskPhotoFolder(new Seed(88104)).Generate();
        var file = folder.Files.Single();
        folder.PlexServerId = library.PlexServerId;
        folder.PlexLibraryId = library.Id;
        file.PlexServerId = library.PlexServerId;
        file.PlexLibraryId = library.Id;
        file.DownloadStatus = DownloadStatus.Downloading;
        file.DataReceived = 512;
        dbContext.DownloadTaskPhotoFolders.Add(folder);
        await dbContext.SaveChangesAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.SetupCommand(() => new UpdateScheduledDownloadLimitsCommand())
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new RecoverInterruptedDownloadsCommand(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(CancellationToken);
        after.DownloadStatus.ShouldBe(DownloadStatus.AutoPaused);
        after.DataReceived.ShouldBe(512);
        var persistedFolder = await dbContext.DownloadTaskPhotoFolders.SingleAsync(CancellationToken);
        persistedFolder.ParentId.ShouldBeNull();
        persistedFolder.DownloadStatus.ShouldBe(DownloadStatus.AutoPaused);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(
                x => x.StartDownloadTaskJob(It.IsAny<DownloadTaskKey>(), It.IsAny<CancellationToken>()),
                Times.Never()
            );
    }
}
