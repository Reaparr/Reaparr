using System.Globalization;
using Reaparr.Application.Contracts;

namespace Reaparr.PublicAPI;

public class TorznabEndpointRequestValidator : Validator<TorznabEndpointRequest>
{
    public TorznabEndpointRequestValidator()
    {
        RuleFor(x => x.ParsedType).NotEqual(TorznabQueryType.Unknown);
        RuleFor(x => x.ApiKey).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0).When(x => x.Offset.HasValue);
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0).When(x => x.Limit.HasValue);
        RuleFor(x => x.Season).GreaterThanOrEqualTo(0).When(x => x.Season.HasValue);
        RuleFor(x => x.Episode).GreaterThanOrEqualTo(0).When(x => x.Episode.HasValue);
        RuleFor(x => x.TvdbId).GreaterThanOrEqualTo(0).When(x => x.TvdbId.HasValue);
        RuleFor(x => x.TmdbId).GreaterThanOrEqualTo(0).When(x => x.TmdbId.HasValue);
        RuleFor(x => x.Extended).Must(x => x is null or 0 or 1);
        RuleFor(x => x.Attributes).Matches("^[a-zA-Z]+(,[a-zA-Z]+)*$").When(x => !string.IsNullOrEmpty(x.Attributes));
    }
}

public sealed class TorznabEndpoint : Endpoint<TorznabEndpointRequest>
{
    private readonly ILogger _log;
    private readonly ICommandExecutor _commandExecutor;

    public TorznabEndpoint(ILogger logger, ICommandExecutor commandExecutor)
    {
        _log = logger.ForContext<TorznabEndpoint>();
        _commandExecutor = commandExecutor;
    }

    public override void Configure()
    {
        Get(PublicApiRoutes.Indexer);
        Description(x =>
        {
            x.IsIndexer();
            x.Produces<BaseResultDTO>();
            x.Produces<BaseResultDTO>(StatusCodes.Status401Unauthorized);
            x.Produces<BaseResultDTO>(StatusCodes.Status403Forbidden);
            x.Produces<BaseResultDTO>(StatusCodes.Status500InternalServerError);
        });
        AllowAnonymous();
        PreProcessor<IndexerAuthenticationPreProcessor<TorznabEndpointRequest>>();
        DontThrowIfValidationFails();
    }

    public override async Task HandleAsync(TorznabEndpointRequest endpointRequest, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, endpointRequest);
        var integration = HttpContext.GetIntegrationIdentity();
        var request = endpointRequest.ToTorznabRequest();

        if (ValidationFailed)
        {
            await Send.TorznabError(201, "Incorrect parameter", ct);
            return;
        }

