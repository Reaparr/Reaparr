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
                tvShow.RuleForEach(y => y.Seasons).NotNull();
                tvShow.RuleForEach(a => a.Seasons).NotEmpty();

                tvShow
                    .RuleForEach(y => y.Seasons)
                    .ChildRules(season =>
                    {
                        season.RuleFor(a => a.PlexApiRatingKey).GreaterThan(0);

                        season.RuleForEach(a => a.Episodes).NotNull();
                        season.RuleForEach(a => a.Episodes).NotEmpty();
                        season
                            .RuleForEach(a => a.Episodes)
                            .ChildRules(episode =>
                            {
                                episode.RuleFor(c => c.PlexApiRatingKey).GreaterThan(0);
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

        var plexLibraryName = command.LibraryMetadata.PlexLibrary.Title;
        var plexServerId = command.LibraryMetadata.PlexLibrary.PlexServerId;

        _log.Here()
            .Debug(
                "Starting incremental sync of TV shows in library: {PlexLibraryName} with id: {PlexLibraryId}",
                plexLibraryName,
                plexLibraryId
            );

        var stopWatch = Stopwatch.StartNew();
        var plexTvShows = command.LibraryMetadata.PlexLibrary.TvShows.ToList();
        var changedShows = new List<PlexTvShow>();
        var bulkInsertRapport = new BulkInsertTvShowsRapport();
        var reconcileResult = await ReconcileTvShows(
            plexTvShows,
            plexServerId,
            plexLibraryId,
            bulkInsertRapport,
            changedShows,
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
        var genreDict = command.LibraryMetadata.PlexGenresVideo;
        var countryDict = command.LibraryMetadata.PlexCountries;
        var actorDict = command.LibraryMetadata.PlexActors;

        if (changedShows.Count > 0)
        {
            var results = await Task.WhenAll(
                SyncTvShowGenres(changedShows, genreDict, plexLibraryId, plexLibraryName, CancellationToken.None),
                SyncTvShowCountries(changedShows, countryDict, plexLibraryId, plexLibraryName, CancellationToken.None),
                SyncTvShowActors(changedShows, actorDict, plexLibraryId, plexLibraryName, CancellationToken.None)
            );
            var mergeResult = results.Merge();
            if (mergeResult.IsFailed)
                return mergeResult.LogError();
        }

        stopWatch.StopAndLog($"Finished media syncing plexLibrary: {plexLibraryName} with id: {plexLibraryId}");

        _log.Here().Debug("Bulk insert report: {@BulkInsertRapport}", bulkInsertRapport);

        return Result.Ok(bulkInsertRapport);
    }

    private async Task<Result> ReconcileTvShows(
        List<PlexTvShow> incomingShows,
        int plexServerId,
        int plexLibraryId,
        BulkInsertTvShowsRapport report,
        List<PlexTvShow> changedShows,
        bool forceMediaRefresh,
        CancellationToken cancellationToken
    )
    {
        var incomingShowKeys = incomingShows.Select(show => show.PlexApiRatingKey).ToHashSet();
        var incomingSeasons = incomingShows.SelectMany(show => show.Seasons).ToList();
        var incomingSeasonKeys = incomingSeasons.Select(season => season.PlexApiRatingKey).ToHashSet();
        var incomingEpisodes = incomingSeasons.SelectMany(season => season.Episodes).ToList();
        var incomingEpisodeKeys = incomingEpisodes.Select(episode => episode.PlexApiRatingKey).ToHashSet();

        return await Result.Try(async Task () =>
        {
            var currentShows = await _dbContext
                .PlexTvShows.Where(x => x.PlexLibraryId == plexLibraryId)
                .Select(x => new CurrentShow(x.Id, x.PlexApiRatingKey, x.UpdatedAt))
                .ToListAsync(cancellationToken);
            var currentSeasons = await _dbContext
                .PlexTvShowSeason.Where(x => x.PlexLibraryId == plexLibraryId)
                .Select(x => new CurrentSeason(x.Id, x.PlexApiRatingKey, x.UpdatedAt))
                .ToListAsync(cancellationToken);
            var currentEpisodes = await _dbContext
                .PlexTvShowEpisodes.Where(x => x.PlexLibraryId == plexLibraryId)
                .Select(x => new CurrentEpisode(
                    x.Id,
                    x.PlexApiRatingKey,
                    x.UpdatedAt,
                    x.MediaSize,
                    x.MediaDataList.Count
                ))
                .ToListAsync(cancellationToken);
            Dictionary<int, CurrentEpisode>? currentEpisodeByKey = null;

            var createdShows = new List<PlexTvShow>();
            var updatedShows = new List<PlexTvShow>();
            var createdSeasons = new List<PlexTvShowSeason>();
            var updatedSeasons = new List<PlexTvShowSeason>();
            var createdEpisodes = new List<PlexTvShowEpisode>();
            var updatedEpisodes = new List<PlexTvShowEpisode>();
            var deletedShows = currentShows.Where(x => !incomingShowKeys.Contains(x.PlexApiRatingKey)).ToList();
            var deletedSeasons = currentSeasons.Where(x => !incomingSeasonKeys.Contains(x.PlexApiRatingKey)).ToList();
            var deletedEpisodes = currentEpisodes
                .Where(x => !incomingEpisodeKeys.Contains(x.PlexApiRatingKey))
                .ToList();

            if (forceMediaRefresh)
            {
                await _dbContext
                    .PlexTvShows.Where(x => x.PlexLibraryId == plexLibraryId)
                    .ExecuteDeleteAsync(cancellationToken);
                createdShows = incomingShows;
                createdSeasons = incomingSeasons;
                createdEpisodes = incomingEpisodes;
                deletedShows = currentShows;
                deletedSeasons = currentSeasons;
                deletedEpisodes = currentEpisodes;
            }
            else
            {
                // A forced refresh replaces rows without matching their cached rating keys.
                var currentShowByKey = currentShows.ToDictionary(x => x.PlexApiRatingKey);
                var currentSeasonByKey = currentSeasons.ToDictionary(x => x.PlexApiRatingKey);
                currentEpisodeByKey = currentEpisodes.ToDictionary(x => x.PlexApiRatingKey);

                foreach (var show in incomingShows)
                {
                    if (!currentShowByKey.TryGetValue(show.PlexApiRatingKey, out var current))
                    {
                        createdShows.Add(show);
                        continue;
                    }

                    show.Id = current.Id;
                    if (show.UpdatedAt != current.UpdatedAt)
                        updatedShows.Add(show);
                }

                foreach (var season in incomingSeasons)
                {
                    if (!currentSeasonByKey.TryGetValue(season.PlexApiRatingKey, out var current))
                    {
                        createdSeasons.Add(season);
                        continue;
                    }

                    season.Id = current.Id;
                    if (season.UpdatedAt != current.UpdatedAt)
                        updatedSeasons.Add(season);
                }

                foreach (var episode in incomingEpisodes)
                {
                    if (!currentEpisodeByKey.TryGetValue(episode.PlexApiRatingKey, out var current))
                    {
                        createdEpisodes.Add(episode);
                        continue;
                    }

                    episode.Id = current.Id;
                    if (episode.UpdatedAt != current.UpdatedAt)
                        updatedEpisodes.Add(episode);
                }
            }

            changedShows.AddRange(createdShows.Concat(updatedShows));
            foreach (var show in createdShows.Concat(updatedShows))
                show.VideoQuality = show.Seasons.SelectMany(x => x.Episodes)
                    .SelectMany(x => x.MediaDataList)
                    .Select(x => (VideoQuality?)x.VideoQuality)
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

            var changedEpisodeKeys = createdEpisodes
                .Concat(updatedEpisodes)
                .Select(x => x.PlexApiRatingKey)
                .ToHashSet();
            var mediaSize = 0L;
            var episodeMediaDataCount = 0;
            foreach (var episode in incomingEpisodes)
            {
                if (currentEpisodeByKey is not null && !changedEpisodeKeys.Contains(episode.PlexApiRatingKey))
                {
                    var current = currentEpisodeByKey[episode.PlexApiRatingKey];
                    mediaSize += current.MediaSize;
                    episodeMediaDataCount += current.MediaDataCount;
                    continue;
                }

                mediaSize += episode.MediaSize;
                episodeMediaDataCount += episode.MediaDataList.Count;
            }
            incomingShows.SetRelationshipIds(plexServerId, plexLibraryId);

            var changedSeasonIds = createdSeasons.Concat(updatedSeasons).Select(x => x.Id).ToList();
            await _dbContext.BulkDeleteByIdsAsync(
                changedSeasonIds,
                (db, ids) => db.PlexTvShowSeasonMediaQualities.Where(x => ids.Contains(x.PlexTvShowSeasonId)),
                cancellationToken
            );

            var changedShowIds = createdShows.Concat(updatedShows).Select(x => x.Id).ToList();
            await _dbContext.BulkDeleteByIdsAsync(
                changedShowIds,
                (db, ids) => db.PlexTvShowMediaQualities.Where(x => ids.Contains(x.PlexTvShowId)),
                cancellationToken
            );

            var updatedEpisodeIds = updatedEpisodes.Select(x => x.Id).ToList();
            await _dbContext.BulkDeleteByIdsAsync(
                updatedEpisodeIds,
                (db, ids) => db.PlexTvShowEpisodeData.Where(x => ids.Contains(x.PlexTvShowEpisodeId)),
                cancellationToken
            );

            var deletedEpisodeIds = deletedEpisodes.Select(x => x.Id).ToList();
            await _dbContext.BulkDeleteByIdsAsync(
                deletedEpisodeIds,
                (db, ids) => db.PlexTvShowEpisodes.Where(x => ids.Contains(x.Id)),
                cancellationToken
            );

            var deletedSeasonIds = deletedSeasons.Select(x => x.Id).ToList();
            var deletedShowIds = deletedShows.Select(x => x.Id).ToList();

            if (updatedShows.Count > 0)
                await _dbContext.BulkUpdateAsync(updatedShows, BulkConfigPreset.Default, cancellationToken);
            if (createdShows.Count > 0)
                await _dbContext.BulkInsertAsync(createdShows, BulkConfigPreset.Default, cancellationToken);

            foreach (var show in incomingShows)
            foreach (var season in show.Seasons)
                season.TvShowId = show.Id;

            if (updatedSeasons.Count > 0)
                await _dbContext.BulkUpdateAsync(updatedSeasons, BulkConfigPreset.Default, cancellationToken);
            if (createdSeasons.Count > 0)
                await _dbContext.BulkInsertAsync(createdSeasons, BulkConfigPreset.Default, cancellationToken);

            foreach (var show in incomingShows)
            foreach (var season in show.Seasons)
            foreach (var episode in season.Episodes)
            {
                episode.TvShowId = show.Id;
                episode.TvShowSeasonId = season.Id;
            }

            if (updatedEpisodes.Count > 0)
                await _dbContext.BulkUpdateAsync(updatedEpisodes, BulkConfigPreset.Default, cancellationToken);
            if (createdEpisodes.Count > 0)
                await _dbContext.BulkInsertAsync(createdEpisodes, BulkConfigPreset.Default, cancellationToken);

            var changedMediaData = createdEpisodes
                .Concat(updatedEpisodes)
                .SelectMany(episode =>
                {
                    episode.MediaDataList.SetRelationshipIds(episode.PlexServerId, episode.PlexLibraryId, episode.Id);
                    return episode.MediaDataList;
                })
                .ToList();
            if (changedMediaData.Count > 0)
                await _dbContext.BulkInsertAsync(changedMediaData, BulkConfigPreset.Default, cancellationToken);

            // Re-parent surviving children before deleting obsolete parents with cascading foreign keys.
            await _dbContext.BulkDeleteByIdsAsync(
                deletedSeasonIds,
                (db, ids) => db.PlexTvShowSeason.Where(x => ids.Contains(x.Id)),
                cancellationToken
            );
            await _dbContext.BulkDeleteByIdsAsync(
                deletedShowIds,
                (db, ids) => db.PlexTvShows.Where(x => ids.Contains(x.Id)),
                cancellationToken
            );
            await ReconcileTvShowEpisodeStreamsAsync(_dbContext, changedMediaData, cancellationToken);

            var seasonQualities = createdSeasons
                .Concat(updatedSeasons)
                .SelectMany(season =>
                    season
                        .Episodes.SelectMany(episode => episode.MediaDataList)
                        .Select(mediaData => new PlexTvShowSeasonMediaQuality
                        {
                            VideoQuality = mediaData.VideoQuality,
                            PlexLibraryId = plexLibraryId,
                            PlexTvShowSeasonId = season.Id,
                            PlexTvShowSeason = season,
                        })
                )
                .DistinctBy(x => (Quality: x.VideoQuality, x.PlexTvShowSeasonId))
                .ToList();
            if (seasonQualities.Count > 0)
                await _dbContext.BulkInsertAsync(seasonQualities, BulkConfigPreset.Default, cancellationToken);

            var tvShowQualities = createdShows
                .Concat(updatedShows)
                .SelectMany(show =>
                    show.Seasons.SelectMany(season => season.Episodes)
                        .SelectMany(episode => episode.MediaDataList)
                        .Select(mediaData => new PlexTvShowMediaQuality
                        {
                            VideoQuality = mediaData.VideoQuality,
                            PlexLibraryId = plexLibraryId,
                            PlexTvShowId = show.Id,
                        })
                )
                .DistinctBy(x => (Quality: x.VideoQuality, x.PlexTvShowId))
                .ToList();
            if (tvShowQualities.Count > 0)
                await _dbContext.BulkInsertAsync(tvShowQualities, BulkConfigPreset.Default, cancellationToken);

            await _dbContext.SetTvShowMediaMetrics(
                plexLibraryId,
                incomingShows.Count,
                incomingSeasons.Count,
                incomingEpisodes.Count,
                episodeMediaDataCount,
                mediaSize
            );
        });
    }

    private static Task ReconcileTvShowEpisodeStreamsAsync(
        IReaparrDbContext dbContext,
        IReadOnlyCollection<PlexTvShowEpisodeMediaData> mediaData,
        CancellationToken cancellationToken
    ) =>
        PlexMediaStreamReconciler.SyncDataStreamsAsync(
            dbContext,
            mediaData,
            static data => data.Streams,
            static context => context.PlexTvShowEpisodeDataStreams,
            static (sourceStream, ownerId, languageId) =>
                new PlexTvShowEpisodeDataStream
                {
                    MediaDataId = ownerId,
                    PlexApiStreamId = sourceStream.PlexApiStreamId,
                    StreamType = sourceStream.StreamType,
                    LanguageId = languageId,
                    IsExternal = sourceStream.IsExternal,
                    SidecarFormat = sourceStream.SidecarFormat,
                },
            cancellationToken
        );

    private sealed record CurrentShow(int Id, int PlexApiRatingKey, DateTime? UpdatedAt);

    private sealed record CurrentSeason(int Id, int PlexApiRatingKey, DateTime? UpdatedAt);

    private sealed record CurrentEpisode(
        int Id,
        int PlexApiRatingKey,
        DateTime? UpdatedAt,
        long MediaSize,
        int MediaDataCount
    );

    private async Task<Result> SyncTvShowGenres(
        List<PlexTvShow> plexTvShows,
        Dictionary<string, PlexGenreVideo> genreDict,
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
        var showIds = plexTvShows.Select(show => show.Id).ToList();
        var insertResult = await Result.Try(async Task () =>
        {
            await dbContext
                .PlexTvShowGenres.Where(x => x.PlexLibraryId == plexLibraryId && showIds.Contains(x.PlexTvShowId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.BulkInsertAsync(distinctGenres, CreateBulkConfig(), cancellationToken);
        });

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
        var showIds = plexTvShows.Select(show => show.Id).ToList();

        var insertResult = await Result.Try(async Task () =>
        {
            await dbContext
                .PlexTvShowCountries.Where(x => x.PlexLibraryId == plexLibraryId && showIds.Contains(x.PlexTvShowId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.BulkInsertAsync(distinctCountries, CreateBulkConfig(), cancellationToken);
        });

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
        var showIds = plexTvShows.Select(show => show.Id).ToList();
        var insertResult = await Result.Try(async Task () =>
        {
            await dbContext
                .PlexTvShowActors.Where(x => x.PlexLibraryId == plexLibraryId && showIds.Contains(x.PlexTvShowId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.BulkInsertAsync(distinctActors, CreateBulkConfig(), cancellationToken);
        });

        stopWatch.StopAndLog(
            $"Synced {plexTvShowRoles.Count} {nameof(PlexTvShowActors)} for library: {libraryName} with id: {plexLibraryId}"
        );

        return insertResult;
    }
}
