namespace Reaparr.Application.UnitTests;

public class DeleteDownloadTasksByKeyCommandMediaTypesUnitTests : BaseCommandUnitTest<DeleteDownloadTasksByKeyCommand>
{
    [Test]
    [Arguments(DownloadTaskType.Movie, false)]
    [Arguments(DownloadTaskType.Movie, true)]
    [Arguments(DownloadTaskType.TvShow, false)]
    [Arguments(DownloadTaskType.TvShow, true)]
    [Arguments(DownloadTaskType.MusicArtist, false)]
    [Arguments(DownloadTaskType.MusicArtist, true)]
    [Arguments(DownloadTaskType.PhotoAlbum, false)]
    [Arguments(DownloadTaskType.PhotoAlbum, true)]
    [Arguments(DownloadTaskType.OtherVideo, false)]
    [Arguments(DownloadTaskType.OtherVideo, true)]
    public async Task ShouldDeleteOnlySelectedHierarchyAndNotifyExactKeys_WhenDeletingRootOrLastFile(
        DownloadTaskType rootType,
        bool selectFile
    )
    {
        // Arrange
        await SetupDatabase(88400, config =>
        {
            config.PlexMovieLibraryCount = 1;
            config.MovieDownloadTasksCount = 2;
            config.PlexTvShowLibraryCount = 1;
            config.TvShowDownloadTasksCount = 2;
            config.TvShowSeasonDownloadTasksCount = 1;
            config.TvShowEpisodeDownloadTasksCount = 1;
            config.PlexMusicLibraryCount = 1;
            config.MusicArtistDownloadTasksCount = 2;
            config.MusicAlbumDownloadTasksCount = 1;
            config.MusicTrackDownloadTasksCount = 1;
            config.MusicTrackFileDownloadTasksCount = 1;
            config.PlexPhotoLibraryCount = 1;
            config.PhotoAlbumDownloadTasksCount = 2;
            config.PhotoImageDownloadTasksCount = 1;
            config.PhotoImageFileDownloadTasksCount = 1;
            config.PlexOtherVideoLibraryCount = 1;
            config.OtherVideoDownloadTasksCount = 2;
            config.OtherVideoFileDownloadTasksCount = 1;
        });
        var dbContext = IDbContext;
        var before = await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        before.Count.ShouldBe(10);
        var roots = before.Where(x => x.DownloadTaskType == rootType).OrderBy(x => x.Id).ToArray();
        roots.Length.ShouldBe(2);
        var targetNodes = new[] { roots[0] }.Flatten(x => x.Children).ToArray();
        var file = targetNodes.Single(x => x.IsDownloadable);
        var key = (selectFile ? file : roots[0]).ToKey();
        var deletedIds = targetNodes.Select(x => x.Id).ToHashSet();
        var beforeKeys = before.Flatten(x => x.Children).Select(x => x.ToKey()).OrderBy(x => x.Id).ToArray();
        (await dbContext.GetDownloadTaskKeysAsync(beforeKeys.Select(x => x.Id).ToList(), CancellationToken))
            .OrderBy(x => x.Id).ShouldBe(beforeKeys);
        var notificationKeys = targetNodes.Where(x => selectFile || !x.IsDownloadable)
            .Select(x => x.ToKey()).ToArray();
        foreach (var notificationKey in notificationKeys)
            Mock.Mock<IDownloadTaskUpdateDispatcher>()
                .Setup(x => x.OnStatusChangedAsync(notificationKey, DownloadStatus.Deleted, CancellationToken))
                .Returns(Task.CompletedTask)
                .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync(new DeleteDownloadTasksByKeyCommand([key]));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        (await dbContext.GetDownloadTaskKeysAsync(beforeKeys.Select(x => x.Id).ToList(), CancellationToken))
            .OrderBy(x => x.Id).ShouldBe(beforeKeys.Where(x => !deletedIds.Contains(x.Id)));
        (await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken))
            .Select(x => x.Id).Order().ShouldBe(before.Where(x => x.Id != roots[0].Id).Select(x => x.Id).Order());
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify();
        Mock.Mock<IDownloadTaskUpdateDispatcher>().Verify(
            x => x.OnStatusChangedAsync(
                It.Is<DownloadTaskKey>(k => !notificationKeys.Contains(k)),
                It.IsAny<DownloadStatus>(),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