        switch (request.Mode)
        {
            case TorznabRequestMode.Capabilities:
                await SendCapabilitiesAsync(ct);
                break;
            case TorznabRequestMode.Rss:
                await SendMediaResultAsync(await GetRssAsync(request, integration, ct), ct);
                break;
            case TorznabRequestMode.ActiveSearch:
                await SendMediaResultAsync(await SearchAsync(request, integration, ct), ct);
                break;
            default:
                await Send.TorznabError(201, "Incorrect parameter", ct);
                break;
        }
    }

    private async Task SendCapabilitiesAsync(CancellationToken ct)
    {
        var result = await _commandExecutor.Send(new GetCapabilitiesCommand(), ct);
        result.LogIfFailed();

        if (result.IsCancelled)
        {
            await Send.TorznabError(900, "Indexer request cancelled", ct);
            return;
        }

        if (result.IsFailed)
        {
            await Send.TorznabError(900, "Indexer request failed", ct);
            return;
        }

        await Send.XmlAsync(result.Value, cancellationToken: ct);
    }

    private async Task SendMediaResultAsync(Result<TorznabMediaSearchResponseDTO> result, CancellationToken ct)
    {
        result.LogIfFailed();

        if (result.IsCancelled)
        {
            await Send.TorznabError(900, "Indexer request cancelled", ct);
            return;
        }

        if (result.IsFailed)
        {
            await Send.TorznabError(900, "Indexer request failed", ct);
            return;
        }

        await Send.XmlAsync(result.Value, cancellationToken: ct);
    }

    private Task<Result<TorznabMediaSearchResponseDTO>> GetRssAsync(
        TorznabRequest request,
        IntegrationIdentity integration,
        CancellationToken ct
    ) =>
        _commandExecutor.Send(
            new GetTorznabRssFeedCommand
            {
                Integration = integration,
                Categories = request.Categories,
                IncludeMovies = request.IncludesMovies,
                IncludeEpisodes = request.IncludesEpisodes,
                Limit = request.Limit,
                Offset = request.Offset,
                TorznabApiKey = request.ApiKey,
                Attributes = request.Attributes,
                IncludeAllAttributes = request.IncludeAllAttributes,
            },
            ct
        );

    private Task<Result<TorznabMediaSearchResponseDTO>> SearchAsync(
        TorznabRequest request,
        IntegrationIdentity integration,
        CancellationToken ct
    ) =>
        request.Type switch
        {
            TorznabQueryType.Search => GenericSearchAsync(request, integration, ct),
            TorznabQueryType.TvSearch => SearchTvAsync(request, integration, ct),
            TorznabQueryType.Movie => SearchMovieAsync(request, integration, ct),
            _ => Task.FromResult(Result.Fail<TorznabMediaSearchResponseDTO>("Unsupported Torznab search type")),
        };

    private Task<Result<TorznabMediaSearchResponseDTO>> SearchTvAsync(
        TorznabRequest request,
        IntegrationIdentity integration,
        CancellationToken ct
    ) =>
        _commandExecutor.Send(
            new SearchTvShowCommand
            {
                Query = request.Query,
                Season = request.Season,
                Episode = request.Episode,
                TVDB_ID = request.TvdbId,
                IMDB_ID = request.ImdbId,
                TMDB_ID = request.TmdbId,
                Limit = request.Limit,
                Offset = request.Offset,
                Integration = integration,
                TorznabApiKey = request.ApiKey,
                Categories = request.Categories,
                Attributes = request.Attributes,
                IncludeAllAttributes = request.IncludeAllAttributes,
            },
            ct
        );

    private Task<Result<TorznabMediaSearchResponseDTO>> SearchMovieAsync(
        TorznabRequest request,
        IntegrationIdentity integration,
        CancellationToken ct
    ) =>
        _commandExecutor.Send(
            new SearchMovieCommand
            {
                Query = request.Query,
                IMDB_ID = request.ImdbId,
                TMDB_ID = request.TmdbId,
                Limit = request.Limit,
                Offset = request.Offset,
                Integration = integration,
                TorznabApiKey = request.ApiKey,
                Categories = request.Categories,
                Attributes = request.Attributes,
                IncludeAllAttributes = request.IncludeAllAttributes,
            },
            ct
        );

    /// <summary>
    /// Handles generic active searches by running the TV and movie searches requested by categories.
    /// </summary>
    private async Task<Result<TorznabMediaSearchResponseDTO>> GenericSearchAsync(
        TorznabRequest request,
        IntegrationIdentity integration,
        CancellationToken ct
    )
    {
        var items = new List<TorznabItem>();
        var failures = new List<IError>();
        var successfulSearches = 0;
        var total = 0;
        var branchRequest = request with { Offset = 0, Limit = request.Offset + request.Limit };

        if (request.IncludesEpisodes)
        {
            var tvResult = await SearchTvAsync(branchRequest with { Season = 0, Episode = 0 }, integration, ct);
            if (tvResult.IsFailed)
                failures.AddRange(tvResult.Errors);
            else
            {
                successfulSearches++;
                total += tvResult.Value.Channel.Response.Total;
                items.AddRange(tvResult.Value.Channel.Items);
            }
        }

        if (request.IncludesMovies)
        {
            var movieResult = await SearchMovieAsync(branchRequest, integration, ct);
            if (movieResult.IsFailed)
                failures.AddRange(movieResult.Errors);
            else
            {
                successfulSearches++;
                total += movieResult.Value.Channel.Response.Total;
                items.AddRange(movieResult.Value.Channel.Items);
            }
        }

        if (failures.Count > 0 && successfulSearches == 0)
            return Result.Fail(failures);

        if (failures.Count > 0)
            _log.Here().Warning("Generic Torznab search partially failed: {Errors}", failures);

        return Result.Ok(
            new TorznabMediaSearchResponseDTO
            {
                Channel = new TorznabChannel
                {
                    Title = "Reaparr Indexer",
                    Description = $"Search results for {request.Query}",
                    Items = items
                        .OrderByDescending(item =>
                            DateTimeOffset.Parse(item.PubDate, CultureInfo.InvariantCulture)
                        )
                        .ThenBy(item => item.Guid.Value, StringComparer.Ordinal)
                        .Skip(request.Offset)
                        .Take(request.Limit)
                        .ToList(),
                    Response = new TorznabResponseMetadata { Offset = request.Offset, Total = total },
                },
            }
        );
    }
}
