namespace Reaparr.Application.UnitTests;

public class RestartDownloadTaskCommandPhotoUnitTests : BaseCommandUnitTest<RestartDownloadTaskCommand>
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldRefreshOnlySelectedPhotoSourceOrMarkUnavailable_WhenRestarting(
        bool sourceMissing,
        bool selectFile
    )
    {
        // Arrange
        await SetupDatabase(
            88070,
            c =>
            {
                c.PlexPhotoLibraryCount = 1;
                c.PhotoAlbumCount = 1;
                c.PhotoCount = 2;
            }
        );
        var dbContext = IDbContext;
        var library = await dbContext.PlexLibraries.SingleAsync(CancellationToken);
        var sources = await dbContext
            .PlexPhotoData.Include(x => x.PlexPhoto)
                .ThenInclude(x => x!.PlexPhotoAlbum)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var source = sources[0];
        var album = FakeData
            .GetDownloadTaskPhotoAlbum(new Seed(88070))
            .RuleFor(
                x => x.Children,
                _ =>
                    sources
                        .Select(
                            (s, i) =>
                                FakeData
                                    .GetDownloadTaskPhotoImage(new Seed(88071 + i))
                                    .RuleFor(
                                        x => x.Children,
                                        _ =>
                                            FakeData
                                                .GetDownloadTaskPhotoImageFile(new Seed(88073 + i))
                                                .RuleFor(x => x.PlexApiRatingKey, _ => s.PlexPhoto!.PlexApiRatingKey)
                                                .RuleFor(x => x.PlexApiMediaId, _ => s.PlexApiMediaId)
                                                .RuleFor(x => x.PlexApiPartId, _ => s.PlexApiPartId)
                                                .RuleFor(x => x.FileName, _ => $"old-{i}.jpg")
                                                .RuleFor(x => x.DataReceived, _ => 128)
                                                .Generate(1)
                                    )
                                    .Generate()
                        )
                        .ToList()
            )
            .Generate();
        var files = album.Children.SelectMany(x => x.Children).ToList();
        foreach (
            var node in new DownloadTaskBase[] { album }
                .Concat(album.Children)
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
        if (sourceMissing)
            await dbContext.PlexPhotoData.Where(x => x.Id == source.Id).ExecuteDeleteAsync(CancellationToken);
        SetupDependencies(b => b.RegisterType<DownloadTaskUpdateDispatcher>().As<IDownloadTaskUpdateDispatcher>());
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(It.Is<StopDownloadTaskCommand>(c => c.DownloadTaskGuid == target.Id), CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        Mock.Mock<IEventPublisher>()
            .Setup(x =>
                x.PublishAsync(
                    It.Is<CheckDownloadQueueEvent>(e => e.PlexServerIds.SequenceEqual(new[] { library.PlexServerId })),
                    CancellationToken
                )
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(
            new RestartDownloadTaskCommand(selectFile ? target.Id : target.ParentId)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var after = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(x => x.Id == target.Id, CancellationToken);
        after.ParentId.ShouldBe(target.ParentId);
        after.DownloadStatus.ShouldBe(sourceMissing ? DownloadStatus.SourceUnavailable : DownloadStatus.Queued);
        after.DataReceived.ShouldBe(sourceMissing ? 128 : 0);
        after.FileName.ShouldBe(sourceMissing ? target.FileName : source.OriginalFilename);
        after.FileLocationUrl.ShouldBe(sourceMissing ? target.FileLocationUrl : source.Key);
        after.DataTotal.ShouldBe(sourceMissing ? target.DataTotal : source.Size);
        after.DownloadClientType.ShouldBe(PlexDownloadClientType.Direct);
        after.DirectoryMeta.PhotoAlbumFolder.ShouldBe(
            sourceMissing
                ? target.DirectoryMeta.PhotoAlbumFolder
                : source.PlexPhoto!.PlexPhotoAlbum!.Title.SanitizeFolderName()
        );
        var retained = await dbContext.DownloadTaskPhotoImageFiles.SingleAsync(
            x => x.Id == sibling.Id,
            CancellationToken
        );
        retained.FileName.ShouldBe(sibling.FileName);
        retained.DataReceived.ShouldBe(128);
        retained.DownloadStatus.ShouldBe(DownloadStatus.Queued);
        (
            await dbContext.DownloadTaskPhotoImages.SingleAsync(x => x.Id == target.ParentId, CancellationToken)
        ).DownloadStatus.ShouldBe(after.DownloadStatus);
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<IEventPublisher>().Verify();
    }

}
