namespace Reaparr.Application;

public record RefreshPlexTvShowLibraryCommand(
    InsertMediaMetaDataCommandResponse LibraryMetadata,
    bool ForceMediaRefresh = false
) : ICommand<Result<PlexLibrary>>;

public class RefreshPlexTvShowLibraryCommandValidator : AbstractValidator<RefreshPlexTvShowLibraryCommand>
{
    public RefreshPlexTvShowLibraryCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary.Type)
            .Equal(PlexMediaType.TvShow)
            .WithMessage("PlexLibrary must be of type TvShow to continue with the refresh process.");
        RuleFor(x => x.LibraryMetadata.PlexLibrary.TvShows).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary.TvShows.Count)
            .GreaterThan(0)
            .WithMessage("PlexLibrary must contain TV shows to continue with the refresh process.");
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
    }
}

public class RefreshPlexTvShowLibraryCommandHandler
    : ICommandHandler<RefreshPlexTvShowLibraryCommand, Result<PlexLibrary>>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IReaparrDbContext _dbContext;
    private readonly ILibrarySyncProgressStore _librarySyncProgressStore;

    public RefreshPlexTvShowLibraryCommandHandler(
        ILogger log,
        ICommandExecutor commandExecutor,
        IReaparrDbContext dbContext,
        ILibrarySyncProgressStore librarySyncProgressStore
    )
    {
        _log = log.ForContext<RefreshPlexTvShowLibraryCommandHandler>();
        _commandExecutor = commandExecutor;
        _dbContext = dbContext;
        _librarySyncProgressStore = librarySyncProgressStore;
    }

    public async Task<Result<PlexLibrary>> ExecuteAsync(
        RefreshPlexTvShowLibraryCommand command,
        CancellationToken cancellationToken
    )
    {
        var plexLibrary = command.LibraryMetadata.PlexLibrary;
        var plexLibraryId = plexLibrary.Id;

        var stopwatch = Stopwatch.StartNew();

        // Phase 2 of 5: Season data was retrieved successfully.
        var rawSeasonDataResult = await Result.Try(() =>
            _commandExecutor.Send(new GetAllMediaSeasonsCommand(plexLibrary), cancellationToken)
        );

        if (rawSeasonDataResult.IsCancelled)
            return rawSeasonDataResult.ToResult();

        if (rawSeasonDataResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(
                plexLibraryId,
                rawSeasonDataResult.ToResult(),
                cancellationToken
            );
            return rawSeasonDataResult.ToResult();
        }

        // Phase 3 of 5: Episode data was retrieved successfully.
        var rawEpisodesDataResult = await Result.Try(() =>
            _commandExecutor.Send(new GetAllMediaEpisodesCommand(plexLibrary), cancellationToken)
        );
        if (rawEpisodesDataResult.IsCancelled)
            return rawEpisodesDataResult.ToResult();

        if (rawEpisodesDataResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(
                plexLibraryId,
                rawEpisodesDataResult.ToResult(),
                cancellationToken
            );
            return rawEpisodesDataResult.ToResult();
        }

        // Phase 4 of 5: PlexLibrary media data was parsed successfully.
        _log.Here()
            .Debug(
                "Finished retrieving all media for library {PlexLibraryName} in {ElapsedTime}",
                plexLibrary.Title,
                stopwatch.Elapsed.ToFormattedString()
            );

        stopwatch.Restart();

        var rawSeasonData = rawSeasonDataResult.Value;
        var rawEpisodesData = rawEpisodesDataResult.Value;

        _log.Here()
            .Information("Merging all data received from PlexApi for library {PlexLibraryName}", plexLibrary.Name);
        BuildTvShowTree(plexLibrary, plexLibrary.TvShows, rawSeasonData, rawEpisodesData);

        // Write all the tv-show, season and episode to the database
        var syncResult = await Result.Try(() =>
            _commandExecutor.Send(
                new SyncPlexTvShowsCommand(command.LibraryMetadata, command.ForceMediaRefresh),
                cancellationToken
            )
        );
        if (syncResult.IsCancelled)
            return syncResult.ToResult();

        if (syncResult.IsFailed)
        {
            await _librarySyncProgressStore.UpdateErrorAsync(plexLibraryId, syncResult.ToResult(), cancellationToken);
            return syncResult.ToResult().LogError();
        }

        var optimizationResult = await _commandExecutor.Send(
            new ScheduleOptimizeDatabaseJobCommand
            {
                ChangedItemCount = new List<int>
                {
                    syncResult.Value.CreatedTvShows,
                    syncResult.Value.UpdatedTvShows,
                    syncResult.Value.DeletedTvShows,
                    syncResult.Value.CreatedSeasons,
                    syncResult.Value.UpdatedSeasons,
                    syncResult.Value.DeletedSeasons,
                    syncResult.Value.CreatedEpisodes,
                    syncResult.Value.UpdatedEpisodes,
                    syncResult.Value.DeletedEpisodes,
                }.Sum(),
            },
            cancellationToken
        );

        optimizationResult.LogIfFailed();

        _log.Here()
            .Debug(
                "Finished updating all media in the database for library {PlexLibraryName} in {Elapsed}",
                plexLibrary.Title,
                stopwatch.Elapsed.ToFormattedString()
            );

        // This is updated in the BuildTvShowTree method
        var totalTvShows = plexLibrary.TvShows.Count;
        var totalSeasons = plexLibrary.TvShows.Sum(x => x.ChildCount);
        var totalEpisodes = plexLibrary.TvShows.Sum(x => x.GrandChildCount);
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.TvShow,
                Received = totalTvShows,
                Total = totalTvShows,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Season,
                Received = totalSeasons,
                Total = totalSeasons,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );
        await _librarySyncProgressStore.UpdateItemAsync(
            plexLibraryId,
            new LibraryProgressItem
            {
                MediaType = PlexMediaType.Episode,
                Received = totalEpisodes,
                Total = totalEpisodes,
                TimeRemaining = TimeSpan.Zero,
            },
            cancellationToken
        );

        _log.Here()
            .Information(
                "Successfully refreshed library {PlexLibraryName} with id: {PlexLibraryId}",
                plexLibrary.Title,
                plexLibrary.Id
            );

        var rebuildResult = await _commandExecutor.Send(new QueueMediaOverviewRebuildCommand(), cancellationToken);
        rebuildResult.LogIfFailed();

        // Refresh the PlexLibrary from the database to ensure we have the latest data
        var plexLibraryDb = await _dbContext.PlexLibraries.GetAsync(plexLibraryId, cancellationToken);
        return plexLibraryDb is null
            ? ResultExtensions.EntityNotFound(nameof(PlexLibrary), plexLibraryId)
            : Result.Ok(plexLibraryDb);
    }

    private void BuildTvShowTree(
        PlexLibrary plexLibrary,
        ICollection<PlexTvShow> rawTvShowData,
        ICollection<PlexTvShowSeason> rawSeasonData,
        ICollection<PlexTvShowEpisode> rawEpisodesData
    )
    {
        var (validSeasons, validEpisodes) = Filter(rawSeasonData, rawEpisodesData, plexLibrary);

        // Group seasons and episodes by parent key upfront
        var seasonsByTvShowKey = validSeasons.GroupBy(x => x.ParentGuid!).ToDictionary(g => g.Key, g => g.ToList());
        var episodesBySeasonKey = validEpisodes.GroupBy(x => x.ParentGuid!).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var plexTvShow in rawTvShowData)
        {
            plexTvShow.PlexLibraryId = plexLibrary.Id;
            plexTvShow.PlexServerId = plexLibrary.PlexServerId;

            // Retrieve and assign seasons for this TV show
            if (seasonsByTvShowKey.TryGetValue(plexTvShow.Guid, out var seasons))
            {
                plexTvShow.Seasons = seasons;
                plexTvShow.ChildCount = seasons.Count;

                // Remove seasons that have been assigned
                seasonsByTvShowKey.Remove(plexTvShow.Guid);
            }

            foreach (var plexTvShowSeason in plexTvShow.Seasons)
            {
                plexTvShowSeason.PlexLibraryId = plexLibrary.Id;
                plexTvShowSeason.PlexServerId = plexLibrary.PlexServerId;
                plexTvShowSeason.TvShow = plexTvShow;

                // Retrieve and assign episodes for this season
                if (!episodesBySeasonKey.TryGetValue(plexTvShowSeason.Guid, out var episodes))
                    continue;

                // Set library ID in each episode
                episodes.ForEach(
                    (x) =>
                    {
                        x.PlexLibraryId = plexLibrary.Id;
                        x.PlexServerId = plexLibrary.PlexServerId;
                    }
                );

                plexTvShowSeason.Episodes = episodes;
                plexTvShowSeason.ChildCount = episodes.Count;

                if (plexTvShowSeason.SeasonNumber < 0)
                {
                    var expectedSeasonNumbers = episodes
                        .Where(x => x.ExpectedSeasonNumber >= 0)
                        .Select(x => x.ExpectedSeasonNumber)
                        .Distinct()
                        .ToList();
                    if (expectedSeasonNumbers.Count == 1)
                        plexTvShowSeason.SeasonNumber = expectedSeasonNumbers[0];
                    else if (expectedSeasonNumbers.Count > 1)
                        _log.Here()
                            .Warning(
                                "Season {PlexApiRatingKey} in library {PlexLibraryId} has conflicting episode parent indexes: {ParentIndexes}",
                                plexTvShowSeason.PlexApiRatingKey,
                                plexLibrary.Id,
                                expectedSeasonNumbers
                            );
                }

                // Remove episodes that have been assigned
                episodesBySeasonKey.Remove(plexTvShowSeason.Guid);

                // Set the season's year based on the first episode's year
                if (plexTvShowSeason.Year == 0 && episodes.Any())
                    plexTvShowSeason.Year = episodes.First().Year;

                plexTvShowSeason.MediaSize = episodes.Sum(x => x.MediaSize);
                plexTvShowSeason.Duration = episodes.Sum(x => x.Duration);
            }

            plexTvShow.MediaSize = plexTvShow.Seasons.Sum(x => x.MediaSize);
            plexTvShow.Duration = plexTvShow.Seasons.Sum(x => x.Duration);
            plexTvShow.GrandChildCount = plexTvShow.Seasons.Sum(x => x.ChildCount);
        }

        var unresolvedSeasonCount = rawTvShowData.SelectMany(x => x.Seasons).Count(x => x.SeasonNumber < 0);
        var unresolvedEpisodeCount = rawTvShowData
            .SelectMany(x => x.Seasons)
            .SelectMany(x => x.Episodes)
            .Count(x => x.EpisodeNumber < 0);
        if (unresolvedSeasonCount > 0 || unresolvedEpisodeCount > 0)
            _log.Here()
                .Warning(
                    "Library {PlexLibraryId} contains {UnresolvedSeasonCount} unresolved seasons and {UnresolvedEpisodeCount} unresolved episodes",
                    plexLibrary.Id,
                    unresolvedSeasonCount,
                    unresolvedEpisodeCount
                );
    }

    private (List<PlexTvShowSeason> validSeasons, List<PlexTvShowEpisode> validEpisodes) Filter(
        ICollection<PlexTvShowSeason> rawSeasonData,
        ICollection<PlexTvShowEpisode> rawEpisodesData,
        PlexLibrary library
    )
    {
        var validSeasons = new List<PlexTvShowSeason>();
        var inValidSeasons = new List<PlexTvShowSeason>();

        var validEpisodes = new List<PlexTvShowEpisode>();
        var inValidEpisodes = new List<PlexTvShowEpisode>();

        foreach (var plexTvShowSeason in rawSeasonData)
        {
            if (plexTvShowSeason.ParentGuid != null)
            {
                validSeasons.Add(plexTvShowSeason);
                continue;
            }

            inValidSeasons.Add(plexTvShowSeason);
        }

        foreach (var episode in rawEpisodesData)
        {
            if (episode.ParentGuid != null)
            {
                validEpisodes.Add(episode);
                continue;
            }

            inValidEpisodes.Add(episode);
        }

        // Log invalid seasons and episodes
        if (inValidSeasons.Any())
        {
            _log.Here()
                .Warning(
                    "Found {Count} invalid seasons which are missing a ParentGUID in library {PlexLibraryName} with id: {PlexLibraryId}",
                    inValidSeasons.Count,
                    library.Title,
                    library.Id
                );
        }

        if (inValidEpisodes.Any())
        {
            _log.Here()
                .Warning(
                    "Found {Count} invalid episodes which are missing a ParentGUID in library {PlexLibraryName} with id: {PlexLibraryId}",
                    inValidEpisodes.Count,
                    library.Title,
                    library.Id
                );
        }

        return (validSeasons, validEpisodes);
    }
}
