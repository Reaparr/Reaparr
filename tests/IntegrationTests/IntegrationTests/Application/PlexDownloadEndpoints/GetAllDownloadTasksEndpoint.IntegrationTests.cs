namespace Reaparr.IntegrationTests;

public class GetAllDownloadTasksEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldHaveAllDownloadTasksNested_WhenTasksAreAvailable()
    {
        // Arrange
        var tvShowDownloadTasksCount = 5;
        var tvShowSeasonDownloadTasksCount = 2;
        var tvShowEpisodeDownloadTasksCount = 3;

        using var container = await CreateContainer(
            45485864,
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexServerCount = 1;
                    x.PlexMovieLibraryCount = 2;
                    x.TvShowCount = 5;
                    x.TvShowDownloadTasksCount = tvShowDownloadTasksCount;
                    x.TvShowSeasonDownloadTasksCount = tvShowSeasonDownloadTasksCount;
                    x.TvShowEpisodeDownloadTasksCount = tvShowEpisodeDownloadTasksCount;
                };
            }
        );
        using var beforeContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var beforeTasks = await beforeContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var expectedDownloads = beforeTasks.ToServerDownloadProgressDTOList();
        expectedDownloads.ShouldNotBeEmpty();

        var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var testResult = await client.GETAsync<
            GetAllDownloadTasksEndpoint,
            ResultDTO<DownloadSnapshotDTO>
        >();

        // Assert
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue();
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        result.Value.Downloads.ShouldBeEquivalentTo(expectedDownloads);
        result.Value.PatchSequenceByServer.ShouldBeEmpty();

        var plexServer = result.Value.Downloads.Single();
        plexServer.Downloads.Count.ShouldBe(tvShowDownloadTasksCount);
        foreach (var downloadProgressDto in plexServer.Downloads)
        {
            downloadProgressDto.Children.Count.ShouldBe(tvShowSeasonDownloadTasksCount);
            foreach (var child in downloadProgressDto.Children)
                child.Children.Count.ShouldBe(tvShowEpisodeDownloadTasksCount);
        }

        using var afterContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var afterTasks = await afterContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        afterTasks.ToServerDownloadProgressDTOList().ShouldBeEquivalentTo(expectedDownloads);
    }

    [Test]
    public async Task ShouldReturnEmptySnapshotWithoutMutatingPersistence_WhenNoTasksAreAvailable()
    {
        // Arrange
        using var container = await CreateContainer(
            45485865,
            config =>
            {
                config.DatabaseOptions = x => x.PlexServerCount = 1;
            }
        );
        using var beforeContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var beforeTasks = await beforeContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        beforeTasks.ShouldBeEmpty();

        var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var testResult = await client.GETAsync<
            GetAllDownloadTasksEndpoint,
            ResultDTO<DownloadSnapshotDTO>
        >();

        // Assert
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue();
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        result.Value.Downloads.ShouldBeEmpty();
        result.Value.PatchSequenceByServer.ShouldBeEmpty();

        using var afterContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var afterTasks = await afterContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        afterTasks.ShouldBeEmpty();
    }

    [Test]
    public async Task ShouldReturnDispatcherSequenceWatermark_WhenPatchWasPublishedBeforeHostedGet()
    {
        // Arrange
        using var container = await CreateContainer(
            45485866,
            config =>
            {
                config.DatabaseOptions = x => x.PlexServerCount = 1;
            }
        );
        var key = new DownloadTaskKey
        {
            Id = Guid.NewGuid(),
            PlexServerId = 1,
            PlexLibraryId = 0,
            Type = DownloadTaskType.MovieData,
        };
        await container.Resolve<IDownloadTaskUpdateDispatcher>()
            .OnStatusChangedAsync(key, DownloadStatus.Deleted, CancellationToken);
        container.MockDownloadHubService.DownloadPatchList.TryTake(out var patch).ShouldBeTrue();
        patch.ServerId.ShouldBe(1);
        patch.Sequence.ShouldBe(1);
        patch.Upserts.ShouldBeEmpty();
        patch.DeletedIds.ShouldBe([key.Id]);

        var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var testResult = await client.GETAsync<
            GetAllDownloadTasksEndpoint,
            ResultDTO<DownloadSnapshotDTO>
        >();

        // Assert
        testResult.Response.IsSuccessStatusCode.ShouldBeTrue();
        var result = testResult.Result;
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        result.Value.Downloads.ShouldBeEmpty();
        result.Value.PatchSequenceByServer.ShouldBe(new Dictionary<int, long> { [1] = 1 });
    }
}
