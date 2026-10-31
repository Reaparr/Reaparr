namespace Reaparr.Application;

public record GenerateDownloadTaskTvShowEpisodesCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskTvShowEpisodesCommand(CreateDownloadTasksRequest request)
    {
        Request = request;
    }

    public GenerateDownloadTaskTvShowEpisodesCommand(List<DownloadMediaDTO> downloadMediaDtos)
    {
        Request = new CreateDownloadTasksRequest(downloadMediaDtos);
    }

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskTvShowEpisodesCommandValidator
    : AbstractValidator<GenerateDownloadTaskTvShowEpisodesCommand>
{
    public GenerateDownloadTaskTvShowEpisodesCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Request.DownloadMedias).NotNull();
                RuleFor(x => x.Request.DownloadMedias).NotEmpty();
                RuleForEach(x => x.Request.DownloadMedias).SetValidator(new DownloadMediaDTOValidator());
            });
    }
}

public class GenerateDownloadTaskTvShowEpisodesCommandHandler
    : ICommandHandler<GenerateDownloadTaskTvShowEpisodesCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    private readonly List<DownloadTaskTvShow> _tvShowDownloads = [];

    public GenerateDownloadTaskTvShowEpisodesCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GenerateDownloadTaskTvShowEpisodesCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskTvShowEpisodesCommand command,
        CancellationToken ct
    )
    {
        var request = command.Request;
        var downloadRootPath = (await _dbContext.GetDownloadFolder(request.Integration)).DirectoryPath;
        var groupedList = command.Request.DownloadMedias.MergeAndGroupList();
        var downloadMediaList = groupedList.FindAll(x => x.Type == PlexMediaType.Episode);
        var episodeIds = downloadMediaList.SelectMany(x => x.MediaIds).Distinct().ToList();

        if (!downloadMediaList.Any())
            return ResultExtensions.IsEmpty(nameof(downloadMediaList)).LogWarning();

        _log.Here().Debug("Processing {PlexEpisodeIdsCount} episodes download tasks", episodeIds.Count);

        // Get all unique library IDs to optimize database queries
        var libraryIds = groupedList.Select(x => x.PlexLibraryId).Distinct().ToList();

        // Preload all required libraries into a dictionary
        var plexLibraries = await _dbContext
            .PlexLibraries.Include(x => x.PlexServer)
            .Include(x => x.DefaultDestination)
            .Where(x => libraryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        // Validate that all required libraries exist
        var missingLibraryIds = libraryIds.Except(plexLibraries.Keys).ToList();
        if (missingLibraryIds.Any())
        {
            _log.Here()
                .Warning("Missing libraries with IDs: {MissingLibraryIds}", string.Join(", ", missingLibraryIds));
            return Result.Fail("Missing libraries with IDs: {Join}", string.Join(", ", missingLibraryIds)).LogError();
        }

        var plexEpisodes = await _dbContext
            .PlexTvShowEpisodes.AsTracking()
            .Include(x => x.TvShow)
            .Include(x => x.TvShowSeason)
            .Include(x => x.MediaDataList)
            .Where(x => episodeIds.Contains(x.Id))
            .ToListAsync(ct);

        if (!plexEpisodes.Any())
        {
            _log.Here().Warning("No episodes found for media IDs: {MediaIds}", string.Join(", ", episodeIds));
        }

        var downloadTasks = new List<DownloadTaskTvShowEpisodeFile>();
        foreach (var tvShowEpisode in plexEpisodes)
        {
            var plexTvShow = tvShowEpisode.TvShow!;
            var plexSeason = tvShowEpisode.TvShowSeason!;

            _log.Here()
                .Debug(
                    "Processing episode \"{EpisodeTitle}\" with key: {EpisodeKey} from season \"{SeasonTitle}\" with key: {SeasonKey} of show \"{TvShowTitle}\" with key: {ShowKey}",
                    tvShowEpisode.Title,
                    tvShowEpisode.PlexApiRatingKey,
                    plexSeason.Title,
                    plexSeason.PlexApiRatingKey,
                    plexTvShow.Title,
                    plexTvShow.PlexApiRatingKey
                );

            // Get or create Tv-show download tasks
            var downloadTaskTvShow = await GetOrCreateTvShowDownloadTaskAsync(plexTvShow, request.Integration, ct);
            if (downloadTaskTvShow is null)
            {
                return Result.Fail("Failed to create or retrieve TV Show download task for {Title}", plexTvShow.Title);
            }

            // Get or create season download task
            var downloadTaskTvShowSeason = GetOrCreateSeasonDownloadTask(plexSeason, downloadTaskTvShow);

            // Get or create episode download task
            var episodeDownloadTask = downloadTaskTvShowSeason
                .Children.AsQueryable()
                .WhereIntegrationOwnershipMatches(request.Integration)
                .FirstOrDefault(x => x.PlexApiRatingKey == tvShowEpisode.PlexApiRatingKey);
            if (episodeDownloadTask is null)
            {
                _log.Here()
                    .Debug(
                        "Creating new episode download task for episode {EpisodeKey}",
                        tvShowEpisode.PlexApiRatingKey
                    );
                episodeDownloadTask = tvShowEpisode.MapToDownloadTask(request.Integration);
                episodeDownloadTask.ParentId = downloadTaskTvShowSeason.Id;
                downloadTaskTvShowSeason.Children.Add(episodeDownloadTask);
                _dbContext.DownloadTaskTvShowEpisode.Add(episodeDownloadTask);
            }
            else
            {
                _log.Here()
                    .Debug(
                        "Found existing episode download task for episode {EpisodeKey} with ID {EpisodeId}",
                        tvShowEpisode.PlexApiRatingKey,
                        episodeDownloadTask.Id
                    );
            }

            // Find the download media DTO that contains this episode
            var downloadMediaDto = downloadMediaList.FirstOrDefault(x => x.MediaIds.Contains(tvShowEpisode.Id));
            if (downloadMediaDto is null)
            {
                _log.Here()
                    .Warning("No download media DTO found for episode {EpisodeKey}", tvShowEpisode.PlexApiRatingKey);
                continue;
            }

            // Process episode media data
            var processResult = ProcessEpisodeMediaData(
                tvShowEpisode,
                episodeDownloadTask,
                downloadMediaDto,
                request,
                downloadRootPath
            );
            if (processResult.IsFailed)
            {
                processResult.LogError();
                continue;
            }

            downloadTasks.AddRange(processResult.Value);
        }

        var saveResult = await Result.Try(() => _dbContext.SaveChangesAsync(ct));
        if (saveResult.IsFailed)
        {
            return saveResult.LogError();
        }

        var logs = new List<DownloadTaskTvShowEpisodeFileLog>();

        logs.AddRange(
            downloadTasks.Select(downloadTaskTvShowEpisodeFile => new DownloadTaskTvShowEpisodeFileLog
            {
                Status = DownloadStatus.Queued,
                LogLevel = NotificationLevel.Information,
                Message = $"DownloadTask {downloadTaskTvShowEpisodeFile.FileName} was queued for downloading",
                DownloadTaskFileId = downloadTaskTvShowEpisodeFile.Id,
                DownloadTaskTvShowEpisodeId = downloadTaskTvShowEpisodeFile.ParentId,
                DownloadTaskTvShowSeasonId =
                    downloadTaskTvShowEpisodeFile.Parent?.Parent?.Id ?? throw new ArgumentNullException(),
                DownloadTaskTvShowId =
                    downloadTaskTvShowEpisodeFile.Parent?.Parent?.Parent?.Id ?? throw new ArgumentNullException(),
                CreatedAt = DateTime.UtcNow,
            })
        );

        await _dbContext.CreateDownloadClientLogs(logs, ct);

        return Result.Ok(
            new DownloadTaskCreationReport { Episodes = downloadTasks.DistinctBy(x => x.ParentId).Count() }
        );
    }

    private async Task<DownloadTaskTvShow?> GetOrCreateTvShowDownloadTaskAsync(
        PlexTvShow plexTvShow,
        IntegrationIdentity? integration,
        CancellationToken ct
    )
    {
        // Check if the tvShowDownloadTask has already been created this run
        var downloadTaskTvShow = _tvShowDownloads
            .AsQueryable()
            .WhereIntegrationOwnershipMatches(integration)
            .FirstOrDefault(x => x.PlexApiRatingKey == plexTvShow.PlexApiRatingKey);

        // Check if the tvShowDownloadTask has already been created in the database
        if (downloadTaskTvShow is null)
        {
            downloadTaskTvShow = await _dbContext
                .DownloadTaskTvShow.AsTracking()
                .WhereIntegrationOwnershipMatches(integration)
                .IncludeAll()
                .SingleOrDefaultAsync(
                    x => x.PlexServerId == plexTvShow.PlexServerId && x.PlexApiRatingKey == plexTvShow.PlexApiRatingKey,
                    ct
                );
            if (downloadTaskTvShow is not null)
                _tvShowDownloads.Add(downloadTaskTvShow);
        }

        // Create a new TV Show download task if none exists
        if (downloadTaskTvShow is null)
        {
            downloadTaskTvShow = plexTvShow.MapToDownloadTask(integration);
            _tvShowDownloads.Add(downloadTaskTvShow);
            _dbContext.DownloadTaskTvShow.Add(downloadTaskTvShow);
        }

        return downloadTaskTvShow;
    }

    private DownloadTaskTvShowSeason GetOrCreateSeasonDownloadTask(
        PlexTvShowSeason plexSeason,
        DownloadTaskTvShow downloadTaskTvShow
    )
    {
        // Check if the SeasonDownloadTask has already been created
        var integration = (
            downloadTaskTvShow.SonarrIntegrationId,
            downloadTaskTvShow.RadarrIntegrationId
        ).ToIntegrationIdentity();
        var downloadTaskTvShowSeason = downloadTaskTvShow
            .Children.AsQueryable()
            .WhereIntegrationOwnershipMatches(integration)
            .FirstOrDefault(x => x.PlexApiRatingKey == plexSeason.PlexApiRatingKey);

        if (downloadTaskTvShowSeason is null)
        {
            downloadTaskTvShowSeason = plexSeason.MapToDownloadTask(integration);
            downloadTaskTvShowSeason.ParentId = downloadTaskTvShow.Id;
            downloadTaskTvShow.Children.Add(downloadTaskTvShowSeason);
            _dbContext.DownloadTaskTvShowSeason.Add(downloadTaskTvShowSeason);
        }

        return downloadTaskTvShowSeason;
    }

    private Result<List<DownloadTaskTvShowEpisodeFile>> ProcessEpisodeMediaData(
        PlexTvShowEpisode tvShowEpisode,
        DownloadTaskTvShowEpisode episodeDownloadTask,
        DownloadMediaDTO downloadMediaDto,
        CreateDownloadTasksRequest request,
        string downloadRootPath
    )
    {
        var episodeData = SelectEpisodeQuality(tvShowEpisode, downloadMediaDto);
        if (episodeData is null)
        {
            _log.Here().Error("Failed to select quality for episode {EpisodeKey}", tvShowEpisode.PlexApiRatingKey);
            return Result.Fail(
                "No suitable quality found for episode {PlexApiRatingKey} ({Title})",
                tvShowEpisode.PlexApiRatingKey,
                tvShowEpisode.Title
            );
        }

        // A selected quality may contain multiple physical parts; every part must be queued.
        var downloadFiles = tvShowEpisode
            .MediaDataList.Where(x => x.PlexApiMediaId == episodeData.PlexApiMediaId)
            .Where(mediaData =>
                !episodeDownloadTask.Children.Any(existing =>
                    existing.PlexApiMediaId == mediaData.PlexApiMediaId
                    && existing.PlexApiPartId == mediaData.PlexApiPartId
                )
            )
            .Select(x =>
                x.MapToDownloadTask(
                    tvShowEpisode,
                    request,
                    downloadRootPath,
                    downloadMediaDto.KeepCompletedInDownloadFolder
                )
            )
            .ToList();

        episodeDownloadTask.Children.AddRange(downloadFiles);
        _dbContext.DownloadTaskTvShowEpisodeFile.AddRange(downloadFiles);

        return Result.Ok(downloadFiles);
    }

    /// <summary>
    /// Selects the appropriate episode quality from the available media data list.
    /// First attempts to use the requested quality, then falls back to the best available quality.
    /// </summary>
    /// <param name="tvShowEpisode">The episode containing media data options</param>
    /// <param name="downloadMediaDto">The download request containing quality preferences</param>
    /// <returns>The selected episode media data, or null if no suitable quality is found</returns>
    private PlexTvShowEpisodeMediaData? SelectEpisodeQuality(
        PlexTvShowEpisode tvShowEpisode,
        DownloadMediaDTO downloadMediaDto
    )
    {
        if (!tvShowEpisode.MediaDataList.Any())
        {
            _log.Here().Warning("Episode {EpisodeKey} has no media data available", tvShowEpisode.PlexApiRatingKey);
            return null;
        }

        // Try to find the specifically requested quality
        var requestedQuality = downloadMediaDto.Qualities.FirstOrDefault(x => x.MediaId == tvShowEpisode.Id);
        if (requestedQuality is not null)
        {
            var specificQuality = tvShowEpisode.MediaDataList.FirstOrDefault(x => x.Id == requestedQuality.DataId);
            if (specificQuality is not null)
            {
                _log.Here()
                    .Debug(
                        "Selected requested quality {Quality} for episode {EpisodeKey} ({EpisodeTitle}) (DataId: {DataId})",
                        requestedQuality.VideoQuality,
                        tvShowEpisode.PlexApiRatingKey,
                        tvShowEpisode.Title,
                        requestedQuality.DataId
                    );
                return specificQuality;
            }
        }

        // Fall back to the best available quality
        var bestQuality = tvShowEpisode.MediaDataList.PickMediaQuality();
        if (bestQuality is not null)
        {
            _log.Here()
                .Debug(
                    "Selected best available quality {Quality} for episode {EpisodeKey} ({EpisodeTitle}) (DataId: {DataId})",
                    bestQuality.VideoQuality,
                    tvShowEpisode.PlexApiRatingKey,
                    tvShowEpisode.Title,
                    bestQuality.Id
                );
        }
        else
        {
            _log.Here()
                .Error(
                    "No suitable quality found for episode {EpisodeKey} ({EpisodeTitle}) (ID: {EpisodeId}) from {AvailableCount} media data options",
                    tvShowEpisode.PlexApiRatingKey,
                    tvShowEpisode.Title,
                    tvShowEpisode.Id,
                    tvShowEpisode.MediaDataList.Count
                );
        }

        return bestQuality;
    }
}
