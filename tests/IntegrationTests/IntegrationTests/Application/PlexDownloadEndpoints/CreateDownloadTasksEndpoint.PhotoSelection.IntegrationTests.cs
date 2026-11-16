using System.Collections.Concurrent;
using System.Text.Json;
using Moq.Protected;
using PostHog;

namespace Reaparr.IntegrationTests;

public class CreateDownloadTasksEndpointPhotoSelectionIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPreviewAndCreateRecursiveFolderSelectionThroughHttp()
    {
        var telemetryBodies = new ConcurrentQueue<string>();
        PostHogClient? telemetryClient = null;
        var buildInfo = new MockAppBuildInfo
        {
            TelemetryHost = "https://telemetry.example.test",
            TelemetryProjectToken = "phc_test",
        };
        using var container = await CreatePhotoContainerAsync(new Seed(231159), photoCount: 2, config =>
        {
            config.OverrideAppBuildInfo = buildInfo;
            config.OverrideServices = builder =>
            {
                var handler = new Mock<HttpMessageHandler>();
                handler
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>()
                    )
                    .Returns(
                        async (HttpRequestMessage request, CancellationToken token) =>
                        {
                            telemetryBodies.Enqueue(await request.Content!.ReadAsStringAsync(token));
                            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
                        }
                    );
                var httpClient = new HttpClient(handler.Object);
                var factory = new Mock<IHttpClientFactory>();
                factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
                telemetryClient = new PostHogClient(
                    new PostHogOptions
                    {
                        ProjectToken = buildInfo.TelemetryProjectToken,
                        HostUrl = new Uri(buildInfo.TelemetryHost),
                        EnableCompression = false,
                    },
                    httpClientFactory: factory.Object
                );
                builder.RegisterInstance(telemetryClient).As<IPostHogClient>();
            };
        });
        var factory = container.Resolve<IReaparrDbContextFactory>();
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var (media, selection) = await CreateRecursiveFolderSelectionAsync(context, library);
        await PauseServerAsync(container, library.PlexServerId);
        using var client = await CreateSignedInClientAsync(container);
        var destination = container.PathProvider.DefaultPhotosDestinationFolder;

        // Act
        var preview = await client.POSTAsync<
            GetDownloadPreviewEndpoint,
            DownloadPreviewRequest,
            ResultDTO<DownloadPreviewContainerDTO>
        >(new DownloadPreviewRequest { DownloadMedias = [selection], CustomDestinationFolderPath = destination });
        var create = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest([selection], customDestinationFolderPath: destination),
            }
        );

        // Assert
        preview.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        preview.Result.IsSuccess.ShouldBeTrue();
        preview.Result.Value.ShouldNotBeNull();
        var previewRoot = preview.Result.Value.Previews.Single();
        previewRoot.Children.Count.ShouldBe(1);
        previewRoot.Children.Single().Children.Count.ShouldBe(2);
        preview.Result.Value.TotalSize.ShouldBe(media.Sum(x => x.Size));
        create.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        create.Result.IsSuccess.ShouldBeTrue();
        create.Result.Value.ShouldBe(PhotoReport(folders: 3, images: 2));
        await telemetryClient!.FlushAsync();
        var nativeEvents = new List<JsonElement>();
        foreach (var body in telemetryBodies)
        {
            using var document = JsonDocument.Parse(body);
            nativeEvents.AddRange(
                document.RootElement.GetProperty("batch").EnumerateArray().Select(item => item.Clone())
            );
        }
        var previewEvents = nativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.DownloadPreviewFinished.ToString())
            .ToArray();
        previewEvents.Length.ShouldBe(1);
        var previewProperties = previewEvents.Single().GetProperty("properties");
        previewProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Succeeded.ToString());
        previewProperties
            .GetProperty(TelemetryProperty.Downloads.SelectedCount)
            .GetInt32()
            .ShouldBe(selection.MediaIds.Count);
        previewProperties.GetProperty(TelemetryProperty.Downloads.SelectionCount).GetInt32().ShouldBe(1);
        previewProperties
            .GetProperty(TelemetryProperty.Downloads.SelectedMediaTypes)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe([PlexMediaType.PhotoImage.ToString()]);
        previewProperties
            .GetProperty(TelemetryProperty.Downloads.CustomDestinationFolderPath)
            .GetString()
            .ShouldBe(destination);
        previewProperties.GetProperty(TelemetryProperty.Downloads.PreviewCount).GetInt32().ShouldBe(1);
        previewProperties
            .GetProperty(TelemetryProperty.Downloads.PreviewTotalSize)
            .GetInt64()
            .ShouldBe(media.Sum(x => x.Size));
        previewProperties.GetProperty(TelemetryProperty.Downloads.PreviewTotalChildren).GetInt32().ShouldBe(2);
        var previewReport = previewProperties.GetProperty(TelemetryProperty.Downloads.Report).EnumerateArray().Single();
        previewReport.GetProperty(TelemetryProperty.Downloads.ReportId).GetInt32().ShouldBe(library.Id);
        previewReport.GetProperty(TelemetryProperty.Downloads.ReportTitle).GetString().ShouldBe(previewRoot.Title);
        previewReport.GetProperty(TelemetryProperty.Downloads.ReportSize).GetInt64().ShouldBe(media.Sum(x => x.Size));
        previewReport.GetProperty(TelemetryProperty.Downloads.ReportChildCount).GetInt32().ShouldBe(2);
        previewReport
            .GetProperty(TelemetryProperty.Downloads.ReportMediaType)
            .GetString()
            .ShouldBe(PlexMediaType.PhotoAlbum.ToString());
        var creationEvents = nativeEvents
            .Where(item => item.GetProperty("event").GetString() == TelemetryEvent.DownloadCreationProcessed.ToString())
            .ToArray();
        creationEvents.Length.ShouldBe(1);
        var creationProperties = creationEvents.Single().GetProperty("properties");
        creationProperties
            .GetProperty(TelemetryProperty.Outcome)
            .GetString()
            .ShouldBe(TelemetryOutcome.Succeeded.ToString());
        creationProperties
            .GetProperty(TelemetryProperty.Downloads.SelectedCount)
            .GetInt32()
            .ShouldBe(selection.MediaIds.Count);
        creationProperties.GetProperty(TelemetryProperty.Downloads.SelectionCount).GetInt32().ShouldBe(1);
        creationProperties
            .GetProperty(TelemetryProperty.Downloads.SelectedMediaTypes)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe([PlexMediaType.PhotoImage.ToString()]);
        creationProperties.GetProperty(TelemetryProperty.Downloads.CreatedTotal).GetInt32().ShouldBe(5);
        var createdReport = creationProperties.GetProperty(TelemetryProperty.Downloads.CreatedReport);
        createdReport.GetProperty(TelemetryProperty.Downloads.Movies).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.TvShows).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.Seasons).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.Episodes).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.PhotoFolders).GetInt32().ShouldBe(3);
        createdReport.GetProperty(TelemetryProperty.Downloads.PhotoImages).GetInt32().ShouldBe(2);
        createdReport.GetProperty(TelemetryProperty.Downloads.MusicArtists).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.MusicAlbums).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.MusicTracks).GetInt32().ShouldBe(0);
        createdReport.GetProperty(TelemetryProperty.Downloads.OtherVideos).GetInt32().ShouldBe(0);
        using var finalContext = await factory.CreateAsync();
        var server = await context.PlexServers.SingleAsync(x => x.Id == library.PlexServerId, CancellationToken);
        var expectedRootName =
            $"{server.Name.SanitizeFolderName()} - {library.Title.SanitizeFolderName()}".SanitizeFolderName();
        var expectedDownloadRoot = container.PathProvider.DefaultDownloadsDestinationFolder;
        var folders = await finalContext
            .DownloadTaskPhotoFolders.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(CancellationToken);
        folders.Count.ShouldBe(3);
        folders.ShouldAllBe(x =>
            x.PlexServerId == library.PlexServerId
            && x.PlexLibraryId == library.Id
            && x.DownloadStatus == DownloadStatus.Queued
            && x.DownloadRootPath == expectedDownloadRoot
            && x.DestinationRootPath == destination
        );
        var taskNodes = (
            await finalContext.GetAllDownloadTasksByServerAsync(
                library.PlexServerId,
                cancellationToken: CancellationToken
            )
        )
            .Where(x => x.PlexLibraryId == library.Id && x.DownloadTaskType == DownloadTaskType.PhotoFolder)
            .Flatten(x => x.Children)
            .ToDictionary(x => x.Id);

        var rootFolder = folders.Single(x => x.ParentId is null);
        (rootFolder.RelativePath, rootFolder.Title, taskNodes[rootFolder.Id].DataTotal).ShouldBe(
            (string.Empty, expectedRootName, media.Sum(x => x.Size))
        );
        var tripsFolder = folders.Single(x => x.RelativePath == "Trips");
        (tripsFolder.ParentId, tripsFolder.Title, taskNodes[tripsFolder.Id].DataTotal).ShouldBe(
            (rootFolder.Id, "Trips", media.Sum(x => x.Size))
        );
        var dayFolder = folders.Single(x => x.RelativePath == "Trips/Day 1");
        (dayFolder.ParentId, dayFolder.Title, taskNodes[dayFolder.Id].DataTotal).ShouldBe(
            (tripsFolder.Id, "Day 1", media[1].Size)
        );
        var files = await finalContext
            .DownloadTaskPhotoImageFiles.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.PlexApiPartId)
            .ToListAsync(CancellationToken);
        files.Count.ShouldBe(2);
        files
            .Select(x => (x.PlexApiRatingKey, x.PlexApiMediaId, x.PlexApiPartId, x.AssetKind, x.DataTotal))
            .ShouldBe(
                media
                    .OrderBy(x => x.PlexApiPartId)
                    .Select(x => (x.PlexApiRatingKey, x.PlexApiMediaId, x.PlexApiPartId, x.AssetKind, x.Size))
                    .ToArray()
            );
        foreach (var file in files)
        {
            var source = media.Single(x => x.PlexApiPartId == file.PlexApiPartId);
            file.ParentId.ShouldBe(source.Id == media[0].Id ? tripsFolder.Id : dayFolder.Id);
            file.FileName.ShouldBe(source.OriginalFilename);
            file.FileLocationUrl.ShouldBe(source.Key);
            file.HashId.ShouldBeNull();
            file.DownloadStatus.ShouldBe(DownloadStatus.Queued);
            file.DataReceived.ShouldBe(0);
            file.DirectoryMeta.DownloadRootPath.ShouldBe(expectedDownloadRoot);
            file.DirectoryMeta.DestinationRootPath.ShouldBe(destination);
            file.DestinationFolderPathId.ShouldBeNull();
        }
    }

    [Test]
    public async Task ShouldReturnSuccessfulEmptyPreviewForEmptyFolderThroughHttp()
    {
        // Arrange
        using var container = await CreatePhotoContainerAsync(new Seed(231160), photoCount: 1);
        var factory = container.Resolve<IReaparrDbContextFactory>();
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var selection = await CreateEmptyFolderSelectionAsync(context, library);
        using var client = await CreateSignedInClientAsync(container);

        // Act
        var preview = await client.POSTAsync<
            GetDownloadPreviewEndpoint,
            DownloadPreviewRequest,
            ResultDTO<DownloadPreviewContainerDTO>
        >(new DownloadPreviewRequest { DownloadMedias = [selection] });

        // Assert
        preview.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        preview.Result.IsSuccess.ShouldBeTrue();
        preview.Result.Value.ShouldNotBeNull();
        preview.Result.Value.Previews.ShouldBeEmpty();
        preview.Result.Value.TotalSize.ShouldBe(0);
        using var emptyContext = await factory.CreateAsync();
        (await emptyContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldPreviewAndCreateSameAssetForNewDestinationAfterItWasQueued()
    {
        // Arrange
        using var container = await CreatePhotoContainerAsync(new Seed(231161), photoCount: 1);
        var factory = container.Resolve<IReaparrDbContextFactory>();
        var (destinationA, destinationB) = CreateDestinations(container);
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var file = await context.PlexPhotoMediaData.SingleAsync(x => x.PlexLibraryId == library.Id, CancellationToken);
        var selection = CreateAssetSelection(library, file);
        using var client = await CreateSignedInClientAsync(container);
        var first = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest([selection], customDestinationFolderPath: destinationA),
            }
        );

        // Act
        var preview = await client.POSTAsync<
            GetDownloadPreviewEndpoint,
            DownloadPreviewRequest,
            ResultDTO<DownloadPreviewContainerDTO>
        >(new DownloadPreviewRequest { DownloadMedias = [selection], CustomDestinationFolderPath = destinationB });
        var second = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest([selection], customDestinationFolderPath: destinationB),
            }
        );
        var repeatedA = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest([selection], customDestinationFolderPath: destinationA),
            }
        );

        // Assert
        first.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Result.IsSuccess.ShouldBeTrue();
        first.Result.Value.ShouldBe(PhotoReport(folders: 1, images: 1));
        preview.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        preview.Result.IsSuccess.ShouldBeTrue();
        preview.Result.Value.ShouldNotBeNull().Previews.ShouldNotBeEmpty();
        second.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Result.IsSuccess.ShouldBeTrue();
        second.Result.Value.ShouldBe(PhotoReport(folders: 1, images: 1));
        repeatedA.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        repeatedA.Result.IsSuccess.ShouldBeTrue();
        repeatedA.Result.Value.ShouldBe(PhotoReport());
        await AssertDestinationRootsAsync(factory, library.Id, destinationA, destinationB);
    }

    private static (string First, string Second) CreateDestinations(BaseContainer container)
    {
        var fileSystem = container.Resolve<IFileSystem>();
        var first = Path.Combine(container.PathProvider.DefaultPhotosDestinationFolder, "destination-a");
        var second = Path.Combine(container.PathProvider.DefaultPhotosDestinationFolder, "destination-b");
        fileSystem.Directory.CreateDirectory(first);
        fileSystem.Directory.CreateDirectory(second);
        return (first, second);
    }

    private static async Task<HttpClient> CreateSignedInClientAsync(BaseContainer container)
    {
        var client = container.GetApiClient();
        await client.SignIn();
        return client;
    }

    private async Task PauseServerAsync(BaseContainer container, int serverId)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        await context
            .PlexServers.Where(x => x.Id == serverId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsDownloadsPausedByUser, true), CancellationToken);
    }

    private Task<BaseContainer> CreatePhotoContainerAsync(
        Seed seed,
        int photoCount,
        Action<UnitTestDataConfig>? overrides = null
    ) => CreateContainer(seed, config =>
        {
            config.DatabaseOptions = x =>
            {
                x.PlexAccountCount = 1;
                x.PlexServerCount = 1;
                x.PlexPhotoLibraryCount = 1;
                x.PhotoCount = photoCount;
            };
            config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
            overrides?.Invoke(config);
        });

    private async Task<(
        List<PlexPhotoMediaData> Media,
        DownloadMediaDTO Selection
    )> CreateRecursiveFolderSelectionAsync(IReaparrDbContext context, PlexLibrary library)
    {
        var root = await context.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var media = await context
            .PlexPhotoMediaData.AsTracking()
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var child = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            ParentId = root.Id,
            RelativePath = "Trips",
            Title = "Trips",
        };
        context.PlexPhotoFolders.Add(child);
        await context.SaveChangesAsync(CancellationToken);
        var grandchild = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            ParentId = child.Id,
            RelativePath = "Trips/Day 1",
            Title = "Day 1",
        };
        context.PlexPhotoFolders.Add(grandchild);
        await context.SaveChangesAsync(CancellationToken);
        media[0].PlexPhotoFolderId = child.Id;
        media[1].PlexPhotoFolderId = grandchild.Id;
        await context.SaveChangesAsync(CancellationToken);
        return (
            media,
            new DownloadMediaDTO
            {
                Type = PlexMediaType.PhotoImage,
                SelectionKind = PhotoItemKind.Folder,
                MediaIds = [child.Id],
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Qualities = [],
            }
        );
    }

    private async Task<DownloadMediaDTO> CreateEmptyFolderSelectionAsync(IReaparrDbContext context, PlexLibrary library)
    {
        var root = await context.PlexPhotoFolders.SingleAsync(
            x => x.PlexLibraryId == library.Id && x.ParentId == null,
            CancellationToken
        );
        var empty = new PlexPhotoFolder
        {
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            ParentId = root.Id,
            RelativePath = "Empty",
            Title = "Empty",
        };
        context.PlexPhotoFolders.Add(empty);
        await context.SaveChangesAsync(CancellationToken);
        return new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Folder,
            MediaIds = [empty.Id],
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Qualities = [],
        };
    }

    private static DownloadMediaDTO CreateAssetSelection(PlexLibrary library, PlexPhotoMediaData file) =>
        new()
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            MediaIds = [file.Id],
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Qualities = [],
        };

    private async Task AssertDestinationRootsAsync(
        IReaparrDbContextFactory factory,
        int libraryId,
        string destinationA,
        string destinationB
    )
    {
        using var finalContext = await factory.CreateAsync();
        var folders = await finalContext
            .DownloadTaskPhotoFolders.Where(x => x.PlexLibraryId == libraryId)
            .ToListAsync(CancellationToken);
        folders.Select(x => x.DestinationRootPath).Order().ShouldBe(new[] { destinationA, destinationB }.Order());
    }

    private static DownloadTaskCreationReportDTO PhotoReport(int folders = 0, int images = 0) =>
        new()
        {
            Movies = 0,
            TvShows = 0,
            Seasons = 0,
            Episodes = 0,
            PhotoFolders = folders,
            PhotoImages = images,
            MusicArtists = 0,
            MusicAlbums = 0,
            MusicTracks = 0,
            OtherVideos = 0,
        };
}
