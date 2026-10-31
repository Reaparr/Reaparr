namespace Reaparr.Application;

public record GenerateDownloadTaskTvShowsCommand : ICommand<Result<DownloadTaskCreationReport>>
{
    public GenerateDownloadTaskTvShowsCommand(CreateDownloadTasksRequest request)
    {
        Request = request;
    }

    public GenerateDownloadTaskTvShowsCommand(List<DownloadMediaDTO> downloadMediaDtos)
    {
        Request = new CreateDownloadTasksRequest(downloadMediaDtos);
    }

    public CreateDownloadTasksRequest Request { get; }
}

public class GenerateDownloadTaskTvShowsCommandValidator : AbstractValidator<GenerateDownloadTaskTvShowsCommand>
{
    public GenerateDownloadTaskTvShowsCommandValidator()
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

public class GenerateDownloadTaskTvShowsCommandHandler
    : ICommandHandler<GenerateDownloadTaskTvShowsCommand, Result<DownloadTaskCreationReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly ICommandExecutor _commandExecutor;

    public GenerateDownloadTaskTvShowsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        ICommandExecutor commandExecutor
    )
    {
        _log = log.ForContext<GenerateDownloadTaskTvShowsCommandHandler>();
        _dbContext = dbContext;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<DownloadTaskCreationReport>> ExecuteAsync(
        GenerateDownloadTaskTvShowsCommand command,
        CancellationToken cancellationToken
    )
    {
        var groupedList = command.Request.DownloadMedias.MergeAndGroupList();
        var plexTvShowList = groupedList.FindAll(x => x.Type == PlexMediaType.TvShow);
        if (!plexTvShowList.Any())
            return ResultExtensions.IsEmpty(nameof(plexTvShowList)).LogWarning();

        _log.Here()
            .Debug(
                "Creating {PlexTvShowIdsCount} TvShow download tasks",
                plexTvShowList.SelectMany(x => x.MediaIds).Distinct().Count()
            );

        var report = new DownloadTaskCreationReport();

        var request = command.Request;

        foreach (var downloadMediaDto in plexTvShowList)
        {
            var mediaIds = downloadMediaDto.MediaIds.Distinct().ToList();
            var plexTvShows = await _dbContext
                .PlexTvShows.Include(x => x.Seasons)
                .Where(x => mediaIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var seasonsIds = new List<DownloadMediaDTO>();
            var tvShowsToInsert = new List<DownloadTaskTvShow>();

            foreach (var tvShow in plexTvShows)
            {
                // Check if the tvShowDownloadTask has already been created
                var downloadTaskTvShow = await _dbContext
                    .DownloadTaskTvShow.AsTracking()
                    .WhereIntegrationOwnershipMatches(request.Integration)
                    .Include(x => x.Children)
                    .SingleOrDefaultAsync(
                        x => x.PlexServerId == tvShow.PlexServerId && x.PlexApiRatingKey == tvShow.PlexApiRatingKey,
                        cancellationToken
                    );

                if (downloadTaskTvShow is null)
                {
                    downloadTaskTvShow = tvShow.MapToDownloadTask(request.Integration);

                    tvShowsToInsert.Add(downloadTaskTvShow);
                    seasonsIds.Add(
                        new DownloadMediaDTO
                        {
                            MediaIds = tvShow.Seasons.Select(x => x.Id).ToList(),
                            PlexLibraryId = tvShow.PlexLibraryId,
                            PlexServerId = tvShow.PlexServerId,
                            Type = PlexMediaType.Season,
                            SelectionKind = null,
                            Qualities = [],
                            KeepCompletedInDownloadFolder = downloadMediaDto.KeepCompletedInDownloadFolder,
                        }
                    );
                }
            }

            // Insert the tvShowDownloadTask into the database
            report = report with
            {
                TvShows = report.TvShows + tvShowsToInsert.Count,
            };
            _dbContext.DownloadTaskTvShow.AddRange(tvShowsToInsert);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (seasonsIds.Count == 0)
                continue;

            // Create seasons downloadTasks
            var seasonsResult = await _commandExecutor.Send(
                new GenerateDownloadTaskTvShowSeasonsCommand(
                    new CreateDownloadTasksRequest(
                        seasonsIds,
                        request.DestinationFolderPathId,
                        request.CustomDestinationFolderPath,
                        request.Integration
                    )
                ),
                cancellationToken
            );
            if (seasonsResult.IsFailed)
                return seasonsResult.LogError();

            report += seasonsResult.Value;
        }

        return Result.Ok(report);
    }
}
