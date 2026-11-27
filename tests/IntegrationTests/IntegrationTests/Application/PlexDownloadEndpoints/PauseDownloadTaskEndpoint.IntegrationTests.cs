namespace Reaparr.IntegrationTests;

public class PauseDownloadTaskEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPauseADownloadTask_WhenDownloadTaskIsInProgressAndIsPaused()
    {
        // Arrange
        var seed = new Seed(21345);
        using var container = await CreateContainer(seed, config =>
        {
            config.DownloadSpeedLimitInKib = 500;
            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = 1;
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 1;
                x.MovieCount = 1;
                x.MovieDownloadTasksCount = 1;
                x.DownloadFileSizeInMb = 50;
            };

            config.HttpClientOptions = (x, _) =>
            {
                x.SetupIdentityRequest(seed);
            };

            config.MockDownloadServiceFactory = MockDownloadService.PausableFactory;

            config.FileSystemOptions = (system, dbContext) =>
            {
                var downloadTask = dbContext.DownloadTaskMovieFile.First();
                downloadTask.DownloadFilePath.ShouldNotBeNullOrEmpty();

                var directoryPath = system.Path.GetDirectoryName(downloadTask.DownloadFilePath);
                directoryPath.ShouldNotBeNullOrEmpty();
                system.Directory.CreateDirectory(directoryPath);
                using var partialFile = system.File.Create(downloadTask.DownloadFilePath);
                partialFile.SetLength(downloadTask.DataTotal / 4);
            };
        });

        var downloadTasks = await container.DbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        downloadTasks.Count.ShouldBe(1);
        var childDownloadTask = downloadTasks[0].Children[0];

        // Act
        var client = container.GetApiClient();
        await client.SignIn();

        var startTestResult = await client.PUTAsync<
            StartDownloadTaskEndpoint,
            StartDownloadTaskEndpointRequest,
            BaseResultDTO
        >(new StartDownloadTaskEndpointRequest { DownloadTaskGuid = childDownloadTask.Id });
        startTestResult.Response.IsSuccessStatusCode.ShouldBeTrue(
            await startTestResult.Response.Content.ReadAsStringAsync(CancellationToken)
        );
        startTestResult.Result.IsSuccess.ShouldBeTrue();

        // Wait for the download to actually start
        await WaitForDatabaseConditionAsync(
            () =>
            {
                var task = container
                    .DbContext.DownloadTaskMovieFile.AsNoTracking()
                    .FirstOrDefault(x => x.Id == childDownloadTask.Id);
                return task?.DownloadStatus == DownloadStatus.Downloading;
            },
            maxRetries: 20,
            delayMs: 500
        );

        // Pause the download
        var pauseTestResult = await client.PUTAsync<
            PauseDownloadTaskEndpoint,
            PauseDownloadTaskEndpointRequest,
            BaseResultDTO
        >(new PauseDownloadTaskEndpointRequest { DownloadTaskGuid = childDownloadTask.Id });
        pauseTestResult.Response.IsSuccessStatusCode.ShouldBeTrue(
            await pauseTestResult.Response.Content.ReadAsStringAsync(CancellationToken)
        );
        pauseTestResult.Result.IsSuccess.ShouldBeTrue();

        // Wait for the database to reflect the paused status
        await WaitForDatabaseConditionAsync(
            () =>
            {
                var task = container
                    .DbContext.DownloadTaskMovieFile.AsNoTracking()
                    .FirstOrDefault(x => x.Id == childDownloadTask.Id);
                return task?.DownloadStatus == DownloadStatus.Paused;
            },
            maxRetries: 30,
            delayMs: 500
        );

        // Assert - use AsNoTracking to ensure fresh data from database
        var downloadTaskDb = await container
            .DbContext.DownloadTaskMovieFile.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == childDownloadTask.Id, CancellationToken);
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadStatus.ShouldBe(DownloadStatus.Paused);
    }
}
