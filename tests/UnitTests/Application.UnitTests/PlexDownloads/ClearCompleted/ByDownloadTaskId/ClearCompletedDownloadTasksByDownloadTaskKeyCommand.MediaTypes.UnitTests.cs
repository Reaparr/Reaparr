namespace Reaparr.Application.UnitTests;

public class ClearCompletedDownloadTasksByDownloadTaskKeyCommandMediaTypesUnitTests
    : BaseCommandUnitTest<ClearCompletedDownloadTasksByDownloadTaskKeyCommand>
{
    [Test]
    [Arguments(DownloadTaskType.Movie)]
    [Arguments(DownloadTaskType.TvShow)]
    [Arguments(DownloadTaskType.MusicArtist)]
    [Arguments(DownloadTaskType.PhotoAlbum)]
    [Arguments(DownloadTaskType.OtherVideo)]
    public async Task ShouldDispatchOnlyCompletedTypedRootAndReturnExactCount_WhenClearingMixedStates(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(88410, config =>
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
        var roots = before.Where(x => x.DownloadTaskType == type).OrderBy(x => x.Id).ToArray();
        roots.Length.ShouldBe(2);
        var completedKey = roots[0].ToKey();
        foreach (var node in new[] { roots[0] }.Flatten(x => x.Children))
            await dbContext.SetDownloadStatus(node.ToKey(), DownloadStatus.Completed);
        (await dbContext.GetDownloadTaskAsync(completedKey, CancellationToken))!.DownloadStatus
            .ShouldBe(DownloadStatus.Completed);
        (await dbContext.GetDownloadTaskAsync(roots[1].ToKey(), CancellationToken))!.DownloadStatus
            .ShouldBe(DownloadStatus.Queued);
        var requestedKeys = before.Select(x => x.ToKey()).ToList();
        Mock.Mock<ICommandExecutor>()
            .Setup(x => x.Send(
                It.Is<DeleteDownloadTasksByKeyCommand>(c => c.Keys.SequenceEqual(new[] { completedKey })),
                CancellationToken))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());

        // Act
        var result = await TestHandlerExecuteAsync<int>(
            new ClearCompletedDownloadTasksByDownloadTaskKeyCommand(requestedKeys)
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);
        (await dbContext.GetDownloadTaskKeysAsync(requestedKeys.Select(x => x.Id).ToList(), CancellationToken))
            .OrderBy(x => x.Id).ShouldBe(requestedKeys.OrderBy(x => x.Id));
        Mock.Mock<ICommandExecutor>().Verify();
        Mock.Mock<ICommandExecutor>().Verify(
            x => x.Send(
                It.Is<DeleteDownloadTasksByKeyCommand>(c => !c.Keys.SequenceEqual(new[] { completedKey })),
                It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
