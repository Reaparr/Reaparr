namespace Reaparr.IntegrationTests;

public class CreateDownloadTasksEndpointRawSelectionIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(PlexMediaType.PhotoAlbum, PhotoItemKind.Album, 2)]
    [Arguments(PlexMediaType.PhotoImage, PhotoItemKind.Asset, 1)]
    public async Task ShouldPreviewOnlySelectedPhotos_WhenRawClientPayloadUsesMatchingKind(
        PlexMediaType type, PhotoItemKind kind, int selectedCount)
    {
        // Arrange
        using var container = await CreateMediaContainerAsync(PlexMediaType.PhotoAlbum);
        var fixture = await LoadPhotoFixtureAsync(container);
        var selected = fixture.Assets.Take(selectedCount).ToList();
        var selection = new DownloadMediaDTO
        {
            Type = type, SelectionKind = kind, Qualities = [],
            PlexServerId = fixture.Library.PlexServerId, PlexLibraryId = fixture.Library.Id,
            MediaIds = [type == PlexMediaType.PhotoAlbum ? fixture.Album.Id : selected.Single().Id],
        };
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await PostRawJsonAsync(client, "/preview", CreateDownloadRequestJson(selection));
        var result = await response.Deserialize<DownloadPreviewContainerDTO>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(CancellationToken));
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        result.Value.ShouldNotBeNull();
        result.Value.TotalSize.ShouldBe(selected.Sum(x => x.Size));
        var root = result.Value.Previews.Single();
        (root.Title, root.Type, root.Size).ShouldBe((fixture.RootName, PlexMediaType.PhotoAlbum, selected.Sum(x => x.Size)));
        root.Children.Select(x => (x.Title, x.Type, x.Size, QualityCount: x.Qualities.Count)).OrderBy(x => x.Title)
            .ShouldBe(selected.Select(x => (x.OriginalFilename, PlexMediaType.PhotoImage, x.Size, 0)).OrderBy(x => x.OriginalFilename));
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (await context.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
        (await context.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    [Arguments(PlexMediaType.PhotoAlbum, PhotoItemKind.Album, 2)]
    [Arguments(PlexMediaType.PhotoImage, PhotoItemKind.Asset, 1)]
    public async Task ShouldQueueOnlySelectedPhotos_WhenRawClientPayloadUsesMatchingKind(
        PlexMediaType type, PhotoItemKind kind, int selectedCount)
    {
        // Arrange
        using var container = await CreateMediaContainerAsync(PlexMediaType.PhotoAlbum);
        var fixture = await LoadPhotoFixtureAsync(container);
        var selected = fixture.Assets.Take(selectedCount).OrderBy(x => x.PlexApiMediaId).ToList();
        var selection = new DownloadMediaDTO
        {
            Type = type, SelectionKind = kind, Qualities = [],
            PlexServerId = fixture.Library.PlexServerId, PlexLibraryId = fixture.Library.Id,
            MediaIds = [type == PlexMediaType.PhotoAlbum ? fixture.Album.Id : selected.Single().Id],
        };
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await PostRawJsonAsync(client, "/create", CreateDownloadRequestJson(selection));
        var result = await response.Deserialize<DownloadTaskCreationReportDTO>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(CancellationToken));
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        result.Value.ShouldNotBeNull();
        (result.Value.PhotoFolders, result.Value.PhotoImages, result.Value.Total).ShouldBe((1, selected.Count, selected.Count + 1));
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var folder = (await context.DownloadTaskPhotoFolders.Include(x => x.Files).ToListAsync(CancellationToken)).Single();
        (folder.ParentId, folder.PlexServerId, folder.PlexLibraryId, folder.Title, folder.DownloadStatus)
            .ShouldBe(((Guid?)null, fixture.Library.PlexServerId, fixture.Library.Id, fixture.RootName, DownloadStatus.Queued));
        var files = folder.Files.OrderBy(x => x.PlexApiMediaId).ToList();
        files.Select(x => (x.ParentId, x.PlexApiMediaId, x.PlexApiPartId, x.FileName, x.FileLocationUrl, x.DataTotal, x.DownloadStatus))
            .ShouldBe(selected.Select(x => (folder.Id, x.PlexApiMediaId, x.PlexApiPartId, x.OriginalFilename, x.Key, x.Size, DownloadStatus.Queued)));
        files.Select(x => x.DestinationFilePath).ShouldBe(selected.Select(x => Path.Combine(fixture.DestinationRoot, fixture.RootName, x.OriginalFilename)));
        files.Select(x => x.DownloadFilePath).ShouldBe(selected.Select(x => Path.Combine(fixture.DownloadRoot,
            IPathProvider.DefaultPhotosFolderName, fixture.RootName, x.OriginalFilename.AddReaparrTempSuffixToFileName())));
        var logs = await context.DownloadTaskPhotoImageFileLogs.OrderBy(x => x.DownloadTaskFileId)
            .Select(x => new { x.DownloadTaskFileId, x.Status }).ToListAsync(CancellationToken);
        logs.Select(x => (x.DownloadTaskFileId, x.Status)).ShouldBe(files.OrderBy(x => x.Id).Select(x => (x.Id, DownloadStatus.Queued)));
        var fileSystem = container.Resolve<IFileSystem>();
        files.SelectMany(x => new[] { x.DownloadFilePath, x.DownloadFilePath.RemoveReapTempSuffix(), x.DestinationFilePath })
            .ShouldAllBe(x => !fileSystem.File.Exists(x));
    }

    [Test]
    [Arguments(null, true)]
    [Arguments(PhotoItemKind.Asset, false)]
    public async Task ShouldRejectPhotoAlbumSelection_WhenRawClientPayloadHasMissingOrMismatchedKind(
        PhotoItemKind? kind, bool omitKind)
    {
        // Arrange
        using var container = await CreateMediaContainerAsync(PlexMediaType.PhotoAlbum);
        var fixture = await LoadPhotoFixtureAsync(container);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoAlbum, SelectionKind = kind, Qualities = [],
            PlexServerId = fixture.Library.PlexServerId, PlexLibraryId = fixture.Library.Id,
            MediaIds = [fixture.Album.Id],
        };
        var json = CreateDownloadRequestJson(selection, omitKind);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var previewResponse = await PostRawJsonAsync(client, "/preview", json);
        using var createResponse = await PostRawJsonAsync(client, "/create", json);
        var preview = await previewResponse.Deserialize<DownloadPreviewContainerDTO>();
        var create = await createResponse.Deserialize<DownloadTaskCreationReportDTO>();

        // Assert
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (preview.IsSuccess, preview.Errors.Count).ShouldBe((false, 1));
        (create.IsSuccess, create.Errors.Count).ShouldBe((false, 1));
        preview.Value.ShouldBeNull();
        create.Value.ShouldBeNull();
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        (await context.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
        (await context.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
        (await context.DownloadTaskPhotoImageFileLogs.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldPreviewMovieSelection_WhenRawClientPayloadUsesExplicitNullSelectionKind()
    {
        // Arrange
        using var container = await CreateMediaContainerAsync(PlexMediaType.Movie);
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var movie = await context.PlexMovies.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var media = movie.MediaDataList.Single();
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await PostRawJsonAsync(client, "/preview", CreateMovieRequestJson(movie));
        var result = await response.Deserialize<DownloadPreviewContainerDTO>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        result.Value.ShouldNotBeNull();
        result.Value.TotalSize.ShouldBe(media.Size);
        var preview = result.Value.Previews.Single();
        (preview.Title, preview.Type, preview.Size, preview.Children.Count).ShouldBe((movie.Title, PlexMediaType.Movie, media.Size, 0));
        preview.Qualities.Select(x => (x.MediaDataType, x.MediaId, x.DataId, x.VideoQuality))
            .ShouldBe(new[] { (PlexMediaType.Movie, movie.Id, media.Id, media.VideoQuality) });
        (await context.DownloadTaskMovie.CountAsync(CancellationToken)).ShouldBe(0);
        (await context.DownloadTaskMovieFile.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldQueueMovieSelection_WhenRawClientPayloadUsesExplicitNullSelectionKind()
    {
        // Arrange
        using var container = await CreateMediaContainerAsync(PlexMediaType.Movie);
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var movie = await context.PlexMovies.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var media = movie.MediaDataList.Single();
        var downloadRoot = (await context.GetDownloadFolder()).DirectoryPath;
        await PauseServerAsync(container, movie.PlexServerId);
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        using var response = await PostRawJsonAsync(client, "/create", CreateMovieRequestJson(movie));
        var result = await response.Deserialize<DownloadTaskCreationReportDTO>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(CancellationToken));
        (result.IsSuccess, result.Errors.Count).ShouldBe((true, 0));
        result.Value.ShouldNotBeNull();
        (result.Value.Movies, result.Value.Total).ShouldBe((1, 1));
        using var finalContext = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var root = (await finalContext.DownloadTaskMovie.Include(x => x.Children).ToListAsync(CancellationToken)).Single();
        (root.PlexServerId, root.PlexLibraryId, root.PlexApiRatingKey, root.DownloadStatus)
            .ShouldBe((movie.PlexServerId, movie.PlexLibraryId, movie.PlexApiRatingKey, DownloadStatus.Queued));
        var file = root.Children.Single();
        (file.ParentId, file.PlexApiMediaId, file.PlexApiPartId, file.FileName, file.DataTotal, file.DownloadStatus)
            .ShouldBe((root.Id, media.PlexApiMediaId, media.PlexApiPartId, media.GetFileName, media.Size, DownloadStatus.Queued));
        file.DownloadFilePath.ShouldBe(Path.Combine(downloadRoot, IPathProvider.DefaultMovieFolderName,
            movie.Title.SanitizeFolderName(), media.GetFileName.AddReaparrTempSuffixToFileName()));
        var logs = await finalContext.DownloadTaskMovieFileLogs.Select(x => new { x.DownloadTaskFileId, x.Status }).ToListAsync(CancellationToken);
        logs.Select(x => (x.DownloadTaskFileId, x.Status)).ShouldBe(new[] { (file.Id, DownloadStatus.Queued) });
        var fileSystem = container.Resolve<IFileSystem>();
        new[] { file.DownloadFilePath, file.DownloadFilePath.RemoveReapTempSuffix(), file.DestinationFilePath }
            .ShouldAllBe(x => !fileSystem.File.Exists(x));
    }

    private static string CreateMovieRequestJson(PlexMovie movie) => CreateDownloadRequestJson(new DownloadMediaDTO
    {
        Type = PlexMediaType.Movie, SelectionKind = null, Qualities = [], MediaIds = [movie.Id],
        PlexServerId = movie.PlexServerId, PlexLibraryId = movie.PlexLibraryId,
    });

    private static async Task<HttpResponseMessage> PostRawJsonAsync(HttpClient client, string route, string json)
    {
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await client.PostAsync(ApiRoutes.DownloadController + route, content);
    }

    private static string CreateDownloadRequestJson(DownloadMediaDTO selection, bool omitKind = false)
    {
        var media = new System.Text.Json.Nodes.JsonObject
        {
            ["mediaIds"] = System.Text.Json.JsonSerializer.SerializeToNode(selection.MediaIds),
            ["qualities"] = new System.Text.Json.Nodes.JsonArray(), ["type"] = selection.Type.ToString(),
            ["plexServerId"] = selection.PlexServerId, ["plexLibraryId"] = selection.PlexLibraryId,
            ["keepCompletedInDownloadFolder"] = selection.KeepCompletedInDownloadFolder,
        };
        if (!omitKind)
            media["selectionKind"] = selection.SelectionKind?.ToString();
        return new System.Text.Json.Nodes.JsonObject
        {
            ["downloadMedias"] = new System.Text.Json.Nodes.JsonArray(media),
            ["customDestinationFolderPath"] = "",
        }.ToJsonString();
    }

    private async Task<(PlexLibrary Library, PlexPhotoAlbum Album, List<PlexPhotoMediaData> Assets,
        string RootName, string DownloadRoot, string DestinationRoot)> LoadPhotoFixtureAsync(BaseContainer container)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum, CancellationToken);
        var server = await context.PlexServers.SingleAsync(x => x.Id == library.PlexServerId, CancellationToken);
        var album = await context.PlexPhotoAlbums.SingleAsync(CancellationToken);
        var assets = await context.PlexPhotoMediaData.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await PauseServerAsync(container, server.Id);
        return (library, album, assets,
            $"{server.Name.SanitizeFolderName()} - {library.Title.SanitizeFolderName()}".SanitizeFolderName(),
            (await context.GetDownloadFolder()).DirectoryPath, (await context.GetDestinationFolder(library.Id))!.DirectoryPath);
    }

    private async Task PauseServerAsync(BaseContainer container, int serverId)
    {
        using var context = await container.Resolve<IReaparrDbContextFactory>().CreateAsync();
        await context.PlexServers.Where(x => x.Id == serverId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsDownloadsPausedByUser, true), CancellationToken);
    }

    private Task<BaseContainer> CreateMediaContainerAsync(PlexMediaType mediaType) => CreateContainer(new Seed(231162), config =>
    {
        config.DatabaseOptions = database =>
        {
            database.PlexAccountCount = 1;
            database.PlexServerCount = 1;
            database.DownloadFileSizeInMb = 1;
            database.PlexPhotoLibraryCount = mediaType == PlexMediaType.PhotoAlbum ? 1 : 0;
            database.PhotoAlbumCount = 1;
            database.PhotoCount = 2;
            database.PlexMovieLibraryCount = mediaType == PlexMediaType.Movie ? 1 : 0;
            database.MovieCount = 1;
        };
        config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(new Seed(231162));
    });
}
