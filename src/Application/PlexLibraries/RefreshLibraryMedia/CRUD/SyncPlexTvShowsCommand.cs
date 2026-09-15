using EFCore.BulkExtensions;

namespace Reaparr.Application;

/// <summary>
/// Incrementally syncs the PlexTvShows of a PlexLibrary.
/// In addition to syncing the PlexTvShows, also syncs the related entities such as actors, genres and countries.
/// </summary>
public record SyncPlexTvShowsCommand(InsertMediaMetaDataCommandResponse LibraryMetadata, bool ForceMediaRefresh = false)
    : ICommand<Result<BulkInsertTvShowsRapport>>;

public class SyncPlexTvShowsCommandValidator : AbstractValidator<SyncPlexTvShowsCommand>
{
    public SyncPlexTvShowsCommandValidator(ILogger log)
    {
        var stopWatch = Stopwatch.StartNew();
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);

        RuleFor(x => x.LibraryMetadata.PlexLibrary.TvShows).NotNull();

        RuleForEach(x => x.LibraryMetadata.PlexLibrary.TvShows)
            .ChildRules(tvShow =>
            {
                tvShow.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0);
                tvShow.RuleFor(y => y.PlexLibraryId).GreaterThan(0);
                tvShow.RuleFor(y => y.PlexServerId).GreaterThan(0);
                tvShow.RuleForEach(y => y.Seasons).NotNull();
                tvShow.RuleForEach(a => a.Seasons).NotEmpty();

                tvShow
                    .RuleForEach(y => y.Seasons)
                    .ChildRules(season =>
                    {
                        season.RuleFor(a => a.PlexApiRatingKey).GreaterThan(0);
                        season.RuleFor(y => y.PlexLibraryId).GreaterThan(0);
                        season.RuleFor(y => y.PlexServerId).GreaterThan(0);

                        season.RuleForEach(a => a.Episodes).NotNull();
                        season.RuleForEach(a => a.Episodes).NotEmpty();
                        season
                            .RuleForEach(a => a.Episodes)
                            .ChildRules(episode =>
                            {
                                episode.RuleFor(c => c.PlexApiRatingKey).GreaterThan(0);
                                season.RuleFor(y => y.PlexLibraryId).GreaterThan(0);
                                season.RuleFor(y => y.PlexServerId).GreaterThan(0);
                            });
                    });
            });

        stopWatch.Stop();
        log.Here()
            .Debug(
                "Finished validating {ClassName} in {TotalMilliseconds} milliseconds",
                nameof(SyncPlexTvShowsCommandValidator),
                stopWatch.Elapsed.TotalMilliseconds
            );
    }
}

