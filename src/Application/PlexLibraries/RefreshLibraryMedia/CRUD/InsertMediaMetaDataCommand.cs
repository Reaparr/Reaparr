namespace Reaparr.Application;

/// <summary>
/// Command to insert or update media metadata (actors, genres, and countries) for a <see cref="PlexLibrary"/> into the database.
/// This operation ensures all metadata entities exist with proper database IDs, enabling media items to be linked to their associated metadata.
/// The command performs bulk upsert operations for optimal performance and returns dictionaries of inserted entities keyed by their Plex IDs.
/// </summary>
/// <param name="LibraryMetadata">The library metadata containing collections of actors, genres, and countries to be inserted or updated in the database</param>
public record InsertMediaMetaDataCommand(LibraryMetadata LibraryMetadata)
    : ICommand<Result<InsertMediaMetaDataCommandResponse>>;

public class InsertMediaMetaDataCommandValidator : Validator<InsertMediaMetaDataCommand>
{
    public InsertMediaMetaDataCommandValidator()
    {
        RuleFor(x => x.LibraryMetadata).NotNull();
    }
}

public record InsertMediaMetaDataCommandResponse
{
    [SetsRequiredMembers]
    public InsertMediaMetaDataCommandResponse(PlexLibrary plexLibrary)
    {
        PlexLibrary = plexLibrary;
    }

    public PlexLibrary PlexLibrary { get; private set; }

    public int PlexLibraryId => PlexLibrary.Id;

    public int PhotoClipCount { get; set; }

    /// <summary>
    /// The string key is the hashkey of the actor name for this <see cref="PlexLibrary"/>
    /// </summary>
    public required Dictionary<string, PlexActor> PlexActors { get; init; } = new();

    /// <summary>
    /// The string key is the hashkey of each video genre name for this <see cref="PlexLibrary"/>.
    /// </summary>
    public required Dictionary<string, PlexGenreVideo> PlexGenresVideo { get; init; } = new();

    /// <summary>
    /// The string key is the hashkey of each audio genre name for this <see cref="PlexLibrary"/>.
    /// </summary>
    public required Dictionary<string, PlexGenreAudio> PlexGenresAudio { get; init; } = new();

    /// <summary>
    /// The string key is the hashkey of the country name for this <see cref="PlexLibrary"/>
    /// </summary>
    public required Dictionary<string, PlexCountry> PlexCountries { get; init; } = new();
}

