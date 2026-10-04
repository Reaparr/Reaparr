using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewPhotoCommand(MediaQueryFilter Filter) : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewPhotoCommandValidator : AbstractValidator<GetMediaOverviewPhotoCommand>
{
    public GetMediaOverviewPhotoCommandValidator()
    {
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.PhotoAlbum);
        RuleFor(x => x.Filter.ComparisonState).Null();
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
    }
}

public sealed class GetMediaOverviewPhotoCommandHandler
    : ICommandHandler<GetMediaOverviewPhotoCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;

    public GetMediaOverviewPhotoCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
        _log = log.ForContext<GetMediaOverviewPhotoCommandHandler>();
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewPhotoCommand command,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var filter = command.Filter;
        var options = QueryOptionsParser.Parse(filter.Parameters);
        var sort = options.ResolveSort();
        if (sort is null || sort.Value.Field == "Quality")
        {
            var mediaResult = await _commandExecutor.Send(
                new GetMediaByTypeCommand { Filter = filter },
                cancellationToken
            );
            LogPhase(filter, "CanonicalFallback", stopwatch.Elapsed, 0);
            return mediaResult.LogIfFailed();
        }

        using var context = await _dbContextFactory.CreateAsync();
        var allowedLibraryIds = await context.ResolveAllowedLibraryIdsAsync(filter, PlexMediaType.Photos, cancellationToken);
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
            !await context.MediaOverviewPhotoAlbumSnapshots.AnyAsync(
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

        var candidates = context.PlexPhotoAlbums.ApplyFilter(options);
        var snapshots = context.MediaOverviewPhotoAlbumSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(photoAlbum => photoAlbum.Id == snapshot.PlexPhotoAlbumId)
        );
        var orderedSnapshots = ApplyOrder(snapshots, sort.Value.Field, sort.Value.Descending);
        var ids = await orderedSnapshots
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexPhotoAlbumId)
            .ToListAsync(cancellationToken);
        LogPhase(filter, "PageIds", stopwatch.Elapsed, ids.Count);
        stopwatch.Restart();
        var aggregate = await snapshots
            .Join(
                context.PlexPhotoAlbums,
                snapshot => snapshot.PlexPhotoAlbumId,
                photoAlbum => photoAlbum.Id,
                (_, photoAlbum) => photoAlbum.MediaSize
            )
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), MediaSize = group.Sum() })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.Count ?? 0;
        var mediaSize = aggregate?.MediaSize ?? 0;
        LogPhase(filter, "Aggregate", stopwatch.Elapsed, totalCount);
        stopwatch.Restart();
        var items = await context
            .PlexPhotoAlbums.Where(x => ids.Contains(x.Id))
            .Select(x => new PlexMediaSlimDTO
            {
                Id = x.Id,
                Title = x.Title,
                SearchTitle = x.SearchTitle,
                SortIndex = x.SortIndex,
                Year = x.Year,
                Duration = x.Duration,
                MediaSize = x.MediaSize,
                ChildCount = x.Photos.Count,
                GrandChildCount = 0,
                AddedAt = x.AddedAt,
                UpdatedAt = x.UpdatedAt,
                PlexLibraryId = x.PlexLibraryId,
                PlexServerId = x.PlexServerId,
                Type = PlexMediaType.PhotoAlbum,
                HasThumb = x.HasThumb,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Qualities = new List<PlexMediaQualityDTO>(),
            })
            .ToListAsync(cancellationToken);
        items = items.RestorePageOrder(ids);
        LogPhase(filter, "Items", stopwatch.Elapsed, items.Count);
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
                context.PlexPhotoAlbums,
                snapshot => snapshot.PlexPhotoAlbumId,
                photoAlbum => photoAlbum.Id,
                (_, photoAlbum) =>
                    new MediaNavigationIndexRow(
                        photoAlbum.SearchTitle,
                        photoAlbum.Year,
                        null,
                        photoAlbum.Duration,
                        photoAlbum.AddedAt,
                        photoAlbum.UpdatedAt,
                        photoAlbum.MediaSize
                    )
            )
            .ToListAsync(cancellationToken);
        result.NavigationIndexes = MediaNavigationIndexBuilder.Build(
            navigationRows,
            options.Sort.FirstOrDefault()?.Field
        );
        LogPhase(filter, "Statistics", stopwatch.Elapsed, allowedLibraryIds.Count);
        stopwatch.Restart();
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

    private static IQueryable<MediaOverviewPhotoAlbumSnapshot> ApplyOrder(
        IQueryable<MediaOverviewPhotoAlbumSnapshot> query,
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
