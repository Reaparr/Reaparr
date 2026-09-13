using System.Diagnostics.CodeAnalysis;
using Reaparr.Application.Contracts;

// ReSharper disable InconsistentNaming

namespace Reaparr.PublicAPI;

public record SearchTvShowCommand : ICommand<Result<TorznabMediaSearchResponseDTO>>
{
    [SetsRequiredMembers]
    public SearchTvShowCommand()
    {
        Query = string.Empty;
        IMDB_ID = string.Empty;
        Integration = new IntegrationIdentity(IntegrationType.Sonarr, Guid.Empty);
    }

    public required string Query { get; init; }
    public required int Season { get; init; }
    public required int Episode { get; init; }
    public required int Limit { get; init; }
    public required int Offset { get; init; }
    public required int TVDB_ID { get; init; }
    public required int TMDB_ID { get; init; }
    public required string IMDB_ID { get; init; }
    public required IntegrationIdentity Integration { get; init; }
    public string TorznabApiKey { get; init; } = string.Empty;
    public int[] Categories { get; init; } = [];
    public string[] Attributes { get; init; } = [];
    public bool IncludeAllAttributes { get; init; } = true;
}

public class SearchTvShowCommandValidator : AbstractValidator<SearchTvShowCommand>
{
    public SearchTvShowCommandValidator()
    {
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset, x.Limit))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
        RuleFor(x => x.Season).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Episode).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TMDB_ID).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TVDB_ID).GreaterThanOrEqualTo(0);
        RuleFor(x => x.IMDB_ID).NotNull();
        RuleFor(x => x.Categories).NotNull();
        RuleFor(x => x.Attributes).NotNull();
        When(
            x => x.Season > 0 || x.Episode > 0,
            () =>
            {
                RuleFor(x => x.Season).GreaterThan(0);
                RuleFor(x => x)
                    .Must(HasAnyExternalId)
                    .WithMessage(
                        "Provide at least one of IMDB_ID, TMDB_ID, or TVDB_ID when Season/Episode are specified."
                    );
            }
        );
    }

    private static bool HasAnyExternalId(SearchTvShowCommand cmd) =>
        !string.IsNullOrWhiteSpace(cmd.IMDB_ID) || cmd.TMDB_ID > 0 || cmd.TVDB_ID > 0;
}

public class SearchTvShowCommandHandler : ICommandHandler<SearchTvShowCommand, Result<TorznabMediaSearchResponseDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly INetworkSettings _networkSettings;

    public SearchTvShowCommandHandler(ILogger log, IReaparrDbContext dbContext, INetworkSettings networkSettings)
    {
        _log = log.ForContext<SearchTvShowCommandHandler>();
        _dbContext = dbContext;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        SearchTvShowCommand command,
        CancellationToken cancellationToken
    )
    {
        var onlineServerIds = await _dbContext.GetDownloadableServerIds();
        if (onlineServerIds.Count == 0)
        {
            _log.Here()
                .Warning("No online Plex servers with downloads enabled were found, returning empty search results");
            return Result.Ok(
                TorznabSearchHelpers.CreateResponse($"TV Search results for {command.Query}", command.Offset, 0, [])
            );
        }

        var query = _dbContext.PlexTvShowEpisodeData.Where(x =>
            onlineServerIds.Contains(x.PlexServerId)
            && x.PlexTvShowEpisode!.TvShow!.PlexLibrary!.PlexAccountLibraries.Any(libraryAccess =>
                x.PlexTvShowEpisode.TvShow.PlexServer!.PlexAccountServers.Any(serverAccess =>
                    serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                )
            )
        );

        if (!string.IsNullOrWhiteSpace(command.Query))
        {
            var searchTitle = command.Query.ToSearchTitle();
            if (string.IsNullOrWhiteSpace(searchTitle))
                return Result.Ok(
                    TorznabSearchHelpers.CreateResponse($"TV Search results for {command.Query}", command.Offset, 0, [])
                );
            query = query.Where(x => EF.Functions.Like(x.PlexTvShowEpisode!.TvShow!.SearchTitle, $"{searchTitle}%"));
        }

        if (!string.IsNullOrWhiteSpace(command.IMDB_ID))
        {
            var imdbId = command.IMDB_ID.StartsWith("tt", StringComparison.OrdinalIgnoreCase)
                ? command.IMDB_ID
                : $"tt{command.IMDB_ID}";
            query = query.Where(x => x.PlexTvShowEpisode!.TvShow!.Guid_IMDB == imdbId);
        }
        if (command.TMDB_ID > 0)
            query = query.Where(x => x.PlexTvShowEpisode!.TvShow!.Guid_TMDB == command.TMDB_ID);
        if (command.TVDB_ID > 0)
            query = query.Where(x => x.PlexTvShowEpisode!.TvShow!.Guid_TVDB == command.TVDB_ID);
        if (command.Season > 0)
            query = query.Where(x => x.PlexTvShowEpisode!.TvShowSeason!.SeasonNumber == command.Season);
        if (command.Episode > 0)
            query = query.Where(x => x.PlexTvShowEpisode!.EpisodeNumber == command.Episode);

        query = query.ApplyTorznabCategories(command.Categories);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.PlexTvShowEpisodeId)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId)
            .Skip(command.Offset)
            .Take(command.Limit)
            .ProjectToTorznabFeedItems()
            .ToListAsync(cancellationToken);

        var requestedAttributes = TorznabSearchHelpers.GetRequestedAttributes(
            command.IncludeAllAttributes,
            command.Attributes
        );
        var items = rows.Select(x =>
                x.ToTorznabItem(command.Integration, command.TorznabApiKey, _networkSettings.Url, requestedAttributes)
            )
            .ToList();
        return Result.Ok(
            TorznabSearchHelpers.CreateResponse($"TV Search results for {command.Query}", command.Offset, total, items)
        );
    }
}
