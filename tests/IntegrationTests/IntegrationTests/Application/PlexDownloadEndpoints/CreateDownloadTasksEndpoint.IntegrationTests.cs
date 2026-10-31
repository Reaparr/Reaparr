using System.Collections.Concurrent;
using System.ComponentModel;
using Moq;
using Moq.Contrib.HttpClient;
using Reaparr.Settings.Contracts;

namespace Reaparr.IntegrationTests;

public class CreateDownloadTasksEndpointIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldCompleteOriginalDownloadsForAllFiveFamilies_WhenCreatedThroughHttp()
    {
        // Arrange
        const int fileSize = 1024 * 1024;
        var seed = new Seed(231156);
        var originalBytes = new ConcurrentDictionary<string, byte[]>();
        var downloadRequests = new ConcurrentBag<(string Url, string TargetPath)>();
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexMovieLibraryCount = 1;
                    x.MovieCount = 3;
                    x.PlexTvShowLibraryCount = 1;
                    x.TvShowCount = 1;
                    x.TvShowSeasonCount = 1;
                    x.TvShowEpisodeCount = 1;
                    x.PlexMusicLibraryCount = 1;
                    x.MusicArtistCount = 1;
                    x.MusicAlbumCount = 1;
                    x.MusicTrackCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoAlbumCount = 1;
                    x.PhotoCount = 1;
                    x.PhotoClipCount = 1;
                    x.PlexOtherVideoLibraryCount = 1;
                    x.OtherVideoDownloadTasksCount = 1;
                    x.OtherVideoFileDownloadTasksCount = 1;
                    x.OtherVideoCount = 1;
                    x.DownloadFileSizeInMb = 1;
                };
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
                config.MockDownloadServiceFactory = file =>
                    configuration =>
                    {
                        var downloader = MockDownloadService.SuccessFactory(file)(configuration);
                        var mock = Mock.Get(downloader);
                        mock.Setup(x =>
                                x.DownloadFileTaskAsync(
                                    It.IsAny<string>(),
                                    It.IsAny<string>(),
                                    It.IsAny<CancellationToken>()
                                )
                            )
                            .Returns<string, string, CancellationToken>(
                                (url, targetPath, _) =>
                                {
                                    downloadRequests.Add((url, targetPath));
                                    file.WriteAllBytes(targetPath, originalBytes[new Uri(url).AbsolutePath]);
                                    mock.Raise(
                                        x => x.DownloadFileCompleted += null,
                                        downloader,
                                        new AsyncCompletedEventArgs(null, false, downloader.Package)
                                    );
                                    return Task.CompletedTask;
                                }
                            );
                        return downloader;
                    };
            }
        );
        container.Resolve<IDownloadManagerSettings>().DownloadSegments = 1;
        var factory = container.Resolve<IReaparrDbContextFactory>();
        var fileSystem = container.Resolve<IFileSystem>();
        var paths = container.PathProvider;
        using var arrangeContext = await factory.CreateAsync();
        var movies = await arrangeContext
            .PlexMovies.Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var show = await arrangeContext.PlexTvShows.SingleAsync(CancellationToken);
        var season = await arrangeContext.PlexTvShowSeason.SingleAsync(CancellationToken);
        var episode = await arrangeContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        var artist = await arrangeContext.PlexMusicArtists.SingleAsync(CancellationToken);
        var album = await arrangeContext.PlexMusicAlbums.SingleAsync(CancellationToken);
        var track = await arrangeContext.PlexMusicTracks.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var photoLibrary = await arrangeContext.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var photoAlbum = await arrangeContext.PlexPhotoAlbums.SingleAsync(CancellationToken);
        var photos = await arrangeContext
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == photoLibrary.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var video = await arrangeContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        movies.Count.ShouldBe(3);
        photos.Count.ShouldBe(2);
        (
            await arrangeContext.PlexPhotoAlbumMemberships.CountAsync(
                x => x.PlexPhotoAlbumId == photoAlbum.Id,
                CancellationToken
            )
        ).ShouldBe(2);
        var movieOriginals = movies.Select(x => x.MediaDataList.OrderBy(data => data.Id).First()).ToList();
        var episodeOriginal = episode.MediaDataList.PickMediaQuality();
        episodeOriginal.ShouldNotBeNull();
        var trackOriginal = track.MediaDataList.Single();
        var videoOriginal = video.MediaDataList.Single();
        var photoServer = await arrangeContext.PlexServers.SingleAsync(
            x => x.Id == photoLibrary.PlexServerId,
            CancellationToken
        );
        var photoRootName = $"{photoServer.Name} - {photoLibrary.Title}".SanitizeFolderName();
        var expectedFiles =
            new List<(
                DownloadTaskType Type,
                int RatingKey,
                int MediaId,
                int PartId,
                int LibraryId,
                string FileName,
                string Key,
                string Destination
            )>();
        foreach (var movie in movies)
        {
            var original = movieOriginals.Single(x => x.PlexMovieId == movie.Id);
            expectedFiles.Add(
                (
                    DownloadTaskType.MovieData,
                    original.PlexApiRatingKey,
                    original.PlexApiMediaId,
                    original.PlexApiPartId,
                    movie.PlexLibraryId,
                    original.GetFileName,
                    original.Key,
                    Path.Combine(
                        paths.DefaultMovieDestinationFolder,
                        movie.Title.SanitizeFolderName(),
                        original.GetFileName
                    )
                )
            );
        }
        expectedFiles.Add(
            (
                DownloadTaskType.EpisodeData,
                episodeOriginal.PlexApiRatingKey,
                episodeOriginal.PlexApiMediaId,
                episodeOriginal.PlexApiPartId,
                episode.PlexLibraryId,
                episodeOriginal.GetFileName,
                episodeOriginal.Key,
                Path.Combine(
                    paths.DefaultTvShowsDestinationFolder,
                    show.Title.SanitizeFolderName(),
                    season.Title.SanitizeFolderName(),
                    episodeOriginal.GetFileName
                )
            )
        );
        expectedFiles.Add(
            (
                DownloadTaskType.MusicTrackData,
                trackOriginal.PlexApiRatingKey,
                trackOriginal.PlexApiMediaId,
                trackOriginal.PlexApiPartId,
                track.PlexLibraryId,
                trackOriginal.OriginalFilename.GetFileName(),
                trackOriginal.Key,
                Path.Combine(
                    paths.DefaultMusicDestinationFolder,
                    artist.Title.SanitizeFolderName(),
                    album.Title.SanitizeFolderName(),
                    trackOriginal.OriginalFilename.GetFileName()
                )
            )
        );
        foreach (var photo in photos)
        {
            expectedFiles.Add(
                (
                    DownloadTaskType.PhotoImage,
                    photo.PlexApiRatingKey,
                    photo.PlexApiMediaId,
                    photo.PlexApiPartId,
                    photo.PlexLibraryId,
                    photo.OriginalFilename.GetFileName(),
                    photo.Key,
                    Path.Combine(
                        paths.DefaultPhotosDestinationFolder,
                        photoRootName,
                        photo.OriginalFilename.GetFileName()
                    )
                )
            );
        }
        expectedFiles.Add(
            (
                DownloadTaskType.OtherVideoData,
                videoOriginal.PlexApiRatingKey,
                videoOriginal.PlexApiMediaId,
                videoOriginal.PlexApiPartId,
                video.PlexLibraryId,
                videoOriginal.OriginalFilename.GetFileName(),
                videoOriginal.Key,
                Path.Combine(
                    paths.DefaultOtherDestinationFolder,
                    video.Title.SanitizeFolderName(),
                    videoOriginal.OriginalFilename.GetFileName()
                )
            )
        );
        foreach (var (key, index) in expectedFiles.Select(x => x.Key).Distinct().Select((key, index) => (key, index)))
            originalBytes[key] = Enumerable.Repeat((byte)(index + 1), fileSize).ToArray();
        _ = container.Resolve<IHttpClientFactory>();
        container
            .Resolve<Mock<HttpMessageHandler>>()
            .SetupRequest(request => request.RequestUri!.AbsolutePath.StartsWith("/library/parts/"))
            .ReturnsAsync(
                (HttpRequestMessage request, CancellationToken _) =>
                {
                    var path = request.RequestUri!.AbsolutePath;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(originalBytes[path]),
                    };
                }
            );
        var control = await arrangeContext
            .DownloadTaskOtherVideoFiles.AsTracking()
            .Include(x => x.Parent)
            .SingleAsync(CancellationToken);
        control.DownloadStatus = DownloadStatus.Paused;
        control.Parent!.DownloadStatus = DownloadStatus.Paused;
        control.DirectoryMeta.DownloadRootPath = paths.DefaultDownloadsDestinationFolder;
        control.DirectoryMeta.DestinationRootPath = paths.DefaultOtherDestinationFolder;
        arrangeContext.Entry(control).Property(nameof(DownloadTaskFileBase.DirectoryMeta)).IsModified = true;
        await arrangeContext.SaveChangesAsync(CancellationToken);
        byte[] controlBytes = [9, 8, 7];
        fileSystem.Directory.CreateDirectory(control.DownloadDirectory);
        fileSystem.File.WriteAllBytes(control.DownloadFilePath, controlBytes);
        var controlSourcePath = control.DownloadFilePath;
        var controlDestinationPath = control.DestinationFilePath;
        var selections = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Movie,
                SelectionKind = null,
                MediaIds = movies.Select(x => x.Id).ToList(),
                PlexServerId = movies[0].PlexServerId,
                PlexLibraryId = movies[0].PlexLibraryId,
                Qualities = movieOriginals
                    .Select(x => new PlexMediaQualityDTO
                    {
                        MediaDataType = PlexMediaType.Movie,
                        MediaId = x.PlexMovieId,
                        DataId = x.Id,
                        VideoQuality = x.VideoQuality,
                    })
                    .ToList(),
            },
            new()
            {
                Type = PlexMediaType.TvShow,
                SelectionKind = null,
                MediaIds = [show.Id],
                PlexServerId = show.PlexServerId,
                PlexLibraryId = show.PlexLibraryId,
                Qualities = [],
            },
            new()
            {
                Type = PlexMediaType.MusicArtist,
                SelectionKind = null,
                MediaIds = [artist.Id],
                PlexServerId = artist.PlexServerId,
                PlexLibraryId = artist.PlexLibraryId,
                Qualities = [],
            },
            CreatePhotoSelection(
                PlexMediaType.PhotoAlbum,
                PhotoItemKind.Album,
                (photoAlbum.Id, photoAlbum.PlexServerId, photoAlbum.PlexLibraryId)
            ),
            new()
            {
                Type = PlexMediaType.OtherVideos,
                SelectionKind = null,
                MediaIds = [video.Id],
                PlexServerId = video.PlexServerId,
                PlexLibraryId = video.PlexLibraryId,
                Qualities = [],
            },
        };
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var response = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(new CreateDownloadTasksEndpointRequest { Request = new CreateDownloadTasksRequest(selections) });

        // Assert
        response.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Result.IsSuccess.ShouldBeTrue();
        response.Result.Errors.Count.ShouldBe(0);
        response.Result.Value.ShouldNotBeNull();
        response.Result.Value.ShouldBe(
            new DownloadTaskCreationReportDTO
            {
                Movies = 3,
                TvShows = 1,
                Seasons = 1,
                Episodes = 1,
                PhotoFolders = 1,
                PhotoImages = 2,
                MusicArtists = 1,
                MusicAlbums = 1,
                MusicTracks = 1,
                OtherVideos = 1,
            }
        );
        response.Result.Value.Total.ShouldBe(13);
        await WaitForDatabaseConditionAsync(
            async () =>
            {
                using var pollContext = await factory.CreateAsync();
                var tasks = await pollContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
                var targets = tasks.Where(x => x.Id != control.ParentId).Flatten(x => x.Children).ToList();
                return targets.Count > expectedFiles.Count
                    && targets.All(x => x.DownloadStatus == DownloadStatus.Completed)
                    && expectedFiles.All(x => fileSystem.File.Exists(x.Destination));
            },
            maxRetries: 120,
            delayMs: 1000
        );
        using var finalContext = await factory.CreateAsync();
        var roots = await finalContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var nodes = roots.Where(x => x.Id != control.ParentId).Flatten(x => x.Children).ToList();
        nodes.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Completed);
        nodes.Select(x => x.Id).Distinct().Count().ShouldBe(nodes.Count);
        var photoFolder = nodes.Single(x => x.DownloadTaskType == DownloadTaskType.PhotoFolder);
        photoFolder.Children.Count.ShouldBe(2);
        photoFolder.Children.ShouldAllBe(x => x.DownloadTaskType == DownloadTaskType.PhotoImage);
        photoFolder.Children.ShouldAllBe(x => x.ParentId == photoFolder.Id);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.Movie).ShouldBe(3);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.TvShow).ShouldBe(1);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.Season).ShouldBe(1);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.Episode).ShouldBe(1);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.MusicArtist).ShouldBe(1);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.MusicAlbum).ShouldBe(1);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.MusicTrack).ShouldBe(1);
        nodes.Count(x => x.DownloadTaskType == DownloadTaskType.OtherVideo).ShouldBe(1);
        var photoFolders = await finalContext
            .DownloadTaskPhotoFolders.Include(x => x.Files)
            .Where(x => x.PlexLibraryId == photoLibrary.Id)
            .ToListAsync(CancellationToken);
        photoFolders.Count.ShouldBe(1);
        photoFolders[0].Children.ShouldBeEmpty();
        photoFolders[0].Files.Count.ShouldBe(2);
        photoFolders[0].Files.ShouldAllBe(x => x.ParentId == photoFolders[0].Id);
        var files = new List<DownloadTaskFileBase>();
        files.AddRange(await finalContext.DownloadTaskMovieFile.ToListAsync(CancellationToken));
        files.AddRange(await finalContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken));
        files.AddRange(await finalContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken));
        files.AddRange(await finalContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken));
        files.AddRange(
            await finalContext.DownloadTaskOtherVideoFiles.Where(x => x.Id != control.Id).ToListAsync(CancellationToken)
        );
        files.Count.ShouldBe(expectedFiles.Count);
        files
            .Select(x =>
                (
                    x.DownloadTaskType,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexLibraryId,
                    x.FileName,
                    x.FileLocationUrl,
                    x.DestinationFilePath
                )
            )
            .OrderBy(x => x.DownloadTaskType)
            .ThenBy(x => x.PlexApiPartId)
            .ShouldBe(expectedFiles.OrderBy(x => x.Type).ThenBy(x => x.PartId));
        files.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Completed);
        files.ShouldAllBe(x => x.DataReceived == fileSize && x.FileDataTransferred == fileSize);
        foreach (var file in files)
        {
            fileSystem.File.ReadAllBytes(file.DestinationFilePath).ShouldBe(originalBytes[file.FileLocationUrl]);
            fileSystem.File.Exists(file.DownloadFilePath).ShouldBeFalse();
            fileSystem.File.Exists(file.DownloadFilePath.RemoveReapTempSuffix()).ShouldBeFalse();
            fileSystem.Directory.Exists(file.DownloadDirectory).ShouldBeFalse();
            var logResult = await finalContext.GetDownloadTaskLogsAsync(file.ToKey(), null, null, CancellationToken);
            logResult.IsSuccess.ShouldBeTrue();
            logResult.Errors.Count.ShouldBe(0);
            logResult.Value.ShouldContain(x => x.Status == DownloadStatus.Queued);
            logResult.Value.ShouldContain(x => x.Status == DownloadStatus.Completed);
            logResult.Value.Where(x => x.LogLevel == NotificationLevel.Error).ShouldBeEmpty();
        }
        var photoFiles = files.OfType<DownloadTaskPhotoImageFile>().ToList();
        photoFiles.Count.ShouldBe(2);
        (
            await finalContext
                .DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId)
                .ToListAsync(CancellationToken)
        )
            .Distinct()
            .Order()
            .ShouldBe(photoFiles.Select(x => x.Id).Order());
        var retainedControl = await finalContext.DownloadTaskOtherVideoFiles.SingleAsync(x => x.Id == control.Id);
        retainedControl.ParentId.ShouldBe(control.ParentId);
        retainedControl.DownloadStatus.ShouldBe(DownloadStatus.Paused);
        retainedControl.DownloadFilePath.ShouldBe(controlSourcePath);
        retainedControl.DestinationFilePath.ShouldBe(controlDestinationPath);
        fileSystem.File.ReadAllBytes(controlSourcePath).ShouldBe(controlBytes);
        fileSystem.File.Exists(controlDestinationPath).ShouldBeFalse();
        downloadRequests.Count.ShouldBe(files.Count);
        downloadRequests
            .Select(x => new Uri(x.Url).AbsolutePath)
            .Order()
            .ShouldBe(files.Select(x => x.FileLocationUrl).Order());
    }

    [Test]
    public async Task ShouldCreateAndTransferDeduplicatedPhotoFiles_WhenAlbumAndDirectSelectionsOverlap()
    {
        // Arrange
        byte[] expectedContent = FakeData.GetDownloadFile(1);
        var seed = new Seed(231156);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoAlbumCount = 1;
                    x.PhotoCount = 1;
                    x.PhotoClipCount = 1;
                    x.DownloadFileSizeInMb = 1;
                };
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
                config.MockDownloadServiceFactory = file =>
                    configuration =>
                    {
                        var downloader = MockDownloadService.SuccessFactory(file)(configuration);
                        var mock = Mock.Get(downloader);
                        mock.Setup(x =>
                                x.DownloadFileTaskAsync(
                                    It.IsAny<string>(),
                                    It.IsAny<string>(),
                                    It.IsAny<CancellationToken>()
                                )
                            )
                            .Returns<string, string, CancellationToken>(
                                (_, targetPath, _) =>
                                {
                                    file.WriteAllBytes(targetPath.RemoveReapTempSuffix(), expectedContent);
                                    mock.Raise(
                                        x => x.DownloadFileCompleted += null,
                                        downloader,
                                        new AsyncCompletedEventArgs(null, false, downloader.Package)
                                    );
                                    return Task.CompletedTask;
                                }
                            );
                        return downloader;
                    };
            }
        );
        container.Resolve<IDownloadManagerSettings>().DownloadSegments = 1;
        var factory = container.Resolve<IReaparrDbContextFactory>();
        var fileSystem = container.Resolve<IFileSystem>();
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var album = await context.PlexPhotoAlbums.SingleAsync(x => x.PlexLibraryId == library.Id, CancellationToken);
        var assets = await context
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        _ = container.Resolve<IHttpClientFactory>();
        container
            .Resolve<Mock<HttpMessageHandler>>()
            .SetupRequest(request => request.RequestUri!.AbsolutePath.StartsWith("/library/parts/"))
            .ReturnsAsync(
                (HttpRequestMessage request, CancellationToken _) =>
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expectedContent) }
            );
        var selection = CreatePhotoSelection(
            PlexMediaType.PhotoImage,
            PhotoItemKind.Asset,
            (assets[0].Id, assets[0].PlexServerId, assets[0].PlexLibraryId)
        );
        var albumSelection = CreatePhotoSelection(
            PlexMediaType.PhotoAlbum,
            PhotoItemKind.Album,
            (album.Id, album.PlexServerId, album.PlexLibraryId)
        );
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var preview = await client.POSTAsync<
            GetDownloadPreviewEndpoint,
            DownloadPreviewRequest,
            ResultDTO<DownloadPreviewContainerDTO>
        >(new DownloadPreviewRequest { DownloadMedias = [albumSelection, selection] });
        var create = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest([albumSelection, selection]),
            }
        );

        // Assert
        preview.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        preview.Result.IsSuccess.ShouldBeTrue();
        preview.Result.Errors.Count.ShouldBe(0);
        preview.Result.Value.ShouldNotBeNull().Previews.SelectMany(x => x.Qualities).ShouldBeEmpty();
        create.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        create.Result.IsSuccess.ShouldBeTrue();
        create.Result.Errors.Count.ShouldBe(0);
        create.Result.Value.ShouldBe(PhotoReport(folders: 1, images: assets.Count));

        await WaitForDatabaseConditionAsync(
            async () =>
            {
                using var pollContext = await factory.CreateAsync();
                return await pollContext.DownloadTaskPhotoImageFiles.CountAsync(
                        x => x.PlexLibraryId == library.Id && x.DownloadStatus == DownloadStatus.Completed,
                        CancellationToken
                    ) == assets.Count;
            },
            maxRetries: 120,
            delayMs: 250
        );

        using var finalContext = await factory.CreateAsync();
        var folders = await finalContext
            .DownloadTaskPhotoFolders.Include(x => x.Files)
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(CancellationToken);
        folders.Count.ShouldBe(1);
        folders[0].ParentId.ShouldBeNull();
        folders[0].Children.ShouldBeEmpty();
        folders[0].Files.Count.ShouldBe(assets.Count);
        folders[0].DownloadRootPath.ShouldContain("Downloads");
        folders[0].DestinationRootPath.ShouldBe(container.PathProvider.DefaultPhotosDestinationFolder);

        var files = folders[0].Files.OrderBy(x => x.Id).ToList();
        files.Select(x => x.ParentId).ShouldAllBe(x => x == folders[0].Id);
        files.Select(x => x.PlexApiPartId).Order().ShouldBe(assets.Select(x => x.PlexApiPartId).Order());
        files.ShouldAllBe(x => x.DownloadStatus == DownloadStatus.Completed);
        files.ShouldAllBe(x =>
            x.DirectoryMeta.DestinationRootPath == container.PathProvider.DefaultPhotosDestinationFolder
        );
        files.ShouldAllBe(x => x.DestinationFilePath.StartsWith(container.PathProvider.DefaultPhotosDestinationFolder));
        files.ShouldAllBe(x => !x.DestinationFilePath.Contains("/Photos/Photos", StringComparison.Ordinal));
        files.ShouldAllBe(x => fileSystem.File.Exists(x.DestinationFilePath));
        files.ShouldAllBe(x => fileSystem.File.ReadAllBytes(x.DestinationFilePath).SequenceEqual(expectedContent));
        files.ShouldAllBe(x => !fileSystem.File.Exists(x.DownloadFilePath));
        files.ShouldAllBe(x => !fileSystem.File.Exists(x.DownloadFilePath.RemoveReapTempSuffix()));
        (
            await finalContext
                .DownloadTaskPhotoImageFileLogs.Select(x => x.DownloadTaskFileId)
                .ToListAsync(CancellationToken)
        )
            .Distinct()
            .Order()
            .ShouldBe(files.Select(x => x.Id).Order());

        var folderDto = await client.GETAsync<
            GetDownloadTaskByGuidEndpoint,
            GetDownloadTaskByGuidRequest,
            ResultDTO<DownloadTaskDTO>
        >(new GetDownloadTaskByGuidRequest(DownloadTaskType.PhotoFolder) { DownloadTaskGuid = folders[0].Id });
        var fileDto = await client.GETAsync<
            GetDownloadTaskByGuidEndpoint,
            GetDownloadTaskByGuidRequest,
            ResultDTO<DownloadTaskDTO>
        >(new GetDownloadTaskByGuidRequest(DownloadTaskType.PhotoImage) { DownloadTaskGuid = files[0].Id });
        folderDto.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        folderDto.Result.IsSuccess.ShouldBeTrue();
        folderDto.Result.Errors.Count.ShouldBe(0);
        folderDto.Result.Value.ShouldNotBeNull().Id.ShouldBe(folders[0].Id);
        fileDto.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        fileDto.Result.IsSuccess.ShouldBeTrue();
        fileDto.Result.Errors.Count.ShouldBe(0);
        fileDto.Result.Value.ShouldNotBeNull().Id.ShouldBe(files[0].Id);
    }

    [Test]
    public async Task ShouldRejectSameDestinationCollisionsWithoutCreatingFoldersOrFiles()
    {
        // Arrange
        var seed = new Seed(231157);
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoCount = 2;
                };
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
            }
        );
        var factory = container.Resolve<IReaparrDbContextFactory>();
        using (var arrangeContext = await factory.CreateAsync())
        {
            var files = await arrangeContext
                .PlexPhotoMediaData.AsTracking()
                .Where(x =>
                    x.PlexLibraryId == arrangeContext.PlexLibraries.Single(x => x.Type == PlexMediaType.PhotoAlbum).Id
                )
                .OrderBy(x => x.Id)
                .ToListAsync(CancellationToken);
            files.Count.ShouldBe(2);
            arrangeContext.Entry(files[1]).Property(nameof(PlexPhotoMediaData.OriginalFilename)).CurrentValue = files[
                0
            ].OriginalFilename;
            await arrangeContext.SaveChangesAsync(CancellationToken);
        }
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var filesToSelect = await context
            .PlexPhotoMediaData.Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        var selection = new DownloadMediaDTO
        {
            Type = PlexMediaType.PhotoImage,
            SelectionKind = PhotoItemKind.Asset,
            MediaIds = filesToSelect.Select(x => x.Id).ToList(),
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Qualities = [],
        };
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var create = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(new CreateDownloadTasksEndpointRequest { Request = new CreateDownloadTasksRequest([selection]) });

        // Assert
        create.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        create.Result.IsSuccess.ShouldBeTrue();
        create.Result.Errors.Count.ShouldBe(0);
        create.Result.Value.ShouldBe(PhotoReport());
        using var finalContext = await factory.CreateAsync();
        (await finalContext.DownloadTaskPhotoFolders.CountAsync(CancellationToken)).ShouldBe(0);
        (await finalContext.DownloadTaskPhotoImageFiles.CountAsync(CancellationToken)).ShouldBe(0);
    }

    [Test]
    public async Task ShouldReuseCapturedRootForRepeatedSelection_ButSeparateDestinationContexts()
    {
        // Arrange
        var seed = new Seed(231158);
        string alternateDestination;
        using var container = await CreateContainer(
            seed,
            config =>
            {
                config.DatabaseOptions = x =>
                {
                    x.PlexAccountCount = 1;
                    x.PlexServerCount = 1;
                    x.PlexPhotoLibraryCount = 1;
                    x.PhotoCount = 1;
                };
                config.HttpClientOptions = (handler, _) => handler.SetupIdentityRequest(seed);
            }
        );
        alternateDestination = Path.Combine(container.PathProvider.DefaultPhotosDestinationFolder, "alternate");
        var fileSystem = container.Resolve<IFileSystem>();
        fileSystem.Directory.CreateDirectory(alternateDestination);
        var factory = container.Resolve<IReaparrDbContextFactory>();
        using var context = await factory.CreateAsync();
        var library = await context.PlexLibraries.SingleAsync(
            x => x.Type == PlexMediaType.PhotoAlbum,
            CancellationToken
        );
        var originalLibraryTitle = library.Title;
        var file = await context.PlexPhotoMediaData.SingleAsync(x => x.PlexLibraryId == library.Id, CancellationToken);
        var selection = CreatePhotoSelection(
            PlexMediaType.PhotoImage,
            PhotoItemKind.Asset,
            (file.Id, file.PlexServerId, file.PlexLibraryId)
        );
        using var client = container.GetApiClient();
        await client.SignIn();

        // Act
        var first = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest(
                    [selection],
                    customDestinationFolderPath: container.PathProvider.DefaultPhotosDestinationFolder
                ),
            }
        );
        using (var changedContext = await factory.CreateAsync())
        {
            var changedLibrary = await changedContext
                .PlexLibraries.AsTracking()
                .SingleAsync(x => x.Id == library.Id, CancellationToken);
            changedLibrary.Title = "Renamed Photos";
            await changedContext.SaveChangesAsync(CancellationToken);
        }
        var repeated = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest(
                    [selection],
                    customDestinationFolderPath: container.PathProvider.DefaultPhotosDestinationFolder
                ),
            }
        );
        var alternate = await client.POSTAsync<
            CreateDownloadTasksEndpoint,
            CreateDownloadTasksEndpointRequest,
            ResultDTO<DownloadTaskCreationReportDTO>
        >(
            new CreateDownloadTasksEndpointRequest
            {
                Request = new CreateDownloadTasksRequest(
                    [selection],
                    customDestinationFolderPath: alternateDestination
                ),
            }
        );

        // Assert
        first.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Result.IsSuccess.ShouldBeTrue();
        first.Result.Errors.Count.ShouldBe(0);
        first.Result.Value.ShouldBe(PhotoReport(folders: 1, images: 1));
        repeated.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        repeated.Result.IsSuccess.ShouldBeTrue();
        repeated.Result.Errors.Count.ShouldBe(0);
        repeated.Result.Value.ShouldBe(PhotoReport());
        alternate.Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        alternate.Result.IsSuccess.ShouldBeTrue();
        alternate.Result.Errors.Count.ShouldBe(0);
        alternate.Result.Value.ShouldBe(PhotoReport(folders: 1, images: 1));

        using var finalContext = await factory.CreateAsync();
        var folders = await finalContext
            .DownloadTaskPhotoFolders.Include(x => x.Files)
            .Where(x => x.PlexLibraryId == library.Id)
            .OrderBy(x => x.DestinationRootPath)
            .ToListAsync(CancellationToken);
        folders.Count.ShouldBe(2);
        folders.Select(x => x.ParentId).ShouldAllBe(x => x == null);
        folders.Select(x => x.Files.Count).ShouldBe([1, 1]);
        var defaultFolder = folders.Single(x =>
            x.DestinationRootPath == container.PathProvider.DefaultPhotosDestinationFolder
        );
        var alternateFolder = folders.Single(x => x.DestinationRootPath == alternateDestination);
        defaultFolder.Title.ShouldContain(originalLibraryTitle);
        alternateFolder.Title.ShouldContain("Renamed Photos");
        defaultFolder.Title.ShouldNotBe(alternateFolder.Title);
        folders
            .Select(x => x.DestinationRootPath)
            .Order()
            .ShouldBe(new[] { alternateDestination, container.PathProvider.DefaultPhotosDestinationFolder }.Order());
        folders.ShouldAllBe(folder =>
            folder.Files.All(file => file.DirectoryMeta.PhotoFolder.StartsWith(folder.Title, StringComparison.Ordinal))
        );
    }

    private static DownloadMediaDTO CreatePhotoSelection(
        PlexMediaType type,
        PhotoItemKind selectionKind,
        (int Id, int ServerId, int LibraryId) identity
    ) =>
        new()
        {
            Type = type,
            SelectionKind = selectionKind,
            MediaIds = [identity.Id],
            PlexServerId = identity.ServerId,
            PlexLibraryId = identity.LibraryId,
            Qualities = [],
        };

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
