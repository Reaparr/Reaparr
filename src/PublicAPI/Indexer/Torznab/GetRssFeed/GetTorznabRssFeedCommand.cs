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
        var queries = new List<IQueryable<TorznabFeedItemProjection>>(2);
        if (command.IncludeMovies)
            queries.Add(CreateMovieQuery(command.Categories));
        if (command.IncludeEpisodes)
            queries.Add(CreateEpisodeQuery(command.Categories));

        var total = 0;
        var rows = new List<TorznabFeedItemProjection>();
        foreach (var query in queries)
        {
            total += await query.CountAsync(cancellationToken);
            rows.AddRange(
                await query
                    .OrderByDescending(x => x.AddedAt)
                    .ThenByDescending(x => x.PlexServerMachineIdentifier)
                    .ThenByDescending(x => x.PlexApiMediaId)
                    .ThenByDescending(x => x.PlexApiPartId)
                    .Take(command.Offset + command.Limit)
                    .ToListAsync(cancellationToken)
            );
        }

        var requestedAttributes = TorznabSearchHelpers.GetRequestedAttributes(
            command.IncludeAllAttributes,
            command.Attributes
        );
        var items = rows.OrderByDescending(x => x.AddedAt)
            .ThenByDescending(x => x.PlexServerMachineIdentifier)
            .ThenByDescending(x => x.PlexApiMediaId)
            .ThenByDescending(x => x.PlexApiPartId)
            .Skip(command.Offset)
            .Take(command.Limit)
            .Select(x =>
                x.ToTorznabItem(command.Integration, command.TorznabApiKey, _networkSettings.Url, requestedAttributes)
            )
            .ToList();
        return Result.Ok(CreateResponse(command.Offset, total, items));
    }

    private IQueryable<TorznabFeedItemProjection> CreateMovieQuery(int[] categories)
    {
        var query = _dbContext
            .PlexMovies.Where(movie =>
                movie.PlexServer!.ServerStatus.Any(status => status.IsSuccessful)
                && movie.PlexServer.IsEnabled
                && !movie.PlexServer.IsDownloadsPausedByUser
                && movie.PlexLibrary!.PlexAccountLibraries.Any(libraryAccess =>
                    movie.PlexServer.PlexAccountServers.Any(serverAccess =>
                        serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                    )
                )
            )
            .SelectMany(movie => movie.MediaDataList);
        return query.ApplyTorznabCategories(categories).ProjectToTorznabFeedItems();
    }

    private IQueryable<TorznabFeedItemProjection> CreateEpisodeQuery(int[] categories)
    {
        var query = _dbContext
            .PlexTvShowEpisodes.Where(episode =>
                episode.PlexServer!.ServerStatus.Any(status => status.IsSuccessful)
                && episode.PlexServer.IsEnabled
                && !episode.PlexServer.IsDownloadsPausedByUser
                && episode.TvShow!.PlexLibrary!.PlexAccountLibraries.Any(libraryAccess =>
                    episode.PlexServer.PlexAccountServers.Any(serverAccess =>
                        serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                    )
                )
            )
            .SelectMany(episode => episode.MediaDataList);
        return query.ApplyTorznabCategories(categories).ProjectToTorznabFeedItems();
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
