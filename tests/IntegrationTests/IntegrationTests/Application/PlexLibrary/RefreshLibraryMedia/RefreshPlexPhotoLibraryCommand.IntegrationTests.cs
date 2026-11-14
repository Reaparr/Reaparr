using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using Moq.Contrib.HttpClient;
using PostHog;

namespace Reaparr.IntegrationTests;

public class RefreshPlexPhotoLibraryCommandIntegrationTests : BaseIntegrationTests
{
    public RefreshPlexPhotoLibraryCommandIntegrationTests() { }

    [Test]
    public async Task ShouldPersistMergedPhotoFilesAndMemberships_ThenPreserveOtherLibrariesAcrossRefreshDisableReenable()
    {
        // Arrange
        var seed = new Seed(9625);
        var telemetryBodies = new ConcurrentQueue<string>();
        var telemetryRequests = new ConcurrentQueue<(
            HttpMethod Method,
            string? RequestUri,
            string? Authorization,
            string? ContentType,
            string Body
        )>();
        var telemetryHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        Mock<IHttpClientFactory>? telemetryFactory = null;
        var buildInfo = new MockAppBuildInfo
        {
            TelemetryHost = "https://telemetry.example.test",
            TelemetryProjectToken = "phc_test",
        };
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.HttpClientOptions = (http, _) => http.SetupIdentityRequest(seed);
                config.OverrideAppBuildInfo = buildInfo;
                config.OverrideServices = builder =>
                {
                    telemetryHandler
                        .SetupRequest(HttpMethod.Post, $"{buildInfo.TelemetryHost.TrimEnd('/')}/batch")
                        .Returns(
                            async (HttpRequestMessage request, CancellationToken token) =>
                            {
                                var body = request.Content is null
                                    ? string.Empty
                                    : await request.Content.ReadAsStringAsync(token);
                                telemetryBodies.Enqueue(body);
                                telemetryRequests.Enqueue(
                                    (
                                        request.Method,
                                        request.RequestUri?.ToString(),
                                        request.Headers.Authorization?.ToString(),
                                        request.Content?.Headers.ContentType?.MediaType,
                                        body
                                    )
                                );
                                return new HttpResponseMessage(HttpStatusCode.OK)
                                {
                                    Content = new StringContent("{}"),
                                };
                            }
                        )
                        .Verifiable(Times.AtLeastOnce());
                    var httpClient = new HttpClient(telemetryHandler.Object, disposeHandler: false);
                    telemetryFactory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
                    telemetryFactory
                        .Setup(x => x.CreateClient(It.IsAny<string>()))
                        .Returns(httpClient)
                        .Verifiable(Times.Once());
                    var telemetryClient = new PostHogClient(
                        new PostHogOptions
                        {
                            ProjectToken = buildInfo.TelemetryProjectToken,
                            HostUrl = new Uri(buildInfo.TelemetryHost),
                            EnableCompression = false,
                        },
                        httpClientFactory: telemetryFactory.Object
                    );
                    builder.RegisterInstance(telemetryClient).As<IPostHogClient>();
                };
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
        var telemetryClient = container.Resolve<IPostHogClient>();
        using var client = container.GetApiClient();
        await client.SignIn();
        telemetryBodies.Clear();
        telemetryRequests.Clear();
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

        var beforeBrowseResponse = await ReadBrowseAssetsAsync(client, photoLibrary.Id);
        beforeBrowseResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        beforeBrowseResponse.Result.ShouldNotBeNull();
        var beforeBrowse = beforeBrowseResponse.Result!;
        beforeBrowse.IsSuccess.ShouldBeTrue();
        beforeBrowse.Errors.Count.ShouldBe(0);
        beforeBrowse.Value.ShouldNotBeNull();
        var beforeBrowseValue = beforeBrowse.Value!;
        beforeBrowseValue.Items.ShouldBeEmpty();
        (beforeBrowseValue.TotalCount, beforeBrowseValue.TotalBytes).ShouldBe((0, 0L));

        // Act
        var refresh = await client.POSTAsync<
            RefreshLibraryMediaEndpoint,
            RefreshLibraryMediaEndpointRequest,
            ResultDTO<LibrarySyncJobQueueDTO>
        >(new RefreshLibraryMediaEndpointRequest { PlexLibraryId = photoLibrary.Id });

        // Assert
        refresh.Response.IsSuccessStatusCode.ShouldBeTrue();
        refresh.Result.IsSuccess.ShouldBeTrue();
        refresh.Result.Errors.Count.ShouldBe(0);
        var refreshQueue = refresh.Result.Value.ShouldNotBeNull();
        refreshQueue.PlexLibraryId.ShouldBe(photoLibrary.Id);
        refreshQueue.PlexServerId.ShouldBe(photoLibrary.PlexServerId);
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);
        await telemetryClient.FlushAsync();
        var firstNativeEvents = new List<JsonElement>();
        foreach (var body in telemetryBodies)
        {
            using var document = JsonDocument.Parse(body);
            firstNativeEvents.AddRange(
                document.RootElement.GetProperty("batch").EnumerateArray().Select(item => item.Clone())
            );
        }
        var firstRefreshRequests = firstNativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.LibraryRefreshRequested.ToString())
            .ToArray();
        firstRefreshRequests.Length.ShouldBe(1);
        var refreshRequestProperties = firstRefreshRequests.Single().GetProperty("properties");
        refreshRequestProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(photoLibrary.Id);
        refreshRequestProperties
            .GetProperty(TelemetryProperty.Libraries.ForceLibrarySync)
            .GetBoolean()
            .ShouldBeFalse();
        refreshRequestProperties
            .GetProperty(TelemetryProperty.Libraries.ForceMediaRefresh)
            .GetBoolean()
            .ShouldBeFalse();
        refreshRequestProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Succeeded.ToString());
        refreshRequestProperties
            .GetProperty(TelemetryProperty.Libraries.QueueStatus)
            .GetString()
            .ShouldBe(refreshQueue.Status.ToString());
        var queuedEvents = firstNativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.LibrarySyncQueued.ToString())
            .ToArray();
        queuedEvents.Length.ShouldBe(1);
        var queuedProperties = queuedEvents.Single().GetProperty("properties");
        queuedProperties.GetProperty(TelemetryProperty.ServerId).GetInt32().ShouldBe(photoLibrary.PlexServerId);
        queuedProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(photoLibrary.Id);
        queuedProperties.GetProperty(TelemetryProperty.MediaType).GetString().ShouldBe(PlexMediaType.PhotoAlbum.ToString());
        queuedProperties.GetProperty(TelemetryProperty.Libraries.ForceLibrarySync).GetBoolean().ShouldBeFalse();
        queuedProperties.GetProperty(TelemetryProperty.Libraries.ForceMediaRefresh).GetBoolean().ShouldBeFalse();
        queuedProperties.GetProperty(TelemetryProperty.Trigger).GetString().ShouldBe(TelemetryTrigger.Manual.ToString());
        var admissionCreatedAt = queuedProperties.GetProperty(TelemetryProperty.CreatedAt).GetString();
        admissionCreatedAt.ShouldNotBeNullOrWhiteSpace();
        var finishedEvents = firstNativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.LibrarySyncFinished.ToString())
            .ToArray();
        finishedEvents.Length.ShouldBe(1);
        var finishedProperties = finishedEvents.Single().GetProperty("properties");
        finishedProperties.GetProperty(TelemetryProperty.ServerId).GetInt32().ShouldBe(photoLibrary.PlexServerId);
        finishedProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(photoLibrary.Id);
        finishedProperties.GetProperty(TelemetryProperty.MediaType).GetString().ShouldBe(PlexMediaType.PhotoAlbum.ToString());
        finishedProperties.GetProperty(TelemetryProperty.Libraries.ForceMediaRefresh).GetBoolean().ShouldBeFalse();
        finishedProperties.GetProperty(TelemetryProperty.CreatedAt).GetString().ShouldBe(admissionCreatedAt);
        finishedProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Succeeded.ToString());
        firstNativeEvents
            .Count(item => item.GetProperty("event").GetString() == TelemetryEvent.LibraryComparisonFinished.ToString())
            .ShouldBe(0);
        telemetryBodies.Clear();

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
        firstMemberships.ShouldBe(
            new[]
            {
                new { PlexPhotoAlbumId = firstAlbums[0].Id, PlexPhotoMediaDataId = firstFiles[0].Id },
                new { PlexPhotoAlbumId = firstAlbums[0].Id, PlexPhotoMediaDataId = firstFiles[1].Id },
                new { PlexPhotoAlbumId = firstAlbums[0].Id, PlexPhotoMediaDataId = firstFiles[2].Id },
                new { PlexPhotoAlbumId = firstAlbums[1].Id, PlexPhotoMediaDataId = firstFiles[0].Id },
                new { PlexPhotoAlbumId = firstAlbums[1].Id, PlexPhotoMediaDataId = firstFiles[3].Id },
                new { PlexPhotoAlbumId = firstAlbums[1].Id, PlexPhotoMediaDataId = firstFiles[4].Id },
            }
                .OrderBy(x => x.PlexPhotoAlbumId)
                .ThenBy(x => x.PlexPhotoMediaDataId)
        );
        firstFiles.Select(x => x.AssetKind).ShouldBe(
            new[]
            {
                PhotoAssetKind.Image,
                PhotoAssetKind.Image,
                PhotoAssetKind.VideoClip,
                PhotoAssetKind.Image,
                PhotoAssetKind.VideoClip,
            }
        );
        firstFolders.ShouldContain(x => x.ParentId == null && x.IsActive == true);
        firstFolders.Where(x => x.ParentId == null).Count().ShouldBe(1);
        firstFiles.ShouldAllBe(x => x.PlexPhotoFolderId.HasValue && x.OriginalFilename.Length > 0);
        firstFolders.ShouldAllBe(x => x.ParentId == null || x.RelativePath.Length > 0);
        firstFolders
            .Where(x => x.ParentId == null)
            .ShouldAllBe(x => x.RootIdentityKey != null && x.SourceRootPath != null);

        var publishedBrowseResponse = await ReadBrowseAssetsAsync(client, photoLibrary.Id);
        publishedBrowseResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        publishedBrowseResponse.Result.ShouldNotBeNull();
        var publishedBrowse = publishedBrowseResponse.Result!;
        publishedBrowse.IsSuccess.ShouldBeTrue();
        publishedBrowse.Errors.Count.ShouldBe(0);
        publishedBrowse.Value.ShouldNotBeNull();
        var publishedBrowseValue = publishedBrowse.Value!;
        publishedBrowseValue.Items.Select(x => x.Id).Order().ShouldBe(firstFiles.Select(x => x.Id).Order());
        publishedBrowseValue.Items.ShouldAllBe(x => x.PlexLibraryId == photoLibrary.Id);
        (publishedBrowseValue.TotalCount, publishedBrowseValue.TotalBytes).ShouldBe((5, firstFiles.Sum(x => x.Size)));
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
        await telemetryClient.FlushAsync();
        telemetryBodies.Clear();

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
        await telemetryClient.FlushAsync();
        telemetryBodies.Clear();

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
        await container.BackgroundJobScheduler.AwaitScheduler(CancellationToken);
        await telemetryClient.FlushAsync();
        var finalNativeEvents = new List<JsonElement>();
        foreach (var body in telemetryBodies)
        {
            using var document = JsonDocument.Parse(body);
            finalNativeEvents.AddRange(document.RootElement.GetProperty("batch").EnumerateArray().Select(item => item.Clone()));
        }
        finalNativeEvents
            .Count(item => item.GetProperty("event").GetString() == TelemetryEvent.LibraryRefreshRequested.ToString())
            .ShouldBe(0);
        var enabledEvents = finalNativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.PlexLibraryChanged.ToString())
            .ToArray();
        enabledEvents.Length.ShouldBe(1);
        var enabledProperties = enabledEvents.Single().GetProperty("properties");
        enabledProperties.GetProperty(TelemetryProperty.Action).GetString().ShouldBe(TelemetryAction.Enabled.ToString());
        enabledProperties.GetProperty(TelemetryProperty.ServerId).GetInt32().ShouldBe(photoLibrary.PlexServerId);
        enabledProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(photoLibrary.Id);
        enabledProperties.GetProperty(TelemetryProperty.MediaType).GetString().ShouldBe(PlexMediaType.PhotoAlbum.ToString());
        enabledProperties.GetProperty(TelemetryProperty.Libraries.IsEnabled).GetBoolean().ShouldBeTrue();
        enabledProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Succeeded.ToString());
        var finalQueuedEvents = finalNativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.LibrarySyncQueued.ToString())
            .ToArray();
        finalQueuedEvents.Length.ShouldBe(1);
        var finalQueuedProperties = finalQueuedEvents.Single().GetProperty("properties");
        finalQueuedProperties.GetProperty(TelemetryProperty.ServerId).GetInt32().ShouldBe(photoLibrary.PlexServerId);
        finalQueuedProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(photoLibrary.Id);
        finalQueuedProperties.GetProperty(TelemetryProperty.MediaType).GetString().ShouldBe(PlexMediaType.PhotoAlbum.ToString());
        finalQueuedProperties.GetProperty(TelemetryProperty.Trigger).GetString().ShouldBe(TelemetryTrigger.Manual.ToString());
        var finalAdmissionCreatedAt = finalQueuedProperties.GetProperty(TelemetryProperty.CreatedAt).GetString();
        finalAdmissionCreatedAt.ShouldNotBeNullOrWhiteSpace();
        var finalFinishedEvents = finalNativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.LibrarySyncFinished.ToString())
            .ToArray();
        finalFinishedEvents.Length.ShouldBe(1);
        var finalFinishedProperties = finalFinishedEvents.Single().GetProperty("properties");
        finalFinishedProperties.GetProperty(TelemetryProperty.ServerId).GetInt32().ShouldBe(photoLibrary.PlexServerId);
        finalFinishedProperties.GetProperty(TelemetryProperty.LibraryId).GetInt32().ShouldBe(photoLibrary.Id);
        finalFinishedProperties.GetProperty(TelemetryProperty.MediaType).GetString().ShouldBe(PlexMediaType.PhotoAlbum.ToString());
        finalFinishedProperties.GetProperty(TelemetryProperty.CreatedAt).GetString().ShouldBe(finalAdmissionCreatedAt);
        finalFinishedProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Succeeded.ToString());
        finalNativeEvents
            .Count(item => item.GetProperty("event").GetString() == TelemetryEvent.LibraryComparisonFinished.ToString())
            .ShouldBe(0);

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

        var enabledBrowseResponse = await ReadBrowseAssetsAsync(client, photoLibrary.Id);
        enabledBrowseResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        enabledBrowseResponse.Result.ShouldNotBeNull();
        var enabledBrowse = enabledBrowseResponse.Result!;
        enabledBrowse.IsSuccess.ShouldBeTrue();
        enabledBrowse.Errors.Count.ShouldBe(0);
        enabledBrowse.Value.ShouldNotBeNull();
        var enabledBrowseValue = enabledBrowse.Value!;
        enabledBrowseValue
            .Items.Select(x => x.PlexApiRatingKey)
            .Order()
            .ShouldBe(firstFiles.Select(x => x.PlexApiRatingKey).Order());
        (enabledBrowseValue.TotalCount, enabledBrowseValue.TotalBytes).ShouldBe((5, firstFiles.Sum(x => x.Size)));
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
        foreach (var request in telemetryRequests)
        {
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri.ShouldBe($"{buildInfo.TelemetryHost.TrimEnd('/')}/batch");
            request.Authorization.ShouldBeNull();
            request.ContentType.ShouldBe("application/json");
            using var document = JsonDocument.Parse(request.Body);
            document.RootElement.GetProperty("api_key").GetString().ShouldBe(buildInfo.TelemetryProjectToken);
            document.RootElement.GetProperty("batch").ValueKind.ShouldBe(JsonValueKind.Array);
        }
        telemetryHandler.Verify();
        telemetryFactory!.Verify();
    }


    private async Task<(HttpStatusCode StatusCode, ResultDTO<PhotoAssetPageDTO>? Result)> ReadBrowseAssetsAsync(
        HttpClient client,
        int libraryId
    )
    {
        using var response = await client.GetAsync(
            $"{ApiRoutes.PlexPhotoController}/assets?plexLibraryId={libraryId}",
            CancellationToken
        );
        var result = await response.Content.ReadFromJsonAsync<ResultDTO<PhotoAssetPageDTO>>(
            cancellationToken: CancellationToken
        );
        return (response.StatusCode, result);
    }
}
