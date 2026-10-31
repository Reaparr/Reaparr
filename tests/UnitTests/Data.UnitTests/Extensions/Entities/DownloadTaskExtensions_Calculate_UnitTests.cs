namespace Reaparr.Data.UnitTests.Entities;

public class DownloadTaskExtensionsCalculateUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldUpdateDownloadProgressAcrossAllLayers_WhenCallingCalculateOnRootDownloadTask()
    {
        // Arrange
        await SetupDatabase(
            72869,
            config =>
            {
                config.TvShowDownloadTasksCount = 5;
                config.TvShowSeasonDownloadTasksCount = 5;
                config.TvShowEpisodeDownloadTasksCount = 5;
            }
        );

        var downloadTasks = await IDbContext.DownloadTaskTvShow.IncludeAll().ToListAsync(CancellationToken);

        foreach (var downloadTaskTvShow in downloadTasks)
        foreach (var downloadTaskTvShowSeason in downloadTaskTvShow.Children)
        foreach (var downloadTaskTvShowEpisode in downloadTaskTvShowSeason.Children)
        foreach (var downloadTaskTvShowEpisodeFile in downloadTaskTvShowEpisode.Children)
        {
            downloadTaskTvShowEpisodeFile.DownloadSpeed = 20;
            downloadTaskTvShowEpisodeFile.FileTransferSpeed = 100;
            downloadTaskTvShowEpisodeFile.DataReceived = 5000;
            downloadTaskTvShowEpisodeFile.DataTotal = 10000;
            downloadTaskTvShowEpisodeFile.Percentage = 50;
        }

        // Act
        foreach (var downloadTaskTvShow in downloadTasks)
            downloadTaskTvShow.Calculate();

        // Assert
        downloadTasks.ShouldAllBe(x => x.DownloadSpeed == 20);
        downloadTasks.ShouldAllBe(x => x.FileTransferSpeed == 100);
        downloadTasks.ShouldAllBe(x => x.DataReceived == 125000);
        downloadTasks.ShouldAllBe(x => x.DataTotal == 250000);
        downloadTasks.ShouldAllBe(x => x.Percentage == 50);

        downloadTasks.SelectMany(x => x.Children).ShouldAllBe(x => x.DownloadSpeed == 20);
        downloadTasks.SelectMany(x => x.Children).ShouldAllBe(x => x.FileTransferSpeed == 100);
        downloadTasks.SelectMany(x => x.Children).ShouldAllBe(x => x.DataReceived == 25000);
        downloadTasks.SelectMany(x => x.Children).ShouldAllBe(x => x.DataTotal == 50000);
        downloadTasks.SelectMany(x => x.Children).ShouldAllBe(x => x.Percentage == 50);

        downloadTasks.SelectMany(x => x.Children).SelectMany(x => x.Children).ShouldAllBe(x => x.DownloadSpeed == 20);
        downloadTasks
            .SelectMany(x => x.Children)
            .SelectMany(x => x.Children)
            .ShouldAllBe(x => x.FileTransferSpeed == 100);
        downloadTasks.SelectMany(x => x.Children).SelectMany(x => x.Children).ShouldAllBe(x => x.DataReceived == 5000);
        downloadTasks.SelectMany(x => x.Children).SelectMany(x => x.Children).ShouldAllBe(x => x.DataTotal == 10000);
        downloadTasks.SelectMany(x => x.Children).SelectMany(x => x.Children).ShouldAllBe(x => x.Percentage == 50);
    }

    [Test]
    public async Task ShouldCalculateRootTimeRemaining_FromTotalReceivedAndTotalSize()
    {
        // Arrange
        await SetupDatabase(
            72870,
            config =>
            {
                config.TvShowDownloadTasksCount = 5;
                config.TvShowSeasonDownloadTasksCount = 5;
                config.TvShowEpisodeDownloadTasksCount = 5;
            }
        );

        var downloadTask = await IDbContext.DownloadTaskTvShow.IncludeAll().FirstAsync(CancellationToken);
        var files = downloadTask.Children.SelectMany(x => x.Children).SelectMany(x => x.Children).ToList();
        files.ForEach(x => x.DataTotal = 10_000);
        files.Take(5).ToList().ForEach(x => x.DataReceived = 10_000);
        files[5].DataReceived = 5_000;
        files[5].DownloadSpeed = 20;
        files[5].TimeRemaining = 250;

        // Act
        var result = downloadTask.ToGeneric();
        result.Calculate();

        // Assert
        result.DataReceived.ShouldBe(55_000);
        result.DataTotal.ShouldBe(250_000);
        result.DownloadSpeed.ShouldBe(20);
        result.TimeRemaining.ShouldBe(9_750);
    }

    [Test]
    public async Task ShouldCalculateMovingRootTimeRemaining_FromFileTransferProgress()
    {
        // Arrange
        await SetupDatabase(72871, config => config.MovieDownloadTasksCount = 1);

        var downloadTask = await IDbContext.DownloadTaskMovie.IncludeAll().FirstAsync(CancellationToken);
        var downloadFile = downloadTask.Children.Single();
        downloadFile.DownloadStatus = DownloadStatus.Moving;
        downloadFile.DataTotal = 1_000;
        downloadFile.DataReceived = 1_000;
        downloadFile.DownloadSpeed = 0;
        downloadFile.FileDataTransferred = 500;
        downloadFile.FileTransferSpeed = 50;

        // Act
        var result = downloadTask.ToGeneric();
        result.Calculate();

        // Assert
        result.DownloadStatus.ShouldBe(DownloadStatus.Moving);
        result.TimeRemaining.ShouldBe(10);
    }
}
