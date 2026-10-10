namespace Reaparr.IntegrationTests;

public class RefreshPlexMusicLibraryCommandIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPreserveMusicHierarchyAndOriginals_WhenLibraryIsRefreshedAgain()
    {
        // Arrange
        // Synthetic Plex responses, real hosted refresh and SQLite persistence.
        var seed = new Seed(9625);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (http, _) => http.SetupIdentityRequest(seed);
                config.DatabaseOptions = database =>
                {
                    database.PlexAccountCount = 1;
                    database.PlexServerCount = 1;
                    database.PlexMusicLibraryCount = 1;
                };
                config.BaseMockHttpClientOptions = plex =>
                {
                    plex.GenerateFromDatabase = true;
                    plex.PlexServerAccessCount = 1;
                    plex.MusicLibraryCount = 1;
                    plex.ArtistsPerLibraryCount = 2;
                    plex.AlbumsPerArtistCount = 2;
                    plex.TracksPerAlbumCount = 2;
                };
            }
        );
        var client = container.GetApiClient();
        await client.SignIn();
        var library = await container.DbContext.PlexLibraries.SingleAsync(CancellationToken);
        library.Type.ShouldBe(PlexMediaType.MusicArtist);
        (await container.DbContext.PlexArtists.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await container.DbContext.PlexAlbums.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await container.DbContext.PlexTracks.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await container.DbContext.PlexTrackData.ToListAsync(CancellationToken)).ShouldBeEmpty();

        // Act
        var response = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<PlexLibraryDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = library.Id });
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        // Assert
        response.Response.IsSuccessStatusCode.ShouldBeTrue();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.Count.ShouldBe(0);
        using var firstContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var refreshed = await firstContext.PlexLibraries.SingleAsync(CancellationToken);
        (
            refreshed.Id,
            refreshed.Type,
            refreshed.MusicArtistCount,
            refreshed.MusicAlbumCount,
            refreshed.MusicTrackCount,
            refreshed.PhotoAlbumCount,
            refreshed.PhotoImageCount,
            refreshed.PhotoClipCount,
            refreshed.OtherVideoCount,
            refreshed.MediaSize,
            refreshed.SyncedAt != null
        ).ShouldBe(
            (
                library.Id,
                PlexMediaType.MusicArtist,
                2,
                4,
                8,
                0,
                0,
                0,
                0,
                await firstContext.PlexTrackData.SumAsync(x => x.Size, CancellationToken),
                true
            )
        );
        var artists = await firstContext
            .PlexArtists.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var albums = await firstContext
            .PlexAlbums.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexArtistId,
            })
            .ToListAsync(CancellationToken);
        var tracks = await firstContext
            .PlexTracks.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexAlbumId,
            })
            .ToListAsync(CancellationToken);
        var originals = await firstContext
            .PlexTrackData.OrderBy(x => x.PlexApiPartId)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexTrackId,
                x.OriginalFilename,
                x.Key,
                x.Container,
                x.Duration,
                x.Size,
            })
            .ToListAsync(CancellationToken);
        (artists.Count, albums.Count, tracks.Count, originals.Count).ShouldBe((2, 4, 8, 8));

        var repeatResponse = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<PlexLibraryDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = library.Id, ForceLibrarySync = true });
        repeatResponse.Response.IsSuccessStatusCode.ShouldBeTrue();
        repeatResponse.Result.IsSuccess.ShouldBeTrue();
        repeatResponse.Result.Errors.Count.ShouldBe(0);
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        using var repeatedContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (
            await repeatedContext
                .PlexArtists.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(artists);
        (
            await repeatedContext
                .PlexAlbums.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexArtistId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(albums);
        (
            await repeatedContext
                .PlexTracks.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexAlbumId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(tracks);
        (
            await repeatedContext
                .PlexTrackData.OrderBy(x => x.PlexApiPartId)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexTrackId,
                    x.OriginalFilename,
                    x.Key,
                    x.Container,
                    x.Duration,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(originals);
    }
}
