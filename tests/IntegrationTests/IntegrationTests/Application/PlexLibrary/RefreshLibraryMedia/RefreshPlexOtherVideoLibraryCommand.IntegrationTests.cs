namespace Reaparr.IntegrationTests;

public class RefreshPlexOtherVideoLibraryCommandIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPreserveOtherVideosAndOriginals_WhenLibraryIsRefreshedAgain()
    {
        // Arrange
        // Synthetic Plex responses, real hosted refresh and SQLite persistence.
        var seed = new Seed(9625);
        using var container = await CreateContainer(seed, config =>
        {
            config.HttpClientOptions = (http, _) => http.SetupIdentityRequest(seed);
            config.DatabaseOptions = database =>
            {
                database.PlexAccountCount = 1;
                database.PlexServerCount = 1;
                database.PlexOtherVideoLibraryCount = 1;
            };
            config.BaseMockHttpClientOptions = plex =>
            {
                plex.GenerateFromDatabase = true;
                plex.PlexServerAccessCount = 1;
                plex.OtherVideoLibraryCount = 1;
                plex.OtherVideosPerLibraryCount = 3;
            };
        });
        var client = container.GetApiClient();
        await client.SignIn();
        var library = await container.DbContext.PlexLibraries.SingleAsync(CancellationToken);
        library.Type.ShouldBe(PlexMediaType.OtherVideos);
        (await container.DbContext.PlexOtherVideos.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await container.DbContext.PlexOtherVideoData.ToListAsync(CancellationToken)).ShouldBeEmpty();

        // Act
        var response = await client.POSTAsync<RefreshLibraryMediaEndpoint, RefreshLibraryMediaEndpointRequest, ResultDTO<PlexLibraryDTO>>(
            new RefreshLibraryMediaEndpointRequest { PlexLibraryId = library.Id });
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        // Assert
        response.Response.IsSuccessStatusCode.ShouldBeTrue();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.Count.ShouldBe(0);
        using var firstContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var refreshed = await firstContext.PlexLibraries.SingleAsync(CancellationToken);
        (refreshed.Id, refreshed.Type, refreshed.ArtistCount, refreshed.AlbumCount, refreshed.TrackCount,
            refreshed.PhotoAlbumCount, refreshed.PhotoCount, refreshed.PhotoClipCount, refreshed.OtherVideoCount,
            refreshed.MediaSize, refreshed.SyncedAt != null).ShouldBe(
            (library.Id, PlexMediaType.OtherVideos, 0, 0, 0, 0, 0, 0, 3,
                await firstContext.PlexOtherVideoData.SumAsync(x => x.Size, CancellationToken), true));
        var videos = await firstContext.PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey }).ToListAsync(CancellationToken);
        var originals = await firstContext.PlexOtherVideoData.OrderBy(x => x.PlexApiPartId)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexApiMediaId, x.PlexApiPartId, x.PlexOtherVideoId,
                x.OriginalFilename, x.Key, x.Container, x.Duration, x.Size }).ToListAsync(CancellationToken);
        (videos.Count, originals.Count).ShouldBe((3, 3));

        var repeatResponse = await client.POSTAsync<RefreshLibraryMediaEndpoint, RefreshLibraryMediaEndpointRequest, ResultDTO<PlexLibraryDTO>>(
            new RefreshLibraryMediaEndpointRequest { PlexLibraryId = library.Id, ForceLibrarySync = true });
        repeatResponse.Response.IsSuccessStatusCode.ShouldBeTrue();
        repeatResponse.Result.IsSuccess.ShouldBeTrue();
        repeatResponse.Result.Errors.Count.ShouldBe(0);
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        using var repeatedContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (await repeatedContext.PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey }).ToListAsync(CancellationToken)).ShouldBe(videos);
        (await repeatedContext.PlexOtherVideoData.OrderBy(x => x.PlexApiPartId)
            .Select(x => new { x.Id, x.PlexApiRatingKey, x.PlexApiMediaId, x.PlexApiPartId, x.PlexOtherVideoId,
                x.OriginalFilename, x.Key, x.Container, x.Duration, x.Size }).ToListAsync(CancellationToken)).ShouldBe(originals);
    }
}
