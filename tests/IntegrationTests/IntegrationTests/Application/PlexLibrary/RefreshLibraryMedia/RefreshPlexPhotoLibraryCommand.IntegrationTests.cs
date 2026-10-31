using System.Net.Http.Json;

namespace Reaparr.IntegrationTests;

public class RefreshPlexPhotoLibraryCommandIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPersistMergedPhotoFilesAndMemberships_ThenPreserveOtherLibrariesAcrossRefreshDisableReenable()
    {
        // Arrange
        var seed = new Seed(9625);
        using var container = await CreatePhotoRefreshContainer(seed);
        using var client = container.GetApiClient();
        await client.SignIn();
        var factory = container.Resolve<IReaparrDbContextFactory>();
        using var beforeContext = await factory.CreateAsync();
        var photoLibrary = await beforeContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var firstArtists = await beforeContext
            .PlexMusicArtists.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var firstVideos = await beforeContext
            .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new { x.Id, x.PlexApiRatingKey })
            .ToListAsync(CancellationToken);
        var firstMusicAlbums = await beforeContext
            .PlexMusicAlbums.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexArtistId,
            })
            .ToListAsync(CancellationToken);
        var firstTracks = await beforeContext
            .PlexMusicTracks.OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexAlbumId,
            })
            .ToListAsync(CancellationToken);
        var firstTrackData = await beforeContext
            .PlexMusicTrackData.OrderBy(x => x.PlexApiPartId)
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
            firstMusicAlbums.Count,
            firstTracks.Count,
            firstTrackData.Count,
            firstVideos.Count,
            firstVideoData.Count
        ).ShouldBe((2, 4, 8, 8, 3, 3));
        (await beforeContext.PlexPhotoAlbums.CountAsync(CancellationToken)).ShouldBe(0);
        (await beforeContext.PlexPhotoMediaData.CountAsync(CancellationToken)).ShouldBe(0);

        var beforeBrowse = await ReadBrowseAssetsAsync(client, photoLibrary.Id);
        beforeBrowse.Items.ShouldBeEmpty();
        (beforeBrowse.TotalCount, beforeBrowse.TotalBytes).ShouldBe((0, 0L));

        // Act
        var refresh = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<LibrarySyncJobQueueDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = photoLibrary.Id });
        refresh.Response.IsSuccessStatusCode.ShouldBeTrue();
        refresh.Result.IsSuccess.ShouldBeTrue();
        refresh.Result.Errors.Count.ShouldBe(0);
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        // Assert
        using var firstContext = await factory.CreateAsync();
        var refreshedLibrary = await firstContext.PlexLibraries.SingleAsync(
            x => x.Id == photoLibrary.Id,
            CancellationToken
        );
        (
            refreshedLibrary.PhotoAlbumCount,
            refreshedLibrary.PhotoImageCount,
            refreshedLibrary.PhotoClipCount,
            refreshedLibrary.MediaCount,
            refreshedLibrary.SyncedAt is not null,
            refreshedLibrary.Outdated,
            refreshedLibrary.SyncedContentChangedAt
        ).ShouldBe((2, 3, 2, 2, true, false, refreshedLibrary.ContentChangedAt));
        var firstAlbums = await firstContext
            .PlexPhotoAlbums.Where(x => x.PlexLibraryId == photoLibrary.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.Title,
            })
            .ToListAsync(CancellationToken);
        var firstFiles = await firstContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == photoLibrary.Id)
            .OrderBy(x => x.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .Select(x => new
            {
                x.Id,
                x.PlexApiRatingKey,
                x.PlexApiMediaId,
                x.PlexApiPartId,
                x.PlexPhotoFolderId,
                x.AssetKind,
                x.OriginalFilename,
                x.Key,
                x.Container,
                x.Size,
            })
            .ToListAsync(CancellationToken);
        var firstMemberships = await firstContext
            .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbum!.PlexLibraryId == photoLibrary.Id)
            .OrderBy(x => x.PlexPhotoAlbumId)
            .ThenBy(x => x.PlexPhotoMediaDataId)
            .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
            .ToListAsync(CancellationToken);
        var firstFolders = await firstContext
            .PlexPhotoFolders.Where(x => x.PlexLibraryId == photoLibrary.Id)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.RelativePath,
                x.Title,
                x.RootIdentityKey,
                x.SourceRootPath,
                x.PlexApiLocationId,
                x.IsActive,
            })
            .ToListAsync(CancellationToken);
        firstAlbums.Count.ShouldBe(2);
        firstFiles.Count.ShouldBe(5);
        firstMemberships.Count.ShouldBe(6);
        firstFolders.ShouldContain(x => x.ParentId == null && x.IsActive == true);
        firstFolders.Where(x => x.ParentId == null).Count().ShouldBe(1);
        firstFiles.ShouldAllBe(x => x.PlexPhotoFolderId.HasValue && x.OriginalFilename.Length > 0);
        firstFiles.Select(x => x.AssetKind).ShouldContain(PhotoAssetKind.Image);
        firstFiles.Select(x => x.AssetKind).ShouldContain(PhotoAssetKind.VideoClip);
        firstFolders.ShouldAllBe(x => x.ParentId == null || x.RelativePath.Length > 0);
        firstFolders
            .Where(x => x.ParentId == null)
            .ShouldAllBe(x => x.RootIdentityKey != null && x.SourceRootPath != null);

        var publishedBrowse = await ReadBrowseAssetsAsync(client, photoLibrary.Id);
        publishedBrowse.Items.Select(x => x.Id).Order().ShouldBe(firstFiles.Select(x => x.Id).Order());
        publishedBrowse.Items.ShouldAllBe(x => x.PlexLibraryId == photoLibrary.Id);
        (publishedBrowse.TotalCount, publishedBrowse.TotalBytes).ShouldBe((5, firstFiles.Sum(x => x.Size)));
        (
            await firstContext
                .PlexMusicArtists.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstArtists);
        (
            await firstContext
                .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideos);
        (
            await firstContext
                .PlexMusicAlbums.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexArtistId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstMusicAlbums);
        (
            await firstContext
                .PlexMusicTracks.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexAlbumId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstTracks);
        (
            await firstContext
                .PlexMusicTrackData.OrderBy(x => x.PlexApiPartId)
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
            await firstContext
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

        var repeatedRefresh = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<LibrarySyncJobQueueDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = photoLibrary.Id, ForceLibrarySync = true });
        repeatedRefresh.Response.IsSuccessStatusCode.ShouldBeTrue();
        repeatedRefresh.Result.IsSuccess.ShouldBeTrue();
        repeatedRefresh.Result.Errors.Count.ShouldBe(0);
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);

        using var repeatedContext = await factory.CreateAsync();
        (
            await repeatedContext
                .PlexPhotoAlbums.Where(x => x.PlexLibraryId == photoLibrary.Id)
                .OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.Title,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstAlbums);
        (
            await repeatedContext
                .PlexPhotoMediaData.Where(x => x.PlexLibraryId == photoLibrary.Id)
                .OrderBy(x => x.PlexApiRatingKey)
                .ThenBy(x => x.PlexApiMediaId)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexPhotoFolderId,
                    x.AssetKind,
                    x.OriginalFilename,
                    x.Key,
                    x.Container,
                    x.Size,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstFiles);
        (
            await repeatedContext
                .PlexPhotoAlbumMemberships.Where(x => x.PlexPhotoAlbum!.PlexLibraryId == photoLibrary.Id)
                .OrderBy(x => x.PlexPhotoAlbumId)
                .ThenBy(x => x.PlexPhotoMediaDataId)
                .Select(x => new { x.PlexPhotoAlbumId, x.PlexPhotoMediaDataId })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstMemberships);
        (
            await repeatedContext
                .PlexPhotoFolders.Where(x => x.PlexLibraryId == photoLibrary.Id)
                .OrderBy(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.ParentId,
                    x.RelativePath,
                    x.Title,
                    x.RootIdentityKey,
                    x.SourceRootPath,
                    x.PlexApiLocationId,
                    x.IsActive,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstFolders);
        (
            await repeatedContext
                .PlexMusicAlbums.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexArtistId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstMusicAlbums);
        (
            await repeatedContext
                .PlexMusicTracks.OrderBy(x => x.PlexApiRatingKey)
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
                .PlexMusicTrackData.OrderBy(x => x.PlexApiPartId)
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

        var disable = await client.PUTAsync<
            SetLibraryEnabledEndpoint,
            SetLibraryEnabledRequest,
            ResultDTO<PlexLibraryDTO>
        >(new SetLibraryEnabledRequest { PlexLibraryId = photoLibrary.Id, IsEnabled = false });
        disable.Response.IsSuccessStatusCode.ShouldBeTrue();
        disable.Result.IsSuccess.ShouldBeTrue();
        disable.Result.Errors.Count.ShouldBe(0);

        using var disabledBrowseResponse = await client.GetAsync(
            $"{ApiRoutes.PlexPhotoController}/assets?plexLibraryId={photoLibrary.Id}",
            CancellationToken
        );
        disabledBrowseResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var disabledContext = await factory.CreateAsync();
        var disabledLibrary = await disabledContext
            .PlexLibraries.IgnoreIsEnabledFilter()
            .SingleAsync(x => x.Id == photoLibrary.Id, CancellationToken);
        disabledLibrary.IsEnabled.ShouldBeFalse();
        disabledLibrary.SyncedAt.ShouldBeNull();
        (disabledLibrary.PhotoAlbumCount, disabledLibrary.PhotoImageCount, disabledLibrary.PhotoClipCount).ShouldBe(
            (0, 0, 0)
        );
        (
            await disabledContext.PlexPhotoAlbums.CountAsync(x => x.PlexLibraryId == photoLibrary.Id, CancellationToken)
        ).ShouldBe(0);
        (
            await disabledContext.PlexPhotoMediaData.CountAsync(
                x => x.PlexLibraryId == photoLibrary.Id,
                CancellationToken
            )
        ).ShouldBe(0);
        (
            await disabledContext.PlexPhotoAlbumMemberships.CountAsync(
                x => x.PlexPhotoAlbum!.PlexLibraryId == photoLibrary.Id,
                CancellationToken
            )
        ).ShouldBe(0);
        (
            await disabledContext.PlexPhotoFolders.CountAsync(
                x => x.PlexLibraryId == photoLibrary.Id && x.ParentId != null,
                CancellationToken
            )
        ).ShouldBe(0);
        (
            await disabledContext.PlexPhotoFolders.CountAsync(
                x => x.PlexLibraryId == photoLibrary.Id && x.ParentId == null && x.IsActive == true,
                CancellationToken
            )
        ).ShouldBe(0);
        (
            await disabledContext
                .PlexMusicArtists.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstArtists);
        (
            await disabledContext
                .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideos);
        (
            await disabledContext
                .PlexMusicAlbums.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexArtistId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstMusicAlbums);
        (
            await disabledContext
                .PlexMusicTracks.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexAlbumId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstTracks);
        (
            await disabledContext
                .PlexMusicTrackData.OrderBy(x => x.PlexApiPartId)
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

        await container.BackgroundJobScheduler.Standby(CancellationToken);
        try
        {
            var enable = await client.PUTAsync<
                SetLibraryEnabledEndpoint,
                SetLibraryEnabledRequest,
                ResultDTO<PlexLibraryDTO>
            >(new SetLibraryEnabledRequest { PlexLibraryId = photoLibrary.Id, IsEnabled = true });
            enable.Response.IsSuccessStatusCode.ShouldBeTrue();
            enable.Result.IsSuccess.ShouldBeTrue();
            enable.Result.Errors.Count.ShouldBe(0);
            using var pendingContext = await factory.CreateAsync();
            (
                await pendingContext.PlexPhotoFolders.CountAsync(
                    x => x.PlexLibraryId == photoLibrary.Id && x.ParentId == null && x.IsActive == true,
                    CancellationToken
                )
            ).ShouldBe(0);
        }
        finally
        {
            await container.BackgroundJobScheduler.Start(CancellationToken);
        }

        await WaitForDatabaseConditionAsync(
            async () =>
            {
                using var current = await factory.CreateAsync();
                var library = await current.PlexLibraries.SingleAsync(x => x.Id == photoLibrary.Id, CancellationToken);
                return library.IsEnabled
                    && library.SyncedAt is not null
                    && library.PhotoAlbumCount == 2
                    && library.PhotoImageCount == 3
                    && library.PhotoClipCount == 2
                    && await current.PlexPhotoAlbums.CountAsync(
                        x => x.PlexLibraryId == photoLibrary.Id,
                        CancellationToken
                    ) == 2
                    && await current.PlexPhotoMediaData.CountAsync(
                        x => x.PlexLibraryId == photoLibrary.Id,
                        CancellationToken
                    ) == 5
                    && await current.PlexPhotoAlbumMemberships.CountAsync(
                        x => x.PlexPhotoAlbum!.PlexLibraryId == photoLibrary.Id,
                        CancellationToken
                    ) == 6;
            },
            maxRetries: 60,
            delayMs: 250
        );

        using var enabledContext = await factory.CreateAsync();
        (
            await enabledContext.PlexPhotoFolders.CountAsync(
                x => x.PlexLibraryId == photoLibrary.Id && x.ParentId == null && x.IsActive == true,
                CancellationToken
            )
        ).ShouldBe(1);
        (
            await enabledContext.PlexPhotoMediaData.CountAsync(
                x => x.PlexLibraryId == photoLibrary.Id,
                CancellationToken
            )
        ).ShouldBe(5);

        var enabledBrowse = await ReadBrowseAssetsAsync(client, photoLibrary.Id);
        enabledBrowse
            .Items.Select(x => x.PlexApiRatingKey)
            .Order()
            .ShouldBe(firstFiles.Select(x => x.PlexApiRatingKey).Order());
        (enabledBrowse.TotalCount, enabledBrowse.TotalBytes).ShouldBe((5, firstFiles.Sum(x => x.Size)));
        (
            await enabledContext
                .PlexMusicArtists.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstArtists);
        (
            await enabledContext
                .PlexMusicAlbums.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexArtistId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstMusicAlbums);
        (
            await enabledContext
                .PlexMusicTracks.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new
                {
                    x.Id,
                    x.PlexApiRatingKey,
                    x.PlexAlbumId,
                })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstTracks);
        (
            await enabledContext
                .PlexMusicTrackData.OrderBy(x => x.PlexApiPartId)
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
            await enabledContext
                .PlexOtherVideos.OrderBy(x => x.PlexApiRatingKey)
                .Select(x => new { x.Id, x.PlexApiRatingKey })
                .ToListAsync(CancellationToken)
        ).ShouldBe(firstVideos);
        (
            await enabledContext
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
    }

    private Task<BaseContainer> CreatePhotoRefreshContainer(Seed seed) =>
        CreateContainer(
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
                    plex.PhotoAlbumsOverlap = true;
                };
            }
        );

    private async Task<PhotoAssetPageDTO> ReadBrowseAssetsAsync(HttpClient client, int libraryId)
    {
        using var response = await client.GetAsync(
            $"{ApiRoutes.PlexPhotoController}/assets?plexLibraryId={libraryId}",
            CancellationToken
        );
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ResultDTO<PhotoAssetPageDTO>>(
            cancellationToken: CancellationToken
        );
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldNotBeNull();
        return result.Value;
    }
}
