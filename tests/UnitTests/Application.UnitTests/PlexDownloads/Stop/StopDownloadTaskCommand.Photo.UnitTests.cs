namespace Reaparr.Application.UnitTests;

public class StopDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<StopDownloadTaskCommand>
{
    [Test]
    [Arguments(DownloadTaskType.PhotoAlbum)]
    [Arguments(DownloadTaskType.PhotoImage)]
    [Arguments(DownloadTaskType.PhotoData)]
    [Arguments(DownloadTaskType.PhotoPart)]
    public async Task ShouldStopOnlySelectedPhotoAndPreserveQueuedSibling_WhenStopping(DownloadTaskType selection)
    {
        // Arrange
        await SetupDatabase(88010, c => c.PlexPhotoLibraryCount = 1);
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88010))
            .RuleFor(x => x.Children, _ => FakeData.GetDownloadTaskPhotoImage(new Seed(88011)).Generate(2))
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
        files[0].DownloadStatus = DownloadStatus.Downloading;
        files[0].DataReceived = 128;
        files[1].DataReceived = 64;
        dbContext.DownloadTaskPhotoAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken);
        var target = files[0];
        var sibling = files[1];
        var id = selection switch
        {
            DownloadTaskType.PhotoAlbum => album.Id,
            DownloadTaskType.PhotoImage => images[0].Id,
            _ => target.Id,
        };
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.IsDownloading(target.ToKey(), CancellationToken))
            .ReturnsAsync(true)
            .Verifiable(Times.Once());
        Mock.Mock<IDownloadTaskScheduler>()
            .Setup(x => x.StopDownloadTaskJob(target.ToKey(), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IMoveDownloadFileScheduler>()
            .Setup(x => x.IsDownloadFileMoving(target.ToKey(), CancellationToken))
            .ReturnsAsync(false)
            .Verifiable(Times.Once());
        if (selection == DownloadTaskType.PhotoAlbum)
        {
            Mock.Mock<IDownloadTaskScheduler>()
                .Setup(x => x.IsDownloading(sibling.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
            Mock.Mock<IMoveDownloadFileScheduler>()
                .Setup(x => x.IsDownloadFileMoving(sibling.ToKey(), CancellationToken))
                .ReturnsAsync(false)
                .Verifiable(Times.Once());
        }
        Mock.Mock<ICommandExecutor>()
            .Setup(x =>
                x.Send(
                    It.Is<DeleteDownloadTaskFilesCommand>(c => c.Keys.Count == 1 && c.Keys[0] == target.ToKey()),
                    CancellationToken
                )
            )
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new StopDownloadTaskCommand(id));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext
            .DownloadTaskPhotoImageFiles.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.DownloadStatus,
                x.DataReceived,
            })
            .ToListAsync(CancellationToken);
        after.ShouldBe(
            new[]
            {
                new
                {
                    target.Id,
                    target.ParentId,
                    DownloadStatus = DownloadStatus.Stopped,
                    DataReceived = 0L,
                },
                new
                {
                    sibling.Id,
                    sibling.ParentId,
                    DownloadStatus = DownloadStatus.Queued,
                    DataReceived = 64L,
                },
            }.OrderBy(x => x.Id)
        );
        Mock.Mock<IDownloadTaskScheduler>().Verify();
        Mock.Mock<IMoveDownloadFileScheduler>().Verify();
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IDownloadTaskScheduler>()
            .Verify(x => x.StopDownloadTaskJob(sibling.ToKey(), It.IsAny<CancellationToken>()), Times.Never());
    }

}
