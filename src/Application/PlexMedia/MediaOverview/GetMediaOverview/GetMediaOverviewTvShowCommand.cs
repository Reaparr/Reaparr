using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewTvShowCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewTvShowCommandValidator : AbstractValidator<GetMediaOverviewTvShowCommand>
{
    public GetMediaOverviewTvShowCommandValidator()
    {
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.TvShow);
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
    }
}

public sealed class GetMediaOverviewTvShowCommandHandler
    : ICommandHandler<GetMediaOverviewTvShowCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;

    public GetMediaOverviewTvShowCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
        _log = log.ForContext<GetMediaOverviewTvShowCommandHandler>();
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewTvShowCommand command,
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
            !await context.MediaOverviewTvShowSnapshots.AnyAsync(
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

        var candidates = context.PlexTvShows.ApplyFilter(options);
        var snapshots = context.MediaOverviewTvShowSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(tvShow => tvShow.Id == snapshot.PlexTvShowId)
        );
        var orderedSnapshots = ApplyOrder(snapshots, sort.Value.Field, sort.Value.Descending);
        var ids = await orderedSnapshots
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexTvShowId)
            .ToListAsync(cancellationToken);
        LogPhase(filter, "PageIds", stopwatch.Elapsed, ids.Count);
        stopwatch.Restart();
        var aggregate = await snapshots
            .Join(
                context.PlexTvShows,
                snapshot => snapshot.PlexTvShowId,
                tvShow => tvShow.Id,
                (_, tvShow) => tvShow.MediaSize
            )
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), MediaSize = group.Sum() })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.Count ?? 0;
        var mediaSize = aggregate?.MediaSize ?? 0;
        LogPhase(filter, "Aggregate", stopwatch.Elapsed, totalCount);
        stopwatch.Restart();
        var items = await context
            .PlexTvShows.Where(x => ids.Contains(x.Id))
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
                GrandChildCount = x.GrandChildCount,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.TvShow,
                HasThumb = x.HasThumb,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Qualities = new List<PlexMediaQualityDTO>(),
            })
            .ToListAsync(cancellationToken);
        var qualities = await context
            .PlexTvShowMediaQualities.Where(x => ids.Contains(x.PlexTvShowId))
            .OrderBy(x => x.Quality)
            .Select(x => new PlexMediaQualityDTO
            {
                Quality = x.Quality,
                MediaDataType = PlexMediaType.TvShow,
                DataId = x.Id,
                MediaId = x.PlexTvShowId,
            })
            .ToListAsync(cancellationToken);
        var qualitiesByMediaId = qualities.ToLookup(x => x.MediaId);
        foreach (var item in items)
            item.Qualities.AddRange(qualitiesByMediaId[item.Id]);
        items = items.RestorePageOrder(ids);
        LogPhase(filter, "Items", stopwatch.Elapsed, items.Count);
        var comparisonResult = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(items, filter.PlexLibraryId, filter.MediaType),
            cancellationToken
        );
        LogPhase(filter, "ApplyComparison", stopwatch.Elapsed, items.Count);
        comparisonResult.LogIfFailed();
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
        var navigationRows = await orderedSnapshots
            .Join(
                context.PlexTvShows,
                snapshot => snapshot.PlexTvShowId,
                tvShow => tvShow.Id,
                (_, tvShow) =>
                    new MediaNavigationIndexRow(
                        tvShow.SearchTitle,
                        tvShow.Year,
                        (int?)tvShow.Quality,
                        tvShow.Duration,
                        tvShow.AddedAt,
                        tvShow.UpdatedAt,
                        tvShow.MediaSize
                    )
            )
            .ToListAsync(cancellationToken);
        result.NavigationIndexes = MediaNavigationIndexBuilder.Build(
            navigationRows,
            options.Sort.FirstOrDefault()?.Field
        );
        LogPhase(filter, "Statistics", stopwatch.Elapsed, allowedLibraryIds.Count);
        stopwatch.Restart();
        if (items.Count > 0)
        {
            var pageIds = items.Select(x => x.Id).AsEnumerable();
            var metadata = await context
                .PlexTvShowActors.Where(x => pageIds.Contains(x.PlexTvShowId))
                .Select(x => new { Type = 0, Id = x.PlexActorId })
                .Concat(
                    context
                        .PlexTvShowCountries.Where(x => pageIds.Contains(x.PlexTvShowId))
                        .Select(x => new { Type = 1, Id = x.CountryId })
                )
                .Concat(
                    context
                        .PlexTvShowGenres.Where(x => pageIds.Contains(x.PlexTvShowId))
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
            .Select(x => x.Quality.ToId())
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        return Result.Ok(result);
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

    private static IQueryable<MediaOverviewTvShowSnapshot> ApplyOrder(
        IQueryable<MediaOverviewTvShowSnapshot> query,
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
