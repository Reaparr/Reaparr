namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextExtensionsGetDownloadProgressTasksByServerAsyncUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveEveryTypedHierarchyAndProgress_WhenRetrievingAllOrOneServer(bool filterServer)
    {
        // Arrange
        await SetupDatabase(
            129887,
            config =>
            {
                config.PlexServerCount = 2;
                config.PlexMovieLibraryCount = 1;
                config.MovieDownloadTasksCount = 1;
                config.PlexTvShowLibraryCount = 1;
                config.TvShowDownloadTasksCount = 1;
                config.TvShowSeasonDownloadTasksCount = 1;
                config.TvShowEpisodeDownloadTasksCount = 1;
                config.PlexMusicLibraryCount = 1;
                config.MusicArtistDownloadTasksCount = 1;
                config.MusicAlbumDownloadTasksCount = 1;
                config.MusicTrackDownloadTasksCount = 1;
                config.MusicTrackFileDownloadTasksCount = 1;
                config.PlexPhotoLibraryCount = 1;
                config.PhotoAlbumDownloadTasksCount = 1;
                config.PhotoImageDownloadTasksCount = 1;
                config.PhotoImageFileDownloadTasksCount = 1;
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoDownloadTasksCount = 1;
                config.OtherVideoFileDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var allTasks = await dbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var serverIds = await dbContext.PlexServers.OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync(CancellationToken);
        serverIds.Length.ShouldBe(2);
        DownloadTaskType[] rootTypes =
        [
            DownloadTaskType.Movie,
            DownloadTaskType.TvShow,
            DownloadTaskType.MusicArtist,
            DownloadTaskType.PhotoAlbum,
            DownloadTaskType.OtherVideo,
        ];
        allTasks.Select(x => x.DownloadTaskType).Order().ShouldBe(rootTypes.Order());
        allTasks.ShouldAllBe(x => serverIds.Contains(x.PlexServerId));
        var requestedServerId = filterServer ? allTasks[0].PlexServerId : 0;
        var expected = allTasks.Where(x => !filterServer || x.PlexServerId == requestedServerId)
            .Flatten(x => x.Children).OrderBy(x => x.Id).ToArray();
        if (!filterServer)
            expected.Select(x => x.DownloadTaskType).Distinct().Count().ShouldBe(15);
        expected.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.FullTitle));
        var expectedKeys = expected.Select(x => x.ToKey()).ToArray();
        (await dbContext.GetDownloadTaskKeysByStatusAsync(expectedKeys, [DownloadStatus.Queued], CancellationToken))
            .OrderBy(x => x.Id).ShouldBe(
                expected.Where(x => x.DownloadStatus == DownloadStatus.Queued).Select(x => x.ToKey()).OrderBy(x => x.Id)
            );
        (await dbContext.GetDownloadTaskKeysByStatusAsync(expectedKeys, [DownloadStatus.Completed], CancellationToken))
            .ShouldBeEmpty();

        // Act
        var progressTasks = await dbContext.GetDownloadProgressTasksByServerAsync(requestedServerId, CancellationToken);

        // Assert
        progressTasks.Select(x => x.Id).Order().ShouldBe(
            allTasks.Where(x => !filterServer || x.PlexServerId == requestedServerId).Select(x => x.Id).Order()
        );
        var actual = progressTasks.Flatten(x => x.Children).OrderBy(x => x.Id).ToArray();
        actual.Select(x => new
        {
            x.Id, x.ParentId, x.RatingKey, x.Title, x.FullTitle, x.MediaType, x.DownloadTaskType,
            x.PlexServerId, x.PlexLibraryId, x.IsDownloadable, x.DownloadStatus,
            x.DataTotal, x.DataReceived, x.Percentage, x.DownloadSpeed, x.TimeRemaining,
            x.FileTransferSpeed, x.FileDataTransferred,
        }).ShouldBe(expected.Select(x => new
        {
            x.Id, x.ParentId, x.RatingKey, x.Title, x.FullTitle, x.MediaType, x.DownloadTaskType,
            x.PlexServerId, x.PlexLibraryId, x.IsDownloadable, x.DownloadStatus,
            x.DataTotal, x.DataReceived, x.Percentage, x.DownloadSpeed, x.TimeRemaining,
            x.FileTransferSpeed, x.FileDataTransferred,
        }));
        if (filterServer)
        {
            var otherServerId = serverIds.Single(x => x != requestedServerId);
            var otherProgress = await dbContext.GetDownloadProgressTasksByServerAsync(otherServerId, CancellationToken);
            otherProgress.Select(x => x.Id).Order().ShouldBe(
                allTasks.Where(x => x.PlexServerId == otherServerId).Select(x => x.Id).Order()
            );
            otherProgress.Select(x => x.Id).Intersect(progressTasks.Select(x => x.Id)).ShouldBeEmpty();
        }
    }
}
