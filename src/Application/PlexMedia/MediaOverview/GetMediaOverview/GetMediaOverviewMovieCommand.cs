using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewMovieCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewMovieCommandValidator : AbstractValidator<GetMediaOverviewMovieCommand>
{
    public GetMediaOverviewMovieCommandValidator()
    {
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.Movie);
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
    }
}

public sealed class GetMediaOverviewMovieCommandHandler
    : ICommandHandler<GetMediaOverviewMovieCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;

    public GetMediaOverviewMovieCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
        _log = log.ForContext<GetMediaOverviewMovieCommandHandler>();
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewMovieCommand command,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var filter = command.Filter;
        var options = QueryOptionsParser.Parse(filter.Parameters);
        var sort = options.ResolveSort();
        if (sort is null || filter.ComparisonState.HasValue)
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            LogPhase(filter, "CanonicalFallback", stopwatch.Elapsed, 0);
            return mediaResult.LogIfFailed();
        }

        using var context = await _dbContextFactory.CreateAsync();
        var allowedLibraryIds = await context.ResolveAllowedLibraryIdsAsync(filter, cancellationToken);
        LogPhase(filter, "ResolveLibraries", stopwatch.Elapsed, allowedLibraryIds.Count);
        stopwatch.Restart();
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
            LogPhase(filter, "SnapshotMissing", stopwatch.Elapsed, 0);
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            return mediaResult.LogIfFailed();
        }

        LogPhase(filter, "SnapshotExists", stopwatch.Elapsed, 1);
        stopwatch.Restart();

        var candidates = context.PlexMovies.ApplyFilter(options);
        var snapshots = context.MediaOverviewMovieSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(movie => movie.Id == snapshot.PlexMovieId)
        );
        var orderedSnapshots = ApplyOrder(snapshots, sort.Value.Field, sort.Value.Direction);
        var ids = await orderedSnapshots
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexMovieId)
            .ToListAsync(cancellationToken);
        LogPhase(filter, "PageIds", stopwatch.Elapsed, ids.Count);
        stopwatch.Restart();
        var aggregate = await snapshots
            .Join(
                context.PlexMovies,
                snapshot => snapshot.PlexMovieId,
                movie => movie.Id,
                (_, movie) => movie.MediaSize
            )
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), MediaSize = group.Sum() })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.Count ?? 0;
        var mediaSize = aggregate?.MediaSize ?? 0;
        LogPhase(filter, "Aggregate", stopwatch.Elapsed, totalCount);
        stopwatch.Restart();
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
                Qualities = new List<PlexMediaQualityDTO>(),
            })
            .ToListAsync(cancellationToken);
        var qualities = await context
            .PlexMovieData.Where(x => ids.Contains(x.PlexMovieId))
            .OrderBy(x => x.VideoQuality)
            .Select(x => new PlexMediaQualityDTO
            {
                VideoQuality = x.VideoQuality,
                MediaDataType = PlexMediaType.Movie,
                DataId = x.Id,
                MediaId = x.PlexMovieId,
            })
            .ToListAsync(cancellationToken);
        var qualitiesByMediaId = qualities.ToLookup(x => x.MediaId);
        foreach (var item in items)
            item.Qualities.AddRange(qualitiesByMediaId[item.Id]);
        items = items.RestorePageOrder(ids);
        LogPhase(filter, "Items", stopwatch.Elapsed, items.Count);
        var comparisonResult = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(
                items,
                filter.MediaType,
                filter.PlexLibraryId > 0 ? filter.PlexLibraryId : null
            ),
            cancellationToken
        );
        comparisonResult.LogIfFailed();
        LogPhase(filter, "ApplyComparison", stopwatch.Elapsed, items.Count);

        stopwatch.Restart();

        var result = await context.CreatePageResultAsync(
            filter,
            options,
            allowedLibraryIds,
            items,
            totalCount,
            mediaSize,
            cancellationToken
        );
        result.NavigationIndexes = await BuildNavigationIndexesAsync(
            orderedSnapshots,
            context.PlexMovies,
            sort.Value.Field,
            cancellationToken
        );
        LogPhase(filter, "Statistics", stopwatch.Elapsed, allowedLibraryIds.Count);
        stopwatch.Restart();
        if (items.Count > 0)
        {
            var pageIds = items.Select(x => x.Id).AsEnumerable();
            var metadata = await context
                .PlexMovieActors.Where(x => pageIds.Contains(x.PlexMovieId))
                .Select(x => new { Type = 0, Id = x.PlexActorId })
                .Concat(
                    context
                        .PlexMovieCountries.Where(x => pageIds.Contains(x.PlexMovieId))
                        .Select(x => new { Type = 1, Id = x.CountryId })
                )
                .Concat(
                    context
                        .PlexMovieGenres.Where(x => pageIds.Contains(x.PlexMovieId))
                        .Select(x => new { Type = 2, Id = x.GenresId })
                )
                .Distinct()
                .ToListAsync(cancellationToken);
            result.Roles = metadata.Where(x => x.Type == 0).Select(x => x.Id).OrderBy(x => x).ToList();
            result.Countries = metadata.Where(x => x.Type == 1).Select(x => x.Id).OrderBy(x => x).ToList();
            result.Genres = metadata.Where(x => x.Type == 2).Select(x => x.Id).OrderBy(x => x).ToList();
        }
        LogPhase(filter, "Metadata", stopwatch.Elapsed, items.Count);
        result.Qualities = items
            .SelectMany(x => x.Qualities)
            .Select(x => x.VideoQuality.ToId())
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        return Result.Ok(result);
    }

    private static async Task<List<MediaNavigationIndexDTO>> BuildNavigationIndexesAsync(
        IQueryable<MediaOverviewMovieSnapshot> orderedSnapshots,
        IQueryable<PlexMovie> movies,
        SortType sortType,
        CancellationToken cancellationToken
    )
    {
        var navigationRows = await orderedSnapshots
            .Join(
                movies,
                snapshot => snapshot.PlexMovieId,
                movie => movie.Id,
                (_, movie) =>
                    new MediaNavigationIndexRow
                    {
                        SearchTitle = movie.SearchTitle,
                        Year = movie.Year,
                        QualityValue = (int?)movie.VideoQuality,
                        Duration = movie.Duration,
                        AddedAt = movie.AddedAt,
                        UpdatedAt = movie.UpdatedAt,
                        MediaSize = movie.MediaSize,
                    }
            )
            .ToListAsync(cancellationToken);
        return MediaNavigationIndexBuilder.Build(navigationRows, sortType);
    }

    private void LogPhase(MediaQueryFilter filter, string phase, TimeSpan elapsed, int rowCount) =>
        _log.Here()
            .Debug(
                "Media overview query phase {Phase} completed for {MediaType}: page {Page}, page size {PageSize}, sort {Sort}, {RowCount} rows in {ElapsedMilliseconds} ms",
                phase,
                filter.MediaType,
                filter.Parameters.Page,
                filter.Parameters.PageSize,
                filter.Parameters.Sort,
                rowCount,
                elapsed.TotalMilliseconds
            );

    private static IQueryable<MediaOverviewMovieSnapshot> ApplyOrder(
        IQueryable<MediaOverviewMovieSnapshot> query,
        SortType sortType,
        SortDirection direction
    ) =>
        sortType switch
        {
            SortType.Year => query.ApplyOrderBy(direction, x => x.YearRank),
            SortType.AddedAt => query.ApplyOrderBy(direction, x => x.AddedAtRank),
            SortType.UpdatedAt => query.ApplyOrderBy(direction, x => x.UpdatedAtRank),
            SortType.Duration => query.ApplyOrderBy(direction, x => x.DurationRank),
            SortType.MediaSize => query.ApplyOrderBy(direction, x => x.MediaSizeRank),
            SortType.VideoQuality => query.ApplyOrderBy(direction, x => x.QualityRank),
            _ => query.ApplyOrderBy(direction, x => x.TitleRank),
        };
}
