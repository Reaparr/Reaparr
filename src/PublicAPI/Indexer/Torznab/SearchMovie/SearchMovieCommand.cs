using System.Diagnostics.CodeAnalysis;
using Reaparr.Application.Contracts;
using Reaparr.Environment;

// ReSharper disable InconsistentNaming

namespace Reaparr.PublicAPI;

public record SearchMovieCommand : ICommand<Result<TorznabMediaSearchResponseDTO>>
{
    [SetsRequiredMembers]
    public SearchMovieCommand()
    {
        Query = string.Empty;
        Integration = new IntegrationIdentity(IntegrationType.Radarr, Guid.Empty);
    }

    public required string Query { get; init; }
    public required int Limit { get; init; }
    public required int Offset { get; init; }
    public string? IMDB_ID { get; init; }
    public required int TMDB_ID { get; init; }
    public required IntegrationIdentity Integration { get; init; }
    public string TorznabApiKey { get; init; } = string.Empty;
    public int[] Categories { get; init; } = [];
    public string[] Attributes { get; init; } = [];
    public bool IncludeAllAttributes { get; init; } = true;
    public bool DisablePaging { get; init; }
}

public class SearchMovieCommandValidator : AbstractValidator<SearchMovieCommand>
{
    public SearchMovieCommandValidator()
    {
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset, x.Limit))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
        RuleFor(x => x.TMDB_ID).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Categories).NotNull();
        RuleFor(x => x.Attributes).NotNull();
    }
}

public class SearchMovieCommandHandler : ICommandHandler<SearchMovieCommand, Result<TorznabMediaSearchResponseDTO>>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly INetworkSettings _networkSettings;

    public SearchMovieCommandHandler(
        ILogger log,
        IReaparrDbContext dbContext,
        IAppRuntimeInfo appRuntimeInfo,
        INetworkSettings networkSettings
    )
    {
        _log = log.ForContext<SearchMovieCommandHandler>();
        _dbContext = dbContext;
        _appRuntimeInfo = appRuntimeInfo;
        _networkSettings = networkSettings;
    }

    public async Task<Result<TorznabMediaSearchResponseDTO>> ExecuteAsync(
        SearchMovieCommand command,
        CancellationToken cancellationToken
    )
    {
        var onlineServerIds = await _dbContext.GetDownloadableServerIds();
        if (onlineServerIds.Count == 0)
        {
            _log.Here().Warning("No online Plex servers with downloads enabled were found, returning empty search results");
            return Result.Ok(
                TorznabSearchHelpers.CreateResponse(
                    $"Movie Search results for {command.Query}",
                    command.Offset,
                    0,
                    []
                )
            );
        }

        var query = _dbContext.PlexMovieData.Where(x =>
            onlineServerIds.Contains(x.PlexServerId)
            && x.PlexMovie!.PlexLibrary!.PlexAccountLibraries.Any(libraryAccess =>
                x.PlexMovie.PlexServer!.PlexAccountServers.Any(serverAccess =>
                    serverAccess.PlexAccountId == libraryAccess.PlexAccountId
                )
            )
        );

        if (!string.IsNullOrWhiteSpace(command.Query))
        {
            var searchTitle = command.Query.ToSearchTitle();
            if (string.IsNullOrWhiteSpace(searchTitle))
                return Result.Ok(
                    TorznabSearchHelpers.CreateResponse(
                        $"Movie Search results for {command.Query}",
                        command.Offset,
                        0,
                        []
                    )
                );
            query = query.Where(x => EF.Functions.Like(x.PlexMovie!.SearchTitle, $"{searchTitle}%"));
        }

        if (!string.IsNullOrWhiteSpace(command.IMDB_ID))
            query = query.Where(x => x.PlexMovie!.Guid_IMDB == "tt" + command.IMDB_ID);
        if (command.TMDB_ID > 0)
            query = query.Where(x => x.PlexMovie!.Guid_TMDB == command.TMDB_ID);

        query = query.ApplyTorznabCategories(command.Categories);
        var total = await query.CountAsync(cancellationToken);
        var orderedQuery = query
            .OrderByDescending(x => x.PlexMovie!.AddedAt)
            .ThenBy(x => x.PlexServer!.MachineIdentifier)
            .ThenBy(x => x.PlexMovie!.PlexApiRatingKey)
            .ThenBy(x => x.PlexApiMediaId)
            .ThenBy(x => x.PlexApiPartId);
        var rows = command.DisablePaging
            ? await orderedQuery.ProjectToTorznabFeedItems().ToListAsync(cancellationToken)
            : await orderedQuery
                .Skip(command.Offset)
                .Take(command.Limit)
                .ProjectToTorznabFeedItems()
                .ToListAsync(cancellationToken);
        var requestedAttributes = TorznabSearchHelpers.GetRequestedAttributes(
            command.IncludeAllAttributes,
            command.Attributes
        );
        var items = rows
            .Select(x =>
                x.ToTorznabItem(
                    command.Integration,
                    command.TorznabApiKey,
                    _networkSettings.Url,
                    requestedAttributes,
                    _appRuntimeInfo.IsDevelopmentEnvironment
                )
            )
            .ToList();
        return Result.Ok(
            TorznabSearchHelpers.CreateResponse(
                $"Movie Search results for {command.Query}",
                command.Offset,
                total,
                items
            )
        );
    }
}
