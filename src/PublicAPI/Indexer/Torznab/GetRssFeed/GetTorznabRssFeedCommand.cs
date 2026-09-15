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

        var accessibleLibraryIds = await _dbContext.GetAccessibleLibraryIds(onlineServerIds, cancellationToken);
        if (accessibleLibraryIds.Count == 0)
            return Result.Ok(CreateResponse(command.Offset, 0, []));

        var rows = new List<TorznabFeedItemProjection>(fetchLimit * 2);
        var total = 0;
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
        var movieQuery = _dbContext.PlexMovies.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId));
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

        var parentBatchSize = Math.Max(fetchLimit, 256);
        var parentOffset = 0;
        var rows = new List<TorznabFeedItemProjection>(fetchLimit);
        while (rows.Count < fetchLimit)
        {
            var movieIds = await movieQuery
                .OrderByDescending(x => x.AddedAt)
                .ThenByDescending(x => x.PlexServerId)
                .ThenByDescending(x => x.PlexApiRatingKey)
                .Skip(parentOffset)
                .Take(parentBatchSize)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (movieIds.Count == 0)
                break;

            var batchRows = await mediaDataQuery
                .Where(x => movieIds.Contains(x.PlexMovieId))
                .OrderByDescending(x => x.PlexMovie!.AddedAt)
                .ThenByDescending(x => x.PlexMovie!.PlexServerId)
                .ThenByDescending(x => x.PlexMovie!.PlexApiRatingKey)
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
            rows.AddRange(batchRows);
            parentOffset += movieIds.Count;
            if (movieIds.Count < parentBatchSize)
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
        var episodeQuery = _dbContext.PlexTvShowEpisodes.Where(x => accessibleLibraryIds.Contains(x.PlexLibraryId));
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

        var parentBatchSize = Math.Max(fetchLimit, 256);
        var parentOffset = 0;
        var rows = new List<TorznabFeedItemProjection>(fetchLimit);
        while (rows.Count < fetchLimit)
        {
            var episodeIds = await episodeQuery
                .OrderByDescending(x => x.AddedAt)
                .ThenByDescending(x => x.PlexServerId)
                .ThenByDescending(x => x.PlexApiRatingKey)
                .Skip(parentOffset)
                .Take(parentBatchSize)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (episodeIds.Count == 0)
                break;

            var batchRows = await mediaDataQuery
                .Where(x => episodeIds.Contains(x.PlexTvShowEpisodeId))
                .OrderByDescending(x => x.PlexTvShowEpisode!.AddedAt)
                .ThenByDescending(x => x.PlexTvShowEpisode!.PlexServerId)
                .ThenByDescending(x => x.PlexTvShowEpisode!.PlexApiRatingKey)
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
            rows.AddRange(batchRows);
            parentOffset += episodeIds.Count;
            if (episodeIds.Count < parentBatchSize)
                break;
        }

        return (rows.Take(fetchLimit).ToList(), total);
    }

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
