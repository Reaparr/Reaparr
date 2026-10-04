namespace Reaparr.IntegrationTests;

public class RefreshPlexPhotoLibraryCommandIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPreservePhotoCatalogAndOtherLibraries_WhenRefreshingAndDisablingReenablingPhotos()
    {
        // Arrange
        // Synthetic Plex fixtures exercise the hosted pipeline; this is not live Plex acceptance.
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
                    database.PlexPhotoLibraryCount = 1;
                    database.PlexOtherVideoLibraryCount = 1;
                    database.MusicArtistCount = 2;
                    database.MusicAlbumCount = 2;
                    database.MusicTrackCount = 2;
                    database.OtherVideoCount = 3;
                };
                config.BaseMockHttpClientOptions = plex =>
                {
                    plex.GenerateFromDatabase = true;
                    plex.PlexServerAccessCount = 1;
                    plex.MusicLibraryCount = 1;
                    plex.PhotoLibraryCount = 1;
                    plex.OtherVideoLibraryCount = 1;
                    plex.ArtistsPerLibraryCount = 2;
                    plex.AlbumsPerArtistCount = 2;
                    plex.TracksPerAlbumCount = 2;
                    plex.PhotoAlbumsPerLibraryCount = 2;
                    plex.PhotosPerAlbumCount = 2;
                    plex.PhotoClipsPerAlbumCount = 1;
                    plex.OtherVideosPerLibraryCount = 3;
                };
            }
        );
        var client = container.GetApiClient();
        await client.SignIn();
        var photoLibrary = await container.DbContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        using var beforeContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (await beforeContext.PlexPhotoAlbums.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await beforeContext.PlexPhotoImages.ToListAsync(CancellationToken)).ShouldBeEmpty();
        (await beforeContext.PlexPhotoData.ToListAsync(CancellationToken)).ShouldBeEmpty();
        var firstArtists = await beforeContext
            .PlexArtists.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var firstAlbums = await beforeContext
            .PlexAlbums.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexArtistId,
            })
            .ToListAsync(CancellationToken);
        var firstTracks = await beforeContext
            .PlexTracks.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexAlbumId,
            })
            .ToListAsync(CancellationToken);
        var firstTrackData = await beforeContext
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
        var firstVideos = await beforeContext
            .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var firstVideoData = await beforeContext
            .PlexOtherVideoData.OrderBy(x => x.PlexApiPartId)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexOtherVideoId,
                x.OriginalFilename,
                x.Key,
                x.Container,
                x.Duration,
                x.Size,
            })
            .ToListAsync(CancellationToken);
        (
            firstArtists.Count,
            firstAlbums.Count,
            firstTracks.Count,
            firstTrackData.Count,
            firstVideos.Count,
            firstVideoData.Count
        ).ShouldBe((2, 4, 8, 8, 3, 3));

        // Act
        var response = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<PlexLibraryDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = photoLibrary.Id });
        response.Response.IsSuccessStatusCode.ShouldBeTrue();
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.Count.ShouldBe(0);
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        // Assert
        using var firstContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var refreshedLibrary = await firstContext.PlexLibraries.SingleAsync(
            x => x.Id == photoLibrary.Id,
            CancellationToken
        );
        (
            refreshedLibrary.Id,
            refreshedLibrary.Type,
            refreshedLibrary.PhotoAlbumCount,
            refreshedLibrary.PhotoImageCount,
            refreshedLibrary.PhotoClipCount,
            refreshedLibrary.MediaSize,
            refreshedLibrary.SyncedAt != null
        ).ShouldBe(
            (
                photoLibrary.Id,
                PlexMediaType.PhotoAlbum,
                2,
                4,
                2,
                await firstContext.PlexPhotoData.SumAsync(x => x.Size, CancellationToken),
                true
            )
        );
        refreshedLibrary.MediaCount.ShouldBe(2);
        var firstPhotoAlbums = await firstContext
            .PlexPhotoAlbums.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var firstPhotos = await firstContext
            .PlexPhotoImages.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexPhotoAlbumId,
            })
            .ToListAsync(CancellationToken);
        var firstPhotoData = await firstContext
            .PlexPhotoData.OrderBy(x => x.PlexApiPartId)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexPhotoId,
                x.OriginalFilename,
                x.Key,
                x.Container,
                x.Duration,
                x.Size,
            })
            .ToListAsync(CancellationToken);

        var repeatResponse = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<PlexLibraryDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = photoLibrary.Id, ForceLibrarySync = true });
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
        ).ShouldBe(firstArtists);
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
        ).ShouldBe(firstAlbums);
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
        ).ShouldBe(firstTracks);
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
        ).ShouldBe(firstTrackData);
        (
            await repeatedContext
                .PlexPhotoAlbums.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstPhotoAlbums);
        (
            await repeatedContext
                .PlexPhotoImages.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexPhotoAlbumId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstPhotos);
        (
            await repeatedContext
                .PlexPhotoData.OrderBy(x => x.PlexApiPartId)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexPhotoId,
                    x.OriginalFilename,
                    x.Key,
                    x.Container,
                    x.Duration,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstPhotoData);
        (
            await repeatedContext
                .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideos);
        (
            await repeatedContext
                .PlexOtherVideoData.OrderBy(x => x.PlexApiPartId)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexOtherVideoId,
                    x.OriginalFilename,
                    x.Key,
                    x.Container,
                    x.Duration,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideoData);

        var disableResponse = await client.PUTAsync<
            SetLibraryEnabledEndpoint,
            SetLibraryEnabledRequest,
            ResultDTO<PlexLibraryDTO>
        >(new SetLibraryEnabledRequest { PlexLibraryId = photoLibrary.Id, IsEnabled = false });
        disableResponse.Response.IsSuccessStatusCode.ShouldBeTrue();
        disableResponse.Result.IsSuccess.ShouldBeTrue();
        disableResponse.Result.Errors.Count.ShouldBe(0);

        using var disabledContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var disabledPhotoLibrary = await disabledContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .SingleAsync(x => x.Id == photoLibrary.Id, CancellationToken);
        disabledPhotoLibrary.IsEnabled.ShouldBeFalse();
        disabledPhotoLibrary.SyncedAt.ShouldBeNull();
        disabledPhotoLibrary.SyncedContentChangedAt.ShouldBeNull();
        disabledPhotoLibrary.MediaSize.ShouldBe(0);
        disabledPhotoLibrary.PhotoAlbumCount.ShouldBe(0);
        disabledPhotoLibrary.PhotoImageCount.ShouldBe(0);
        disabledPhotoLibrary.PhotoClipCount.ShouldBe(0);
        (
            await disabledContext.PlexPhotoAlbums.CountAsync(x => x.PlexLibraryId == photoLibrary.Id, CancellationToken)
        ).ShouldBe(0);
        (
            await disabledContext.PlexPhotoImages.CountAsync(x => x.PlexLibraryId == photoLibrary.Id, CancellationToken)
        ).ShouldBe(0);
        (
            await disabledContext.PlexPhotoData.CountAsync(x => x.PlexLibraryId == photoLibrary.Id, CancellationToken)
        ).ShouldBe(0);
        (
            await disabledContext
                .PlexArtists.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstArtists);
        (
            await disabledContext
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
        ).ShouldBe(firstTrackData);
        (
            await disabledContext
                .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideos);
        (
            await disabledContext
                .PlexOtherVideoData.OrderBy(x => x.PlexApiPartId)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexOtherVideoId,
                    x.OriginalFilename,
                    x.Key,
                    x.Container,
                    x.Duration,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideoData);

        var enableResponse = await client.PUTAsync<
            SetLibraryEnabledEndpoint,
            SetLibraryEnabledRequest,
            ResultDTO<PlexLibraryDTO>
        >(new SetLibraryEnabledRequest { PlexLibraryId = photoLibrary.Id, IsEnabled = true });
        enableResponse.Response.IsSuccessStatusCode.ShouldBeTrue();
        enableResponse.Result.IsSuccess.ShouldBeTrue();
        enableResponse.Result.Errors.Count.ShouldBe(0);
        await WaitForDatabaseConditionAsync(
            async () =>
            {
                using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
                var refreshed = await context.PlexLibraries.SingleAsync(
                    x => x.Id == photoLibrary.Id,
                    CancellationToken
                );
                return refreshed.SyncedAt is not null
                    && refreshed.PhotoAlbumCount == 2
                    && refreshed.PhotoImageCount == 4
                    && refreshed.PhotoClipCount == 2
                    && await context.PlexPhotoAlbums.CountAsync(
                        x => x.PlexLibraryId == photoLibrary.Id,
                        CancellationToken
                    ) == 2
                    && await context.PlexPhotoImages.CountAsync(
                        x => x.PlexLibraryId == photoLibrary.Id,
                        CancellationToken
                    ) == 6
                    && await context.PlexPhotoData.CountAsync(
                        x => x.PlexLibraryId == photoLibrary.Id,
                        CancellationToken
                    ) == 6;
            },
            maxRetries: 60,
            delayMs: 250
        );

        using var enabledContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var enabledPhotoLibrary = await enabledContext.PlexLibraries.SingleAsync(
            x => x.Id == photoLibrary.Id,
            CancellationToken
        );
        enabledPhotoLibrary.IsEnabled.ShouldBeTrue();
        enabledPhotoLibrary.SyncedAt.ShouldNotBeNull();
        enabledPhotoLibrary.PhotoAlbumCount.ShouldBe(2);
        enabledPhotoLibrary.PhotoImageCount.ShouldBe(4);
        enabledPhotoLibrary.PhotoClipCount.ShouldBe(2);
        enabledPhotoLibrary.MediaSize.ShouldBe(
            await enabledContext.PlexPhotoData.SumAsync(x => x.Size, CancellationToken)
        );
        var rebuiltPhotoAlbums = await enabledContext
            .PlexPhotoAlbums.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        rebuiltPhotoAlbums.ShouldBe(firstPhotoAlbums.Select(x => new { x.PlexApiRatingKey }).ToList());
        var rebuiltPhotos = await enabledContext
            .PlexPhotoImages.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.PlexApiRatingKey, ParentRatingKey = x.PlexPhotoAlbum!.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var expectedPhotos = firstPhotos
            .Join(
                firstPhotoAlbums,
                x => x.PlexPhotoAlbumId,
                x => x.Id,
                (photo, album) => new { photo.PlexApiRatingKey, ParentRatingKey = album.PlexApiRatingKey }
            )
            .ToList();
        rebuiltPhotos.ShouldBe(expectedPhotos);
        var rebuiltPhotoData = await enabledContext
            .PlexPhotoData.OrderBy(x => x.PlexApiPartId)
            .Select(x => new
            {
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                ParentRatingKey = x.PlexPhoto!.PlexApiRatingKey,
                x.OriginalFilename,
                x.Key,
                x.Container,
                x.Duration,
                x.Size,
            })
            .ToListAsync(CancellationToken);
        var expectedPhotoData = firstPhotoData
            .Join(
                firstPhotos,
                x => x.PlexPhotoId,
                x => x.Id,
                (data, photo) =>
                    new
                    {
                        data.PlexApiRatingKey,
                        data.PlexApiMediaId,
                        data.PlexApiPartId,
                        ParentRatingKey = photo.PlexApiRatingKey,
                        data.OriginalFilename,
                        data.Key,
                        data.Container,
                        data.Duration,
                        data.Size,
                    }
            )
            .ToList();
        rebuiltPhotoData.ShouldBe(expectedPhotoData);
    }
}
