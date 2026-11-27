namespace Reaparr.IntegrationTests;

public class RestartDownloadTaskEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldRestartCompletedMovieDownloadTaskOnRestartCommand_WhenTaskIsDoneDownloading()
    {
        // Arrange
        var seed = new Seed(5594564);
        using var container = await CreateContainer(5594564, config =>
        {
            config.HttpClientOptions = (x, _) =>
            {
                x.SetupIdentityRequest(seed);
            };

            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = 1;
                x.PlexServerCount = 1;
                x.PlexMovieLibraryCount = 2;
                x.MovieCount = 10;
                x.MovieDownloadTasksCount = 1;
                x.DownloadFileSizeInMb = 1;
            };
        });

        var downloadTaskToRestart = await container
            .DbContext.DownloadTaskMovieFile.AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstAsync(CancellationToken);

        var seededMovieData = await container
            .DbContext.PlexMovieData.AsNoTracking()
            .Where(x => x.PlexLibraryId == downloadTaskToRestart.PlexLibraryId)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(CancellationToken);
        seededMovieData.ShouldNotBeNull();

        var seededDownloadTask = await container
            .DbContext.DownloadTaskMovieFile.AsTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(CancellationToken);
        seededDownloadTask.ShouldNotBeNull();

        await container
            .DbContext.DownloadTaskMovieFile.Where(x => x.Id == seededDownloadTask.Id)
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(y => y.PlexApiRatingKey, _ => seededMovieData.PlexApiRatingKey)
                        .SetProperty(y => y.PlexApiMediaId, _ => seededMovieData.PlexApiMediaId)
                        .SetProperty(y => y.PlexApiPartId, _ => seededMovieData.PlexApiPartId)
                        .SetProperty(y => y.FileLocationUrl, _ => seededMovieData.Key),
                CancellationToken
            );

        var downloadTasks = await container.DbContext.GetAllDownloadTasksByServerAsync(
            cancellationToken: CancellationToken
        );
        downloadTasks.Count.ShouldBe(1);
        var downloadTask = downloadTasks[0].Children[0];

        await container.DbContext.SetDownloadStatus(downloadTask.ToKey(), DownloadStatus.Completed);

        // Act
        var client = container.GetApiClient();
        await client.SignIn();
        var testResult = await client.PUTAsync<
            RestartDownloadTaskEndpoint,
            RestartDownloadTaskEndpointRequest,
            BaseResultDTO
        >(new RestartDownloadTaskEndpointRequest { DownloadTaskGuid = downloadTask.Id });
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue(
            await testResult.Response.Content.ReadAsStringAsync(CancellationToken)
        );

        var downloadTaskDb = await container.DbContext.GetDownloadTaskAsync(
            downloadTask.Id,
            cancellationToken: CancellationToken
        );
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadStatus.ShouldBeOneOf(
            DownloadStatus.Queued,
            DownloadStatus.Downloading,
            DownloadStatus.DownloadFinished,
            DownloadStatus.Moving,
            DownloadStatus.Completed
        );

        await WaitForDownloadStatusAsync(container, downloadTask.ToKey(), DownloadStatus.Completed, CancellationToken);

        // Assert
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();

        downloadTaskDb = await container.DbContext.GetDownloadTaskAsync(
            downloadTask.Id,
            cancellationToken: CancellationToken
        );
        downloadTaskDb.ShouldNotBeNull();
        downloadTaskDb.DownloadStatus.ShouldBe(DownloadStatus.Completed);

    }
}
