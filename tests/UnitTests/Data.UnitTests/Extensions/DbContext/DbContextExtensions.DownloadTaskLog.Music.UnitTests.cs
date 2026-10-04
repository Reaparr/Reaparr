namespace Reaparr.Data.UnitTests;

public class DbContextExtensionsDownloadTaskLogMusicUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(DownloadTaskType.MusicTrackData)]
    [Arguments(DownloadTaskType.MusicTrackPart)]
    public async Task ShouldPersistMusicLogWithAncestorIds_WhenMovieLogUsesTheSameId(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(
            62301,
            config =>
            {
                config.PlexMusicLibraryCount = 1;
                config.MovieDownloadTasksCount = 1;
            }
        );
        var dbContext = IDbContext;
        var file = await AddMusicTask(dbContext, 1);
        var movieFile = await dbContext.DownloadTaskMovieFile.SingleAsync(CancellationToken);
        await dbContext.CreateDownloadClientLog(
            movieFile.ToKey(),
            NotificationLevel.Information,
            DownloadStatus.Downloading,
            "movie-control"
        );
        var movieLog = await dbContext.DownloadTaskMovieFileLogs.SingleAsync(CancellationToken);
        movieLog.Id.ShouldBe(1);

        // Act
        await dbContext.CreateDownloadClientLog(
            file.ToKey() with
            {
                Type = type,
            },
            NotificationLevel.Warning,
            DownloadStatus.Downloading,
            "track-log"
        );

        // Assert
        var log = await dbContext.DownloadTaskTrackFileLogs.SingleAsync(CancellationToken);
        log.Id.ShouldBe(movieLog.Id);
        log.Message.ShouldBe("track-log");
        log.LogLevel.ShouldBe(NotificationLevel.Warning);
        log.Status.ShouldBe(DownloadStatus.Downloading);
        log.DownloadTaskFileId.ShouldBe(file.Id);
        log.DownloadTaskTrackId.ShouldBe(file.ParentId);
        log.DownloadTaskAlbumId.ShouldBe(file.Parent!.ParentId);
        log.DownloadTaskArtistId.ShouldBe(file.Parent.Parent!.ParentId);
        (await dbContext.DownloadTaskMovieFileLogs.Select(x => x.Message).ToListAsync(CancellationToken)).ShouldBe([
            "movie-control",
        ]);
    }

    [Test]
    [Arguments(DownloadTaskType.MusicArtist, true)]
    [Arguments(DownloadTaskType.MusicAlbum, false)]
    [Arguments(DownloadTaskType.MusicTrack, false)]
    [Arguments(DownloadTaskType.MusicTrackData, false)]
    [Arguments(DownloadTaskType.MusicTrackPart, false)]
    public async Task ShouldReturnOrderedMusicLogsForRequestedScope_WhenSiblingLogsExist(
        DownloadTaskType type,
        bool incremental
    )
    {
        // Arrange
        await SetupDatabase(62302, config => config.PlexMusicLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await AddMusicTask(dbContext, 1);
        var sibling = await AddMusicTask(dbContext, 2);
        dbContext.DownloadTaskTrackFileLogs.AddRange(
            MusicLog(file, "track-1"),
            MusicLog(file, "track-2"),
            MusicLog(file, "track-3"),
            MusicLog(sibling, "sibling")
        );
        await dbContext.SaveChangesAsync(CancellationToken);
        var logs = await dbContext
            .DownloadTaskTrackFileLogs.Where(x => x.DownloadTaskFileId == file.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(CancellationToken);
        logs.Select(x => x.Message).ShouldBe(["track-1", "track-2", "track-3"]);
        var key = file.ToKey() with
        {
            Type = type,
            Id = type switch
            {
                DownloadTaskType.MusicArtist => file.Parent!.Parent!.ParentId,
                DownloadTaskType.MusicAlbum => file.Parent!.ParentId,
                DownloadTaskType.MusicTrack => file.ParentId,
                _ => file.Id,
            },
        };

        // Act
        var result = await dbContext.GetDownloadTaskLogsAsync(
            key,
            incremental ? logs[0].Id : null,
            incremental ? 1 : null,
            CancellationToken
        );

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        var expected = incremental ? new[] { logs[1] } : logs.ToArray();
        result.Value.Select(x => (x.Id, x.Message)).ShouldBe(expected.Select(x => (x.Id, x.Message)));
    }

    [Test]
    [Arguments(DownloadTaskType.MusicArtist)]
    [Arguments(DownloadTaskType.MusicAlbum)]
    [Arguments(DownloadTaskType.MusicTrack)]
    [Arguments(DownloadTaskType.MusicTrackData)]
    [Arguments(DownloadTaskType.MusicTrackPart)]
    public async Task ShouldDeleteOnlyRequestedMusicLogs_WhenSiblingLogsExist(DownloadTaskType type)
    {
        // Arrange
        await SetupDatabase(62303, config => config.PlexMusicLibraryCount = 1);
        var dbContext = IDbContext;
        var file = await AddMusicTask(dbContext, 1);
        var sibling = await AddMusicTask(dbContext, 2);
        dbContext.DownloadTaskTrackFileLogs.AddRange(MusicLog(file, "target"), MusicLog(sibling, "sibling"));
        await dbContext.SaveChangesAsync(CancellationToken);
        (
            await dbContext
                .DownloadTaskTrackFileLogs.OrderBy(x => x.Id)
                .Select(x => x.Message)
                .ToListAsync(CancellationToken)
        ).ShouldBe(["target", "sibling"]);
        var key = file.ToKey() with
        {
            Type = type,
            Id = type switch
            {
                DownloadTaskType.MusicArtist => file.Parent!.Parent!.ParentId,
                DownloadTaskType.MusicAlbum => file.Parent!.ParentId,
                DownloadTaskType.MusicTrack => file.ParentId,
                _ => file.Id,
            },
        };

        // Act
        var result = await dbContext.DeleteDownloadTaskLogsAsync(key, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        result.Value.ShouldBe(1);
        (
            await dbContext
                .DownloadTaskTrackFileLogs.Select(x => new { x.DownloadTaskFileId, x.Message })
                .ToListAsync(CancellationToken)
        ).ShouldBe([new { DownloadTaskFileId = sibling.Id, Message = "sibling" }]);
    }

    private static async Task<DownloadTaskMusicTrackFile> AddMusicTask(IReaparrDbContext dbContext, int index)
    {
        var library = await dbContext.PlexLibraries.SingleAsync(x => x.Type == PlexMediaType.MusicArtist);
        var createdAt = new DateTime(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);
        var artist = new DownloadTaskMusicArtist
        {
            Id = Guid.NewGuid(),
            PlexApiRatingKey = 1000 + index * 10,
            Title = $"artist-{index}",
            FullTitle = $"artist-{index}",
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Year = 2026,
            Children = [],
            DataReceived = 0,
            FileDataTransferred = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
        };
        var album = new DownloadTaskMusicAlbum
        {
            Id = Guid.NewGuid(),
            ParentId = artist.Id,
            Parent = artist,
            PlexApiRatingKey = 1001 + index * 10,
            Title = $"album-{index}",
            FullTitle = $"album-{index}",
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Year = 2026,
            Children = [],
            DataReceived = 0,
            FileDataTransferred = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
        };
        var track = new DownloadTaskMusicTrack
        {
            Id = Guid.NewGuid(),
            ParentId = album.Id,
            Parent = album,
            PlexApiRatingKey = 1002 + index * 10,
            Title = $"track-{index}",
            FullTitle = $"track-{index}",
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            Year = 2026,
            Children = [],
            DataReceived = 0,
            FileDataTransferred = 0,
            DataTotal = 0,
            DownloadSpeed = 0,
            FileTransferSpeed = 0,
        };
        var file = new DownloadTaskMusicTrackFile
        {
            Id = Guid.NewGuid(),
            ParentId = track.Id,
            Parent = track,
            PlexApiRatingKey = 2000 + index,
            Title = $"track-{index}",
            FullTitle = $"track-{index}",
            DownloadStatus = DownloadStatus.Queued,
            CreatedAt = createdAt,
            PlexServerId = library.PlexServerId,
            PlexLibraryId = library.Id,
            PlexApiMediaId = 1,
            PlexApiPartId = 1,
            FileName = $"track-{index}.flac",
            FileLocationUrl = "/file",
            HashId = null,
            Quality = VideoQuality.HD,
            DirectoryMeta = new DownloadTaskDirectory
            {
                DownloadRootPath = "/downloads",
                DestinationRootPath = "/destination",
                MovieFolder = $"track-{index}",
                TvShowFolder = string.Empty,
                SeasonFolder = string.Empty,
                KeepCompletedInDownloadFolder = false,
            },
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
        dbContext.DownloadTaskTrackFiles.Add(file);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return file;
    }

    private static DownloadTaskTrackFileLog MusicLog(DownloadTaskMusicTrackFile file, string message) =>
        new()
        {
            Message = message,
            LogLevel = NotificationLevel.Information,
            Status = DownloadStatus.Downloading,
            CreatedAt = new DateTime(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc),
            DownloadTaskFileId = file.Id,
            DownloadTaskTrackId = file.ParentId,
            DownloadTaskAlbumId = file.Parent!.ParentId,
            DownloadTaskArtistId = file.Parent.Parent!.ParentId,
        };
}
