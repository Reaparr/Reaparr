using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewMusicCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewMusicCommandValidator : AbstractValidator<GetMediaOverviewMusicCommand>
{
    public GetMediaOverviewMusicCommandValidator()
    {
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.MusicArtist);
        RuleFor(x => x.Filter.ComparisonState)
            .Must(x =>
                x
                    is null
                        or PlexMediaComparisonState.NotCompared
                        or PlexMediaComparisonState.Pending
                        or PlexMediaComparisonState.Owned
                        or PlexMediaComparisonState.Missing
                        or PlexMediaComparisonState.Partial
            );
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
    }
}

public sealed class GetMediaOverviewMusicCommandHandler
    : ICommandHandler<GetMediaOverviewMusicCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;

    public GetMediaOverviewMusicCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
        _log = log.ForContext<GetMediaOverviewMusicCommandHandler>();
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewMusicCommand command,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var filter = command.Filter;
        var options = QueryOptionsParser.Parse(filter.Parameters);
        var sort = options.ResolveSort();
        if (filter.ComparisonState.HasValue || sort is null || sort.Value.Field == "Quality")
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            LogPhase(filter, "CanonicalFallback", stopwatch.Elapsed, 0);
            return mediaResult.LogIfFailed();
        }

        using var context = await _dbContextFactory.CreateAsync();
        var allowedLibraryIds = await context.ResolveAllowedLibraryIdsAsync(
            filter,
            PlexMediaType.MusicArtist,
            cancellationToken
        );
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
            !await context.MediaOverviewMusicArtistSnapshots.AnyAsync(
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

        var candidates = context.PlexArtists.ApplyFilter(options);
        var snapshots = context.MediaOverviewMusicArtistSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(artist => artist.Id == snapshot.PlexArtistId)
        );
        var orderedSnapshots = ApplyOrder(snapshots, sort.Value.Field, sort.Value.Descending);
        var ids = await orderedSnapshots
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexArtistId)
            .ToListAsync(cancellationToken);
        LogPhase(filter, "PageIds", stopwatch.Elapsed, ids.Count);
        stopwatch.Restart();
        var aggregate = await snapshots
            .Join(
                context.PlexArtists,
                snapshot => snapshot.PlexArtistId,
                artist => artist.Id,
                (_, artist) => artist.MediaSize
            )
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), MediaSize = group.Sum() })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.Count ?? 0;
        var mediaSize = aggregate?.MediaSize ?? 0;
        LogPhase(filter, "Aggregate", stopwatch.Elapsed, totalCount);
        stopwatch.Restart();
        var items = await context
            .PlexArtists.Where(x => ids.Contains(x.Id))
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
                GrandChildCount = x.Albums.Sum(album => album.ChildCount),
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.MusicArtist,
                HasThumb = x.HasThumb,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Qualities = new List<PlexMediaQualityDTO>(),
            })
            .ToListAsync(cancellationToken);
        items = items.RestorePageOrder(ids);
        LogPhase(filter, "Items", stopwatch.Elapsed, items.Count);
        stopwatch.Restart();
        var comparisonResult = await _commandExecutor.Send(
            new ApplyComparisonStateCommand(
                items,
                PlexMediaType.MusicArtist,
                filter.PlexLibraryId > 0 ? filter.PlexLibraryId : null
            ),
            cancellationToken
        );

        if (comparisonResult.IsFailed)
            return comparisonResult.LogIfFailed().ToResult<PagedMediaQueryResult>();

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
                context.PlexArtists,
                snapshot => snapshot.PlexArtistId,
                artist => artist.Id,
                (_, artist) =>
                    new MediaNavigationIndexRow(
                        artist.SearchTitle,
                        artist.Year,
                        null,
                        artist.Duration,
                        artist.AddedAt,
                        artist.UpdatedAt,
                        artist.MediaSize
                    )
            )
            .ToListAsync(cancellationToken);
        result.NavigationIndexes = MediaNavigationIndexBuilder.Build(
            navigationRows,
            options.Sort.FirstOrDefault()?.Field
        );
        LogPhase(filter, "Statistics", stopwatch.Elapsed, allowedLibraryIds.Count);
        stopwatch.Restart();
        result.Roles = await context
            .PlexMusicArtistActors.Where(x => ids.Contains(x.PlexMusicArtistId))
            .Select(x => x.PlexActorId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Countries = await context
            .PlexMusicArtistCountries.Where(x => ids.Contains(x.PlexMusicArtistId))
            .Select(x => x.CountryId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Genres = await context
            .PlexMusicArtistGenres.Where(x => ids.Contains(x.PlexMusicArtistId))
            .Select(x => x.GenresId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
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

    private static IQueryable<MediaOverviewMusicArtistSnapshot> ApplyOrder(
        IQueryable<MediaOverviewMusicArtistSnapshot> query,
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
            _ => descending ? query.OrderByDescending(x => x.TitleRank) : query.OrderBy(x => x.TitleRank),
        };
}
