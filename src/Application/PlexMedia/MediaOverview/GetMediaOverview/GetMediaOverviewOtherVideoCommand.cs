using FlexQuery.NET;
using FlexQuery.NET.Parsers;

namespace Reaparr.Application;

public sealed record GetMediaOverviewOtherVideoCommand(MediaQueryFilter Filter)
    : ICommand<Result<PagedMediaQueryResult>>;

public sealed class GetMediaOverviewOtherVideoCommandValidator : AbstractValidator<GetMediaOverviewOtherVideoCommand>
{
    public GetMediaOverviewOtherVideoCommandValidator()
    {
        RuleFor(x => x.Filter.MediaType).Equal(PlexMediaType.OtherVideos);
        RuleFor(x => x.Filter.ComparisonState).Null();
        RuleFor(x => x.Filter.Parameters.Sort)
            .Must((command, sort) => sort.IsSupportedSort(command.Filter.MediaType))
            .WithMessage("Sort fields must be supported media sort types.");
        RuleFor(x => x.Filter.Parameters.Page).GreaterThan(0).When(x => x.Filter.Parameters.Page.HasValue);
        RuleFor(x => x.Filter.Parameters.PageSize)
            .InclusiveBetween(1, MediaQueryFilter.MaximumPageSize)
            .When(x => x.Filter.Parameters.PageSize.HasValue);
    }
}

public sealed class GetMediaOverviewOtherVideoCommandHandler
    : ICommandHandler<GetMediaOverviewOtherVideoCommand, Result<PagedMediaQueryResult>>
{
    private readonly IReaparrDbContextFactory _dbContextFactory;
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger _log;

    public GetMediaOverviewOtherVideoCommandHandler(
        IReaparrDbContextFactory dbContextFactory,
        ICommandExecutor commandExecutor,
        ILogger log
    )
    {
        _dbContextFactory = dbContextFactory;
        _commandExecutor = commandExecutor;
        _log = log.ForContext<GetMediaOverviewOtherVideoCommandHandler>();
    }

    public async Task<Result<PagedMediaQueryResult>> ExecuteAsync(
        GetMediaOverviewOtherVideoCommand command,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var filter = command.Filter;
        var options = QueryOptionsParser.Parse(filter.Parameters);
        var sort = options.ResolveSort();
        if (sort is null)
        {
            var mediaResult = await _commandExecutor.Send(
                new GetOtherVideoMediaByTypeCommand(filter),
                cancellationToken
            );
            LogPhase(filter, "CanonicalFallback", stopwatch.Elapsed, 0);
            return mediaResult.LogIfFailed();
        }

        using var context = await _dbContextFactory.CreateAsync();
        var allowedLibraryIds = await context.ResolveAllowedLibraryIdsAsync(
            filter,
            PlexMediaType.OtherVideos,
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
            !await context.MediaOverviewOtherVideoSnapshots.AnyAsync(
                x => allowedLibraryIds.Contains(x.PlexLibraryId),
                cancellationToken
            )
        )
        {
            LogPhase(filter, "SnapshotMissing", stopwatch.Elapsed, 0);
            var mediaResult = await _commandExecutor.Send(
                new GetOtherVideoMediaByTypeCommand(filter),
                cancellationToken
            );
            return mediaResult.LogIfFailed();
        }

        LogPhase(filter, "SnapshotExists", stopwatch.Elapsed, 1);
        stopwatch.Restart();

        var candidates = context.PlexOtherVideos.ApplyFilter(options);
        var snapshots = context.MediaOverviewOtherVideoSnapshots.Where(snapshot =>
            allowedLibraryIds.Contains(snapshot.PlexLibraryId)
            && candidates.Any(otherVideo => otherVideo.Id == snapshot.PlexOtherVideoId)
        );
        var orderedSnapshots = snapshots.ApplyRankOrder(sort.Value);
        var ids = await orderedSnapshots
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => x.PlexOtherVideoId)
            .ToListAsync(cancellationToken);
        LogPhase(filter, "PageIds", stopwatch.Elapsed, ids.Count);
        stopwatch.Restart();
        var aggregate = await snapshots
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.Count(), MediaSize = group.Sum(x => x.MediaSize) })
            .FirstOrDefaultAsync(cancellationToken);
        var totalCount = aggregate?.Count ?? 0;
        var mediaSize = aggregate?.MediaSize ?? 0;
        LogPhase(filter, "Aggregate", stopwatch.Elapsed, totalCount);
        stopwatch.Restart();
        var items = await context
            .PlexOtherVideos.Where(x => ids.Contains(x.Id))
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
                Type = PlexMediaType.OtherVideos,
                HasThumb = x.HasThumb,
                PlexApiRatingKey = x.PlexApiRatingKey,
                PlexApiMetaDataKey = x.PlexApiMetaDataKey,
                Qualities = new List<PlexMediaQualityDTO>(),
            })
            .ToListAsync(cancellationToken);
        var qualities = await context
            .PlexOtherVideoData.Where(x => ids.Contains(x.PlexOtherVideoId))
            .OrderBy(x => x.VideoQuality)
            .Select(x => new PlexMediaQualityDTO
            {
                VideoQuality = x.VideoQuality,
                MediaDataType = PlexMediaType.OtherVideos,
                DataId = x.Id,
                MediaId = x.PlexOtherVideoId,
            })
            .ToListAsync(cancellationToken);
        var qualitiesByMediaId = qualities.ToLookup(x => x.MediaId);
        foreach (var item in items)
            item.Qualities.AddRange(qualitiesByMediaId[item.Id]);
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
        var navigationRows = snapshots.SelectNavigationRows(sort.Value);
        result.NavigationIndexes = await MediaNavigationIndexBuilder.BuildAsync(
            navigationRows,
            sort.Value,
            cancellationToken
        );

        LogPhase(filter, "Statistics", stopwatch.Elapsed, allowedLibraryIds.Count);
        stopwatch.Restart();

        result.Roles = await context
            .PlexOtherVideoActors.Where(x => ids.Contains(x.PlexOtherVideoId))
            .Select(x => x.PlexActorId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Countries = await context
            .PlexOtherVideoCountries.Where(x => ids.Contains(x.PlexOtherVideoId))
            .Select(x => x.CountryId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        result.Genres = await context
            .PlexOtherVideoGenres.Where(x => ids.Contains(x.PlexOtherVideoId))
            .Select(x => x.GenresId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        LogPhase(filter, "Metadata", stopwatch.Elapsed, items.Count);
        result.Qualities = items
            .SelectMany(x => x.Qualities)
            .Select(x => x.VideoQuality.ToId())
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
}