public class SyncPlexTvShowsCommandHandler : ICommandHandler<SyncPlexTvShowsCommand, Result<BulkInsertTvShowsRapport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IReaparrDbContextFactory _dbContextFactory;

    private static BulkConfig CreateBulkConfig() =>
        new()
        {
            BatchSize = 1000,
            SetOutputIdentity = true,
            PreserveInsertOrder = true,
            CalculateStats = true,
            EnableStreaming = true,
            UseTempDB = true,
        };

    public SyncPlexTvShowsCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IReaparrDbContextFactory dbContextFactory
    )
    {
        _log = log.ForContext<SyncPlexTvShowsCommandHandler>();
        _dbContext = dbContext;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Result<BulkInsertTvShowsRapport>> ExecuteAsync(
        SyncPlexTvShowsCommand command,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SyncPlexTvShowsCommand)).LogWarning();

        var plexLibraryId = command.LibraryMetadata.PlexLibraryId;

        var plexLibraryName = await _dbContext.GetPlexLibraryNameById(plexLibraryId);
        var plexServerId = await _dbContext.GetPlexServerIdFromPlexLibraryId(plexLibraryId);

        if (string.IsNullOrWhiteSpace(plexLibraryName))
            return ResultExtensions.EntityNotFound(nameof(command.LibraryMetadata.PlexLibrary), plexLibraryId);

        _log.Here()
            .Debug(
                "Starting incremental sync of TV shows in library: {PlexLibraryName} with id: {PlexLibraryId}",
                plexLibraryName,
                plexLibraryId
            );

        var stopWatch = Stopwatch.StartNew();
        var plexTvShows = command.LibraryMetadata.PlexLibrary.TvShows.ToList();
        var bulkInsertRapport = new BulkInsertTvShowsRapport();
        var reconcileResult = await ReconcileTvShows(
            plexTvShows,
            plexServerId,
            plexLibraryId,
            bulkInsertRapport,
            command.ForceMediaRefresh,
            cancellationToken
        );

        if (reconcileResult.IsCancelled)
        {
            stopWatch.Stop();
            return reconcileResult.LogWarning();
        }

        if (reconcileResult.IsFailed)
        {
            stopWatch.Stop();
            return reconcileResult.LogError();
        }

        // Sync metadata such as Countries, Roles and Genre using separate DbContext instances
        // to avoid EF Core DbContext thread-safety issues when running in parallel.
        var genreDict = command.LibraryMetadata.PlexGenres;
        var countryDict = command.LibraryMetadata.PlexCountries;
        var actorDict = command.LibraryMetadata.PlexActors;

        ResultBase[] results = await Task.WhenAll(
            SyncTvShowGenres(plexTvShows, genreDict, plexLibraryId, plexLibraryName, CancellationToken.None),
            SyncTvShowCountries(plexTvShows, countryDict, plexLibraryId, plexLibraryName, CancellationToken.None),
            SyncTvShowActors(plexTvShows, actorDict, plexLibraryId, plexLibraryName, CancellationToken.None)
        );

        var mergeResult = Result.Merge(results);
        if (mergeResult.IsFailed)
            return mergeResult.LogError();

        stopWatch.StopAndLog($"Finished media syncing plexLibrary: {plexLibraryName} with id: {plexLibraryId}");

        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _log.Here().Debug(bulkInsertRapport.ToString());

        return Result.Ok(bulkInsertRapport);
    }

    private async Task<Result> ReconcileTvShows(
        List<PlexTvShow> incomingShows,
        int plexServerId,
        int plexLibraryId,
        BulkInsertTvShowsRapport report,
        bool forceMediaRefresh,
        CancellationToken cancellationToken
    )
    {
        var currentShows = await _dbContext
            .PlexTvShows.AsNoTracking()
            .Where(x => x.PlexLibraryId == plexLibraryId)
            .Select(x => new CurrentShow(x.Id, x.PlexApiRatingKey, x.UpdatedAt))
            .ToListAsync(cancellationToken);
        var currentSeasons = await _dbContext
            .PlexTvShowSeason.AsNoTracking()
            .Where(x => x.PlexLibraryId == plexLibraryId)
            .Select(x => new CurrentSeason(x.Id, x.PlexApiRatingKey, x.UpdatedAt, x.ParentKey))
            .ToListAsync(cancellationToken);
        var currentEpisodes = await _dbContext
            .PlexTvShowEpisodes.AsNoTracking()
            .Where(x => x.PlexLibraryId == plexLibraryId)
            .Select(x => new CurrentEpisode(
                x.Id,
                x.PlexApiRatingKey,
                x.UpdatedAt,
                x.ParentKey,
                x.MediaSize,
                x.MediaDataList.Count
            ))
            .ToListAsync(cancellationToken);
        var currentShowByKey = currentShows.ToDictionary(x => x.PlexApiRatingKey);
        var currentSeasonByKey = currentSeasons.ToDictionary(x => x.PlexApiRatingKey);
        var currentEpisodeByKey = currentEpisodes.ToDictionary(x => x.PlexApiRatingKey);

        incomingShows.SetRelationshipIds(plexServerId, plexLibraryId);
        var incomingSeasons = incomingShows.SelectMany(x => x.Seasons).ToList();
        var incomingEpisodes = incomingSeasons.SelectMany(x => x.Episodes).ToList();

        var createdShows = new List<PlexTvShow>();
        var updatedShows = new List<PlexTvShow>();
        foreach (var show in incomingShows)
        {
            if (!currentShowByKey.TryGetValue(show.PlexApiRatingKey, out var current))
                createdShows.Add(show);
            else
            {
                show.Id = current.Id;
                if (show.UpdatedAt != current.UpdatedAt)
                    updatedShows.Add(show);
            }
        }

        var createdSeasons = new List<PlexTvShowSeason>();
        var updatedSeasons = new List<PlexTvShowSeason>();
        foreach (var season in incomingSeasons)
        {
            if (!currentSeasonByKey.TryGetValue(season.PlexApiRatingKey, out var current))
                createdSeasons.Add(season);
            else
            {
                season.Id = current.Id;
                if (season.UpdatedAt != current.UpdatedAt || season.ParentKey != current.ParentKey)
                    updatedSeasons.Add(season);
            }
        }

        var movedSeasonKeys = updatedSeasons
            .Where(x => x.ParentKey != currentSeasonByKey[x.PlexApiRatingKey].ParentKey)
            .Select(x => x.PlexApiRatingKey)
            .ToHashSet();
        var createdEpisodes = new List<PlexTvShowEpisode>();
        var updatedEpisodes = new List<PlexTvShowEpisode>();
        foreach (var episode in incomingEpisodes)
        {
            if (!currentEpisodeByKey.TryGetValue(episode.PlexApiRatingKey, out var current))
                createdEpisodes.Add(episode);
            else
            {
                episode.Id = current.Id;
                if (
                    episode.UpdatedAt != current.UpdatedAt
                    || episode.ParentKey != current.ParentKey
                    || movedSeasonKeys.Contains(episode.ParentKey)
                )
                    updatedEpisodes.Add(episode);
            }
        }

        var incomingShowKeys = incomingShows.Select(x => x.PlexApiRatingKey).ToHashSet();
        var incomingSeasonKeys = incomingSeasons.Select(x => x.PlexApiRatingKey).ToHashSet();
        var incomingEpisodeKeys = incomingEpisodes.Select(x => x.PlexApiRatingKey).ToHashSet();
        var deletedShows = currentShows.Where(x => !incomingShowKeys.Contains(x.PlexApiRatingKey)).ToList();
        var deletedSeasons = currentSeasons.Where(x => !incomingSeasonKeys.Contains(x.PlexApiRatingKey)).ToList();
        var deletedEpisodes = currentEpisodes.Where(x => !incomingEpisodeKeys.Contains(x.PlexApiRatingKey)).ToList();

        foreach (var show in incomingShows)
            show.Quality =
                show.Seasons.SelectMany(x => x.Episodes)
                    .SelectMany(x => x.MediaDataList)
                    .Select(x => (VideoQuality?)x.Quality)
                    .Max()
                ?? VideoQuality.Unknown;

        report.CreatedTvShows = createdShows.Count;
        report.UpdatedTvShows = updatedShows.Count;
        report.DeletedTvShows = deletedShows.Count;
        report.UnchangedTvShows = incomingShows.Count - createdShows.Count - updatedShows.Count;
        report.CreatedSeasons = createdSeasons.Count;
        report.UpdatedSeasons = updatedSeasons.Count;
        report.DeletedSeasons = deletedSeasons.Count;
        report.UnchangedSeasons = incomingSeasons.Count - createdSeasons.Count - updatedSeasons.Count;
        report.CreatedEpisodes = createdEpisodes.Count;
        report.UpdatedEpisodes = updatedEpisodes.Count;
        report.DeletedEpisodes = deletedEpisodes.Count;
        report.UnchangedEpisodes = incomingEpisodes.Count - createdEpisodes.Count - updatedEpisodes.Count;

        var changedEpisodeKeys = createdEpisodes.Concat(updatedEpisodes).Select(x => x.PlexApiRatingKey).ToHashSet();
        var mediaSize = 0L;
        var episodeMediaDataCount = 0;
        foreach (var episode in incomingEpisodes)
        {
            if (!forceMediaRefresh && !changedEpisodeKeys.Contains(episode.PlexApiRatingKey))
            {
                var current = currentEpisodeByKey[episode.PlexApiRatingKey];
                mediaSize += current.MediaSize;
                episodeMediaDataCount += current.MediaDataCount;
                continue;
            }

            mediaSize += episode.MediaSize;
            episodeMediaDataCount += episode.MediaDataList.Count;
        }

        return await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                if (forceMediaRefresh)
                {
                    await ctx.PlexTvShows.Where(x => x.PlexLibraryId == plexLibraryId).ExecuteDeleteAsync(txCt);
                    createdShows = incomingShows;
                    createdSeasons = incomingSeasons;
                    createdEpisodes = incomingEpisodes;
                    updatedShows = [];
                    updatedSeasons = [];
                    updatedEpisodes = [];
                    deletedShows = currentShows;
                    deletedSeasons = currentSeasons;
                    deletedEpisodes = currentEpisodes;
                }

                var changedSeasonIds = createdSeasons.Concat(updatedSeasons).Select(x => x.Id).ToList();
                await ctx.BulkDeleteByIdsAsync(
                    changedSeasonIds,
                    (db, ids) => db.PlexTvShowSeasonMediaQualities.Where(x => ids.Contains(x.PlexTvShowSeasonId)),
                    txCt
                );

                var changedShowIds = createdShows.Concat(updatedShows).Select(x => x.Id).ToList();
                await ctx.BulkDeleteByIdsAsync(
                    changedShowIds,
                    (db, ids) => db.PlexTvShowMediaQualities.Where(x => ids.Contains(x.PlexTvShowId)),
                    txCt
                );

                var updatedEpisodeIds = updatedEpisodes.Select(x => x.Id).ToList();
                await ctx.BulkDeleteByIdsAsync(
                    updatedEpisodeIds,
                    (db, ids) => db.PlexTvShowEpisodeData.Where(x => ids.Contains(x.PlexTvShowEpisodeId)),
                    txCt
                );

                var deletedEpisodeIds = deletedEpisodes.Select(x => x.Id).ToList();
                await ctx.BulkDeleteByIdsAsync(
                    deletedEpisodeIds,
                    (db, ids) => db.PlexTvShowEpisodes.Where(x => ids.Contains(x.Id)),
                    txCt
                );

                var deletedSeasonIds = deletedSeasons.Select(x => x.Id).ToList();
                var deletedShowIds = deletedShows.Select(x => x.Id).ToList();

                if (updatedShows.Count > 0)
                    await ctx.BulkUpdateAsync(updatedShows, BulkConfigPreset.Default, txCt);
                if (createdShows.Count > 0)
                    await ctx.BulkInsertAsync(createdShows, BulkConfigPreset.Default, txCt);

                foreach (var show in incomingShows)
                foreach (var season in show.Seasons)
                    season.TvShowId = show.Id;

                if (updatedSeasons.Count > 0)
                    await ctx.BulkUpdateAsync(updatedSeasons, BulkConfigPreset.Default, txCt);
                if (createdSeasons.Count > 0)
                    await ctx.BulkInsertAsync(createdSeasons, BulkConfigPreset.Default, txCt);

                foreach (var show in incomingShows)
                foreach (var season in show.Seasons)
                foreach (var episode in season.Episodes)
                {
                    episode.TvShowId = show.Id;
                    episode.TvShowSeasonId = season.Id;
                }

                if (updatedEpisodes.Count > 0)
                    await ctx.BulkUpdateAsync(updatedEpisodes, BulkConfigPreset.Default, txCt);
                if (createdEpisodes.Count > 0)
                    await ctx.BulkInsertAsync(createdEpisodes, BulkConfigPreset.Default, txCt);

                var changedMediaData = createdEpisodes
                    .Concat(updatedEpisodes)
                    .SelectMany(episode =>
                    {
                        episode.MediaDataList.SetRelationshipIds(
                            episode.PlexServerId,
                            episode.PlexLibraryId,
                            episode.Id
                        );
                        return episode.MediaDataList;
                    })
                    .ToList();
                if (changedMediaData.Count > 0)
                    await ctx.BulkInsertAsync(changedMediaData, BulkConfigPreset.Default, txCt);

                // Re-parent surviving children before deleting obsolete parents with cascading foreign keys.
                await ctx.BulkDeleteByIdsAsync(
                    deletedSeasonIds,
                    (db, ids) => db.PlexTvShowSeason.Where(x => ids.Contains(x.Id)),
                    txCt
                );
                await ctx.BulkDeleteByIdsAsync(
                    deletedShowIds,
                    (db, ids) => db.PlexTvShows.Where(x => ids.Contains(x.Id)),
                    txCt
                );

                var seasonQualities = createdSeasons
                    .Concat(updatedSeasons)
                    .SelectMany(season =>
                        season
                            .Episodes.SelectMany(episode => episode.MediaDataList)
                            .Select(mediaData => new PlexTvShowSeasonMediaQuality
                            {
                                Id = 0,
                                Quality = mediaData.Quality,
                                PlexLibraryId = plexLibraryId,
                                PlexTvShowSeasonId = season.Id,
                                PlexTvShowSeason = season,
                            })
                    )
                    .DistinctBy(x => (x.Quality, x.PlexTvShowSeasonId))
                    .ToList();
                if (seasonQualities.Count > 0)
                    await ctx.BulkInsertAsync(seasonQualities, BulkConfigPreset.Default, txCt);

                var tvShowQualities = createdShows
                    .Concat(updatedShows)
                    .SelectMany(show =>
                        show.Seasons.SelectMany(season => season.Episodes)
                            .SelectMany(episode => episode.MediaDataList)
                            .Select(mediaData => new PlexTvShowMediaQuality
                            {
                                Id = 0,
                                Quality = mediaData.Quality,
                                PlexLibraryId = plexLibraryId,
                                PlexTvShowId = show.Id,
                            })
                    )
                    .DistinctBy(x => (x.Quality, x.PlexTvShowId))
                    .ToList();
                if (tvShowQualities.Count > 0)
                    await ctx.BulkInsertAsync(tvShowQualities, BulkConfigPreset.Default, txCt);

                await ctx.SetTvShowMediaMetrics(
                    plexLibraryId,
                    incomingShows.Count,
                    incomingSeasons.Count,
                    incomingEpisodes.Count,
                    episodeMediaDataCount,
                    mediaSize
                );
            },
            cancellationToken
        );
    }

    private sealed record CurrentShow(int Id, int PlexApiRatingKey, DateTime? UpdatedAt);

    private sealed record CurrentSeason(int Id, int PlexApiRatingKey, DateTime? UpdatedAt, int ParentKey);

    private sealed record CurrentEpisode(
        int Id,
        int PlexApiRatingKey,
        DateTime? UpdatedAt,
        int ParentKey,
        long MediaSize,
        int MediaDataCount
    );

    private async Task<Result> SyncTvShowGenres(
        List<PlexTvShow> plexTvShows,
        Dictionary<string, PlexGenre> genreDict,
        int plexLibraryId,
        string libraryName,
        CancellationToken cancellationToken
    )
    {
        using var dbContext = await _dbContextFactory.CreateAsync();

        _log.Here()
            .Debug(
                "Starting syncing of TV show genres for library: {LibraryName} with id: {LibraryId}",
                libraryName,
                plexLibraryId
            );
        var stopWatch = Stopwatch.StartNew();

        var plexTvShowGenres = new List<PlexTvShowGenres>();
        var keyToIdDict = genreDict.ToDictionary(kv => kv.Value.Key, kv => kv.Value.Id);

        foreach (var plexTvShow in plexTvShows)
        {
            foreach (var plexGenre in plexTvShow.Genres)
            {
                if (keyToIdDict.TryGetValue(plexGenre.Key, out var genreId))
                    plexTvShowGenres.Add(new PlexTvShowGenres(genreId, plexLibraryId, plexTvShow.Id));
            }
        }

        var distinctGenres = plexTvShowGenres.DistinctBy(x => new { x.PlexTvShowId, x.GenresId }).ToList();
        var insertResult = await dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexTvShowGenres.Where(x => x.PlexLibraryId == plexLibraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(distinctGenres, CreateBulkConfig(), txCt);
                await ctx
                    .PlexLibraries.Where(x => x.Id == plexLibraryId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.GenresCount, distinctGenres.Count), txCt);
            },
            cancellationToken
        );

        stopWatch.StopAndLog(
            $"Synced {plexTvShowGenres.Count} {nameof(PlexTvShowGenres)} for library: {libraryName} with id: {plexLibraryId}"
        );

        return insertResult;
    }

    private async Task<Result> SyncTvShowCountries(
        List<PlexTvShow> plexTvShows,
        Dictionary<string, PlexCountry> countryDict,
        int plexLibraryId,
        string libraryName,
        CancellationToken cancellationToken
    )
    {
        using var dbContext = await _dbContextFactory.CreateAsync();

        _log.Here()
            .Debug(
                "Starting syncing of TV show countries for library: {LibraryName} with id: {LibraryId}",
                libraryName,
                plexLibraryId
            );
        var stopWatch = Stopwatch.StartNew();

        var plexTvShowCountries = new List<PlexTvShowCountries>();
        var keyToIdDict = countryDict.ToDictionary(kv => kv.Value.Key, kv => kv.Value.Id);

        foreach (var plexTvShow in plexTvShows)
        {
            foreach (var plexCountry in plexTvShow.Countries)
            {
                if (keyToIdDict.TryGetValue(plexCountry.Key, out var countryId))
                    plexTvShowCountries.Add(new PlexTvShowCountries(countryId, plexLibraryId, plexTvShow.Id));
            }
        }

        var distinctCountries = plexTvShowCountries.DistinctBy(x => new { x.PlexTvShowId, x.CountryId }).ToList();

        var insertResult = await dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexTvShowCountries.Where(x => x.PlexLibraryId == plexLibraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(distinctCountries, CreateBulkConfig(), txCt);
                await ctx
                    .PlexLibraries.Where(x => x.Id == plexLibraryId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.CountriesCount, distinctCountries.Count), txCt);
            },
            cancellationToken
        );

        stopWatch.StopAndLog(
            $"Synced {plexTvShowCountries.Count} {nameof(PlexTvShowCountries)} for library: {libraryName} with id: {plexLibraryId}"
        );

        return insertResult;
    }

    private async Task<Result> SyncTvShowActors(
        List<PlexTvShow> plexTvShows,
        Dictionary<string, PlexActor> actorDict,
        int plexLibraryId,
        string libraryName,
        CancellationToken cancellationToken
    )
    {
        using var dbContext = await _dbContextFactory.CreateAsync();

        _log.Here()
            .Debug(
                "Starting syncing of TV show actors for library: {LibraryName} with id: {LibraryId}",
                libraryName,
                plexLibraryId
            );
        var stopWatch = Stopwatch.StartNew();

        var plexTvShowRoles = new List<PlexTvShowActors>();
        var keyToIdDict = actorDict.ToDictionary(kv => kv.Value.Key, kv => kv.Value.Id);

        foreach (var plexTvShow in plexTvShows)
        {
            foreach (var actor in plexTvShow.Actors)
            {
                if (keyToIdDict.TryGetValue(actor.Key, out var plexActorId))
                    plexTvShowRoles.Add(new PlexTvShowActors(plexActorId, plexLibraryId, plexTvShow.Id));
            }
        }

        var distinctActors = plexTvShowRoles.DistinctBy(x => new { x.PlexTvShowId, RolesId = x.PlexActorId }).ToList();
        var insertResult = await dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexTvShowActors.Where(x => x.PlexLibraryId == plexLibraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(distinctActors, CreateBulkConfig(), txCt);
                await ctx
                    .PlexLibraries.Where(x => x.Id == plexLibraryId)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.ActorsCount, distinctActors.Count), txCt);
            },
            cancellationToken
        );

        stopWatch.StopAndLog(
            $"Synced {plexTvShowRoles.Count} {nameof(PlexTvShowActors)} for library: {libraryName} with id: {plexLibraryId}"
        );

        return insertResult;
    }
}
