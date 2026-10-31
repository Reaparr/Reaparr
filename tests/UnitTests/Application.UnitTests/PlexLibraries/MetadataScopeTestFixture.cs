namespace Reaparr.Application.UnitTests;

internal sealed record VideoMetadataRows(
    IReadOnlyList<PlexActor> Actors,
    IReadOnlyList<PlexCountry> Countries,
    IReadOnlyList<PlexGenreVideo> Genres
);

internal sealed class MetadataScopeTestFixture
{
    private readonly IReaparrDbContext _dbContext;
    private readonly CancellationToken _cancellationToken;

    public MetadataScopeTestFixture(IReaparrDbContext dbContext, CancellationToken cancellationToken)
    {
        _dbContext = dbContext;
        _cancellationToken = cancellationToken;
    }

    public static void ConfigureVideoSeed(FakeDataConfig config, PlexMediaType mediaType, int serverCount)
    {
        config.PlexAccountCount = 1;
        config.PlexServerCount = serverCount;
        switch (mediaType)
        {
            case PlexMediaType.Movie:
                config.PlexMovieLibraryCount = 1;
                config.MovieCount = 1;
                break;
            case PlexMediaType.TvShow:
                config.PlexTvShowLibraryCount = 1;
                config.TvShowCount = 1;
                config.TvShowSeasonCount = 1;
                config.TvShowEpisodeCount = 1;
                break;
            case PlexMediaType.OtherVideos:
                config.PlexOtherVideoLibraryCount = 1;
                config.OtherVideoCount = 1;
                break;
        }
    }

    public static void ConfigureNoAccountVideoSeed(FakeDataConfig config)
    {
        config.PlexServerCount = 2;
        config.PlexMovieLibraryCount = 1;
        config.MovieCount = 1;
    }

    public async Task<VideoMetadataRows> AddVideoMetadataAsync(IReadOnlyList<PlexLibrary> libraries, string keyPrefix)
    {
        var actors = libraries
            .Select(
                (library, index) =>
                    new PlexActor { Key = $"{keyPrefix}-role-{library.Id}", Name = $"{keyPrefix} role {index}" }
            )
            .ToArray();
        var countries = libraries
            .Select(
                (library, index) =>
                    new PlexCountry { Key = $"{keyPrefix}-country-{library.Id}", Name = $"{keyPrefix} country {index}" }
            )
            .ToArray();
        var genres = libraries
            .Select(
                (library, index) =>
                    new PlexGenreVideo
                    {
                        Key = $"{keyPrefix}-genre-{library.Id}",
                        Name = $"{keyPrefix} genre {index}",
                        Type = PlexGenreType.Drama,
                    }
            )
            .ToArray();

        _dbContext.PlexActors.AddRange(actors);
        _dbContext.PlexCountries.AddRange(countries);
        _dbContext.PlexGenresVideo.AddRange(genres);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        _dbContext.PlexLibraryActors.AddRange(
            libraries.Select((library, index) => new PlexLibraryActors(library.Id, actors[index].Id))
        );
        _dbContext.PlexLibraryCountries.AddRange(
            libraries.Select((library, index) => new PlexLibraryCountries(library.Id, countries[index].Id))
        );
        _dbContext.PlexLibraryGenresVideo.AddRange(
            libraries.Select((library, index) => new PlexLibraryGenresVideo(library.Id, genres[index].Id))
        );
        await _dbContext.SaveChangesAsync(_cancellationToken);
        return new VideoMetadataRows(actors, countries, genres);
    }

    public async Task SetVideoQualitiesAsync(
        PlexMediaType mediaType,
        IReadOnlyList<PlexLibrary> libraries,
        IReadOnlyList<VideoQuality> qualities
    )
    {
        foreach (var (library, index) in libraries.Select((library, index) => (library, index)))
        {
            switch (mediaType)
            {
                case PlexMediaType.Movie:
                    await _dbContext
                        .PlexMovieData.Where(x => x.PlexLibraryId == library.Id)
                        .ExecuteUpdateAsync(
                            x => x.SetProperty(y => y.VideoQuality, qualities[index]),
                            _cancellationToken
                        );
                    break;
                case PlexMediaType.TvShow:
                    await _dbContext
                        .PlexTvShowMediaQualities.Where(x => x.PlexLibraryId == library.Id)
                        .ExecuteUpdateAsync(
                            x => x.SetProperty(y => y.VideoQuality, qualities[index]),
                            _cancellationToken
                        );
                    break;
                case PlexMediaType.OtherVideos:
                    await _dbContext
                        .PlexOtherVideoData.Where(x => x.PlexLibraryId == library.Id)
                        .ExecuteUpdateAsync(
                            x => x.SetProperty(y => y.VideoQuality, qualities[index]),
                            _cancellationToken
                        );
                    break;
            }
        }
    }

