namespace Reaparr.Application;

/// <summary>
/// Creates <see cref="DownloadTaskMovie">DownloadTaskMovies</see> from <see cref="PlexMovie">PlexMovies</see> and inserts it into Database.
/// </summary>
/// <returns>The created <see cref="DownloadTaskGeneric"/>.</returns>
public record GenerateDownloadTaskMoviesCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskMoviesCommand(CreateDownloadTasksRequest request)
    {
        Request = request;
    }

    public GenerateDownloadTaskMoviesCommand(List<DownloadMediaDTO> downloadMediaDtos)
    {
        Request = new CreateDownloadTasksRequest(downloadMediaDtos);
    }

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskMoviesCommandValidator : AbstractValidator<GenerateDownloadTaskMoviesCommand>
{
    public GenerateDownloadTaskMoviesCommandValidator()
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

public class GenerateDownloadTaskMoviesCommandHandler
    : ICommandHandler<GenerateDownloadTaskMoviesCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GenerateDownloadTaskMoviesCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GenerateDownloadTaskMoviesCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskMoviesCommand command,
        CancellationToken cancellationToken
    )
    {
        var groupedList = command.Request.DownloadMedias.MergeAndGroupList();
        var request = command.Request;
        var plexMoviesList = groupedList.FindAll(x => x.Type == PlexMediaType.Movie);
        if (!plexMoviesList.Any())
            return ResultExtensions.IsEmpty(nameof(plexMoviesList)).LogWarning();

        _log.Here()
            .Debug(
                "Creating {PlexMovieIdsCount} movie download tasks",
                plexMoviesList.SelectMany(x => x.MediaIds).ToList().Count
            );

        var allDownloadTasks = new List<DownloadTaskMovie>();
        foreach (var downloadMediaDto in plexMoviesList)
        {
            var downloadTasks = new List<DownloadTaskMovie>();

            var plexLibrary = await _dbContext
                .PlexLibraries.Include(x => x.PlexServer)
                .Include(x => x.DefaultDestination)
                .GetAsync(downloadMediaDto.PlexLibraryId, cancellationToken);
            if (plexLibrary is null)
            {
                ResultExtensions.EntityNotFound(nameof(PlexLibrary), downloadMediaDto.PlexLibraryId).LogError();
                continue;
            }

            if (plexLibrary.PlexServerId != downloadMediaDto.PlexServerId || plexLibrary.Type != PlexMediaType.Movie)
                return ResultExtensions
                    .Create400BadRequestResult("The selected Movie library does not match the requested server or media type.")
                    .LogError();

            var plexServer = plexLibrary.PlexServer!;
            var downloadRootPath = (await _dbContext.GetDownloadFolder(request.Integration)).DirectoryPath;

            var plexMovies = await _dbContext
                .PlexMovies.Where(x =>
                    downloadMediaDto.MediaIds.Contains(x.Id)
                    && x.PlexLibraryId == plexLibrary.Id
                    && x.PlexServerId == plexLibrary.PlexServerId
                )
                .IncludeAll()
                .ToListAsync(cancellationToken);

            if (plexMovies.Count != downloadMediaDto.MediaIds.Count)
                return ResultExtensions
                    .Create400BadRequestResult("Every selected Movie must belong to the requested library and server.")
                    .LogError();

            foreach (var plexMovie in plexMovies)
            {
                var downloadTaskAlreadyExists = await _dbContext
                    .DownloadTaskMovie.WhereIntegrationOwnershipMatches(request.Integration)
                    .AnyAsync(
                        x =>
                            x.PlexServerId == plexMovie.PlexServerId
                            && x.PlexApiRatingKey == plexMovie.PlexApiRatingKey,
                        cancellationToken
                    );
                if (downloadTaskAlreadyExists)
                {
                    _log.Here()
                        .Debug(
                            "Skipping duplicate movie download task for {MovieTitle} ({MovieKey})",
                            plexMovie.Title,
                            plexMovie.PlexApiRatingKey
                        );
                    continue;
                }

                var movieDownloadTask = plexMovie.MapToDownloadTask(request.Integration);

                var movieData = SelectMovieQuality(plexMovie, downloadMediaDto);
                if (movieData is null)
                {
                    _log.Here()
                        .Error(
                            "Failed to select quality for movie {MovieTitle} (ID: {MovieId})",
                            plexMovie.Title,
                            plexMovie.Id
                        );
                    continue;
                }

                // Get all parts for the selected media (multi-part movies have multiple parts with the same PlexMediaId)
                var allPartsForSelectedMedia = plexMovie
                    .MediaDataList.Where(x => x.PlexApiMediaId == movieData.PlexApiMediaId)
                    .ToList();

                // Map all parts to DownloadTaskMovieFile and add to movieDownloadTask
                movieDownloadTask.Children.AddRange(
                    allPartsForSelectedMedia.Select(x =>
                        x.MapToDownloadTask(
                            plexMovie,
                            request,
                            downloadRootPath,
                            downloadMediaDto.KeepCompletedInDownloadFolder
                        )
                    )
                );
                movieDownloadTask.Calculate();
                downloadTasks.Add(movieDownloadTask);
            }

            if (downloadTasks.Count == 0)
                continue;

            downloadTasks.SetRelationshipIds(plexServer.Id, plexLibrary.Id);
            allDownloadTasks.AddRange(downloadTasks);
        }

        if (allDownloadTasks.Count == 0)
            return Result.Ok(new DownloadTaskCreationReport { Movies = 0 });

        _dbContext.DownloadTaskMovie.AddRange(allDownloadTasks);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var logs = new List<DownloadTaskMovieFileLog>();
        foreach (var downloadTaskMovie in allDownloadTasks)
        {
            logs.AddRange(
                downloadTaskMovie.Children.Select(downloadTaskMovieFile => new DownloadTaskMovieFileLog
                {
                    Status = DownloadStatus.Queued,
                    LogLevel = NotificationLevel.Information,
                    Message = $"DownloadTask {downloadTaskMovieFile.FileName} was queued for downloading",
                    DownloadTaskFileId = downloadTaskMovieFile.Id,
                    CreatedAt = DateTime.UtcNow,
                    DownloadTaskMovieId = downloadTaskMovieFile.ParentId,
                })
            );
        }

        _dbContext.DownloadTaskMovieFileLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok(new DownloadTaskCreationReport { Movies = allDownloadTasks.Count });
    }

    /// <summary>
    /// Selects the appropriate movie quality from the available media data list.
    /// First attempts to use the requested quality, then falls back to the best available quality.
    /// </summary>
    /// <param name="plexMovie">The movie containing media data options</param>
    /// <param name="downloadMediaDto">The download request containing quality preferences</param>
    /// <returns>The selected movie media data, or null if no suitable quality is found</returns>
    private PlexMovieMediaData? SelectMovieQuality(PlexMovie plexMovie, DownloadMediaDTO downloadMediaDto)
    {
        if (!plexMovie.MediaDataList.Any())
        {
            _log.Here()
                .Warning(
                    "Movie {MovieTitle} (ID: {MovieId}) has no media data available",
                    plexMovie.Title,
                    plexMovie.Id
                );
            return null;
        }

        // Try to find the specifically requested quality
        var requestedQuality = downloadMediaDto.Qualities.FirstOrDefault(x => x.MediaId == plexMovie.Id);
        if (requestedQuality is not null)
        {
            var specificQuality = plexMovie.MediaDataList.FirstOrDefault(x => x.Id == requestedQuality.DataId);
            if (specificQuality is not null)
            {
                _log.Here()
                    .Debug(
                        "Selected requested quality {Quality} for movie {MovieTitle} (DataId: {DataId})",
                        requestedQuality.Quality,
                        plexMovie.Title,
                        requestedQuality.DataId
                    );
                return specificQuality;
            }
        }

        // Fall back to the best available quality
        var bestQuality = plexMovie.MediaDataList.PickMediaQuality();
        if (bestQuality is not null)
        {
            _log.Here()
                .Debug(
                    "Selected best available quality {Quality} for movie {MovieTitle} (DataId: {DataId})",
                    bestQuality.Quality,
                    plexMovie.Title,
                    bestQuality.Id
                );
        }
        else
        {
            _log.Here()
                .Error(
                    "No suitable quality found for movie {MovieTitle} (ID: {MovieId}) from {AvailableCount} media data options",
                    plexMovie.Title,
                    plexMovie.Id,
                    plexMovie.MediaDataList.Count
                );
        }

        return bestQuality;
    }
}
