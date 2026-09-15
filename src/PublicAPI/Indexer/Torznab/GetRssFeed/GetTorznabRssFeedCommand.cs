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

    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    public GetTorznabRssFeedCommandHandler(IReaparrDbContext dbContext, INetworkSettings networkSettings)
    {
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        GetTorznabRssFeedCommand command,
        CancellationToken cancellationToken
    )
    {
        var fetchLimit = checked(command.Offset + command.Limit + 1);
        var onlineServerIds = await _dbContext.GetDownloadableServerIds();
        if (onlineServerIds.Count == 0)
            return Result.Ok(CreateResponse(command.Offset, 0, []));

        var accessibleMovieLibraryIds = command.IncludeMovies
            ? await _dbContext.GetAccessibleLibraryIds(onlineServerIds, PlexMediaType.Movie, cancellationToken)
            : [];
        var accessibleTvLibraryIds = command.IncludeEpisodes
            ? await _dbContext.GetAccessibleLibraryIds(onlineServerIds, PlexMediaType.TvShow, cancellationToken)
            : [];
        if (accessibleMovieLibraryIds.Count == 0 && accessibleTvLibraryIds.Count == 0)
            return Result.Ok(CreateResponse(command.Offset, 0, []));

        var rows = new List<TorznabFeedItemProjection>(fetchLimit * 2);
        var total = 0;
        if (command.IncludeMovies)
        {
            var (movieRows, movieTotal) = await LoadMovieRows(
                accessibleMovieLibraryIds,
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
                accessibleTvLibraryIds,
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
        var orderedRows = rows.OrderByDescending(x => x.AddedAt)
            .ThenByDescending(x => x.PlexServerId)
            .ThenByDescending(x => x.PlexApiRatingKey)
            .Take(fetchLimit)
            .ToList();
        var items = orderedRows
            .Skip(command.Offset)
            .Take(command.Limit)
            .Select(x =>
                x.ToTorznabItem(command.Integration, command.TorznabApiKey, _networkSettings.Url, requestedAttributes)
            )
            .ToList();
        return Result.Ok(CreateResponse(command.Offset, total, items));
    }

    private async Task<(List<TorznabFeedItemProjection> Rows, int Total)> LoadMovieRows(
        IReadOnlyCollection<int> accessibleLibraryIds,
        int[] categories,
        int fetchLimit,
        CancellationToken cancellationToken
    )
    {
        var mediaDataQuery = _dbContext
            .PlexMovieData.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId))
            .ApplyTorznabCategories(categories);

        int total;
        if (categories.Length == 0 || categories.Contains((int)TorznabCategoryId.Movies))
        {
            total = await _dbContext
                .PlexLibraries.Where(x => x.Type == PlexMediaType.Movie && accessibleLibraryIds.Contains(x.Id))
                .SumAsync(x => x.MovieMediaDataCount, cancellationToken);
        }
        else
            total = await mediaDataQuery.CountAsync(cancellationToken);

        var parentBatchSize = Math.Max(fetchLimit, MinimumParentBatchSize);
        TorznabFeedParent? cursor = null;
        var rows = new List<TorznabFeedItemProjection>(fetchLimit);
        while (rows.Count < fetchLimit)
        {
            var parents = await LoadParentBatch(
                PlexMediaType.Movie,
                accessibleLibraryIds,
                parentBatchSize,
                cursor,
                cancellationToken
            );
            if (parents.Count == 0)
                break;

            var movieIds = parents.Select(x => x.Id).ToList();
            var batchRows = await mediaDataQuery
                .Where(x => movieIds.Contains(x.PlexMovieId))
                .OrderByDescending(x => x.PlexMovie!.AddedAt)
                .ThenByDescending(x => x.PlexMovie!.PlexServerId)
                .ThenByDescending(x => x.PlexMovie!.PlexApiRatingKey)
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
            rows.AddRange(batchRows);
            cursor = parents[^1];
            if (parents.Count < parentBatchSize)
                break;
        }

        return (rows.Take(fetchLimit).ToList(), total);
    }

    private async Task<(List<TorznabFeedItemProjection> Rows, int Total)> LoadEpisodeRows(
        IReadOnlyCollection<int> accessibleLibraryIds,
        int[] categories,
        int fetchLimit,
        CancellationToken cancellationToken
    )
    {
        var mediaDataQuery = _dbContext
            .PlexTvShowEpisodeData.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId))
            .ApplyTorznabCategories(categories);
        int total;
        if (categories.Length == 0 || categories.Contains((int)TorznabCategoryId.TV))
        {
            total = await _dbContext
                .PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow && accessibleLibraryIds.Contains(x.Id))
                .SumAsync(x => x.EpisodeMediaDataCount, cancellationToken);
        }
        else
            total = await mediaDataQuery.CountAsync(cancellationToken);

        var parentBatchSize = Math.Max(fetchLimit, MinimumParentBatchSize);
        TorznabFeedParent? cursor = null;
        var rows = new List<TorznabFeedItemProjection>(fetchLimit);
        while (rows.Count < fetchLimit)
        {
            var parents = await LoadParentBatch(
                PlexMediaType.Episode,
                accessibleLibraryIds,
                parentBatchSize,
                cursor,
                cancellationToken
            );
            if (parents.Count == 0)
                break;

            var episodeIds = parents.Select(x => x.Id).ToList();
            var batchRows = await mediaDataQuery
                .Where(x => episodeIds.Contains(x.PlexTvShowEpisodeId))
                .OrderByDescending(x => x.PlexTvShowEpisode!.AddedAt)
                .ThenByDescending(x => x.PlexTvShowEpisode!.PlexServerId)
                .ThenByDescending(x => x.PlexTvShowEpisode!.PlexApiRatingKey)
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
            rows.AddRange(batchRows);
            cursor = parents[^1];
            if (parents.Count < parentBatchSize)
                break;
        }

        return (rows.Take(fetchLimit).ToList(), total);
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
                _dbContext.PlexMovies,
                accessibleLibraryIds,
                batchSize,
                cursor,
                cancellationToken
            ),
            PlexMediaType.Episode => LoadParentBatch(
                _dbContext.PlexTvShowEpisodes,
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
        var candidates = new List<TorznabFeedParent>(checked(accessibleLibraryIds.Count * batchSize));
        foreach (var libraryId in accessibleLibraryIds)
        {
            query = query.Where(x => x.PlexLibraryId == libraryId);
            if (cursor is not null)
            {
                query = query.Where(x =>
                    x.AddedAt < cursor.AddedAt
                    || (
                        x.AddedAt == cursor.AddedAt
                        && (
                            x.PlexServerId < cursor.PlexServerId
                            || (x.PlexServerId == cursor.PlexServerId && x.PlexApiRatingKey < cursor.PlexApiRatingKey)
                        )
                    )
                );
            }

            candidates.AddRange(
                await query
                    .OrderByDescending(x => x.AddedAt)
                    .ThenByDescending(x => x.PlexServerId)
                    .ThenByDescending(x => x.PlexApiRatingKey)
                    .Take(batchSize)
                    .Select(x => new TorznabFeedParent(x.Id, x.AddedAt, x.PlexServerId, x.PlexApiRatingKey))
                    .ToListAsync(cancellationToken)
            );
        }

        return candidates
            .OrderByDescending(x => x.AddedAt)
            .ThenByDescending(x => x.PlexServerId)
            .ThenByDescending(x => x.PlexApiRatingKey)
            .Take(batchSize)
            .ToList();
    }

    /// <summary>
    /// Minimal parent projection carried between keyset batches and media-data projection.
    /// </summary>
    private sealed record TorznabFeedParent(int Id, DateTime AddedAt, int PlexServerId, int PlexApiRatingKey);

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
