using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewMovieCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewMovieCommandValidator : AbstractValidator<GetMediaOverviewMovieCommand>
{
    public GetMediaOverviewMovieCommandValidator() => RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.Movie);
}

public sealed class GetMediaOverviewMovieCommandHandler
    : ICommandHandler<GetMediaOverviewMovieCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;

    public GetMediaOverviewMovieCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewMovieCommand command,
        CancellationToken cancellationToken
    )
    {
        var filter = command.Filter;
        var options = QueryOptionsParser.Parse(filter.Parameters);
        var sort = options.ResolveSort();
        if (sort is null || filter.ComparisonState.HasValue)
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            return mediaResult.LogIfFailed();
        }

        using var context = await _dbContextFactory.CreateAsync();
        var allowedLibraryIds = await context.ResolveAllowedLibraryIdsAsync(filter, cancellationToken);
        if (allowedLibraryIds.Count == 0)
        {
            return Result.Ok(
                new PagedMediaQueryResult
                {
                    QueryHash = filter.QueryHash,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                }
            );
        }

        if (
            !await context.MediaOverviewMovieSnapshots.AnyAsync(
                x => allowedLibraryIds.Contains(x.PlexLibraryId),
                cancellationToken
            )
        )
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            return mediaResult.LogIfFailed();
        }

        var candidates = context.PlexMovies.ApplyFilter(options);
        var snapshots = context.MediaOverviewMovieSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(movie => movie.Id == snapshot.PlexMovieId)
        );
        var ids = await ApplyOrder(snapshots, sort.Value.Field, sort.Value.Descending)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexMovieId)
            .ToListAsync(cancellationToken);
        var totalCount = await snapshots.CountAsync(cancellationToken);
        var mediaSize =
            await context
                .PlexMovies.Where(x => snapshots.Select(snapshot => snapshot.PlexMovieId).Contains(x.Id))
                .Select(x => (long?)x.MediaSize)
                .SumAsync(cancellationToken)
            ?? 0;
        var items = await context
            .PlexMovies.Where(x => ids.Contains(x.Id))
            .Select(x => new PlexMediaSlimDTO
            {
                Id = x.Id,
                Title = x.Title,
                SearchTitle = x.SearchTitle,
                SortIndex = x.SortIndex,
                Year = x.Year,
                Duration = x.Duration,
                MediaSize = x.MediaSize,
                ChildCount = x.ChildCount,
                GrandChildCount = 0,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.Movie,
                HasThumb = x.HasThumb,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Qualities = x
                    .MediaDataList.OrderBy(data => data.Quality)
                    .Select(data => new PlexMediaQualityDTO
                    {
                        Quality = data.Quality,
                        MediaDataType = PlexMediaType.Movie,
                        DataId = data.Id,
                        MediaId = x.Id,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);
        items = items.RestorePageOrder(ids);

        var result = await context.CreatePageResultAsync(
            filter,
            options,
            allowedLibraryIds,
            items,
            totalCount,
            mediaSize,
            cancellationToken
        );
        var pageIds = items.Select(x => x.Id).ToArray();
        result.Roles = await context
            .PlexMovieActors.Where(x => pageIds.AsEnumerable().Contains(x.PlexMovieId))
            .Select(x => x.PlexActorId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Countries = await context
            .PlexMovieCountries.Where(x => pageIds.AsEnumerable().Contains(x.PlexMovieId))
            .Select(x => x.CountryId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Genres = await context
            .PlexMovieGenres.Where(x => pageIds.AsEnumerable().Contains(x.PlexMovieId))
            .Select(x => x.GenresId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Qualities = items
            .SelectMany(x => x.Qualities)
            .Select(x => x.Quality.ToId())
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        return Result.Ok(result);
    }

    private static IQueryable<MediaOverviewMovieSnapshot> ApplyOrder(
        IQueryable<MediaOverviewMovieSnapshot> query,
        string field,
        bool descending
    ) =>
        field switch
        {
            nameof(BasePlexMedia.Year) => descending
                ? query.OrderByDescending(x => x.YearRank)
                : query.OrderBy(x => x.YearRank),
            nameof(BasePlexMedia.AddedAt) => descending
                ? query.OrderByDescending(x => x.AddedAtRank)
                : query.OrderBy(x => x.AddedAtRank),
            nameof(BasePlexMedia.UpdatedAt) => descending
                ? query.OrderByDescending(x => x.UpdatedAtRank)
                : query.OrderBy(x => x.UpdatedAtRank),
            nameof(BasePlexMedia.Duration) => descending
                ? query.OrderByDescending(x => x.DurationRank)
                : query.OrderBy(x => x.DurationRank),
            nameof(BasePlexMedia.MediaSize) => descending
                ? query.OrderByDescending(x => x.MediaSizeRank)
                : query.OrderBy(x => x.MediaSizeRank),
            "Quality" => descending ? query.OrderByDescending(x => x.QualityRank) : query.OrderBy(x => x.QualityRank),
            _ => descending ? query.OrderByDescending(x => x.TitleRank) : query.OrderBy(x => x.TitleRank),
        };
}
