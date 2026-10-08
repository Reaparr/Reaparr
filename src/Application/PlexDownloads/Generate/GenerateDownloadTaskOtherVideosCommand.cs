namespace Reaparr.Application;

public record GenerateDownloadTaskOtherVideosCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskOtherVideosCommand(CreateDownloadTasksRequest request) => Request = request;

    public GenerateDownloadTaskOtherVideosCommand(List<DownloadMediaDTO> downloadMediaDtos)
        : this(new CreateDownloadTasksRequest(downloadMediaDtos)) { }

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskOtherVideosCommandValidator : AbstractValidator<GenerateDownloadTaskOtherVideosCommand>
{
    public GenerateDownloadTaskOtherVideosCommandValidator()
    {
        RuleFor(x => x.Request).NotNull().DependentRules(() =>
        {
            RuleFor(x => x.Request.DownloadMedias).NotNull().NotEmpty();
            RuleForEach(x => x.Request.DownloadMedias).NotNull().SetValidator(new DownloadMediaDTOValidator());
        });
    }
}

public class GenerateDownloadTaskOtherVideosCommandHandler
    : ICommandHandler<GenerateDownloadTaskOtherVideosCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GenerateDownloadTaskOtherVideosCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GenerateDownloadTaskOtherVideosCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskOtherVideosCommand command,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(GenerateDownloadTaskOtherVideosCommand)).LogWarning();

        var request = command.Request;
        var selections = request.DownloadMedias.MergeAndGroupList().FindAll(x => x.Type == PlexMediaType.OtherVideos);
        if (selections.Count == 0)
            return ResultExtensions.IsEmpty(nameof(selections)).LogWarning();
        if (request.Integration is not null)
            return ResultExtensions.Create400BadRequestResult(
                "Other Video downloads do not support Sonarr or Radarr integrations.").LogError();

        _log.Here().Debug("Creating {MediaCount} Other Video download tasks", selections.Sum(x => x.MediaIds.Count));
        var allDownloadTasks = new List<DownloadTaskOtherVideo>();
        foreach (var selection in selections)
        {
            var library = await _dbContext.PlexLibraries.Include(x => x.PlexServer)
                .GetAsync(selection.PlexLibraryId, cancellationToken);
            if (library is null || library.PlexServerId != selection.PlexServerId || library.Type != PlexMediaType.OtherVideos)
                return ResultExtensions.Create400BadRequestResult(
                    "The selected library does not match the requested server or media type.").LogError();

            var videos = await _dbContext.PlexOtherVideos
                .Where(x => selection.MediaIds.Contains(x.Id) && x.PlexLibraryId == library.Id
                    && x.PlexServerId == library.PlexServerId)
                .Include(x => x.MediaDataList).ToListAsync(cancellationToken);
            if (videos.Count != selection.MediaIds.Count)
                return ResultExtensions.Create400BadRequestResult(
                    "Every selected Other Video must belong to the requested library and server.").LogError();

            var downloadRootPath = (await _dbContext.GetDownloadFolder(request.Integration)).DirectoryPath;
            var downloadTasks = new List<DownloadTaskOtherVideo>();
            foreach (var video in videos)
            {
                var exists = await _dbContext.DownloadTaskOtherVideos.WhereIntegrationOwnershipMatches(request.Integration)
                    .AnyAsync(x => x.PlexServerId == video.PlexServerId
                        && x.PlexApiRatingKey == video.PlexApiRatingKey, cancellationToken);
                if (exists)
                {
                    _log.Here().Debug("Skipping duplicate Other Video download task for {Title} ({RatingKey})",
                        video.Title, video.PlexApiRatingKey);
                    continue;
                }

                var requestedQuality = selection.Qualities.FirstOrDefault(x => x.MediaId == video.Id);
                var selectedData = requestedQuality is null ? null
                    : video.MediaDataList.FirstOrDefault(x => x.Id == requestedQuality.DataId);
                selectedData ??= video.MediaDataList.PickMediaQuality();
                if (selectedData is null)
                {
                    _log.Here().Warning("Other Video {Title} (ID: {Id}) has no suitable media data", video.Title, video.Id);
                    continue;
                }

                var parts = video.MediaDataList.Where(x => x.PlexApiMediaId == selectedData.PlexApiMediaId).ToList();
                if (parts.Any(x => x.PlexLibraryId != library.Id || x.PlexServerId != library.PlexServerId))
                    return ResultExtensions.Create400BadRequestResult("The selected original ownership is invalid.").LogError();

                var task = video.MapToDownloadTask(request.Integration);
                task.Children.AddRange(parts.Select(x => x.MapToDownloadTask(
                    video, request, downloadRootPath, selection.KeepCompletedInDownloadFolder)));
                task.Calculate();
                downloadTasks.Add(task);
            }

            downloadTasks.SetRelationshipIds(library.PlexServerId, library.Id);
            allDownloadTasks.AddRange(downloadTasks);
        }

        if (allDownloadTasks.Count == 0)
            return Result.Ok(new DownloadTaskCreationReport { OtherVideos = 0 });

        _dbContext.DownloadTaskOtherVideos.AddRange(allDownloadTasks);
        await _dbContext.SaveChangesAsync(cancellationToken);
        var logs = allDownloadTasks.SelectMany(task => task.Children.Select(file => new DownloadTaskOtherVideoFileLog
        {
            DownloadTaskFileId = file.Id,
            DownloadTaskOtherVideoId = file.ParentId,
            CreatedAt = DateTime.UtcNow,
            Status = DownloadStatus.Queued,
            LogLevel = NotificationLevel.Information,
            Message = $"DownloadTask {file.FileName} was queued for downloading",
        })).ToList();
        _dbContext.DownloadTaskOtherVideoFileLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok(new DownloadTaskCreationReport { OtherVideos = allDownloadTasks.Count });
    }
}
