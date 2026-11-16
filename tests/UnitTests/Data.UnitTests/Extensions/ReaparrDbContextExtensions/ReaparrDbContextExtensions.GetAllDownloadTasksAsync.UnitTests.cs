namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextExtensionsGetAllDownloadTasksAsyncUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldReturnNoDownloadTasks_WhenNoDownloadTasksAreInDb()
    {
        // Arrange
        await SetupDatabase(50574);

        // Act
        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);

        // Assert
        downloadTasks.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnMovieDownloadTasks_WhenMovieDownloadTasksAreInDB()
    {
        // Arrange
        await SetupDatabase(21467, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 2;
            config.MovieCount = 20;
            config.MovieDownloadTasksCount = 10;
        });

        // Act
        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);

        // Assert
        downloadTasks.ShouldNotBeEmpty();
        downloadTasks.Count.ShouldBe(10);

        var flatList = downloadTasks.Flatten(x => x.Children).ToList();
        flatList.ShouldAllBe(x => x.PlexServer != null);
        flatList.ShouldAllBe(x => x.PlexLibrary != null);
    }

    [Test]
    public async Task ShouldAllTvShowDownloadTasksWithAllIncludes_WhenTvShowDownloadTasksAreInDB()
    {
        // Arrange
        await SetupDatabase(2767, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 2;
            config.TvShowCount = 20;
            config.TvShowDownloadTasksCount = 5;
            config.TvShowSeasonDownloadTasksCount = 5;
            config.TvShowEpisodeDownloadTasksCount = 5;
        });

        // Act
        var downloadTasks = await IDbContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);

        // Assert
        downloadTasks.ShouldNotBeEmpty();

        void ValidateDownloadTasks(List<DownloadTaskGeneric>? shouldDownloadTasks)
        {
            if (shouldDownloadTasks is null)
                return;

            downloadTasks.Count.ShouldBe(5);
            foreach (var downloadTask in shouldDownloadTasks)
            {
                downloadTask.PlexServer.ShouldNotBeNull(
                    $"DownloadTaskType {downloadTask.DownloadTaskType} has PlexServer null"
                );
                downloadTask.PlexLibrary.ShouldNotBeNull(
                    $"DownloadTaskType {downloadTask.DownloadTaskType} has PlexLibrary null"
                );
                ValidateDownloadTasks(downloadTask.Children);
            }
        }

        ValidateDownloadTasks(downloadTasks);
        downloadTasks.Flatten(x => x.Children).ToList().Count.ShouldBe(280);
    }
}
