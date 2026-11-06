namespace Reaparr.PublicAPI;

public class TorznabEndpointRequestValidator : Validator<TorznabEndpointRequest>
{
    public TorznabEndpointRequestValidator()
    {
        RuleFor(x => x.ParsedType).NotEqual(TorznabQueryType.Unknown);
        RuleFor(x => x.ApiKey).NotEmpty();
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0).When(x => x.Offset.HasValue);
        RuleFor(x => x.Limit).GreaterThanOrEqualTo(0).When(x => x.Limit.HasValue);
        RuleFor(x => x.Limit).LessThanOrEqualTo(TorznabSearchHelpers.MaxPageSize).When(x => x.Limit.HasValue);
        RuleFor(x => x)
            .Must(x => TorznabSearchHelpers.IsPaginationWithinLimit(x.Offset ?? 0, x.Limit ?? 50))
            .WithMessage($"The combined offset and limit must not exceed {TorznabSearchHelpers.MaxPaginationWindow}.");
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
    private readonly ITelemetry _telemetry;

    public TorznabEndpoint(ILogger logger, ICommandExecutor commandExecutor, ITelemetry telemetry)
    {
        _log = logger.ForContext<TorznabEndpoint>();
        _commandExecutor = commandExecutor;
        _telemetry = telemetry;
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
        var request = endpointRequest.ToTorznabRequest(integration);

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
            {
                var result = await GetRssAsync(request, ct);
                _telemetry.ExternalIndexerQueryFinished(request.ToTelemetryReport(result), result);
                await SendMediaResultAsync(result, ct);
                break;
            }
            case TorznabRequestMode.ActiveSearch:
            {
                var result = await SearchAsync(request, ct);
                _telemetry.ExternalIndexerQueryFinished(request.ToTelemetryReport(result), result);
                await SendMediaResultAsync(result, ct);
                break;
            }
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
            _telemetry.CaptureErrors(result, TelemetryActivity.ConnectionCheck);
            await Send.TorznabError(900, "Indexer request failed", ct);
            return;
        }

        await Send.XmlAsync(result.Value, cancellationToken: ct);
    }

    private async Task SendMediaResultAsync(Result<TorznabMediaSearchResponseDTO> result, CancellationToken ct)
    {
        result.LogIfFailed();
        if (result.IsCancelled)
            return;

        if (result.IsFailed)
        {
            _telemetry.CaptureErrors(result, TelemetryActivity.ConnectionCheck);
            await Send.TorznabError(900, "Indexer request failed", ct);
            return;
        }

        await Send.XmlAsync(result.Value, cancellationToken: ct);
    }

    private Task<Result<TorznabMediaSearchResponseDTO>> GetRssAsync(TorznabRequest request, CancellationToken ct) =>
        _commandExecutor.Send(
            new GetTorznabRssFeedCommand
            {
                Integration = request.Integration,
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

    private Task<Result<TorznabMediaSearchResponseDTO>> SearchAsync(TorznabRequest request, CancellationToken ct) =>
        request.Type switch
        {
            TorznabQueryType.Search => _commandExecutor.Send(new SearchGenericCommand(request), ct),
            TorznabQueryType.TvSearch => _commandExecutor.Send(new SearchTvShowCommand(request), ct),
            TorznabQueryType.Movie => _commandExecutor.Send(new SearchMovieCommand(request), ct),
            _ => Task.FromResult(Result.Fail<TorznabMediaSearchResponseDTO>("Unsupported Torznab search type")),
        };
}
