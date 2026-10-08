using System.Collections.Concurrent;
using System.ComponentModel;
using Moq;
using Moq.Contrib.HttpClient;
using Quartz.Impl.Matchers;
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
        var probeRequests = new ConcurrentBag<(HttpMethod Method, string Url)>();
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
                // Only the external downloader is faked; creation, verification, status writes and moves stay real.
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
        movies.Count.ShouldBe(3);
        var show = await arrangeContext.PlexTvShows.SingleAsync(CancellationToken);
        var season = await arrangeContext.PlexTvShowSeason.SingleAsync(CancellationToken);
        var episode = await arrangeContext
            .PlexTvShowEpisodes.Include(x => x.MediaDataList)
            .SingleAsync(CancellationToken);
        episode.TvShowId.ShouldBe(show.Id);
        episode.TvShowSeasonId.ShouldBe(season.Id);
        var artist = await arrangeContext.PlexArtists.SingleAsync(CancellationToken);
        var album = await arrangeContext.PlexAlbums.SingleAsync(CancellationToken);
        var track = await arrangeContext.PlexTracks.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        album.PlexArtistId.ShouldBe(artist.Id);
        track.PlexAlbumId.ShouldBe(album.Id);
        var photoAlbum = await arrangeContext.PlexPhotoAlbums.SingleAsync(CancellationToken);
        var photos = await arrangeContext
            .PlexPhotoImages.Include(x => x.MediaDataList)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        photos.Count.ShouldBe(2);
        photos.Select(x => x.PlexPhotoAlbumId).ShouldBe([photoAlbum.Id, photoAlbum.Id]);
        photos.SelectMany(x => x.MediaDataList).Select(x => x.Container).OrderBy(x => x).ShouldBe(["jpg", "mp4"]);
        var video = await arrangeContext.PlexOtherVideos.Include(x => x.MediaDataList).SingleAsync(CancellationToken);
        var movieOriginals = movies.Select(x => x.MediaDataList.OrderBy(data => data.Id).First()).ToList();
        var episodeOriginal = episode.MediaDataList.PickMediaQuality();
        episodeOriginal.ShouldNotBeNull();
        var trackOriginal = track.MediaDataList.Single();
        var videoOriginal = video.MediaDataList.Single();
        var expectedFiles =
            new List<(
                DownloadTaskType Type,
                int RatingKey,
                int MediaId,
                int PartId,
                int LibraryId,
                int ParentRatingKey,
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
                    movie.PlexApiRatingKey,
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
                episode.PlexApiRatingKey,
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
                track.PlexApiRatingKey,
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
            var original = photo.MediaDataList.Single();
            expectedFiles.Add(
                (
                    DownloadTaskType.PhotoData,
                    original.PlexApiRatingKey,
                    original.PlexApiMediaId,
                    original.PlexApiPartId,
                    photo.PlexLibraryId,
                    photo.PlexApiRatingKey,
                    original.OriginalFilename.GetFileName(),
                    original.Key,
                    Path.Combine(
                        paths.DefaultPhotosDestinationFolder,
                        photoAlbum.Title.SanitizeFolderName(),
                        original.OriginalFilename.GetFileName()
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
                video.PlexApiRatingKey,
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
        // Resolve the factory before replacing its default sample-media probe response.
        _ = container.Resolve<IHttpClientFactory>();
        container
            .Resolve<Mock<HttpMessageHandler>>()
            .SetupRequest(request => request.RequestUri!.AbsolutePath.StartsWith("/library/parts/"))
            .ReturnsAsync(
                (HttpRequestMessage request, CancellationToken _) =>
                {
                    var path = request.RequestUri!.AbsolutePath;
                    probeRequests.Add((request.Method, request.RequestUri.AbsoluteUri));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(originalBytes[path]),
                    };
                }
            );
        expectedFiles.Select(x => x.Destination).Distinct().Count().ShouldBe(8);
        foreach (var expected in expectedFiles)
            fileSystem.File.Exists(expected.Destination).ShouldBeFalse();

        var control = await arrangeContext.DownloadTaskOtherVideoFiles.SingleAsync(CancellationToken);
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
        using (var beforeContext = await factory.CreateAsync())
        {
            var before = await beforeContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
            before
                .Flatten(x => x.Children)
                .Select(x => (x.Id, x.DownloadStatus))
                .OrderBy(x => x.Id)
                .ShouldBe(
                    new[] { (control.ParentId, DownloadStatus.Paused), (control.Id, DownloadStatus.Paused) }.OrderBy(
                        x => x.Item1
                    )
                );
            (await beforeContext.DownloadTaskOtherVideoFileLogs.ToListAsync(CancellationToken)).ShouldBeEmpty();
        }
        var executions = new ConcurrentBag<(JobKey JobKey, DownloadTaskKey TaskKey)>();
        var completions = new ConcurrentBag<(JobKey JobKey, JobExecutionException? Error)>();
        var listener = new Mock<IJobListener>();
        listener.SetupGet(x => x.Name).Returns(nameof(CreateDownloadTasksEndpointIntegrationTests));
        listener
            .Setup(x => x.JobToBeExecuted(It.IsAny<IJobExecutionContext>(), It.IsAny<CancellationToken>()))
            .Callback<IJobExecutionContext, CancellationToken>(
                (context, _) =>
                {
                    var taskKey =
                        context.MergedJobDataMap.GetPayload<DownloadJobPayload>()?.DownloadTaskKey
                        ?? context.MergedJobDataMap.GetPayload<MoveDownloadFileJobPayload>()!.DownloadTaskKey;
                    executions.Add((context.JobDetail.Key, taskKey));
                }
            )
            .Returns(Task.CompletedTask);
        listener
            .Setup(x =>
                x.JobWasExecuted(
                    It.IsAny<IJobExecutionContext>(),
                    It.IsAny<JobExecutionException?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IJobExecutionContext, JobExecutionException?, CancellationToken>(
                (context, exception, _) =>
                {
                    completions.Add((context.JobDetail.Key, exception));
                }
            )
            .Returns(Task.CompletedTask);
        container.BackgroundJobScheduler.ListenerManager.AddJobListener(
            listener.Object,
            GroupMatcher<JobKey>.GroupEquals(nameof(DownloadJob)),
            GroupMatcher<JobKey>.GroupEquals(nameof(MoveDownloadFileJob))
        );
        var notifications = (MockNotificationHubService)container.Resolve<INotificationHubService>();
        while (notifications.RefreshNotificationList.TryTake(out _)) { }
        var selections = new List<DownloadMediaDTO>
        {
            new()
            {
                Type = PlexMediaType.Movie,
                MediaIds = movies.Select(x => x.Id).ToList(),
                PlexServerId = movies[0].PlexServerId,
                PlexLibraryId = movies[0].PlexLibraryId,
                Qualities = movieOriginals
                    .Select(x => new PlexMediaQualityDTO
                    {
                        MediaDataType = PlexMediaType.Movie,
                        MediaId = x.PlexMovieId,
                        DataId = x.Id,
                        Quality = x.Quality,
                    })
                    .ToList(),
            },
            new()
            {
                Type = PlexMediaType.TvShow,
                MediaIds = [show.Id],
                PlexServerId = show.PlexServerId,
                PlexLibraryId = show.PlexLibraryId,
                Qualities = [],
            },
            new()
            {
                Type = PlexMediaType.MusicArtist,
                MediaIds = [artist.Id],
                PlexServerId = artist.PlexServerId,
                PlexLibraryId = artist.PlexLibraryId,
                Qualities = [],
            },
            new()
            {
                Type = PlexMediaType.PhotoAlbum,
                MediaIds = [photoAlbum.Id],
                PlexServerId = photoAlbum.PlexServerId,
                PlexLibraryId = photoAlbum.PlexLibraryId,
                Qualities = [],
            },
            new()
            {
                Type = PlexMediaType.OtherVideos,
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
                MusicArtists = 1,
                MusicAlbums = 1,
                MusicTracks = 1,
                PhotoAlbums = 1,
                PhotoImages = 2,
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
                return targets.Count == 21
                    && targets.All(x => x.DownloadStatus == DownloadStatus.Completed)
                    && expectedFiles.All(x => fileSystem.File.Exists(x.Destination))
                    && completions.Count == 16;
            },
            maxRetries: 120,
            delayMs: 1000
        );
        using var finalContext = await factory.CreateAsync();
        var roots = await finalContext.GetAllDownloadTasksByServerAsync(cancellationToken: CancellationToken);
        var nodes = roots.Where(x => x.Id != control.ParentId).Flatten(x => x.Children).ToList();
        var expectedParents = new List<(DownloadTaskType Type, long RatingKey, int LibraryId, long ParentRatingKey)>();
        expectedParents.AddRange(
            movies.Select(x => (DownloadTaskType.Movie, (long)x.PlexApiRatingKey, x.PlexLibraryId, 0L))
        );
        expectedParents.Add((DownloadTaskType.TvShow, show.PlexApiRatingKey, show.PlexLibraryId, 0));
        expectedParents.Add(
            (DownloadTaskType.Season, season.PlexApiRatingKey, season.PlexLibraryId, show.PlexApiRatingKey)
        );
        expectedParents.Add(
            (DownloadTaskType.Episode, episode.PlexApiRatingKey, episode.PlexLibraryId, season.PlexApiRatingKey)
        );
        expectedParents.Add((DownloadTaskType.MusicArtist, artist.PlexApiRatingKey, artist.PlexLibraryId, 0));
        expectedParents.Add(
            (DownloadTaskType.MusicAlbum, album.PlexApiRatingKey, album.PlexLibraryId, artist.PlexApiRatingKey)
        );
        expectedParents.Add(
            (DownloadTaskType.MusicTrack, track.PlexApiRatingKey, track.PlexLibraryId, album.PlexApiRatingKey)
        );
        expectedParents.Add((DownloadTaskType.PhotoAlbum, photoAlbum.PlexApiRatingKey, photoAlbum.PlexLibraryId, 0));
        expectedParents.AddRange(
            photos.Select(x =>
                (
                    DownloadTaskType.PhotoImage,
                    (long)x.PlexApiRatingKey,
                    x.PlexLibraryId,
                    (long)photoAlbum.PlexApiRatingKey
                )
            )
        );
        expectedParents.Add((DownloadTaskType.OtherVideo, video.PlexApiRatingKey, video.PlexLibraryId, 0));
        nodes
            .Where(x => x.Children.Count > 0)
            .Select(x =>
                (
                    x.DownloadTaskType,
                    x.RatingKey,
                    x.PlexLibraryId,
                    nodes.SingleOrDefault(parent => parent.Id == x.ParentId)?.RatingKey ?? 0
                )
            )
            .OrderBy(x => x.DownloadTaskType)
            .ThenBy(x => x.RatingKey)
            .ShouldBe(expectedParents.OrderBy(x => x.Type).ThenBy(x => x.RatingKey));
        nodes
            .Select(x => (x.PlexServerId, x.DownloadStatus))
            .Distinct()
            .ShouldBe([(show.PlexServerId, DownloadStatus.Completed)]);
        nodes.Select(x => x.Id).Distinct().Count().ShouldBe(21);
        // Generic projections calculate aggregate states; also verify the durable parent rows themselves.
        var persistedParents = new List<DownloadTaskParentBase>();
        persistedParents.AddRange(await finalContext.DownloadTaskMovie.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskTvShow.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskTvShowSeason.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskTvShowEpisode.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskMusicArtists.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskMusicAlbums.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskMusicTracks.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskPhotoAlbums.ToListAsync(CancellationToken));
        persistedParents.AddRange(await finalContext.DownloadTaskPhotoImages.ToListAsync(CancellationToken));
        persistedParents.AddRange(
            await finalContext
                .DownloadTaskOtherVideos.Where(x => x.Id != control.ParentId)
                .ToListAsync(CancellationToken)
        );
        persistedParents
            .Select(x =>
                (
                    x.DownloadTaskType,
                    (long)x.PlexApiRatingKey,
                    x.PlexLibraryId,
                    (long)(
                        persistedParents.SingleOrDefault(parent => parent.Id == x.ToParentKey()?.Id)?.PlexApiRatingKey
                        ?? 0
                    )
                )
            )
            .OrderBy(x => x.DownloadTaskType)
            .ThenBy(x => x.Item2)
            .ShouldBe(expectedParents.OrderBy(x => x.Type).ThenBy(x => x.RatingKey));
        persistedParents
            .Select(x => (x.PlexServerId, x.DownloadStatus, x.SonarrIntegrationId, x.RadarrIntegrationId))
            .Distinct()
            .ShouldBe([(show.PlexServerId, DownloadStatus.Completed, (Guid?)null, (Guid?)null)]);
        var files = new List<DownloadTaskFileBase>();
        files.AddRange(await finalContext.DownloadTaskMovieFile.ToListAsync(CancellationToken));
        files.AddRange(await finalContext.DownloadTaskTvShowEpisodeFile.ToListAsync(CancellationToken));
        files.AddRange(await finalContext.DownloadTaskMusicTrackFiles.ToListAsync(CancellationToken));
        files.AddRange(await finalContext.DownloadTaskPhotoImageFiles.ToListAsync(CancellationToken));
        files.AddRange(
            await finalContext.DownloadTaskOtherVideoFiles.Where(x => x.Id != control.Id).ToListAsync(CancellationToken)
        );
        files
            .Select(x =>
                (
                    x.DownloadTaskType,
                    x.PlexApiRatingKey,
                    x.PlexApiMediaId,
                    x.PlexApiPartId,
                    x.PlexLibraryId,
                    (int)nodes.Single(parent => parent.Id == x.ToParentKey()!.Id).RatingKey,
                    x.FileName,
                    x.FileLocationUrl,
                    x.DestinationFilePath
                )
            )
            .OrderBy(x => x.DownloadTaskType)
            .ThenBy(x => x.PlexApiPartId)
            .ShouldBe(expectedFiles.OrderBy(x => x.Type).ThenBy(x => x.PartId));
        files
            .Select(x =>
                (
                    x.DownloadStatus,
                    x.DownloadClientType,
                    x.DataTotal,
                    x.DataReceived,
                    x.FileDataTransferred,
                    x.SonarrIntegrationId,
                    x.RadarrIntegrationId,
                    x.DirectoryMeta.KeepCompletedInDownloadFolder
                )
            )
            .Distinct()
            .ShouldBe([
                (
                    DownloadStatus.Completed,
                    PlexDownloadClientType.Direct,
                    (long)fileSize,
                    (long)fileSize,
                    (long)fileSize,
                    (Guid?)null,
                    (Guid?)null,
                    false
                ),
            ]);
        foreach (var file in files)
        {
            file.DirectoryMeta.DownloadRootPath.ShouldBe(paths.DefaultDownloadsDestinationFolder);
            fileSystem.File.ReadAllBytes(file.DestinationFilePath).ShouldBe(originalBytes[file.FileLocationUrl]);
            fileSystem.File.Exists(file.DownloadFilePath).ShouldBeFalse();
            fileSystem.File.Exists(file.DownloadFilePath.RemoveReapTempSuffix()).ShouldBeFalse();
            fileSystem.Directory.Exists(file.DownloadDirectory).ShouldBeFalse();
            var logResult = await finalContext.GetDownloadTaskLogsAsync(file.ToKey(), null, null, CancellationToken);
            logResult.IsSuccess.ShouldBeTrue();
            logResult.Errors.Count.ShouldBe(0);
            logResult
                .Value.Where(x => x.Status == DownloadStatus.Queued)
                .Select(x => (x.Status, x.LogLevel, x.Message))
                .ShouldBe([
                    (
                        DownloadStatus.Queued,
                        NotificationLevel.Information,
                        $"DownloadTask {file.FileName} was queued for downloading"
                    ),
                ]);
            logResult
                .Value.Where(x => x.Message == $"Download {file.FileName} transitioned to status: Completed")
                .Select(x => x.Status)
                .ShouldBe([DownloadStatus.Completed]);
            logResult.Value.Where(x => x.LogLevel == NotificationLevel.Error).ShouldBeEmpty();
        }
        (
            await finalContext
                .DownloadTaskMovieFileLogs.Where(x => x.Status == DownloadStatus.Queued)
                .ToListAsync(CancellationToken)
        )
            .Select(x => (x.DownloadTaskFileId, x.DownloadTaskMovieId))
            .OrderBy(x => x.DownloadTaskFileId)
            .ShouldBe(files.OfType<DownloadTaskMovieFile>().Select(x => (x.Id, x.ParentId)).OrderBy(x => x.Id));
        var episodeFile = files.OfType<DownloadTaskTvShowEpisodeFile>().Single();
        var episodeNode = nodes.Single(x => x.Id == episodeFile.ParentId);
        var seasonNode = nodes.Single(x => x.Id == episodeNode.ParentId);
        (await finalContext.DownloadTaskTvShowEpisodeFileLogs.ToListAsync(CancellationToken))
            .Select(x =>
                (
                    x.DownloadTaskFileId,
                    x.DownloadTaskTvShowEpisodeId,
                    x.DownloadTaskTvShowSeasonId,
                    x.DownloadTaskTvShowId
                )
            )
            .Distinct()
            .ShouldBe([(episodeFile.Id, episodeNode.Id, seasonNode.Id, seasonNode.ParentId)]);
        var trackFile = files.OfType<DownloadTaskMusicTrackFile>().Single();
        var trackNode = nodes.Single(x => x.Id == trackFile.ParentId);
        var albumNode = nodes.Single(x => x.Id == trackNode.ParentId);
        (await finalContext.DownloadTaskTrackFileLogs.ToListAsync(CancellationToken))
            .Select(x => (x.DownloadTaskFileId, x.DownloadTaskTrackId, x.DownloadTaskAlbumId, x.DownloadTaskArtistId))
            .Distinct()
            .ShouldBe([(trackFile.Id, trackNode.Id, albumNode.Id, albumNode.ParentId)]);
        foreach (var photoFile in files.OfType<DownloadTaskPhotoImageFile>())
        {
            var photoNode = nodes.Single(x => x.Id == photoFile.ParentId);
            (
                await finalContext
                    .DownloadTaskPhotoImageFileLogs.Where(x => x.DownloadTaskFileId == photoFile.Id)
                    .ToListAsync(CancellationToken)
            )
                .Select(x => (x.DownloadTaskFileId, x.DownloadTaskPhotoId, x.DownloadTaskPhotoAlbumId))
                .Distinct()
                .ShouldBe([(photoFile.Id, photoNode.Id, photoNode.ParentId)]);
        }
        var videoFile = files.OfType<DownloadTaskOtherVideoFile>().Single();
        (await finalContext.DownloadTaskOtherVideoFileLogs.ToListAsync(CancellationToken))
            .Select(x => (x.DownloadTaskFileId, x.DownloadTaskOtherVideoId))
            .Distinct()
            .ShouldBe([(videoFile.Id, videoFile.ParentId)]);
        var expectedJobs = files
            .SelectMany(file =>
                new[]
                {
                    (DownloadJob.GetJobKey(file.Id), file.ToKey()),
                    (MoveDownloadFileJob.GetJobKey(file.Id), file.ToKey()),
                }
            )
            .OrderBy(x => x.Item1.ToString())
            .ToList();
        executions.OrderBy(x => x.JobKey.ToString()).ShouldBe(expectedJobs);
        completions.Select(x => x.JobKey).OrderBy(x => x.ToString()).ShouldBe(expectedJobs.Select(x => x.Item1));
        completions.Select(x => x.Error).Distinct().ShouldBe([null]);
        (await container.DownloadTaskScheduler.GetCurrentlyDownloadingKeysByServer(show.PlexServerId)).ShouldBeEmpty();
        (await container.MoveDownloadFileScheduler.IsAnyMoveDownloadFileJobRunning()).ShouldBeFalse();
        var expectedDownloads = new List<(string Url, string TargetPath)>();
        foreach (var file in files)
        {
            var urlResult = await finalContext.GetDownloadUrl(
                file.PlexServerId,
                file.FileLocationUrl,
                CancellationToken
            );
            urlResult.IsSuccess.ShouldBeTrue();
            urlResult.Errors.Count.ShouldBe(0);
            expectedDownloads.Add((new Uri(urlResult.Value).AbsoluteUri, file.DownloadFilePath.RemoveReapTempSuffix()));
        }
        downloadRequests
            .Select(x => (new Uri(x.Url).AbsoluteUri, x.TargetPath))
            .OrderBy(x => x.TargetPath)
            .ShouldBe(expectedDownloads.OrderBy(x => x.TargetPath));
        probeRequests
            .OrderBy(x => x.Url)
            .ShouldBe(
                expectedDownloads
                    .SelectMany(x =>
                        new[]
                        {
                            (HttpMethod.Get, x.Url),
                            (HttpMethod.Get, x.Url + (new Uri(x.Url).Query.Length > 0 ? "&" : "?") + "download=1"),
                        }
                    )
                    .OrderBy(x => x.Item2)
            );
        notifications.RefreshNotificationList.ToArray().ShouldBe([RefreshDataType.DownloadTasks]);
        await WaitForDatabaseConditionAsync(() =>
            nodes.All(node =>
                container
                    .MockDownloadHubService.DownloadPatchList.ToArray()
                    .SelectMany(x => x.Upserts)
                    .Any(x => x.Id == node.Id && x.Status == DownloadStatus.Completed)
            )
        );
        var patches = container.MockDownloadHubService.DownloadPatchList.ToArray();
        patches.Select(x => x.ServerId).Distinct().ShouldBe([show.PlexServerId]);
        patches.SelectMany(x => x.DeletedIds).ShouldBeEmpty();
        patches.SelectMany(x => x.Upserts).Select(x => x.Id).Distinct().Except(nodes.Select(x => x.Id)).ShouldBeEmpty();
        patches
            .SelectMany(x => x.Upserts)
            .Where(x => x.Status == DownloadStatus.Completed)
            .Select(x => x.Id)
            .Distinct()
            .OrderBy(x => x)
            .ShouldBe(nodes.Select(x => x.Id).OrderBy(x => x));
        var retainedControl = await finalContext.DownloadTaskOtherVideoFiles.SingleAsync(x => x.Id == control.Id);
        retainedControl.ParentId.ShouldBe(control.ParentId);
        retainedControl.DownloadStatus.ShouldBe(DownloadStatus.Paused);
        retainedControl.PlexApiRatingKey.ShouldBe(control.PlexApiRatingKey);
        retainedControl.DataReceived.ShouldBe(0);
        retainedControl.FileDataTransferred.ShouldBe(0);
        retainedControl.DownloadFilePath.ShouldBe(controlSourcePath);
        retainedControl.DestinationFilePath.ShouldBe(controlDestinationPath);
        (await finalContext.DownloadTaskOtherVideos.SingleAsync(x => x.Id == control.ParentId)).DownloadStatus.ShouldBe(
            DownloadStatus.Paused
        );
        fileSystem.File.ReadAllBytes(controlSourcePath).ShouldBe(controlBytes);
        fileSystem.File.Exists(controlDestinationPath).ShouldBeFalse();
        fileSystem
            .Directory.GetFiles(paths.DefaultDownloadsDestinationFolder, "*", SearchOption.AllDirectories)
            .ShouldBe([controlSourcePath]);
    }
}
