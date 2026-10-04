namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTaskLogUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPersistExactFileAndAncestorIds_WhenCreatingNewFamilyLogs()
    {
        // Arrange
        await SetupDatabase(62301, ConfigureLibraries);
        var dbContext = IDbContext;
        var graph = await AddNewFamilyTaskGraph(dbContext);

        // Act
        await dbContext.CreateDownloadClientLog(
            graph.TrackFile.ToKey() with { Type = DownloadTaskType.TrackPart },
            NotificationLevel.Warning,
            DownloadStatus.Downloading,
            "track-log"
        );
        await dbContext.CreateDownloadClientLog(
            graph.PhotoFile.ToKey() with { Type = DownloadTaskType.PhotoPart },
            NotificationLevel.Error,
            DownloadStatus.Error,
            "photo-log"
        );
        await dbContext.CreateDownloadClientLog(
            graph.OtherVideoFile.ToKey() with { Type = DownloadTaskType.OtherVideoPart },
            NotificationLevel.Information,
            DownloadStatus.DownloadFinished,
            "other-video-log"
        );

        // Assert
        var trackLog = await dbContext.DownloadTaskTrackFileLogs.SingleAsync(CancellationToken);
        trackLog.Message.ShouldBe("track-log");
        trackLog.LogLevel.ShouldBe(NotificationLevel.Warning);
        trackLog.Status.ShouldBe(DownloadStatus.Downloading);
        trackLog.DownloadTaskFileId.ShouldBe(graph.TrackFile.Id);
        trackLog.DownloadTaskTrackId.ShouldBe(graph.Track.Id);
        trackLog.DownloadTaskAlbumId.ShouldBe(graph.Album.Id);
        trackLog.DownloadTaskArtistId.ShouldBe(graph.Artist.Id);

        var photoLog = await dbContext.DownloadTaskPhotoFileLogs.SingleAsync(CancellationToken);
        photoLog.Message.ShouldBe("photo-log");
        photoLog.LogLevel.ShouldBe(NotificationLevel.Error);
        photoLog.Status.ShouldBe(DownloadStatus.Error);
        photoLog.DownloadTaskFileId.ShouldBe(graph.PhotoFile.Id);
        photoLog.DownloadTaskPhotoId.ShouldBe(graph.Photo.Id);

        var otherVideoLog = await dbContext.DownloadTaskOtherVideoFileLogs.SingleAsync(CancellationToken);
        otherVideoLog.Message.ShouldBe("other-video-log");
        otherVideoLog.LogLevel.ShouldBe(NotificationLevel.Information);
        otherVideoLog.Status.ShouldBe(DownloadStatus.DownloadFinished);
        otherVideoLog.DownloadTaskFileId.ShouldBe(graph.OtherVideoFile.Id);
        otherVideoLog.DownloadTaskOtherVideoId.ShouldBe(graph.OtherVideo.Id);
    }

    [Test]
    public async Task ShouldReturnOrderedIncrementalLogsForEveryNewFamilyScope_WhenHierarchyKeysAreUsed()
    {
        // Arrange
        await SetupDatabase(62302, ConfigureLibraries);
        var dbContext = IDbContext;
        var graph = await AddNewFamilyTaskGraph(dbContext);
        var createdAt = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);
        dbContext.DownloadTaskTrackFileLogs.AddRange(
            NewTrackLog(graph, "track-1", createdAt),
            NewTrackLog(graph, "track-2", createdAt.AddSeconds(1)),
            NewTrackLog(graph, "track-3", createdAt.AddSeconds(2))
        );
        dbContext.DownloadTaskPhotoFileLogs.Add(NewPhotoLog(graph, "photo-1", createdAt));
        dbContext.DownloadTaskOtherVideoFileLogs.Add(NewOtherVideoLog(graph, "video-1", createdAt));
        await dbContext.SaveChangesAsync(CancellationToken);
        var trackLogs = await dbContext.DownloadTaskTrackFileLogs.OrderBy(x => x.Id).ToListAsync(CancellationToken);

        // Act
        var artistResult = await dbContext.GetDownloadTaskLogsAsync(
            graph.Artist.ToKey(),
            trackLogs[0].Id,
            1,
            CancellationToken
        );
        var albumResult = await dbContext.GetDownloadTaskLogsAsync(graph.Album.ToKey(), null, null, CancellationToken);
        var trackResult = await dbContext.GetDownloadTaskLogsAsync(graph.Track.ToKey(), null, null, CancellationToken);
        var trackFileResult = await dbContext.GetDownloadTaskLogsAsync(
            graph.TrackFile.ToKey() with { Type = DownloadTaskType.TrackPart },
            null,
            null,
            CancellationToken
        );
        var photoResult = await dbContext.GetDownloadTaskLogsAsync(graph.Photo.ToKey(), null, null, CancellationToken);
        var photoFileResult = await dbContext.GetDownloadTaskLogsAsync(
            graph.PhotoFile.ToKey() with { Type = DownloadTaskType.PhotoPart },
            null,
            null,
            CancellationToken
        );
        var videoResult = await dbContext.GetDownloadTaskLogsAsync(graph.OtherVideo.ToKey(), null, null, CancellationToken);
        var videoFileResult = await dbContext.GetDownloadTaskLogsAsync(
            graph.OtherVideoFile.ToKey() with { Type = DownloadTaskType.OtherVideoPart },
            null,
            null,
            CancellationToken
        );

        // Assert
        artistResult.IsSuccess.ShouldBeTrue();
        artistResult.Errors.Count.ShouldBe(0);
        artistResult.Value.Select(x => (x.Id, x.Message)).ShouldBe([(trackLogs[1].Id, "track-2")]);
        albumResult.IsSuccess.ShouldBeTrue();
        albumResult.Errors.Count.ShouldBe(0);
        trackResult.IsSuccess.ShouldBeTrue();
        trackResult.Errors.Count.ShouldBe(0);
        trackFileResult.IsSuccess.ShouldBeTrue();
        trackFileResult.Errors.Count.ShouldBe(0);
        photoResult.IsSuccess.ShouldBeTrue();
        photoResult.Errors.Count.ShouldBe(0);
        photoFileResult.IsSuccess.ShouldBeTrue();
        photoFileResult.Errors.Count.ShouldBe(0);
        videoResult.IsSuccess.ShouldBeTrue();
        videoResult.Errors.Count.ShouldBe(0);
        videoFileResult.IsSuccess.ShouldBeTrue();
        videoFileResult.Errors.Count.ShouldBe(0);
        albumResult.Value.Select(x => x.Message).ShouldBe(["track-1", "track-2", "track-3"]);
        trackResult.Value.Select(x => x.Message).ShouldBe(["track-1", "track-2", "track-3"]);
        trackFileResult.Value.Select(x => x.Message).ShouldBe(["track-1", "track-2", "track-3"]);
        photoResult.Value.Select(x => x.Message).ShouldBe(["photo-1"]);
        photoFileResult.Value.Select(x => x.Message).ShouldBe(["photo-1"]);
        videoResult.Value.Select(x => x.Message).ShouldBe(["video-1"]);
        videoFileResult.Value.Select(x => x.Message).ShouldBe(["video-1"]);
    }

    [Test]
    public async Task ShouldDeleteOnlyRequestedNewFamilyScope_WhenSiblingLogsExist()
    {
        // Arrange
        await SetupDatabase(62303, ConfigureLibraries);
        var dbContext = IDbContext;
        var target = await AddNewFamilyTaskGraph(dbContext, "target");
        var sibling = await AddNewFamilyTaskGraph(dbContext, "sibling");
        dbContext.DownloadTaskTrackFileLogs.AddRange(NewTrackLog(target, "target-track"), NewTrackLog(sibling, "sibling-track"));
        dbContext.DownloadTaskPhotoFileLogs.AddRange(NewPhotoLog(target, "target-photo"), NewPhotoLog(sibling, "sibling-photo"));
        dbContext.DownloadTaskOtherVideoFileLogs.AddRange(
            NewOtherVideoLog(target, "target-video"),
            NewOtherVideoLog(sibling, "sibling-video")
        );
        await dbContext.SaveChangesAsync(CancellationToken);

        // Act
        var artistDelete = await dbContext.DeleteDownloadTaskLogsAsync(target.Artist.ToKey(), CancellationToken);
        var photoDelete = await dbContext.DeleteDownloadTaskLogsAsync(
            target.PhotoFile.ToKey() with { Type = DownloadTaskType.PhotoPart },
            CancellationToken
        );
        var videoDelete = await dbContext.DeleteDownloadTaskLogsAsync(target.OtherVideo.ToKey(), CancellationToken);

        // Assert
        artistDelete.IsSuccess.ShouldBeTrue();
        artistDelete.Errors.Count.ShouldBe(0);
        artistDelete.Value.ShouldBe(1);
        photoDelete.IsSuccess.ShouldBeTrue();
        photoDelete.Errors.Count.ShouldBe(0);
        videoDelete.IsSuccess.ShouldBeTrue();
        videoDelete.Errors.Count.ShouldBe(0);
        photoDelete.Value.ShouldBe(1);
        videoDelete.Value.ShouldBe(1);
        (await dbContext.DownloadTaskTrackFileLogs.OrderBy(x => x.Id).Select(x => x.Message).ToListAsync(CancellationToken))
            .ShouldBe(["sibling-track"]);
        (await dbContext.DownloadTaskPhotoFileLogs.OrderBy(x => x.Id).Select(x => x.Message).ToListAsync(CancellationToken))
            .ShouldBe(["sibling-photo"]);
        (await dbContext.DownloadTaskOtherVideoFileLogs.OrderBy(x => x.Id).Select(x => x.Message).ToListAsync(CancellationToken))
            .ShouldBe(["sibling-video"]);
    }

    [Test]
    public async Task ShouldDeleteEveryNewFamilyHierarchyScope_WhenEachScopeIsRequested()
    {
        // Arrange
        await SetupDatabase(62305, ConfigureLibraries);
        var dbContext = IDbContext;
        var graph = await AddNewFamilyTaskGraph(dbContext);

        // Act and Assert
        foreach (var key in new[] { graph.Album.ToKey(), graph.Track.ToKey(), graph.TrackFile.ToKey() with { Type = DownloadTaskType.TrackPart } })
        {
            dbContext.DownloadTaskTrackFileLogs.Add(NewTrackLog(graph, key.Type.ToString()));
            await dbContext.SaveChangesAsync(CancellationToken);
            var result = await dbContext.DeleteDownloadTaskLogsAsync(key, CancellationToken);
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            result.Value.ShouldBe(1);
        }

        dbContext.DownloadTaskPhotoFileLogs.Add(NewPhotoLog(graph, "photo-root"));
        await dbContext.SaveChangesAsync(CancellationToken);
        var photoResult = await dbContext.DeleteDownloadTaskLogsAsync(graph.Photo.ToKey(), CancellationToken);
        photoResult.IsSuccess.ShouldBeTrue();
        photoResult.Errors.Count.ShouldBe(0);
        photoResult.Value.ShouldBe(1);

        dbContext.DownloadTaskOtherVideoFileLogs.Add(NewOtherVideoLog(graph, "video-file"));
        await dbContext.SaveChangesAsync(CancellationToken);
        var videoResult = await dbContext.DeleteDownloadTaskLogsAsync(
            graph.OtherVideoFile.ToKey() with { Type = DownloadTaskType.OtherVideoPart },
            CancellationToken
        );
        videoResult.IsSuccess.ShouldBeTrue();
        videoResult.Errors.Count.ShouldBe(0);
        videoResult.Value.ShouldBe(1);
    }

    [Test]
    public async Task ShouldPreserveMovieReadAndDeleteSemantics_WhenNewFamiliesAreAdded()
    {
        // Arrange
        await SetupDatabase(62304, config => config.MovieDownloadTasksCount = 2);
        var dbContext = IDbContext;
        var files = await dbContext.DownloadTaskMovieFile.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        await dbContext.CreateDownloadClientLog(files[0].ToKey(), NotificationLevel.Information, DownloadStatus.Downloading, "target");
        await dbContext.CreateDownloadClientLog(files[1].ToKey(), NotificationLevel.Warning, DownloadStatus.Error, "control");

        // Act
        var readResult = await dbContext.GetDownloadTaskLogsAsync(files[0].ToParentKey(), null, null, CancellationToken);
        var deleteResult = await dbContext.DeleteDownloadTaskLogsAsync(files[0].ToParentKey(), CancellationToken);

        // Assert
        readResult.IsSuccess.ShouldBeTrue();
        readResult.Errors.Count.ShouldBe(0);
        readResult.Value.Select(x => x.Message).ShouldBe(["target"]);
        deleteResult.IsSuccess.ShouldBeTrue();
        deleteResult.Errors.Count.ShouldBe(0);
        deleteResult.Value.ShouldBe(1);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe(["control"]);
    }

    private static void ConfigureLibraries(FakeDataConfig config)
    {
        config.PlexMusicLibraryCount = 1;
        config.PlexPhotoLibraryCount = 1;
        config.PlexOtherVideoLibraryCount = 1;
    }

    private static async Task<NewFamilyTaskGraph> AddNewFamilyTaskGraph(IReaparrDbContext dbContext, string suffix = "one")
    {
        var musicLibrary = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.MusicArtist);
        var photoLibrary = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.PhotoAlbum);
        var videoLibrary = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.OtherVideos);
        var createdAt = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
        var ratingKeyOffset = suffix switch
        {
            "target" => 10,
            "sibling" => 20,
            _ => 0,
        };

        var artist = new DownloadTaskArtist
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1000 + ratingKeyOffset, Title = $"artist-{suffix}", FullTitle = $"artist-{suffix}",
            DownloadStatus = DownloadStatus.Queued, CreatedAt = createdAt, PlexServerId = musicLibrary.PlexServerId,
            PlexLibraryId = musicLibrary.Id, Year = 2026, Children = [], DataReceived = 0, FileDataTransferred = 0,
            DataTotal = 0, DownloadSpeed = 0, FileTransferSpeed = 0,
        };
        dbContext.DownloadTaskArtists.Add(artist);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var album = new DownloadTaskAlbum
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1001 + ratingKeyOffset, Title = $"album-{suffix}", FullTitle = $"album-{suffix}",
            DownloadStatus = DownloadStatus.Queued, CreatedAt = createdAt, PlexServerId = musicLibrary.PlexServerId,
            PlexLibraryId = musicLibrary.Id, Year = 2026, ParentId = artist.Id, Children = [], DataReceived = 0,
            FileDataTransferred = 0, DataTotal = 0, DownloadSpeed = 0, FileTransferSpeed = 0,
        };
        dbContext.DownloadTaskAlbums.Add(album);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var track = new DownloadTaskTrack
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1002 + ratingKeyOffset, Title = $"track-{suffix}", FullTitle = $"track-{suffix}",
            DownloadStatus = DownloadStatus.Queued, CreatedAt = createdAt, PlexServerId = musicLibrary.PlexServerId,
            PlexLibraryId = musicLibrary.Id, Year = 2026, ParentId = album.Id, Children = [], DataReceived = 0,
            FileDataTransferred = 0, DataTotal = 0, DownloadSpeed = 0, FileTransferSpeed = 0,
        };
        dbContext.DownloadTaskTracks.Add(track);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var trackFile = NewTrackFile(track.Id, musicLibrary, $"track-{suffix}", createdAt);
        dbContext.DownloadTaskTrackFiles.Add(trackFile);

        var photo = new DownloadTaskPhoto
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1003 + ratingKeyOffset, Title = $"photo-{suffix}", FullTitle = $"photo-{suffix}",
            DownloadStatus = DownloadStatus.Queued, CreatedAt = createdAt, PlexServerId = photoLibrary.PlexServerId,
            PlexLibraryId = photoLibrary.Id, Year = 2026, Kind = PhotoAssetKind.Image, Children = [], DataReceived = 0,
            FileDataTransferred = 0, DataTotal = 0, DownloadSpeed = 0, FileTransferSpeed = 0,
        };
        dbContext.DownloadTaskPhotos.Add(photo);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var photoFile = NewPhotoFile(photo.Id, photoLibrary, $"photo-{suffix}", createdAt);
        dbContext.DownloadTaskPhotoFiles.Add(photoFile);

        var otherVideo = new DownloadTaskOtherVideo
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1004 + ratingKeyOffset, Title = $"video-{suffix}", FullTitle = $"video-{suffix}",
            DownloadStatus = DownloadStatus.Queued, CreatedAt = createdAt, PlexServerId = videoLibrary.PlexServerId,
            PlexLibraryId = videoLibrary.Id, Year = 2026, Children = [], DataReceived = 0, FileDataTransferred = 0,
            DataTotal = 0, DownloadSpeed = 0, FileTransferSpeed = 0,
        };
        dbContext.DownloadTaskOtherVideos.Add(otherVideo);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        var otherVideoFile = NewOtherVideoFile(otherVideo.Id, videoLibrary, $"video-{suffix}", createdAt);
        dbContext.DownloadTaskOtherVideoFiles.Add(otherVideoFile);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        return new(artist, album, track, trackFile, photo, photoFile, otherVideo, otherVideoFile);
    }

    private static DownloadTaskTrackFile NewTrackFile(Guid parentId, PlexLibrary library, string title, DateTime createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            PlexApiRatingKey = RatingKeyFor(title, 2000),
            Title = title,
            FullTitle = title,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            PlexApiMediaId = 1,
            PlexApiPartId = 1,
            FileName = $"{title}.mp4",
            FileLocationUrl = "/file",
            HashId = null,
            Quality = VideoQuality.HD,
            DirectoryMeta = NewDirectory(title),
            DataReceived = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            DestinationFolderPathId = null,
        };

    private static DownloadTaskPhotoFile NewPhotoFile(Guid parentId, PlexLibrary library, string title, DateTime createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            PlexApiRatingKey = RatingKeyFor(title, 3000),
            Title = title,
            FullTitle = title,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            PlexApiMediaId = 1,
            PlexApiPartId = 1,
            FileName = $"{title}.jpg",
            FileLocationUrl = "/file",
            HashId = null,
            Quality = VideoQuality.HD,
            DirectoryMeta = NewDirectory(title),
            DataReceived = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            DestinationFolderPathId = null,
        };

    private static DownloadTaskOtherVideoFile NewOtherVideoFile(
        Guid parentId,
        PlexLibrary library,
        string title,
        DateTime createdAt
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            PlexApiRatingKey = RatingKeyFor(title, 4000),
            Title = title,
            FullTitle = title,
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            PlexApiMediaId = 1,
            PlexApiPartId = 1,
            FileName = $"{title}.mp4",
            FileLocationUrl = "/file",
            HashId = null,
            Quality = VideoQuality.HD,
            DirectoryMeta = NewDirectory(title),
            DataReceived = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            DirectDownloadSnapshot = null,
            DownloadClientType = PlexDownloadClientType.Direct,
            FileTransferSpeed = 0,
            FileDataTransferred = 0,
            TimeRemaining = 0,
            DestinationFolderPathId = null,
        };

    private static DownloadTaskDirectory NewDirectory(string title) =>
        new()
        {
            DownloadRootPath = "/downloads",
            DestinationRootPath = "/destination",
            MovieFolder = title,
            TvShowFolder = string.Empty,
            SeasonFolder = string.Empty,
            KeepCompletedInDownloadFolder = false,
        };

    private static int RatingKeyFor(string title, int baseValue) =>
        title.Contains("target", StringComparison.Ordinal) ? baseValue + 1
        : title.Contains("sibling", StringComparison.Ordinal) ? baseValue + 2
        : baseValue;

    private static DownloadTaskTrackFileLog NewTrackLog(NewFamilyTaskGraph graph, string message, DateTime? createdAt = null) =>
        new() { Message = message, LogLevel = NotificationLevel.Information, Status = DownloadStatus.Downloading,
            CreatedAt = createdAt ?? DateTime.UtcNow, DownloadTaskFileId = graph.TrackFile.Id,
            DownloadTaskTrackId = graph.Track.Id, DownloadTaskAlbumId = graph.Album.Id, DownloadTaskArtistId = graph.Artist.Id };

    private static DownloadTaskPhotoFileLog NewPhotoLog(NewFamilyTaskGraph graph, string message, DateTime? createdAt = null) =>
        new() { Message = message, LogLevel = NotificationLevel.Information, Status = DownloadStatus.Downloading,
            CreatedAt = createdAt ?? DateTime.UtcNow, DownloadTaskFileId = graph.PhotoFile.Id, DownloadTaskPhotoId = graph.Photo.Id };

    private static DownloadTaskOtherVideoFileLog NewOtherVideoLog(NewFamilyTaskGraph graph, string message, DateTime? createdAt = null) =>
        new() { Message = message, LogLevel = NotificationLevel.Information, Status = DownloadStatus.Downloading,
            CreatedAt = createdAt ?? DateTime.UtcNow, DownloadTaskFileId = graph.OtherVideoFile.Id,
            DownloadTaskOtherVideoId = graph.OtherVideo.Id };

    private sealed record NewFamilyTaskGraph(
        DownloadTaskArtist Artist, DownloadTaskAlbum Album, DownloadTaskTrack Track, DownloadTaskTrackFile TrackFile,
        DownloadTaskPhoto Photo, DownloadTaskPhotoFile PhotoFile, DownloadTaskOtherVideo OtherVideo,
        DownloadTaskOtherVideoFile OtherVideoFile);
}
