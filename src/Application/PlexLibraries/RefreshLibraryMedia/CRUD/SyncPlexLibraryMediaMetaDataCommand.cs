using EFCore.BulkExtensions;

namespace Reaparr.Application;

/// <summary>
/// Command to synchronize Plex library media metadata (actors, genres, countries) for a
/// specific Plex library. Changed relation sets are replaced from the provided
/// <paramref name="LibraryMetadata"/> collections; identical sets are left untouched.
/// </summary>
/// <param name="LibraryMetadata">Aggregated metadata for the target Plex library.</param>
/// <returns>Returns the result of the operation.</returns>
public record SyncPlexLibraryMediaMetaDataCommand(InsertMediaMetaDataCommandResponse LibraryMetadata)
    : ICommand<Result>;

public class SyncPlexLibraryMediaMetaDataCommandValidator : Validator<SyncPlexLibraryMediaMetaDataCommand>
{
    public SyncPlexLibraryMediaMetaDataCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.LibraryMetadata.PlexActors).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexGenresVideo).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexGenresAudio).NotNull();
        RuleFor(x => x.LibraryMetadata.PlexCountries).NotNull();
    }
}

public class SyncPlexLibraryMediaMetaDataCommandHandler : ICommandHandler<SyncPlexLibraryMediaMetaDataCommand, Result>
{
    private readonly IReaparrDbContext _dbContext;
    private readonly ILogger _log;

    private readonly BulkConfig? _bulkInsertConfig = new()
    {
        SetOutputIdentity = false,
        PreserveInsertOrder = true,
        UseTempDB = true,
    };

    public SyncPlexLibraryMediaMetaDataCommandHandler(IReaparrDbContext dbContext, ILogger log)
    {
        _dbContext = dbContext;
        _log = log.ForContext<SyncPlexLibraryMediaMetaDataCommandHandler>();
    }

    public async Task<Result> ExecuteAsync(SyncPlexLibraryMediaMetaDataCommand command, CancellationToken ct)
    {
        var libraryId = command.LibraryMetadata.PlexLibraryId;

        _log.Here().Debug("[SyncMetaData] ExecuteAsync entered for libraryId {LibraryId}", libraryId);

        // First, verify the library exists
        _log.Here().Debug("[SyncMetaData] Fetching library {LibraryId} from DB", libraryId);
        var library = await _dbContext.PlexLibraries.GetAsync(libraryId, cancellationToken: ct);
        if (library == null)
            return ResultExtensions.EntityNotFound(nameof(PlexLibrary), libraryId);

        _log.Here().Debug("[SyncMetaData] Library found, fetching name");
        var roles = command.LibraryMetadata.PlexActors;
        var genresVideo = command.LibraryMetadata.PlexGenresVideo;
        var genresAudio = command.LibraryMetadata.PlexGenresAudio;
        var countries = command.LibraryMetadata.PlexCountries;

        var libraryName = await _dbContext.GetPlexLibraryNameById(libraryId);
        _log.Here()
            .Debug(
                "[SyncMetaData] Got library name '{LibraryName}'. Actors={ActorCount}, VideoGenres={VideoGenreCount}, AudioGenres={AudioGenreCount}, Countries={CountryCount}",
                libraryName,
                roles.Count,
                genresVideo.Count,
                genresAudio.Count,
                countries.Count
            );

        _log.Here().Debug("[SyncMetaData] Starting SyncGenresVideo");
        var syncGenresVideoResult = await SyncGenresVideo(genresVideo, libraryId, libraryName, ct);
        _log.Here().Debug("[SyncMetaData] SyncGenresVideo done. IsFailed={IsFailed}", syncGenresVideoResult.IsFailed);

        _log.Here().Debug("[SyncMetaData] Starting SyncGenresAudio");
        var syncGenresAudioResult = await SyncGenresAudio(genresAudio, libraryId, libraryName, ct);
        _log.Here().Debug("[SyncMetaData] SyncGenresAudio done. IsFailed={IsFailed}", syncGenresAudioResult.IsFailed);

        _log.Here().Debug("[SyncMetaData] Starting SyncCountries");
        var syncCountriesResult = await SyncCountries(countries, libraryId, libraryName, ct);
        _log.Here().Debug("[SyncMetaData] SyncCountries done. IsFailed={IsFailed}", syncCountriesResult.IsFailed);

        _log.Here().Debug("[SyncMetaData] Starting SyncRoles");
        var syncRolesResult = await SyncRoles(roles, libraryId, libraryName, ct);
        _log.Here().Debug("[SyncMetaData] SyncRoles done. IsFailed={IsFailed}", syncRolesResult.IsFailed);

        var syncResult = Result
            .Merge(syncGenresVideoResult, syncGenresAudioResult, syncCountriesResult, syncRolesResult)
            .ToResult();
        if (syncResult.IsCancelledOrFailed)
            return syncResult.LogIfFailed();

        var actorsCount = syncRolesResult.Value;
        var genresCount = syncGenresVideoResult.Value + syncGenresAudioResult.Value;
        var countriesCount = syncCountriesResult.Value;

        if (
            library.ActorsCount != actorsCount
            || library.GenresCount != genresCount
            || library.CountriesCount != countriesCount
        )
        {
            _log.Here()
                .Debug(
                    "[SyncMetaData] Calling SetLibraryMetaData: actors={ActorCount}, genres={GenreCount}, countries={CountryCount}",
                    actorsCount,
                    genresCount,
                    countriesCount
                );
            await _dbContext.SetLibraryMetaData(libraryId, actorsCount, genresCount, countriesCount);
            _log.Here().Debug("[SyncMetaData] SetLibraryMetaData done");
        }

        return syncResult;
    }

