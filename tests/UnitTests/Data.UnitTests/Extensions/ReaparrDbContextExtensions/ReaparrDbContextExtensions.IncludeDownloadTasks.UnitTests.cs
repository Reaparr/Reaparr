namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextExtensionsIncludeDownloadTasksUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldHaveAllMovieDownloadTaskChildrenIncluded_WhenDbContainsNestedDownloadTasks()
    {
        // Arrange
        await SetupDatabase(334734, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.MovieCount = 10;
            config.MovieDownloadTasksCount = 5;
        });

        // Act
        var downloadTasks = IDbContext.DownloadTaskMovie.IncludeAll().ToList();

        // Assert
        downloadTasks.Count.ShouldBe(5);
        foreach (var downloadTask in downloadTasks)
        {
            downloadTask.Children.Count.ShouldBe(1);
            downloadTask.Children.First().ParentId.ShouldBe(downloadTask.Id);
        }
    }

    [Test]
    public async Task ShouldHaveAllTvShowDownloadTaskChildrenIncluded_WhenDbContainsNestedDownloadTasks()
    {
        // Arrange
        await SetupDatabase(93355, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
            config.TvShowCount = 10;
            config.TvShowSeasonCount = 10;
            config.TvShowEpisodeCount = 10;
            config.TvShowDownloadTasksCount = 5;
            config.TvShowSeasonDownloadTasksCount = 5;
            config.TvShowEpisodeDownloadTasksCount = 5;
        });

        // Act
        var downloadTasksDb = IDbContext.DownloadTaskTvShow.IncludeAll().ToList();

        // Assert
        downloadTasksDb.Count.ShouldBe(5);
        downloadTasksDb.Sum(x => x.Count).ShouldBe(280);
    }

    [Test]
    public async Task ShouldHaveAllNestedRelationshipsIncluded_WhenGivenTvShowDownloadTasks()
    {
        // Arrange
        await SetupDatabase(3882, config =>
        {
            config.TvShowDownloadTasksCount = 5;
            config.TvShowSeasonDownloadTasksCount = 5;
            config.TvShowEpisodeDownloadTasksCount = 5;
        });

        // Act
        var downloadTaskTvShows = IDbContext.DownloadTaskTvShow.IncludeAll().ToList();

        // Assert
        downloadTaskTvShows.Count.ShouldBe(5);
        foreach (var downloadTaskTvShow in downloadTaskTvShows)
        {
            downloadTaskTvShow.Children.Count.ShouldBe(5);
            foreach (var downloadTaskTvShowSeason in downloadTaskTvShow.Children)
            {
                downloadTaskTvShowSeason.Children.Count.ShouldBe(5);
                foreach (var downloadTaskEpisode in downloadTaskTvShowSeason.Children)
                    downloadTaskEpisode.Children.Count.ShouldBe(1);
            }
        }
    }
}
