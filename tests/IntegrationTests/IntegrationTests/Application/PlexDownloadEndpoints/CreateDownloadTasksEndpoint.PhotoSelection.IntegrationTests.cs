namespace Reaparr.IntegrationTests;

public class CreateDownloadTasksEndpointPhotoSelectionIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldPreviewAndCreateRecursiveFolderSelectionThroughHttp()
    {
        // Arrange
        using var container = await CreatePhotoContainerAsync(new Seed(231159), photoCount: 2);
        var factory = container.Resolve<IReaparrDbContextFactory>();
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var (media, selection) = await CreateRecursiveFolderSelectionAsync(context, library);
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
        using var finalContext = await factory.CreateAsync();
        (await finalContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(2);
        (await finalContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(3);
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

    private Task<BaseContainer> CreatePhotoContainerAsync(Seed seed, int photoCount) =>
        CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoCount = photoCount;
                };
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
            }
        );

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