    private async Task<Result<int>> SyncRoles(
        Dictionary<string, PlexActor> sourceDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();

        _log.Here().Debug("Started syncing {Count} roles for library {LibraryName}", sourceDict.Count, libraryName);

        var currentIds = await _dbContext
            .PlexLibraryActors.Where(x => x.PlexLibraryId == libraryId)
            .Select(x => x.PlexActorId)
            .ToListAsync(ct);
        if (sourceDict.Values.Select(x => x.Id).ToHashSet().SetEquals(currentIds))
            return Result.Ok(sourceDict.Count);

        if (!sourceDict.Any())
        {
            _log.Here()
                .Warning(
                    "No {NameOfPlexActor} relations were given to be inserted for library {LibraryName} with {LibraryId}, all current {NameOfPlexActor2} relations will be dropped",
                    nameof(PlexActor),
                    libraryName,
                    libraryId,
                    nameof(PlexActor)
                );

            await _dbContext.PlexLibraryActors.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(ct);
            stopWatch.Stop();

            _log.Here()
                .Debug(
                    "Finished dropping all {NameOfPlexActor} relations for library {LibraryName} in {ElapsedSeconds:F2} seconds",
                    nameof(PlexActor),
                    libraryName,
                    stopWatch.Elapsed.TotalSeconds
                );

            return Result.Ok(0);
        }

        // Reinsert actors for the library
        var newActors = sourceDict
            .Select(x => new PlexLibraryActors(libraryId: libraryId, plexActorId: x.Value.Id))
            .ToList();

        _log.Here().Debug("[SyncMetaData] BulkInsertAsync actors starting ({Count} rows)", newActors.Count);
        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexLibraryActors.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(newActors, _bulkInsertConfig, txCt);
            },
            ct
        );
        _log.Here().Debug("[SyncMetaData] BulkInsertAsync actors done. IsFailed={IsFailed}", insertResult.IsFailed);

        stopWatch.Stop();

        if (insertResult.IsCancelledOrFailed)
            return insertResult.LogIfFailed();

        _log.Here()
            .Debug(
                "Finished creating {Count} {NameOfPlexActor} relations for library {LibraryName} in {ElapsedSeconds:F2} seconds",
                newActors.Count,
                nameof(PlexActor),
                libraryName,
                stopWatch.Elapsed.TotalSeconds
            );
        return Result.Ok(newActors.Count);
    }

    private async Task<Result<int>> SyncGenresVideo(
        Dictionary<string, PlexGenreVideo> sourceDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();
        _log.Here()
            .Debug("Started syncing {Count} video genres for library {LibraryName}", sourceDict.Count, libraryName);

        var currentIds = await _dbContext
            .PlexLibraryGenresVideo.Where(x => x.PlexLibraryId == libraryId)
            .Select(x => x.PlexGenreId)
            .ToListAsync(ct);
        if (sourceDict.Values.Select(x => x.Id).ToHashSet().SetEquals(currentIds))
            return Result.Ok(sourceDict.Count);

        var newGenres = sourceDict.Select(x => new PlexLibraryGenresVideo(libraryId, x.Value.Id)).ToList();
        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexLibraryGenresVideo.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(newGenres, _bulkInsertConfig, txCt);
            },
            ct
        );

        stopWatch.Stop();
        if (insertResult.IsCancelledOrFailed)
            return insertResult.LogIfFailed();

        _log.Here()
            .Debug(
                "Finished creating {Count} video genre relations for library {LibraryName} in {ElapsedSeconds:F2} seconds",
                newGenres.Count,
                libraryName,
                stopWatch.Elapsed.TotalSeconds
            );
        return Result.Ok(newGenres.Count);
    }

    private async Task<Result<int>> SyncGenresAudio(
        Dictionary<string, PlexGenreAudio> sourceDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();
        _log.Here()
            .Debug("Started syncing {Count} audio genres for library {LibraryName}", sourceDict.Count, libraryName);

        var currentIds = await _dbContext
            .PlexLibraryGenresAudio.Where(x => x.PlexLibraryId == libraryId)
            .Select(x => x.PlexGenreId)
            .ToListAsync(ct);
        if (sourceDict.Values.Select(x => x.Id).ToHashSet().SetEquals(currentIds))
            return Result.Ok(sourceDict.Count);

        var newGenres = sourceDict.Select(x => new PlexLibraryGenresAudio(libraryId, x.Value.Id)).ToList();
        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexLibraryGenresAudio.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(newGenres, _bulkInsertConfig, txCt);
            },
            ct
        );

        stopWatch.Stop();
        if (insertResult.IsCancelledOrFailed)
            return insertResult.LogIfFailed();

        _log.Here()
            .Debug(
                "Finished creating {Count} audio genre relations for library {LibraryName} in {ElapsedSeconds:F2} seconds",
                newGenres.Count,
                libraryName,
                stopWatch.Elapsed.TotalSeconds
            );
        return Result.Ok(newGenres.Count);
    }

    private async Task<Result<int>> SyncCountries(
        Dictionary<string, PlexCountry> sourceDict,
        int libraryId,
        string libraryName,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();

        _log.Here().Debug("Started syncing {Count} countries for library {LibraryName}", sourceDict.Count, libraryName);

        var currentIds = await _dbContext
            .PlexLibraryCountries.Where(x => x.PlexLibraryId == libraryId)
            .Select(x => x.PlexCountryId)
            .ToListAsync(ct);
        if (sourceDict.Values.Select(x => x.Id).ToHashSet().SetEquals(currentIds))
            return Result.Ok(sourceDict.Count);

        if (!sourceDict.Any())
        {
            _log.Here()
                .Warning(
                    "No {NameOfPlexCountry} relations were given to be inserted for library {LibraryName} with {LibraryId}, all current {NameOfPlexCountry2} relations will be dropped",
                    nameof(PlexCountry),
                    libraryName,
                    libraryId,
                    nameof(PlexCountry)
                );
            await _dbContext.PlexLibraryCountries.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(ct);
            stopWatch.Stop();

            _log.Here()
                .Debug(
                    "Finished dropping all {NameOfPlexCountry} relations for library {LibraryName} in {ElapsedSeconds:F2} seconds",
                    nameof(PlexCountry),
                    libraryName,
                    stopWatch.Elapsed.TotalSeconds
                );
            return Result.Ok(0);
        }

        // Reinsert countries for the library
        var newCountries = sourceDict.Select(x => new PlexLibraryCountries(libraryId, x.Value.Id)).ToList();
        _log.Here().Debug("[SyncMetaData] BulkInsertAsync countries starting ({Count} rows)", newCountries.Count);
        var insertResult = await _dbContext.ExecuteTransactionAsync(
            async (ctx, txCt) =>
            {
                await ctx.PlexLibraryCountries.Where(x => x.PlexLibraryId == libraryId).ExecuteDeleteAsync(txCt);
                await ctx.BulkInsertAsync(newCountries, _bulkInsertConfig, txCt);
            },
            ct
        );
        _log.Here().Debug("[SyncMetaData] BulkInsertAsync countries done. IsFailed={IsFailed}", insertResult.IsFailed);

        stopWatch.Stop();

        if (insertResult.IsCancelledOrFailed)
            return insertResult.LogIfFailed();

        _log.Here()
            .Debug(
                "Finished creating {Count} {NameOfPlexCountry} relations for library {LibraryName} in {ElapsedSeconds:F2} seconds",
                newCountries.Count,
                nameof(PlexCountry),
                libraryName,
                stopWatch.Elapsed.TotalSeconds
            );
        return Result.Ok(newCountries.Count);
    }
}
