using EFCore.BulkExtensions;

namespace Reaparr.Application;

/// <summary>
/// Incrementally syncs the PlexMovies of a PlexLibrary.
/// In addition to syncing the PlexMovies, also syncs the related entities such as actors, genres and countries.
/// </summary>
public record SyncPlexMoviesCommand(InsertMediaMetaDataCommandResponse LibraryMetadata, bool ForceMediaRefresh = false)
    : ICommand<Result<CrudMoviesReport>>;

public class SyncPlexMoviesCommandValidator : AbstractValidator<SyncPlexMoviesCommand>
{
    public SyncPlexMoviesCommandValidator()
    {
        var stopWatch = Stopwatch.StartNew();
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibrary.PlexServerId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);

        RuleFor(x => x.LibraryMetadata.PlexLibrary.Movies).NotNull();
        RuleForEach(x => x.LibraryMetadata.PlexLibrary.Movies)
            .ChildRules(movie => movie.RuleFor(x => x.PlexApiRatingKey).GreaterThan(0));

        RuleFor(x => x.LibraryMetadata.PlexActors).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexGenres).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexCountries).NotNull();

        stopWatch.StopAndLog($"Finished validating {nameof(SyncPlexMoviesCommand)}");
    }
}

public class SyncPlexMoviesCommandHandler : ICommandHandler<SyncPlexMoviesCommand, Result<CrudMoviesReport>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    private readonly BulkConfig? _config = new() { BatchSize = 500, SetOutputIdentity = true };

    public SyncPlexMoviesCommandHandler(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<SyncPlexMoviesCommandHandler>();
        _dbContext = dbContext;
    }

    public async Task<Result<CrudMoviesReport>> ExecuteAsync(
        SyncPlexMoviesCommand command,
        CancellationToken cancellationToken
    )
    {
        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(SyncPlexMoviesCommand)).LogWarning();

        var plexLibraryId = command.LibraryMetadata.PlexLibraryId;
        var plexServerId = command.LibraryMetadata.PlexLibrary.PlexServerId;

        var libraryName = await _dbContext.GetPlexLibraryNameById(plexLibraryId);

        _log.Here()
            .Debug(
                "Starting incremental sync of movies in library: {PlexLibraryName} with id: {PlexLibraryId}",
                libraryName,
                plexLibraryId
            );

        var stopWatch = Stopwatch.StartNew();
        var plexMovies = command.LibraryMetadata.PlexLibrary.Movies.ToList();
        var report = new CrudMoviesReport();
        var reconcileResult = await ReconcileMovies(
            plexMovies,
            plexServerId,
            plexLibraryId,
            report,
            command.ForceMediaRefresh,
            cancellationToken
        );
        if (reconcileResult.IsCancelled)
            return reconcileResult;

        if (reconcileResult.IsFailed)
        {
            _log.Here()
                .Error(
                    "Failed to reconcile movies for library: {PlexLibraryName} with id: {PlexLibraryId}. Error: {Error}",
                    libraryName,
                    plexLibraryId,
                    reconcileResult.Errors
                );
            return reconcileResult;
        }

        var syncCountriesResult = await SyncMovieCountries(
            plexMovies,
            command.LibraryMetadata.PlexCountries,
            plexLibraryId,
            libraryName,
            cancellationToken
        );

        var syncGenreResult = await SyncMovieGenres(
            plexMovies,
            command.LibraryMetadata.PlexGenres,
            plexLibraryId,
            libraryName,
            cancellationToken
        );

        var syncActorResult = await SyncMovieActors(
            plexMovies,
            command.LibraryMetadata.PlexActors,
            plexLibraryId,
            libraryName,
            cancellationToken
        );

        var mergeResult = Result.Merge(syncActorResult, syncGenreResult, syncCountriesResult);
        if (mergeResult.IsFailed)
        {
            return mergeResult.LogError();
        }

        stopWatch.StopAndLog($"Finished media syncing plexLibrary: {libraryName} with id: {plexLibraryId}");

        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _log.Here().Information(report.ToString());

        return Result.Ok(report);
    }

    private async Task<Result> ReconcileMovies(
        List<PlexMovie> incomingMovies,
        int plexServerId,
        int plexLibraryId,
        CrudMoviesReport report,
        bool forceMediaRefresh,
        CancellationToken cancellationToken
    )
    {
        var currentMovies = await _dbContext
            .PlexMovies.AsNoTracking()
            .Where(x => x.PlexLibraryId == plexLibraryId)
            .Select(x => new CurrentMovie(x.Id, x.PlexApiRatingKey, x.UpdatedAt, x.MediaSize, x.MediaDataList.Count))
            .ToListAsync(cancellationToken);
        _dbContext.PlexMovies.Local.Clear();
        var currentByKey = currentMovies.ToDictionary(x => x.PlexApiRatingKey);
        var incomingKeys = incomingMovies.Select(x => x.PlexApiRatingKey).ToHashSet();
        var created = new List<PlexMovie>();
        var updated = new List<PlexMovie>();

        incomingMovies.SetRelationshipIds(plexServerId, plexLibraryId);
        foreach (var movie in incomingMovies)
        {
            if (!currentByKey.TryGetValue(movie.PlexApiRatingKey, out var current))
            {
                created.Add(movie);
                continue;
            }

            movie.Id = current.Id;
            if (movie.UpdatedAt != current.UpdatedAt)
                updated.Add(movie);
        }

        var deleted = currentMovies.Where(x => !incomingKeys.Contains(x.PlexApiRatingKey)).ToList();
        foreach (var movie in created.Concat(updated))
            movie.Quality =
                movie.MediaDataList.Count == 0 ? VideoQuality.Unknown : movie.MediaDataList.Max(x => x.Quality);

        report.CreatedMovies = created.Count;
        report.UpdatedMovies = updated.Count;
        report.DeletedMovies = deleted.Count;
        report.UnchangedMovies = incomingMovies.Count - created.Count - updated.Count;

        var changedMovieKeys = created.Concat(updated).Select(x => x.PlexApiRatingKey).ToHashSet();
        var mediaSize = 0L;
        var mediaDataCount = 0;
        foreach (var movie in incomingMovies)
        {
            if (!forceMediaRefresh && !changedMovieKeys.Contains(movie.PlexApiRatingKey))
            {
                var current = currentByKey[movie.PlexApiRatingKey];
                mediaSize += current.MediaSize;
                mediaDataCount += current.MediaDataCount;
                continue;
            }

            mediaSize += movie.MediaSize;
            mediaDataCount += movie.MediaDataList.Count;
        }

        return await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                if (forceMediaRefresh)
                {
                    await ctx.PlexMovies.Where(x => x.PlexLibraryId == plexLibraryId).ExecuteDeleteAsync(txCt);
                    created = incomingMovies;
                    updated = [];
                    deleted = currentMovies;
                    report.CreatedMovies = created.Count;
                    report.UpdatedMovies = 0;
                    report.DeletedMovies = deleted.Count;
                    report.UnchangedMovies = 0;
                }

                var updatedIds = updated.Select(x => x.Id).ToList();
                await ctx.BulkDeleteByIdsAsync(
                    updatedIds,
                    (db, ids) => db.PlexMovieData.Where(x => ids.Contains(x.PlexMovieId)),
                    txCt
                );

                var deletedIds = deleted.Select(x => x.Id).ToList();
                await ctx.BulkDeleteByIdsAsync(
                    deletedIds,
                    (db, ids) => db.PlexMovies.Where(x => ids.Contains(x.Id)),
                    txCt
                );

                if (updated.Count > 0)
                    await ctx.BulkUpdateAsync(updated, BulkConfigPreset.Default, txCt);

                if (created.Count > 0)
                    await ctx.BulkInsertAsync(created, BulkConfigPreset.Default, txCt);

                var mediaData = created
                    .Concat(updated)
                    .SelectMany(movie =>
                    {
                        movie.MediaDataList.SetRelationshipIds(movie.PlexServerId, movie.PlexLibraryId, movie.Id);
                        return movie.MediaDataList;
                    })
                    .ToList();
                if (mediaData.Count > 0)
                    await ctx.BulkInsertAsync(mediaData, BulkConfigPreset.Default, txCt);

                await ctx.SetMovieMediaMetrics(plexLibraryId, incomingMovies.Count, mediaDataCount, mediaSize);
            },
            cancellationToken
        );
    }

    private async Task<Result<int>> SyncMovieActors(
        List<PlexMovie> movies,
        Dictionary<string, PlexActor> plexActorsDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        _log.Here()
            .Debug(
                "Starting syncing of movie actors for library: {LibraryName} with id: {LibraryId}",
                libraryName,
                libraryId
            );
        var stopWatch = Stopwatch.StartNew();

        var list = new List<PlexMovieActors>();
        var keyToIdDict = plexActorsDict.ToDictionary(kv => kv.Value.Key, kv => kv.Value.Id);

        foreach (var movie in movies)
        {
            foreach (var actor in movie.Actors)
            {
                if (keyToIdDict.TryGetValue(actor.Key, out var plexActorId))
                {
                    list.Add(new PlexMovieActors(plexActorId, libraryId, movie.Id));
                }
            }
        }

        // Remove duplicates before inserting
        list = list.DistinctBy(x => new { x.PlexActorId, x.PlexMovieId }).ToList();

        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexMovieActors.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(list, _config, txCt);
            },
            ct
        );
        if (insertResult.IsFailed)
        {
            _log.Here().Error("Failed to sync movie actors: {Error}", insertResult.Errors);
            return insertResult;
        }

        stopWatch.StopAndLog(
            $"Synced {list.Count} {nameof(PlexMovieActors)} for library: {libraryName} with id: {libraryId}"
        );

        return Result.Ok(list.Count);
    }

    private async Task<Result<int>> SyncMovieGenres(
        List<PlexMovie> movies,
        Dictionary<string, PlexGenre> plexGenreDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        _log.Here()
            .Debug(
                "Starting syncing of movie genres for library: {LibraryName} with id: {LibraryId}",
                libraryName,
                libraryId
            );
        var stopWatch = Stopwatch.StartNew();

        var list = new List<PlexMovieGenres>();
        var keyToIdDict = plexGenreDict.ToDictionary(kv => kv.Value.Key, kv => kv.Value.Id);

        foreach (var movie in movies)
        {
            foreach (var genre in movie.Genres)
            {
                if (keyToIdDict.TryGetValue(genre.Key, out var plexGenreId))
                {
                    list.Add(new PlexMovieGenres(plexGenreId, libraryId, movie.Id));
                }
            }
        }

        // Remove duplicates before inserting
        list = list.DistinctBy(x => new { x.GenresId, x.PlexMovieId }).ToList();

        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexMovieGenres.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(list, _config, txCt);
            },
            ct
        );
        if (insertResult.IsFailed)
        {
            _log.Here().Error("Failed to sync movie genres: {Error}", insertResult.Errors);
            return insertResult;
        }

        stopWatch.StopAndLog(
            $"Synced {list.Count} {nameof(PlexMovieGenres)} for library: {libraryName} with id: {libraryId}"
        );

        return Result.Ok(list.Count);
    }

    private async Task<Result<int>> SyncMovieCountries(
        List<PlexMovie> movies,
        Dictionary<string, PlexCountry> plexCountryDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        _log.Here()
            .Debug(
                "Starting syncing of movie countries for library: {LibraryName} with id: {LibraryId}",
                libraryName,
                libraryId
            );
        var stopWatch = Stopwatch.StartNew();

        var list = new List<PlexMovieCountries>();
        var keyToIdDict = plexCountryDict.ToDictionary(kv => kv.Value.Key, kv => kv.Value.Id);

        foreach (var movie in movies)
        {
            foreach (var country in movie.Countries)
            {
                if (keyToIdDict.TryGetValue(country.Key, out var plexCountryId))
                {
                    list.Add(new PlexMovieCountries(plexCountryId, libraryId, movie.Id));
                }
            }
        }

        // Remove duplicates before inserting
        list = list.DistinctBy(x => new { x.CountryId, x.PlexMovieId }).ToList();

        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexMovieCountries.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(list, _config, txCt);
            },
            ct
        );
        if (insertResult.IsFailed)
        {
            _log.Here().Error("Failed to sync movie countries: {Error}", insertResult.Errors);
            return insertResult;
        }

        stopWatch.StopAndLog(
            $"Synced {list.Count} {nameof(PlexMovieCountries)} for library: {libraryName} with id: {libraryId}"
        );

        return Result.Ok(list.Count);
    }

    private sealed record CurrentMovie(
        int Id,
        int PlexApiRatingKey,
        DateTime? UpdatedAt,
        long MediaSize,
        int MediaDataCount
    );
}

public record CrudMoviesReport
{
    public int CreatedMovies { get; set; }

    public int UpdatedMovies { get; set; }

    public int DeletedMovies { get; set; }

    public int UnchangedMovies { get; set; }

    public override string ToString() =>
        $@"
        CreatedMovies: {CreatedMovies}
        UpdatedMovies: {UpdatedMovies}
        DeletedMovies: {DeletedMovies}
        UnchangedMovies: {UnchangedMovies}";
}
