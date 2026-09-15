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
                onlineServerIds,
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
                onlineServerIds,
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
        IReadOnlyCollection<int> onlineServerIds,
        IReadOnlyCollection<int> accessibleLibraryIds,
        int[] categories,
        int fetchLimit,
        CancellationToken cancellationToken
    )
    {
        var movieQuery = _dbContext.PlexMovies;
        var mediaDataQuery = _dbContext
            .PlexMovieData.Where(x =>
                onlineServerIds.Contains(x.PlexMovie!.PlexServerId)
                && accessibleLibraryIds.Contains(x.PlexMovie.PlexLibraryId)
            )
            .ApplyTorznabCategories(categories);
        var total = await mediaDataQuery.CountAsync(cancellationToken);
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
        IReadOnlyCollection<int> onlineServerIds,
        IReadOnlyCollection<int> accessibleLibraryIds,
        int[] categories,
        int fetchLimit,
        CancellationToken cancellationToken
    )
    {
        var episodeQuery = _dbContext.PlexTvShowEpisodes;
        var mediaDataQuery = _dbContext
            .PlexTvShowEpisodeData.Where(x =>
                onlineServerIds.Contains(x.PlexTvShowEpisode!.PlexServerId)
                && accessibleLibraryIds.Contains(x.PlexTvShowEpisode.PlexLibraryId)
            )
            .ApplyTorznabCategories(categories);
        var total = await mediaDataQuery.CountAsync(cancellationToken);
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