public class InsertMediaMetaDataCommandHandler
    : ICommandHandler<InsertMediaMetaDataCommand, Result<InsertMediaMetaDataCommandResponse>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public InsertMediaMetaDataCommandHandler(IReaparrDbContext dbContext, ILogger log)
    {
        _dbContext = dbContext;
        _log = log.ForContext<InsertMediaMetaDataCommandHandler>();
    }

    public async Task<Result<InsertMediaMetaDataCommandResponse>> ExecuteAsync(
        InsertMediaMetaDataCommand command,
        CancellationToken ct
    )
    {
        var roles = command.LibraryMetadata.Actors;
        var genresVideo = command.LibraryMetadata.GenresVideo;
        var genresAudio = command.LibraryMetadata.GenresAudio;
        var countries = command.LibraryMetadata.Countries;

        _log.Here().Debug("[InsertMetaData] Starting InsertGenresVideo ({Count} items)", genresVideo.Count);
        var syncGenresVideoResult = await InsertGenresVideo(genresVideo, ct);
        _log.Here()
            .Debug("[InsertMetaData] InsertGenresVideo done. IsFailed={IsFailed}", syncGenresVideoResult.IsFailed);
        if (syncGenresVideoResult.IsFailed)
            return syncGenresVideoResult.ToResult();

        _log.Here().Debug("[InsertMetaData] Starting InsertGenresAudio ({Count} items)", genresAudio.Count);
        var syncGenresAudioResult = await InsertGenresAudio(genresAudio, ct);
        _log.Here()
            .Debug("[InsertMetaData] InsertGenresAudio done. IsFailed={IsFailed}", syncGenresAudioResult.IsFailed);
        if (syncGenresAudioResult.IsFailed)
            return syncGenresAudioResult.ToResult();

        _log.Here().Debug("[InsertMetaData] Starting InsertCountries ({Count} items)", countries.Count);
        var syncCountriesResult = await InsertCountries(countries, ct);
        _log.Here().Debug("[InsertMetaData] InsertCountries done. IsFailed={IsFailed}", syncCountriesResult.IsFailed);
        if (syncCountriesResult.IsFailed)
            return syncCountriesResult.ToResult();

        _log.Here().Debug("[InsertMetaData] Starting InsertPlexActors ({Count} items)", roles.Count);
        var syncRolesResult = await InsertPlexActors(roles, ct);
        _log.Here().Debug("[InsertMetaData] InsertPlexActors done. IsFailed={IsFailed}", syncRolesResult.IsFailed);
        if (syncRolesResult.IsFailed)
            return syncRolesResult.ToResult();

        _log.Here().Debug("[InsertMetaData] Building response object");
        return Result.Ok(
            new InsertMediaMetaDataCommandResponse(command.LibraryMetadata.Library)
            {
                PlexActors = syncRolesResult.Value,
                PlexGenresVideo = syncGenresVideoResult.Value,
                PlexGenresAudio = syncGenresAudioResult.Value,
                PlexCountries = syncCountriesResult.Value,
                PhotoClipCount = command.LibraryMetadata.PhotoClipCount,
            }
        );
    }

    private async Task<Result<Dictionary<string, PlexActor>>> InsertPlexActors(
        IReadOnlyCollection<PlexActor> sourceList,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();

        _log.Here()
            .Debug(
                "Started inserting {RawCount} {NameOfPlexActor} (pre-filtered/grouped)",
                sourceList.Count,
                nameof(PlexActor)
            );

        var newPlexActors = sourceList
            .Where(x => !string.IsNullOrEmpty(x.Key) && !string.IsNullOrEmpty(x.Name))
            .DistinctBy(x => x.Key)
            .ToList();
        if (newPlexActors.Count == 0)
        {
            _log.Here().Debug("No {PlexActorName} to insert", nameof(PlexActor));
            return Result.Ok(new Dictionary<string, PlexActor>());
        }

        _log.Here()
            .Debug("Inserting {Count} {NameOfPlexActor} after deduplication", newPlexActors.Count, nameof(PlexActor));

        var existingIdByKey = await _dbContext.InsertOrIgnorePlexActorsAsync(newPlexActors, ct);

        _log.Here()
            .Debug(
                "Upserted {NameOfPlexActor}, now building result dictionary with {TotalCount} total",
                nameof(PlexActor),
                existingIdByKey.Count
            );

        // Build result dictionary from merged Id map — avoids re-fetching all actors from DB
        var result = new Dictionary<string, PlexActor>(newPlexActors.Count);
        foreach (var actor in newPlexActors)
        {
            if (!existingIdByKey.TryGetValue(actor.Key, out var id))
            {
                _log.Here()
                    .Warning(
                        "{NameOfPlexActor} with key {ActorKey} not found in DB after insert (sourceList count: {SourceCount}); skipping",
                        nameof(PlexActor),
                        actor.Key,
                        sourceList.Count
                    );
                continue;
            }

            actor.Id = id;
            result[actor.Key] = actor;
        }

        stopWatch.StopAndLog($"Finished inserting {newPlexActors.Count} {nameof(PlexActor)} for library");

        return Result.Ok(result);
    }

    private async Task<Result<Dictionary<string, PlexGenreVideo>>> InsertGenresVideo(
        IReadOnlyCollection<PlexGenreVideo> sourceList,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();
        _log.Here().Debug("Started inserting {RawCount} {GenreType} genres", sourceList.Count, nameof(PlexGenreVideo));

        var newGenres = sourceList
            .Where(x => !string.IsNullOrEmpty(x.Key) && !string.IsNullOrEmpty(x.Name))
            .DistinctBy(x => x.Key)
            .ToList();
        if (newGenres.Count == 0)
        {
            _log.Here().Debug("No {GenreType} genres to insert", nameof(PlexGenreVideo));
            return Result.Ok(new Dictionary<string, PlexGenreVideo>());
        }

        var existingIdByKey = await _dbContext.InsertOrIgnorePlexGenresVideoAsync(newGenres, ct);
        var result = new Dictionary<string, PlexGenreVideo>(newGenres.Count);
        foreach (var genre in newGenres)
        {
            if (!existingIdByKey.TryGetValue(genre.Key, out var id))
            {
                _log.Here()
                    .Warning(
                        "{GenreType} with key {GenreKey} not found in DB after insert; skipping",
                        nameof(PlexGenreVideo),
                        genre.Key
                    );
                continue;
            }

            genre.Id = id;
            result[genre.Key] = genre;
        }

        stopWatch.StopAndLog($"Finished inserting {newGenres.Count} {nameof(PlexGenreVideo)}");
        return Result.Ok(result);
    }

    private async Task<Result<Dictionary<string, PlexGenreAudio>>> InsertGenresAudio(
        IReadOnlyCollection<PlexGenreAudio> sourceList,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();
        _log.Here().Debug("Started inserting {RawCount} {GenreType} genres", sourceList.Count, nameof(PlexGenreAudio));

        var newGenres = sourceList
            .Where(x => !string.IsNullOrEmpty(x.Key) && !string.IsNullOrEmpty(x.Name))
            .DistinctBy(x => x.Key)
            .ToList();
        if (newGenres.Count == 0)
        {
            _log.Here().Debug("No {GenreType} genres to insert", nameof(PlexGenreAudio));
            return Result.Ok(new Dictionary<string, PlexGenreAudio>());
        }

        var existingIdByKey = await _dbContext.InsertOrIgnorePlexGenresAudioAsync(newGenres, ct);
        var result = new Dictionary<string, PlexGenreAudio>(newGenres.Count);
        foreach (var genre in newGenres)
        {
            if (!existingIdByKey.TryGetValue(genre.Key, out var id))
            {
                _log.Here()
                    .Warning(
                        "{GenreType} with key {GenreKey} not found in DB after insert; skipping",
                        nameof(PlexGenreAudio),
                        genre.Key
                    );
                continue;
            }

            genre.Id = id;
            result[genre.Key] = genre;
        }

        stopWatch.StopAndLog($"Finished inserting {newGenres.Count} {nameof(PlexGenreAudio)}");
        return Result.Ok(result);
    }

    private async Task<Result<Dictionary<string, PlexCountry>>> InsertCountries(
        IReadOnlyCollection<PlexCountry> sourceList,
        CancellationToken ct
    )
    {
        var stopWatch = Stopwatch.StartNew();

        _log.Here()
            .Debug(
                "Started inserting {RawCount} {NameOfPlexCountry} (pre-filtered/grouped)",
                sourceList.Count,
                nameof(PlexCountry)
            );

        var newPlexCountries = sourceList
            .Where(x => !string.IsNullOrEmpty(x.Key) && !string.IsNullOrEmpty(x.Name))
            .DistinctBy(x => x.Key)
            .ToList();
        if (newPlexCountries.Count == 0)
        {
            _log.Here().Debug("No {NameOfPlexCountry} to insert", nameof(PlexCountry));
            return Result.Ok(new Dictionary<string, PlexCountry>());
        }

        _log.Here()
            .Debug(
                "Inserting {Count} {NameOfPlexCountry} after deduplication",
                newPlexCountries.Count,
                nameof(PlexCountry)
            );

        var existingIdByKey = await _dbContext.InsertOrIgnorePlexCountriesAsync(newPlexCountries, ct);

        // Build result dictionary from merged Id map — avoids re-fetching all countries from DB
        var result = new Dictionary<string, PlexCountry>(newPlexCountries.Count);
        foreach (var country in newPlexCountries)
        {
            if (!existingIdByKey.TryGetValue(country.Key, out var id))
            {
                _log.Here()
                    .Warning(
                        "{NameOfPlexCountry} with key {CountryKey} not found in DB after insert; skipping",
                        nameof(PlexCountry),
                        country.Key
                    );
                continue;
            }

            country.Id = id;
            result[country.Key] = country;
        }

        stopWatch.StopAndLog($"Finished inserting {newPlexCountries.Count} {nameof(PlexCountry)}");

        return Result.Ok(result);
    }
}
