using FlexQuery.NET.Models;

namespace Reaparr.Application;

public record GetMetadataFilterRequest
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

            var isAudioLibrary = plexLibrary.Type.IsMusicType();
            var genres = isAudioLibrary
                ? await _dbContext
                    .PlexLibraryGenresAudio.Where(x => x.PlexLibraryId == req.PlexLibraryId)
                    .Select(x => x.PlexGenreId)
                    .Distinct()
                    .ToListAsync(ct)
                : await _dbContext
                    .PlexLibraryGenresVideo.Where(x => x.PlexLibraryId == req.PlexLibraryId)
                    .Select(x => x.PlexGenreId)
                    .Distinct()
                    .ToListAsync(ct);

            var qualities = await GetQualitiesForMediaType(req.MediaType, [req.PlexLibraryId], ct);

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
            var roles = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.Actors)
                .Select(a => a.Id)
                .Distinct()
                .ToListAsync(ct);

            var countries = await _dbContext
                .PlexLibraries.Where(pl => allowedLibraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.Countries)
                .Select(c => c.Id)
                .Distinct()
                .ToListAsync(ct);

            var genres = await GetGenreIdsForLibrariesAsync(req.MediaType, allowedLibraryIds, ct);

            var qualities = await GetQualitiesForMediaType(req.MediaType, allowedLibraryIds, ct);

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

    private Task<List<int>> GetGenreIdsForLibrariesAsync(
        PlexMediaType mediaType,
        IReadOnlyCollection<int> libraryIds,
        CancellationToken ct
    ) =>
        mediaType.IsMusicType()
            ? _dbContext
                .PlexLibraries.Where(pl => libraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.GenresAudio)
                .Select(g => g.Id)
                .Distinct()
                .ToListAsync(ct)
            : _dbContext
                .PlexLibraries.Where(pl => libraryIds.Contains(pl.Id))
                .SelectMany(pl => pl.GenresVideo)
                .Select(g => g.Id)
                .Distinct()
                .ToListAsync(ct);

    private async Task<List<int>> GetQualitiesForMediaType(
        PlexMediaType mediaType,
        IReadOnlyCollection<int> libraryIds,
        CancellationToken ct
    )
    {
        if (mediaType == PlexMediaType.Movie)
        {
            var qualities = await _dbContext
                .PlexMovieData.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .GroupBy(x => x.VideoQuality)
                .Select(g => g.Key)
                .ToListAsync(ct);

            return qualities.Select(x => x.ToId()).ToList();
        }

        if (mediaType == PlexMediaType.TvShow)
        {
            var qualities = await _dbContext
                .PlexTvShowMediaQualities.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .GroupBy(x => x.VideoQuality)
                .Select(g => g.Key)
                .ToListAsync(ct);

            return qualities.Select(x => x.ToId()).ToList();
        }

        if (mediaType == PlexMediaType.OtherVideos)
        {
            var qualities = await _dbContext
                .PlexOtherVideoData.Where(x => libraryIds.Contains(x.PlexLibraryId))
                .GroupBy(x => x.VideoQuality)
                .Select(g => g.Key)
                .ToListAsync(ct);

            return qualities.Select(x => x.ToId()).ToList();
        }

        return [];
    }
}
