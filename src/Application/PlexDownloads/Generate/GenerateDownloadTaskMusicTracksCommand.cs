namespace Reaparr.Application;

public record GenerateDownloadTaskMusicTracksCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskMusicTracksCommand(CreateDownloadTasksRequest request) => Request = request;

    public GenerateDownloadTaskMusicTracksCommand(List<DownloadMediaDTO> downloadMediaDtos) =>
        Request = new CreateDownloadTasksRequest(downloadMediaDtos);

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskMusicTracksCommandValidator
    : AbstractValidator<GenerateDownloadTaskMusicTracksCommand>
{
    public GenerateDownloadTaskMusicTracksCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.DownloadMedias).NotNull().NotEmpty();
                RuleForEach(x => x.Request.DownloadMedias).SetValidator(new DownloadMediaDTOValidator());
            });
    }
}

public class GenerateDownloadTaskMusicTracksCommandHandler
    : ICommandHandler<GenerateDownloadTaskMusicTracksCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GenerateDownloadTaskMusicTracksCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GenerateDownloadTaskMusicTracksCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskMusicTracksCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var trackSelections = request.DownloadMedias.MergeAndGroupList().FindAll(x => x.Type == PlexMediaType.MusicTrack);
        if (trackSelections.Count == 0)
            return ResultExtensions.IsEmpty(nameof(trackSelections)).LogWarning();
        if (request.Integration is not null)
            return ResultExtensions
                .Create400BadRequestResult("Music downloads do not support Sonarr or Radarr integrations.")
                .LogError();
        _log.Here().Debug("Creating {TrackCount} Music track download tasks", trackSelections.Sum(x => x.MediaIds.Count));

        var downloadRootPath = (await _dbContext.GetDownloadFolder(null)).DirectoryPath;
        var createdTrackIds = new HashSet<int>();
        var artistTasks = new Dictionary<(int ServerId, int LibraryId, int RatingKey), DownloadTaskMusicArtist>();
        var createdFiles = new List<DownloadTaskMusicTrackFile>();
        foreach (var selection in trackSelections)
        {
            var library = await _dbContext.PlexLibraries.GetAsync(selection.PlexLibraryId, cancellationToken);
            if (
                library is null
                || library.PlexServerId != selection.PlexServerId
                || library.Type != PlexMediaType.MusicArtist
            )
                return ResultExtensions
                    .Create400BadRequestResult(
                        "The selected Music library does not match the requested server or media type."
                    )
                    .LogError();

            var trackIds = selection.MediaIds.Distinct().ToList();
            var tracks = await _dbContext
                .PlexTracks.Include(x => x.PlexAlbum)
                    .ThenInclude(x => x!.PlexArtist)
                .Include(x => x.MediaDataList)
                .Where(x =>
                    trackIds.Contains(x.Id)
                    && x.PlexLibraryId == selection.PlexLibraryId
                    && x.PlexServerId == selection.PlexServerId
                )
                .ToListAsync(cancellationToken);
            if (
                tracks.Count != trackIds.Count
                || tracks.Any(x =>
                    x.PlexAlbum?.PlexArtist is null
                    || x.PlexAlbum.PlexLibraryId != x.PlexLibraryId
                    || x.PlexAlbum.PlexServerId != x.PlexServerId
                    || x.PlexAlbum.PlexArtist.PlexLibraryId != x.PlexLibraryId
                    || x.PlexAlbum.PlexArtist.PlexServerId != x.PlexServerId
                )
            )
                return ResultExtensions
                    .Create400BadRequestResult(
                        "Every selected Music track must belong to the requested library and server."
                    )
                    .LogError();

            foreach (var track in tracks)
            {
                var selectedQuality = selection.Qualities.FirstOrDefault(x =>
                    x.MediaId == track.Id && x.MediaDataType == PlexMediaType.MusicTrack
                );
                var selectedMedia = selectedQuality is null
                    ? null
                    : track.MediaDataList.FirstOrDefault(x => x.Id == selectedQuality.DataId);
                selectedMedia ??= track.MediaDataList.PickMediaQuality();
                if (selectedMedia is null)
                {
                    _log.Here().Warning("Music track {TrackId} has no media data available", track.Id);
                    continue;
                }
                var selectedParts = track
                    .MediaDataList.Where(x => x.PlexApiMediaId == selectedMedia.PlexApiMediaId)
                    .ToList();
                var album = track.PlexAlbum!;
                var artist = album.PlexArtist!;
                var artistKey = (artist.PlexServerId, artist.PlexLibraryId, artist.PlexApiRatingKey);
                if (!artistTasks.TryGetValue(artistKey, out var artistTask))
                {
                    artistTask = await _dbContext
                        .DownloadTaskMusicArtists.AsTracking()
                        .WhereIntegrationOwnershipMatches(null)
                        .Include(x => x.Children)
                            .ThenInclude(x => x.Children)
                                .ThenInclude(x => x.Children)
                        .SingleOrDefaultAsync(
                            x =>
                                x.PlexServerId == artist.PlexServerId
                                && x.PlexLibraryId == artist.PlexLibraryId
                                && x.PlexApiRatingKey == artist.PlexApiRatingKey,
                            cancellationToken
                        );
                    if (artistTask is null)
                    {
                        artistTask = artist.MapToDownloadTask(null);
                        _dbContext.DownloadTaskMusicArtists.Add(artistTask);
                    }
                    artistTasks.Add(artistKey, artistTask);
                }
                var albumTask = artistTask.Children.SingleOrDefault(x =>
                    x.PlexApiRatingKey == album.PlexApiRatingKey
                );
                if (albumTask is null)
                {
                    albumTask = album.MapToDownloadTask(artistTask, null);
                    artistTask.Children.Add(albumTask);
                    _dbContext.DownloadTaskMusicAlbums.Add(albumTask);
                }

                var trackTask = albumTask.Children.SingleOrDefault(x =>
                    x.PlexApiRatingKey == track.PlexApiRatingKey
                );
                if (trackTask is null)
                {
                    trackTask = track.MapToDownloadTask(albumTask, null);
                    albumTask.Children.Add(trackTask);
                    _dbContext.DownloadTaskMusicTracks.Add(trackTask);
                }

                foreach (var mediaData in selectedParts)
                {
                    if (
                        trackTask.Children.Any(x =>
                            x.PlexApiMediaId == mediaData.PlexApiMediaId
                            && x.PlexApiPartId == mediaData.PlexApiPartId
                        )
                    )
                        continue;

                    var file = mediaData.MapToDownloadTask(
                        trackTask,
                        track,
                        album,
                        artist,
                        request,
                        downloadRootPath,
                        selection.KeepCompletedInDownloadFolder
                    );
                    trackTask.Children.Add(file);
                    _dbContext.DownloadTaskMusicTrackFiles.Add(file);
                    createdFiles.Add(file);
                    createdTrackIds.Add(track.Id);
                }

                if (createdTrackIds.Contains(track.Id))
                {
                    trackTask.DownloadStatus = DownloadTaskActions.Aggregate(
                        trackTask.Children.Select(x => x.DownloadStatus).ToList()
                    );
                    albumTask.DownloadStatus = DownloadTaskActions.Aggregate(
                        albumTask.Children.Select(x => x.DownloadStatus).ToList()
                    );
                    artistTask.DownloadStatus = DownloadTaskActions.Aggregate(
                        artistTask.Children.Select(x => x.DownloadStatus).ToList()
                    );
                }
            }
        }

        if (createdFiles.Count == 0)
            return Result.Ok(new DownloadTaskCreationReport());

        await _dbContext.SaveChangesAsync(cancellationToken);
        var logs = createdFiles
            .Select(file => new DownloadTaskTrackFileLog
            {
                Status = DownloadStatus.Queued,
                LogLevel = NotificationLevel.Information,
                Message = $"DownloadTask {file.FileName} was queued for downloading",
                DownloadTaskFileId = file.Id,
                DownloadTaskTrackId = file.ParentId,
                DownloadTaskAlbumId = file.Parent!.ParentId,
                DownloadTaskArtistId = file.Parent.Parent!.ParentId,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();
        _dbContext.DownloadTaskTrackFileLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok(new DownloadTaskCreationReport { MusicTracks = createdTrackIds.Count });
    }
}
