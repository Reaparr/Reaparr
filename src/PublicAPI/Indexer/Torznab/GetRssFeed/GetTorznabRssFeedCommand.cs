using System.Diagnostics;
using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI;

public record GetTorznabRssFeedCommand : ICommand<Result<TorznabMediaSearchResponseDTO>>
{
    public required IntegrationIdentity Integration { get; init; }
    public required int[] Categories { get; init; }
    public required bool IncludeMovies { get; init; }
    public required bool IncludeEpisodes { get; init; }
    public required int Limit { get; init; }
    public required int Offset { get; init; }
    public required string TorznabApiKey { get; init; }
    public required string[] Attributes { get; init; }
    public required bool IncludeAllAttributes { get; init; }
}

public class GetTorznabRssFeedCommandValidator : AbstractValidator<GetTorznabRssFeedCommand>
{
    public GetTorznabRssFeedCommandValidator()
    {
        RuleFor(x => x.Integration).NotNull();
        RuleFor(x => x.Categories).NotNull();
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Limit).LessThanOrEqualTo(TorznabSearchHelpers.MaxPageSize);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset, x.Limit))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
        RuleFor(x => x.Attributes).NotNull();
    }
}

public class GetTorznabRssFeedCommandHandler
    : ICommandHandler<GetTorznabRssFeedCommand, Result<TorznabMediaSearchResponseDTO>>
{
    private const int MinimumParentBatchSize = 256;

    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    public GetTorznabRssFeedCommandHandler(ILogger log, IReaparrDbContext dbContext, INetworkSettings networkSettings)
    {
        _log = log.ForContext<GetTorznabRssFeedCommandHandler>();
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        GetTorznabRssFeedCommand command,
        CancellationToken cancellationToken
    )
    {
        var total = 0;
        var fetchLimit = checked(command.Offset + command.Limit);
        var onlineServerIds = await _dbContext.GetDownloadableServerIds();
        if (onlineServerIds.Count == 0)
            return Result.Ok(CreateResponse(command.Offset, 0, []));

        var accessibleLibraryIds = await _dbContext.GetAccessibleLibraryIds(onlineServerIds);
        if (accessibleLibraryIds.Count == 0)
            return Result.Ok(CreateResponse(command.Offset, 0, []));

        var loadResult = await Result.Try(async Task<Result<TorznabMediaSearchResponseDTO>> () =>
        {
            var rows = new List<TorznabFeedCandidate>(fetchLimit * 2);
            if (command.IncludeMovies)
            {
                var (movieRows, movieTotal) = await LoadMovieRows(
                    accessibleLibraryIds,
                    command.Categories,
                    fetchLimit,
                    cancellationToken
                );
                rows.AddRange(movieRows);
                total += movieTotal;
            }

            if (command.IncludeEpisodes)
            {
                var (episodeRows, episodeTotal) = await LoadEpisodeRows(
                    accessibleLibraryIds,
                    command.Categories,
                    fetchLimit,
                    cancellationToken
                );
                rows.AddRange(episodeRows);
                total += episodeTotal;
            }

            var requestedAttributes = TorznabSearchHelpers.GetRequestedAttributes(
                command.IncludeAllAttributes,
                command.Attributes
            );
            var pageCandidates = rows.OrderByDescending(x => x.AddedAt)
                .ThenByDescending(x => x.PlexServerId)
                .ThenByDescending(x => x.PlexApiRatingKey)
                .ThenByDescending(x => x.DataId)
                .Take(fetchLimit)
                .Skip(command.Offset)
                .Take(command.Limit)
                .ToList();
            var pageRows = await HydrateFeedItemsAsync(pageCandidates, cancellationToken);
            pageRows = await pageRows.EnrichGenresAsync(_dbContext, cancellationToken);
            var items = pageRows
                .Select(x =>
                    x.ToTorznabItem(
                        command.Integration,
                        command.TorznabApiKey,
                        _networkSettings.Url,
                        requestedAttributes
                    )
                )
                .ToList();
            return Result.Ok(CreateResponse(command.Offset, total, items));
        });
        return loadResult.LogIfFailed();
    }

    private async Task<(List<TorznabFeedCandidate> Rows, int Total)> LoadMovieRows(
        IReadOnlyCollection<int> accessibleLibraryIds,
        int[] categories,
        int fetchLimit,
        CancellationToken cancellationToken
    )
    {
        var mediaDataQuery = _dbContext
            .PlexMovieData.TagWith("Torznab/Rss/Movie/Releases")
            .Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId))
            .ApplyTorznabCategories(categories);

        int total;
        if (categories.Length == 0 || categories.Contains((int)TorznabCategoryId.Movies))
        {
            total = await _dbContext
                .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie && accessibleLibraryIds.Contains(x.Id))
                .SumAsync(x => x.MovieMediaDataCount, cancellationToken);
        }
        else
        {
            var fallbackCountStartedAt = Stopwatch.GetTimestamp();
            total = await mediaDataQuery.CountAsync(cancellationToken);
            LogFallbackCategoryCountTiming(PlexMediaType.Movie, fallbackCountStartedAt, total);
        }

        var parentBatchSize = Math.Max(fetchLimit, MinimumParentBatchSize);
        TorznabFeedParent? cursor = null;
        var parentScanElapsed = TimeSpan.Zero;
        var projectionElapsed = TimeSpan.Zero;
        var parentCount = 0;
        var projectionCount = 0;
        var rows = new List<TorznabFeedCandidate>(fetchLimit);
        while (rows.Count < fetchLimit)
        {
            var parentScanStartedAt = Stopwatch.GetTimestamp();
            var parents = await LoadParentBatch(
                PlexMediaType.Movie,
                accessibleLibraryIds,
                parentBatchSize,
                cursor,
                cancellationToken
            );
            parentScanElapsed += Stopwatch.GetElapsedTime(parentScanStartedAt);
            parentCount += parents.Count;
            if (parents.Count == 0)
                break;

            var movieIds = parents.Select(x => x.Id).ToList();
            var projectionStartedAt = Stopwatch.GetTimestamp();
            var batchRows = await mediaDataQuery
                .Where(x => movieIds.Contains(x.PlexMovieId))
                .OrderByDescending(x => x.PlexMovie!.AddedAt)
                .ThenByDescending(x => x.PlexMovie!.PlexServerId)
                .ThenByDescending(x => x.PlexMovie!.PlexApiRatingKey)
                .ThenByDescending(x => x.Id)
                .Select(x => new TorznabFeedCandidate(
                    PlexMediaType.Movie,
                    x.Id,
                    x.PlexMovie!.AddedAt,
                    x.PlexServerId,
                    x.PlexApiRatingKey
                ))
                .ToListAsync(cancellationToken);
            projectionElapsed += Stopwatch.GetElapsedTime(projectionStartedAt);
            projectionCount += batchRows.Count;
            rows.AddRange(batchRows);
            cursor = parents[^1];
            if (parents.Count < parentBatchSize)
                break;
        }

        LogMediaStageTimings(PlexMediaType.Movie, parentScanElapsed, parentCount, projectionElapsed, projectionCount);
        return (rows.Take(fetchLimit).ToList(), total);
    }

    private async Task<(List<TorznabFeedCandidate> Rows, int Total)> LoadEpisodeRows(
        IReadOnlyCollection<int> accessibleLibraryIds,
        int[] categories,
        int fetchLimit,
        CancellationToken cancellationToken
    )
    {
        var mediaDataQuery = _dbContext
            .PlexTvShowEpisodeData.TagWith("Torznab/Rss/Episode/Releases")
            .Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId))
            .ApplyTorznabCategories(categories);
        int total;
        if (categories.Length == 0 || categories.Contains((int)TorznabCategoryId.TV))
        {
            total = await _dbContext
                .PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow && accessibleLibraryIds.Contains(x.Id))
                .SumAsync(x => x.EpisodeMediaDataCount, cancellationToken);
        }
        else
        {
            var fallbackCountStartedAt = Stopwatch.GetTimestamp();
            total = await mediaDataQuery.CountAsync(cancellationToken);
            LogFallbackCategoryCountTiming(PlexMediaType.Episode, fallbackCountStartedAt, total);
        }

        var parentBatchSize = Math.Max(fetchLimit, MinimumParentBatchSize);
        TorznabFeedParent? cursor = null;
        var parentScanElapsed = TimeSpan.Zero;
        var projectionElapsed = TimeSpan.Zero;
        var parentCount = 0;
        var projectionCount = 0;
        var rows = new List<TorznabFeedCandidate>(fetchLimit);
        while (rows.Count < fetchLimit)
        {
            var parentScanStartedAt = Stopwatch.GetTimestamp();
            var parents = await LoadParentBatch(
                PlexMediaType.Episode,
                accessibleLibraryIds,
                parentBatchSize,
                cursor,
                cancellationToken
            );
            parentScanElapsed += Stopwatch.GetElapsedTime(parentScanStartedAt);
            parentCount += parents.Count;
            if (parents.Count == 0)
                break;

            var episodeIds = parents.Select(x => x.Id).ToList();
            var projectionStartedAt = Stopwatch.GetTimestamp();
            var batchRows = await mediaDataQuery
                .Where(x => episodeIds.Contains(x.PlexTvShowEpisodeId))
                .OrderByDescending(x => x.PlexTvShowEpisode!.AddedAt)
                .ThenByDescending(x => x.PlexTvShowEpisode!.PlexServerId)
                .ThenByDescending(x => x.PlexTvShowEpisode!.PlexApiRatingKey)
                .ThenByDescending(x => x.Id)
                .Select(x => new TorznabFeedCandidate(
                    PlexMediaType.Episode,
                    x.Id,
                    x.PlexTvShowEpisode!.AddedAt,
                    x.PlexServerId,
                    x.PlexApiRatingKey
                ))
                .ToListAsync(cancellationToken);
            projectionElapsed += Stopwatch.GetElapsedTime(projectionStartedAt);
            projectionCount += batchRows.Count;
            rows.AddRange(batchRows);
            cursor = parents[^1];
            if (parents.Count < parentBatchSize)
                break;
        }

        LogMediaStageTimings(PlexMediaType.Episode, parentScanElapsed, parentCount, projectionElapsed, projectionCount);
        return (rows.Take(fetchLimit).ToList(), total);
    }

    private async Task<List<TorznabFeedItemProjection>> HydrateFeedItemsAsync(
        IReadOnlyCollection<TorznabFeedCandidate> candidates,
        CancellationToken cancellationToken
    )
    {
        var rowsByKey = new Dictionary<(PlexMediaType MediaType, int DataId), TorznabFeedItemProjection>(
            candidates.Count
        );
        var movieDataIds = candidates.Where(x => x.MediaType == PlexMediaType.Movie).Select(x => x.DataId).ToList();
        if (movieDataIds.Count > 0)
        {
            var movieRows = await _dbContext
                .PlexMovieData.TagWith("Torznab/Rss/Movie/Hydrate")
                .Where(x => movieDataIds.Contains(x.Id))
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
            foreach (var row in movieRows)
                rowsByKey[(PlexMediaType.Movie, row.DataId)] = row;
        }

        var episodeDataIds = candidates.Where(x => x.MediaType == PlexMediaType.Episode).Select(x => x.DataId).ToList();
        if (episodeDataIds.Count > 0)
        {
            var episodeRows = await _dbContext
                .PlexTvShowEpisodeData.TagWith("Torznab/Rss/Episode/Hydrate")
                .Where(x => episodeDataIds.Contains(x.Id))
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
            foreach (var row in episodeRows)
                rowsByKey[(PlexMediaType.Episode, row.DataId)] = row;
        }

        return candidates
            .Select(x => rowsByKey.GetValueOrDefault((x.MediaType, x.DataId)))
            .OfType<TorznabFeedItemProjection>()
            .ToList();
    }

    private void LogFallbackCategoryCountTiming(PlexMediaType mediaType, long startedAt, int total) =>
        _log.Here()
            .Information(
                "Torznab RSS fallback category count completed in {ElapsedMilliseconds} ms for {MediaType} with {Total} items",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                mediaType,
                total
            );

    private void LogMediaStageTimings(
        PlexMediaType mediaType,
        TimeSpan parentScanElapsed,
        int parentCount,
        TimeSpan projectionElapsed,
        int projectionCount
    )
    {
        _log.Here()
            .Information(
                "Torznab RSS parent scan completed in {ElapsedMilliseconds} ms for {MediaType} with {ParentCount} parents",
                parentScanElapsed.TotalMilliseconds,
                mediaType,
                parentCount
            );
        _log.Here()
            .Information(
                "Torznab RSS media projection completed in {ElapsedMilliseconds} ms for {MediaType} with {ProjectionCount} rows",
                projectionElapsed.TotalMilliseconds,
                mediaType,
                projectionCount
            );
    }

    private Task<List<TorznabFeedParent>> LoadParentBatch(
        PlexMediaType mediaType,
        IReadOnlyCollection<int> accessibleLibraryIds,
        int batchSize,
        TorznabFeedParent? cursor,
        CancellationToken cancellationToken
    ) =>
        mediaType switch
        {
            PlexMediaType.Movie => LoadParentBatch(
                _dbContext.PlexMovies.TagWith("Torznab/Rss/Movie/Parents"),
                accessibleLibraryIds,
                batchSize,
                cursor,
                cancellationToken
            ),
            PlexMediaType.Episode => LoadParentBatch(
                _dbContext.PlexTvShowEpisodes.TagWith("Torznab/Rss/Episode/Parents"),
                accessibleLibraryIds,
                batchSize,
                cursor,
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(mediaType), mediaType, null),
        };

    private static async Task<List<TorznabFeedParent>> LoadParentBatch<T>(
        IQueryable<T> query,
        IReadOnlyCollection<int> accessibleLibraryIds,
        int batchSize,
        TorznabFeedParent? cursor,
        CancellationToken cancellationToken
    )
        where T : BasePlexMedia
    {
        if (accessibleLibraryIds.Count == 0)
            return [];

        var libraryQuery = query.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId));
        if (cursor is not null)
        {
            libraryQuery = libraryQuery.Where(x =>
                x.AddedAt < cursor.AddedAt
                || (
                    x.AddedAt == cursor.AddedAt
                    && (
                        x.PlexServerId < cursor.PlexServerId
                        || (
                            x.PlexServerId == cursor.PlexServerId
                            && (
                                x.PlexApiRatingKey < cursor.PlexApiRatingKey
                                || (x.PlexApiRatingKey == cursor.PlexApiRatingKey && x.Id < cursor.Id)
                            )
                        )
                    )
                )
            );
        }

        return await libraryQuery
            .OrderByDescending(x => x.AddedAt)
            .ThenByDescending(x => x.PlexServerId)
            .ThenByDescending(x => x.PlexApiRatingKey)
            .ThenByDescending(x => x.Id)
            .Take(batchSize)
            .Select(x => new TorznabFeedParent(x.Id, x.AddedAt, x.PlexServerId, x.PlexApiRatingKey))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Minimal parent projection carried between keyset batches and media-data projection.
    /// </summary>
    private sealed record TorznabFeedParent(int Id, DateTime AddedAt, int PlexServerId, int PlexApiRatingKey);

    /// <summary>
    /// Narrow release projection used for global ordering before the requested page is hydrated.
    /// </summary>
    private sealed record TorznabFeedCandidate(
        PlexMediaType MediaType,
        int DataId,
        DateTime AddedAt,
        int PlexServerId,
        int PlexApiRatingKey
    );

    private static TorznabMediaSearchResponseDTO CreateResponse(int offset, int total, List<TorznabItem> items) =>
        new()
        {
            Channel = new TorznabChannel
            {
                Title = "Reaparr Indexer",
                Description = "Reaparr RSS feed",
                Language = "en-us",
                Category = "search",
                Items = items,
                Response = new TorznabResponseMetadata { Offset = offset, Total = total },
            },
        };
}
