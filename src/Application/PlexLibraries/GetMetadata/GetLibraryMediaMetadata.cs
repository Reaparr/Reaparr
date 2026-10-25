using FlexQuery.NET.Models;

namespace Reaparr.Application;

public record GetLibraryMediaMetadataRequest
{
    public int PlexLibraryId { get; init; }

    [QueryParam, BindFrom("mediaType")]
    [DefaultValue(PlexMediaType.None)]
    public PlexMediaType MediaType { get; init; }

    [QueryParam, BindFrom("filterOwnedMedia")]
    [DefaultValue(false)]
    public bool? FilterOwnedMedia { get; init; } = false;

    [QueryParam, BindFrom("filterOfflineMedia")]
    [DefaultValue(false)]
    public bool? FilterOfflineMedia { get; init; } = false;
}

public class GetLibraryMediaMetadataRequestValidator : Validator<GetLibraryMediaMetadataRequest>
{
    public GetLibraryMediaMetadataRequestValidator()
    {
        RuleFor(x => x.MediaType)
            .NotEqual(PlexMediaType.None)
            .When(x => x.PlexLibraryId == 0)
            .WithMessage("MediaType must not be 'None' when PlexLibraryId is 0.");
    }
}

public class GetLibraryMediaMetadata : Endpoint<GetLibraryMediaMetadataRequest, PlexMediaMetadataDTO>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetLibraryMediaMetadata(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GetLibraryMediaMetadata>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexLibraryController + "/{PlexLibraryId}/metadata");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexMediaMetadataDTO>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetLibraryMediaMetadataRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        if (req.PlexLibraryId > 0)
        {
            // First, verify the library exists
            var plexLibrary = await _dbContext.PlexLibraries.IgnoreQueryFilters().GetAsync(req.PlexLibraryId, ct);
            if (plexLibrary is null)
            {
                await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId), ct);
                return;
            }

            // A disabled library has no browsable media. Return an empty metadata
            // response so the frontend can show the disabled-library alert instead
            // of treating this as a server error.
            if (!plexLibrary.IsEnabled)
            {
                await Send.FluentResult(
                    Result.Ok(
                        new PlexMediaMetadataDTO
                        {
                            MediaCount = 0,
                            Roles = [],
                            Countries = [],
                            Genres = [],
                            Qualities = [],
                            RoleCount = 0,
                            CountryCount = 0,
                            GenreCount = 0,
                            QualityCount = 0,
                            SupportedSorts = plexLibrary.Type.GetSupportedSorts(),
                        }
                    ),
                    ct
                );
                return;
            }

            // Get actual data efficiently using joins
            var roles = await (
                from la in _dbContext.PlexLibraryActors
                join a in _dbContext.PlexActors on la.PlexActorId equals a.Id
                where la.PlexLibraryId == req.PlexLibraryId
                select new PlexRoleDTO { Id = a.Id, Name = a.Name }
            ).ToListAsync(ct);

            var countries = await (
                from lc in _dbContext.PlexLibraryCountries
                join c in _dbContext.PlexCountries on lc.PlexCountryId equals c.Id
                where lc.PlexLibraryId == req.PlexLibraryId
                select new PlexCountryDTO { Id = c.Id, Name = c.Name }
            ).ToListAsync(ct);

            var isAudioLibrary = req.MediaType.IsMusicType() || plexLibrary.Type.IsMusicType();
            var genres = isAudioLibrary
                ? await (
                    from lg in _dbContext.PlexLibraryGenresAudio
                    join g in _dbContext.PlexGenresAudio on lg.PlexGenreId equals g.Id
                    where lg.PlexLibraryId == req.PlexLibraryId
                    select new PlexGenreDTO { Id = g.Id, Name = g.Name }
                ).ToListAsync(ct)
                : await (
                    from lg in _dbContext.PlexLibraryGenresVideo
                    join g in _dbContext.PlexGenresVideo on lg.PlexGenreId equals g.Id
                    where lg.PlexLibraryId == req.PlexLibraryId
                    select new PlexGenreDTO { Id = g.Id, Name = g.Name }
                ).ToListAsync(ct);

            var uniqueQualities = await GetQualitiesForMediaType(req.MediaType, [req.PlexLibraryId], ct);

            var mediaMetadataDTO = new PlexMediaMetadataDTO
            {
                MediaCount = plexLibrary.MediaCount,
                Roles = roles,
                Countries = countries,
                Genres = genres,
                Qualities = uniqueQualities,
                RoleCount = plexLibrary.ActorsCount,
                CountryCount = plexLibrary.CountriesCount,
                GenreCount = plexLibrary.GenresCount,
                QualityCount = uniqueQualities.Count,
                SupportedSorts = plexLibrary.Type.GetSupportedSorts(),
            };

            await Send.FluentResult(Result.Ok(mediaMetadataDTO), ct);
        }
        else
        {
            var allowedLibraryIds = await _dbContext.ResolveAllowedLibraryIdsAsync(
                new MediaQueryFilter
                {
                    MediaType = req.MediaType,
                    PlexLibraryId = 0,
                    FilterOwnedMedia = req.FilterOwnedMedia ?? false,
                    FilterOfflineMedia = req.FilterOfflineMedia ?? false,
                    Parameters = new FlexQueryParameters(),
                },
                ct
            );
            var mediaCount = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SumAsync(pl => pl.MediaCount, ct);

            // Get counts efficiently for global metadata
            var roleCount = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.Actors)
                .Select(a => a.Id)
                .Distinct()
                .CountAsync(ct);

            var countryCount = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.Countries)
                .Select(c => c.Id)
                .Distinct()
                .CountAsync(ct);

            var genreCount = req.MediaType.IsMusicType()
                ? await _dbContext
                    .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                    .SelectMany(pl => pl.GenresAudio)
                    .Select(g => g.Id)
                    .Distinct()
                    .CountAsync(ct)
                : await _dbContext
                    .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                    .SelectMany(pl => pl.GenresVideo)
                    .Select(g => g.Id)
                    .Distinct()
                    .CountAsync(ct);

            // Get actual unique data efficiently
            var uniqueRoles = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.Actors)
                .Select(a => new PlexRoleDTO { Id = a.Id, Name = a.Name })
                .Distinct()
                .ToListAsync(ct);

            var uniqueCountries = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.Countries)
                .Select(c => new PlexCountryDTO { Id = c.Id, Name = c.Name })
                .Distinct()
                .ToListAsync(ct);

            var uniqueGenres = await GetGenresForLibrariesAsync(req.MediaType, allowedLibraryIds, ct);

            var uniqueQualities = await GetQualitiesForMediaType(req.MediaType, allowedLibraryIds, ct);

            await Send.FluentResult(
                Result.Ok(
                    new PlexMediaMetadataDTO
                    {
                        MediaCount = mediaCount,
                        QualityCount = uniqueQualities.Count,
                        Roles = uniqueRoles,
                        Countries = uniqueCountries,
                        Genres = uniqueGenres,
                        RoleCount = roleCount,
                        CountryCount = countryCount,
                        GenreCount = genreCount,
                        Qualities = uniqueQualities,
                        SupportedSorts = req.MediaType.GetSupportedSorts(),
                    }
                ),
                ct
            );
        }
    }

    private Task<List<PlexGenreDTO>> GetGenresForLibrariesAsync(
        PlexMediaType mediaType,
        IReadOnlyCollection<int> libraryIds,
        CancellationToken ct
    ) =>
        mediaType.IsMusicType()
            ? _dbContext
                .PlexLibraries.Where(pl => libraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.GenresAudio)
                .Select(g => new PlexGenreDTO { Id = g.Id, Name = g.Name })
                .Distinct()
                .ToListAsync(ct)
            : _dbContext
                .PlexLibraries.Where(pl => libraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.GenresVideo)
                .Select(g => new PlexGenreDTO { Id = g.Id, Name = g.Name })
                .Distinct()
                .ToListAsync(ct);

    private async Task<List<PlexQualityDTO>> GetQualitiesForMediaType(
        PlexMediaType mediaType,
        IReadOnlyCollection<int> libraryIds,
        CancellationToken ct
    )
    {
        var uniqueQualities = new List<PlexQualityDTO>();
        if (mediaType == PlexMediaType.Movie)
        {
            uniqueQualities = await _dbContext
                .PlexMovieData.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .GroupBy(x => x.VideoQuality)
                .Select(g => new PlexQualityDTO
                {
                    Name = ((int)g.Key).ToString(),
                    Quality = g.Key,
                    Count = g.Count(),
                })
                .ToListAsync(ct);
        }

        if (mediaType == PlexMediaType.TvShow)
        {
            uniqueQualities = await _dbContext
                .PlexTvShowMediaQualities.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .GroupBy(x => x.VideoQuality)
                .Select(g => new PlexQualityDTO
                {
                    Name = ((int)g.Key).ToString(),
                    Quality = g.Key,
                    Count = g.Count(),
                })
                .ToListAsync(ct);
        }

        if (mediaType == PlexMediaType.OtherVideos)
        {
            uniqueQualities = await _dbContext
                .PlexOtherVideoData.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .GroupBy(x => x.VideoQuality)
                .Select(g => new PlexQualityDTO
                {
                    Name = ((int)g.Key).ToString(),
                    Quality = g.Key,
                    Count = g.Count(),
                })
                .ToListAsync(ct);
        }

        return uniqueQualities;
    }
}
