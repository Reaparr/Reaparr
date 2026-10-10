namespace Reaparr.Application;

/// <summary>
/// Restart the <see cref="DownloadTaskGeneric"/> by deleting the PlexDownloadClient and starting a new one.
/// </summary>
/// <param name="DownloadTaskGuid">The id of the <see cref="DownloadTaskGeneric"/> to restart.</param>
/// <returns>Is successful.</returns>
public record RestartDownloadTaskCommand(Guid DownloadTaskGuid) : ICommand<Result>;

public class RestartDownloadTaskCommandValidator : AbstractValidator<RestartDownloadTaskCommand>
{
    public RestartDownloadTaskCommandValidator()
    {
        RuleFor(x => x.DownloadTaskGuid).NotEmpty();
    }
}

public class RestartDownloadTaskCommandHandler : ICommandHandler<RestartDownloadTaskCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;
    private readonly IEventPublisher _eventPublisher;

    public RestartDownloadTaskCommandHandler(
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor,
        IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher,
        IEventPublisher eventPublisher
    )
    {
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result> ExecuteAsync(RestartDownloadTaskCommand command, CancellationToken cancellationToken)
    {
        var downloadTaskKey = await _dbContext.GetDownloadTaskKeyAsync(command.DownloadTaskGuid, cancellationToken);
        if (downloadTaskKey is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), command.DownloadTaskGuid).LogWarning();

        var childKeys = await _dbContext.GetDownloadableChildTaskKeys(downloadTaskKey, cancellationToken);

        await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
            downloadTaskKey,
            DownloadStatus.Restarting,
            cancellationToken
        );

        await _dbContext.CreateDownloadClientLog(
            downloadTaskKey,
            NotificationLevel.Information,
            DownloadStatus.Restarting,
            $"Restart requested for download task group {downloadTaskKey.Id}. {childKeys.Count} child task(s) will be processed."
        );

        foreach (var childKey in childKeys)
        {
            var downloadTask = await _dbContext.GetDownloadTaskAsync(childKey, cancellationToken);
            if (downloadTask is null)
            {
                ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), childKey.Id).LogError();
                continue;
            }

            var stopResult = await _commandExecutor.Send(new StopDownloadTaskCommand(childKey.Id), cancellationToken);

            if (stopResult.IsFailed)
                return stopResult.LogIfFailed();

            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                childKey,
                DownloadStatus.Restarting,
                cancellationToken
            );

            await _dbContext.CreateDownloadClientLog(
                childKey,
                NotificationLevel.Information,
                DownloadStatus.Restarting,
                $"Restart workflow: stop completed for child task {childKey.Id} ({downloadTask.FileName}), preparing to queue."
            );

            switch (downloadTask.DownloadTaskType)
            {
                case DownloadTaskType.MovieData:
                case DownloadTaskType.MoviePart:
                    var refreshResult = await RefreshMovieDownloadTask(childKey, cancellationToken);
                    if (refreshResult.IsCancelled)
                        return refreshResult.LogWarning();

                    if (refreshResult.IsFailed)
                    {
                        refreshResult.LogError();
                        continue;
                    }

                    break;

                case DownloadTaskType.EpisodeData:
                case DownloadTaskType.EpisodePart:
                    var refreshEpisodeResult = await RefreshEpisodeMovieDownloadTask(childKey, cancellationToken);
                    if (refreshEpisodeResult.IsCancelled)
                        return refreshEpisodeResult.LogWarning();

                    if (refreshEpisodeResult.IsFailed)
                    {
                        refreshEpisodeResult.LogError();
                        continue;
                    }
                    break;
                case DownloadTaskType.PhotoData:
                case DownloadTaskType.PhotoPart:
                    var refreshPhotoResult = await RefreshPhotoDownloadTask(childKey, cancellationToken);
                    if (refreshPhotoResult.IsCancelled)
                        return refreshPhotoResult.LogWarning();
                    if (refreshPhotoResult.IsFailed)
                    {
                        refreshPhotoResult.LogError();
                        continue;
                    }
                    break;
                case DownloadTaskType.MusicTrackData:
                case DownloadTaskType.MusicTrackPart:
                    var refreshMusicResult = await RefreshMusicDownloadTask(childKey, cancellationToken);
                    if (refreshMusicResult.IsCancelled)
                        return refreshMusicResult.LogWarning();
                    if (refreshMusicResult.IsFailed)
                    {
                        refreshMusicResult.LogError();
                        continue;
                    }
                    break;
                case DownloadTaskType.OtherVideoData:
                case DownloadTaskType.OtherVideoPart:
                    var refreshOtherVideoResult = await RefreshOtherVideoDownloadTask(childKey, cancellationToken);
                    if (refreshOtherVideoResult.IsCancelled)
                        return refreshOtherVideoResult.LogWarning();
                    if (refreshOtherVideoResult.IsFailed)
                    {
                        refreshOtherVideoResult.LogError();
                        continue;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"The {downloadTask.DownloadTaskType} is unsupported");
            }

            await _dbContext.CreateDownloadClientLog(
                childKey,
                NotificationLevel.Information,
                DownloadStatus.Queued,
                $"Restart workflow: child task {childKey.Id} ({downloadTask.FileName}) queued again."
            );
        }

        await _eventPublisher.PublishAsync(
            new CheckDownloadQueueEvent(downloadTaskKey.PlexServerId),
            cancellationToken
        );

        await _dbContext.CreateDownloadClientLog(
            downloadTaskKey,
            NotificationLevel.Information,
            DownloadStatus.Queued,
            $"Restart workflow complete for group {downloadTaskKey.Id}; queue check published."
        );

        return Result.Ok();
    }

    private async Task<Result> RefreshMovieDownloadTask(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        var downloadTask = await _dbContext.DownloadTaskMovieFile.FirstOrDefaultAsync(
            x => x.Id == downloadTaskKey.Id,
            cancellationToken
        );
        if (downloadTask is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogError();

        var newDownloadTask = await _dbContext
            .PlexMovieData.Include(x => x.PlexMovie)
            .Where(x =>
                x.PlexApiMediaId == downloadTask.PlexApiMediaId && x.PlexApiPartId == downloadTask.PlexApiPartId
            )
            .Select(x => new DownloadTaskMovieFile
            {
                Id = downloadTask.Id,
                Parent = downloadTask.Parent,
                ParentId = downloadTask.ParentId,
                Title = x.GetFileName,
                DownloadStatus = DownloadStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                FullTitle = $"{x.PlexMovie!.FullTitle}/{x.GetFileName}",
                DataTotal = x.Size,
                PlexServerId = downloadTask.PlexServerId,
                PlexLibraryId = downloadTask.PlexLibraryId,
                PlexApiRatingKey = downloadTask.PlexApiRatingKey,
                PlexApiMediaId = downloadTask.PlexApiMediaId,
                PlexApiPartId = downloadTask.PlexApiPartId,
                FileName = x.GetFileName,
                FileLocationUrl = x.Key,
                HashId = downloadTask.HashId,
                Quality = x.VideoResolution,
                DirectoryMeta = new DownloadTaskDirectory
                {
                    DownloadRootPath = downloadTask.DirectoryMeta.DownloadRootPath,
                    DestinationRootPath = downloadTask.DirectoryMeta.DestinationRootPath,
                    MovieFolder = x.PlexMovie.Title.SanitizeFolderName(),
                    TvShowFolder = string.Empty,
                    SeasonFolder = string.Empty,
                    MusicArtistFolder = string.Empty,
                    MusicAlbumFolder = string.Empty,
                    PhotoAlbumFolder = string.Empty,
                    OtherVideoFolder = string.Empty,
                    KeepCompletedInDownloadFolder = downloadTask.DirectoryMeta.KeepCompletedInDownloadFolder,
                },
                DataReceived = 0,
                DownloadSpeed = 0,
                DirectDownloadSnapshot = null,
                DownloadClientType = downloadTask.DownloadClientType,
                FileTransferSpeed = 0,
                FileDataTransferred = 0,
                TimeRemaining = 0,
                DestinationFolderPathId = downloadTask.DestinationFolderPathId,
                SonarrIntegrationId = downloadTask.SonarrIntegrationId,
                RadarrIntegrationId = downloadTask.RadarrIntegrationId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (newDownloadTask is null)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTask.ToKey(),
                DownloadStatus.SourceUnavailable,
                cancellationToken
            );

            await _dbContext.CreateDownloadClientLog(
                downloadTask.ToKey(),
                NotificationLevel.Information,
                DownloadStatus.SourceUnavailable,
                $"Could not find the original source media for download task \"{downloadTaskKey}\" with title \"{downloadTask.FullTitle}\""
            );

            return Result.Fail(
                "Could not find the original source media for download task \"{DownloadTaskKey}\" with title \"{DownloadTaskFullTitle}\"",
                downloadTaskKey,
                downloadTask.FullTitle
            );
        }

        return await Result.Try(async Task () =>
        {
            _dbContext.DownloadTaskMovieFile.Update(newDownloadTask);
            await _dbContext.SaveChangesAsync(cancellationToken);
        });
    }

    private async Task<Result> RefreshEpisodeMovieDownloadTask(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        var downloadTask = await _dbContext.DownloadTaskTvShowEpisodeFile.FirstOrDefaultAsync(
            x => x.Id == downloadTaskKey.Id,
            cancellationToken
        );
        if (downloadTask is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskGeneric), downloadTaskKey.Id).LogError();

        var newDownloadTask = await _dbContext
            .PlexTvShowEpisodeData.Include(x => x.PlexTvShowEpisode)
                .ThenInclude(x => x!.TvShowSeason)
                    .ThenInclude(x => x!.TvShow)
            .Where(x =>
                x.PlexApiMediaId == downloadTask.PlexApiMediaId && x.PlexApiPartId == downloadTask.PlexApiPartId
            )
            .Select(x => new DownloadTaskTvShowEpisodeFile
            {
                Id = downloadTask.Id,
                Parent = downloadTask.Parent,
                ParentId = downloadTask.ParentId,
                Title = x.GetFileName,
                DownloadStatus = DownloadStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                FullTitle = $"{x.PlexTvShowEpisode!.FullTitle}/{x.GetFileName}",
                DataTotal = x.Size,
                PlexServerId = downloadTask.PlexServerId,
                PlexLibraryId = downloadTask.PlexLibraryId,
                PlexApiRatingKey = downloadTask.PlexApiRatingKey,
                PlexApiMediaId = downloadTask.PlexApiMediaId,
                PlexApiPartId = downloadTask.PlexApiPartId,
                FileName = x.GetFileName,
                FileLocationUrl = x.Key,
                HashId = downloadTask.HashId,
                Quality = x.VideoResolution,
                DirectoryMeta = new DownloadTaskDirectory
                {
                    DownloadRootPath = downloadTask.DirectoryMeta.DownloadRootPath,
                    DestinationRootPath = downloadTask.DirectoryMeta.DestinationRootPath,
                    MovieFolder = string.Empty,
                    TvShowFolder = x.PlexTvShowEpisode!.TvShow!.Title.SanitizeFolderName(),
                    SeasonFolder = x.PlexTvShowEpisode!.TvShowSeason!.Title.SanitizeFolderName(),
                    MusicArtistFolder = string.Empty,
                    MusicAlbumFolder = string.Empty,
                    PhotoAlbumFolder = string.Empty,
                    OtherVideoFolder = string.Empty,
                    KeepCompletedInDownloadFolder = downloadTask.DirectoryMeta.KeepCompletedInDownloadFolder,
                },
                DataReceived = 0,
                DownloadSpeed = 0,
                DirectDownloadSnapshot = null,
                DownloadClientType = downloadTask.DownloadClientType,
                FileTransferSpeed = 0,
                FileDataTransferred = 0,
                TimeRemaining = 0,
                DestinationFolderPathId = downloadTask.DestinationFolderPathId,
                SonarrIntegrationId = downloadTask.SonarrIntegrationId,
                RadarrIntegrationId = downloadTask.RadarrIntegrationId,
            })
            .FirstOrDefaultAsync(CancellationToken.None);

        if (newDownloadTask is null)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTask.ToKey(),
                DownloadStatus.SourceUnavailable,
                CancellationToken.None
            );

            await _dbContext.CreateDownloadClientLog(
                downloadTask.ToKey(),
                NotificationLevel.Information,
                DownloadStatus.SourceUnavailable,
                $"Could not find the original source media for download task \"{downloadTaskKey}\" with title \"{downloadTask.FullTitle}\""
            );

            return Result.Fail(
                "Could not find the original source media for download task \"{DownloadTaskKey}\" with title \"{DownloadTaskFullTitle}\"",
                downloadTaskKey,
                downloadTask.FullTitle
            );
        }

        return await Result.Try(async Task () =>
        {
            _dbContext.DownloadTaskTvShowEpisodeFile.Update(newDownloadTask);
            await _dbContext.SaveChangesAsync(cancellationToken);
        });
    }

    private async Task<Result> RefreshPhotoDownloadTask(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        var downloadTask = await _dbContext.DownloadTaskPhotoImageFiles.FirstOrDefaultAsync(
            x => x.Id == downloadTaskKey.Id,
            cancellationToken
        );
        if (downloadTask is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskPhotoImageFile), downloadTaskKey.Id).LogError();

        var source = await _dbContext
            .PlexPhotoData.Where(x =>
                x.PlexApiMediaId == downloadTask.PlexApiMediaId
                && x.PlexApiPartId == downloadTask.PlexApiPartId
                && x.PlexPhoto!.PlexServerId == downloadTask.PlexServerId
                && x.PlexPhoto.PlexLibraryId == downloadTask.PlexLibraryId
                && x.PlexPhoto.PlexApiRatingKey == downloadTask.PlexApiRatingKey
            )
            .Select(x => new
            {
                FileName = x.OriginalFilename.GetFileName(),
                FullTitle = x.PlexPhoto!.FullTitle,
                x.Key,
                x.Size,
                PhotoAlbumFolder = x.PlexPhoto.PlexPhotoAlbum!.Title.SanitizeFolderName(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTaskKey,
                DownloadStatus.SourceUnavailable,
                cancellationToken
            );
            await _dbContext.CreateDownloadClientLog(
                downloadTaskKey,
                NotificationLevel.Information,
                DownloadStatus.SourceUnavailable,
                $"Could not find the original source media for download task \"{downloadTaskKey}\" with title \"{downloadTask.FullTitle}\""
            );
            return Result.Fail("Could not find the original source media").LogError();
        }

        var result = await Result.Try(async Task () =>
        {
            var entry = _dbContext.Entry(downloadTask);
            entry.State = EntityState.Modified;
            entry.CurrentValues.SetValues(
                new
                {
                    Title = source.FileName,
                    FullTitle = $"{source.FullTitle}/{source.FileName}",
                    DownloadStatus = DownloadStatus.Queued,
                    CreatedAt = DateTime.UtcNow,
                    DataTotal = source.Size,
                    source.FileName,
                    FileLocationUrl = source.Key,
                    DataReceived = 0L,
                    DownloadSpeed = 0L,
                    DownloadClientType = PlexDownloadClientType.Direct,
                    FileTransferSpeed = 0L,
                    FileDataTransferred = 0L,
                    CurrentFileTransferBytesOffset = 0L,
                    Percentage = 0m,
                    TimeRemaining = 0,
                }
            );
            downloadTask.DirectDownloadSnapshot = null;
            downloadTask.DirectoryMeta.PhotoAlbumFolder = source.PhotoAlbumFolder;
            downloadTask.DirectoryMeta.MovieFolder = string.Empty;
            downloadTask.DirectoryMeta.TvShowFolder = string.Empty;
            downloadTask.DirectoryMeta.SeasonFolder = string.Empty;
            downloadTask.DirectoryMeta.MusicArtistFolder = string.Empty;
            downloadTask.DirectoryMeta.MusicAlbumFolder = string.Empty;
            downloadTask.DirectoryMeta.OtherVideoFolder = string.Empty;
            await _dbContext.SaveChangesAsync(cancellationToken);
        });
        if (result.IsFailed)
            return result.LogIfFailed();
        await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
            downloadTaskKey,
            DownloadStatus.Queued,
            cancellationToken
        );
        return Result.Ok();
    }

    private async Task<Result> RefreshMusicDownloadTask(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        var downloadTask = await _dbContext.DownloadTaskMusicTrackFiles.FirstOrDefaultAsync(
            x => x.Id == downloadTaskKey.Id,
            cancellationToken
        );
        if (downloadTask is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskMusicTrackFile), downloadTaskKey.Id).LogError();

        var source = await _dbContext
            .PlexTrackData.Where(x =>
                x.PlexApiMediaId == downloadTask.PlexApiMediaId
                && x.PlexApiPartId == downloadTask.PlexApiPartId
                && x.PlexTrack!.PlexServerId == downloadTask.PlexServerId
                && x.PlexTrack.PlexLibraryId == downloadTask.PlexLibraryId
                && x.PlexTrack.PlexApiRatingKey == downloadTask.PlexApiRatingKey
            )
            .Select(x => new
            {
                FileName = x.OriginalFilename.GetFileName(),
                FullTitle = x.PlexTrack!.FullTitle,
                x.Key,
                x.Size,
                Quality = x.VideoResolution,
                MusicArtistFolder = x.PlexTrack.PlexAlbum!.PlexArtist!.Title.SanitizeFolderName(),
                MusicAlbumFolder = x.PlexTrack.PlexAlbum.Title.SanitizeFolderName(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTaskKey,
                DownloadStatus.SourceUnavailable,
                cancellationToken
            );
            await _dbContext.CreateDownloadClientLog(
                downloadTaskKey,
                NotificationLevel.Information,
                DownloadStatus.SourceUnavailable,
                $"Could not find the original source media for download task \"{downloadTaskKey}\" with title \"{downloadTask.FullTitle}\""
            );
            return Result.Fail("Could not find the original source media").LogError();
        }

        var result = await Result.Try(async Task () =>
        {
            var entry = _dbContext.Entry(downloadTask);
            entry.State = EntityState.Modified;
            entry.CurrentValues.SetValues(
                new
                {
                    Title = source.FileName,
                    FullTitle = $"{source.FullTitle}/{source.FileName}",
                    DownloadStatus = DownloadStatus.Queued,
                    CreatedAt = DateTime.UtcNow,
                    DataTotal = source.Size,
                    source.FileName,
                    FileLocationUrl = source.Key,
                    source.Quality,
                    DataReceived = 0L,
                    DownloadSpeed = 0L,
                    DownloadClientType = PlexDownloadClientType.Direct,
                    FileTransferSpeed = 0L,
                    FileDataTransferred = 0L,
                    CurrentFileTransferBytesOffset = 0L,
                    Percentage = 0m,
                    TimeRemaining = 0,
                }
            );
            downloadTask.DirectDownloadSnapshot = null;
            downloadTask.DirectoryMeta.PhotoAlbumFolder = string.Empty;
            downloadTask.DirectoryMeta.MovieFolder = string.Empty;
            downloadTask.DirectoryMeta.TvShowFolder = string.Empty;
            downloadTask.DirectoryMeta.SeasonFolder = string.Empty;
            downloadTask.DirectoryMeta.MusicArtistFolder = source.MusicArtistFolder;
            downloadTask.DirectoryMeta.MusicAlbumFolder = source.MusicAlbumFolder;
            downloadTask.DirectoryMeta.OtherVideoFolder = string.Empty;
            await _dbContext.SaveChangesAsync(cancellationToken);
        });
        if (result.IsFailed)
            return result.LogIfFailed();
        await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
            downloadTaskKey,
            DownloadStatus.Queued,
            cancellationToken
        );
        return Result.Ok();
    }

    private async Task<Result> RefreshOtherVideoDownloadTask(
        DownloadTaskKey downloadTaskKey,
        CancellationToken cancellationToken
    )
    {
        var downloadTask = await _dbContext.DownloadTaskOtherVideoFiles.FirstOrDefaultAsync(
            x => x.Id == downloadTaskKey.Id,
            cancellationToken
        );
        if (downloadTask is null)
            return ResultExtensions.EntityNotFound(nameof(DownloadTaskOtherVideoFile), downloadTaskKey.Id).LogError();

        var source = await _dbContext
            .PlexOtherVideoData.Where(x =>
                x.PlexApiMediaId == downloadTask.PlexApiMediaId
                && x.PlexApiPartId == downloadTask.PlexApiPartId
                && x.PlexOtherVideo!.PlexServerId == downloadTask.PlexServerId
                && x.PlexOtherVideo.PlexLibraryId == downloadTask.PlexLibraryId
                && x.PlexOtherVideo.PlexApiRatingKey == downloadTask.PlexApiRatingKey
            )
            .Select(x => new
            {
                FileName = x.OriginalFilename.GetFileName(),
                FullTitle = x.PlexOtherVideo!.FullTitle,
                x.Key,
                x.Size,
                Quality = x.VideoResolution,
                OtherVideoFolder = x.PlexOtherVideo.Title.SanitizeFolderName(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (source is null)
        {
            await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
                downloadTaskKey,
                DownloadStatus.SourceUnavailable,
                cancellationToken
            );
            await _dbContext.CreateDownloadClientLog(
                downloadTaskKey,
                NotificationLevel.Information,
                DownloadStatus.SourceUnavailable,
                $"Could not find the original source media for download task \"{downloadTaskKey}\" with title \"{downloadTask.FullTitle}\""
            );
            return Result.Fail("Could not find the original source media").LogError();
        }

        var result = await Result.Try(async Task () =>
        {
            var entry = _dbContext.Entry(downloadTask);
            entry.State = EntityState.Modified;
            entry.CurrentValues.SetValues(
                new
                {
                    Title = source.FileName,
                    FullTitle = $"{source.FullTitle}/{source.FileName}",
                    DownloadStatus = DownloadStatus.Queued,
                    CreatedAt = DateTime.UtcNow,
                    DataTotal = source.Size,
                    source.FileName,
                    FileLocationUrl = source.Key,
                    source.Quality,
                    DataReceived = 0L,
                    DownloadSpeed = 0L,
                    FileTransferSpeed = 0L,
                    FileDataTransferred = 0L,
                    CurrentFileTransferBytesOffset = 0L,
                    Percentage = 0m,
                    TimeRemaining = 0,
                }
            );
            downloadTask.DirectDownloadSnapshot = null;
            downloadTask.DirectoryMeta.PhotoAlbumFolder = string.Empty;
            downloadTask.DirectoryMeta.MovieFolder = string.Empty;
            downloadTask.DirectoryMeta.TvShowFolder = string.Empty;
            downloadTask.DirectoryMeta.SeasonFolder = string.Empty;
            downloadTask.DirectoryMeta.MusicArtistFolder = string.Empty;
            downloadTask.DirectoryMeta.MusicAlbumFolder = string.Empty;
            downloadTask.DirectoryMeta.OtherVideoFolder = source.OtherVideoFolder;
            await _dbContext.SaveChangesAsync(cancellationToken);
        });
        if (result.IsFailed)
            return result.LogIfFailed();
        await _downloadTaskUpdateDispatcher.OnStatusChangedAsync(
            downloadTaskKey,
            DownloadStatus.Queued,
            cancellationToken
        );
        return Result.Ok();
    }
}
