namespace Reaparr.Application;

public record GetMetadataFilterRequest
{
    public int PlexLibraryId { get; init; }

    [QueryParam, BindFrom("mediaType")]
    [DefaultValue(PlexMediaType.None)]
    public PlexMediaType MediaType { get; init; }
}

public class GetMetadataFilterRequestValidator : Validator<GetMetadataFilterRequest>
{
    public GetMetadataFilterRequestValidator()
    {
        RuleFor(x => x.MediaType)
            .NotEqual(PlexMediaType.None)
            .When(x => x.PlexLibraryId == 0)
            .WithMessage("MediaType must not be 'None' when PlexLibraryId is 0.");
    }
}

public class GetMetadataFilter : Endpoint<GetMetadataFilterRequest, ResultDTO<PlexMediaFilterMetadataDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;

    public GetMetadataFilter(ILogger log, IReaparrDbContext dbContext)
    {
        _log = log.ForContext<GetMetadataFilter>();
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexLibraryController + "/{PlexLibraryId}/metadata-filter");
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<PlexMediaFilterMetadataDTO>))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetMetadataFilterRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        if (req.PlexLibraryId > 0)
        {
            var plexLibrary = await _dbContext.PlexLibraries.IgnoreQueryFilters().GetAsync(req.PlexLibraryId, ct);
            if (plexLibrary is null)
            {
                await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexLibrary), req.PlexLibraryId), ct);
                return;
            }

            // A disabled library has no browsable media. Return an empty filter
            // response so the frontend can show the disabled-library alert instead
            // of treating this as a server error.
            if (!plexLibrary.IsEnabled)
            {
                await Send.FluentResult(
                    Result.Ok(
                        new PlexMediaFilterMetadataDTO
                        {
                            Roles = [],
                            Countries = [],
                            Genres = [],
                            Qualities = [],
                        }
                    ),
                    ct
                );
                return;
            }

            var roles = await _dbContext
                .PlexLibraryActors.Where(x => x.PlexLibraryId == req.PlexLibraryId)
                .Select(x => x.PlexActorId)
                .Distinct()
                .ToListAsync(ct);

            var countries = await _dbContext
                .PlexLibraryCountries.Where(x => x.PlexLibraryId == req.PlexLibraryId)
                .Select(x => x.PlexCountryId)
                .Distinct()
                .ToListAsync(ct);

            var genres = await _dbContext
                .PlexLibraryGenres.Where(x => x.PlexLibraryId == req.PlexLibraryId)
                .Select(x => x.PlexGenreId)
                .Distinct()
                .ToListAsync(ct);

            var qualities = await GetQualitiesForMediaType(req.MediaType, req.PlexLibraryId, ct);

            await Send.FluentResult(
                Result.Ok(
                    new PlexMediaFilterMetadataDTO
                    {
                        Roles = roles,
                        Countries = countries,
                        Genres = genres,
                        Qualities = qualities,
                    }
                ),
                ct
            );
        }
        else
        {
            var roles = await _dbContext
                .PlexLibraries.Where(pl => pl.Type == req.MediaType)
                .SelectMany(pl => pl.Actors)
                .Select(a => a.Id)
                .Distinct()
                .ToListAsync(ct);

            var countries = await _dbContext
                .PlexLibraries.Where(pl => pl.Type == req.MediaType)
                .SelectMany(pl => pl.Countries)
                .Select(c => c.Id)
                .Distinct()
                .ToListAsync(ct);

            var genres = await _dbContext
                .PlexLibraries.Where(pl => pl.Type == req.MediaType)
                .SelectMany(pl => pl.Genres)
                .Select(g => g.Id)
                .Distinct()
                .ToListAsync(ct);

            var qualities = await GetQualitiesForMediaType(req.MediaType, ct: ct);

            await Send.FluentResult(
                Result.Ok(
                    new PlexMediaFilterMetadataDTO
                    {
                        Roles = roles,
                        Countries = countries,
                        Genres = genres,
                        Qualities = qualities,
                    }
                ),
                ct
            );
        }
    }

    private async Task<List<int>> GetQualitiesForMediaType(
        PlexMediaType mediaType,
        int plexLibraryId = 0,
        CancellationToken ct = default
    )
    {
        if (mediaType == PlexMediaType.Movie)
        {
            var qualities = await _dbContext
                .PlexMovieData.ApplyWhere(plexLibraryId > 0, x => x.PlexLibraryId == plexLibraryId)
                .GroupBy(x => x.Quality)
                .Select(g => g.Key)
                .ToListAsync(ct);

            return qualities.Select(x => x.ToId()).ToList();
        }

        if (mediaType == PlexMediaType.TvShow)
        {
            var qualities = await _dbContext
                .PlexTvShowMediaQualities.ApplyWhere(plexLibraryId > 0, x => x.PlexLibraryId == plexLibraryId)
                .GroupBy(x => x.Quality)
                .Select(g => g.Key)
                .ToListAsync(ct);

            return qualities.Select(x => x.ToId()).ToList();
        }

        if (mediaType == PlexMediaType.OtherVideos)
        {
            var qualities = await _dbContext
                .PlexOtherVideoData.ApplyWhere(plexLibraryId > 0, x => x.PlexLibraryId == plexLibraryId)
                .GroupBy(x => x.Quality)
                .Select(g => g.Key)
                .ToListAsync(ct);

            return qualities.Select(x => x.ToId()).ToList();
        }

        return [];
    }
}