    public async Task RevokeAndDisableLibrariesAsync(
        PlexAccount account,
        PlexLibrary serverRevokedLibrary,
        PlexLibrary libraryRevokedLibrary,
        PlexLibrary disabledLibrary
    )
    {
        _dbContext.PlexAccountServers.RemoveRange(
            await _dbContext
                .PlexAccountServers.Where(x =>
                    x.PlexAccountId == account.Id && x.PlexServerId == serverRevokedLibrary.PlexServerId
                )
                .ToListAsync(_cancellationToken)
        );
        _dbContext.PlexAccountLibraries.RemoveRange(
            await _dbContext
                .PlexAccountLibraries.Where(x =>
                    x.PlexAccountId == account.Id && x.PlexLibraryId == libraryRevokedLibrary.Id
                )
                .ToListAsync(_cancellationToken)
        );
        _dbContext.PlexLibraries.Attach(disabledLibrary);
        disabledLibrary.IsEnabled = false;
        await _dbContext.SaveChangesAsync(_cancellationToken);
    }

    public async Task ConfigureOwnedOfflineLibrariesAsync(PlexAccount account, IReadOnlyList<PlexLibrary> libraries)
    {
        var accountServers = await _dbContext
            .PlexAccountServers.Where(x => x.PlexAccountId == account.Id)
            .AsTracking()
            .ToListAsync(_cancellationToken);
        var accountLibraries = await _dbContext
            .PlexAccountLibraries.Where(x => x.PlexAccountId == account.Id)
            .AsTracking()
            .ToListAsync(_cancellationToken);
        foreach (var accountServer in accountServers.Where(x => x.PlexServerId != libraries[0].PlexServerId))
            accountServer.IsServerOwned = false;
        foreach (var accountLibrary in accountLibraries.Where(x => x.PlexLibraryId != libraries[0].Id))
            accountLibrary.IsLibraryOwned = false;
        await _dbContext.SaveChangesAsync(_cancellationToken);
        var offlineServerId = libraries[^1].PlexServerId;
        await _dbContext
            .PlexServerStatuses.Where(x => x.PlexServerId == offlineServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, false), _cancellationToken);
    }

    public async Task<IReadOnlyList<PlexGenreAudio>> AddAudioGenresAsync(
        IReadOnlyList<PlexLibrary> libraries,
        string keyPrefix
    )
    {
        var genres = libraries
            .Select(
                (library, index) =>
                    new PlexGenreAudio { Key = $"{keyPrefix}-genre-{library.Id}", Name = $"{keyPrefix} genre {index}" }
            )
            .ToArray();
        _dbContext.PlexGenresAudio.AddRange(genres);
        await _dbContext.SaveChangesAsync(_cancellationToken);
        _dbContext.PlexLibraryGenresAudio.AddRange(
            libraries.Select((library, index) => new PlexLibraryGenresAudio(library.Id, genres[index].Id))
        );
        await _dbContext.SaveChangesAsync(_cancellationToken);
        return genres;
    }

    public async Task DisableLibraryAsync(PlexLibrary library)
    {
        _dbContext.PlexLibraries.Attach(library);
        library.IsEnabled = false;
        await _dbContext.SaveChangesAsync(_cancellationToken);
    }

    public async Task RevokeLibraryAccessAsync(PlexAccount account, PlexLibrary library)
    {
        _dbContext.PlexAccountLibraries.RemoveRange(
            await _dbContext
                .PlexAccountLibraries.Where(x => x.PlexAccountId == account.Id && x.PlexLibraryId == library.Id)
                .ToListAsync(_cancellationToken)
        );
        await _dbContext.SaveChangesAsync(_cancellationToken);
    }
}
