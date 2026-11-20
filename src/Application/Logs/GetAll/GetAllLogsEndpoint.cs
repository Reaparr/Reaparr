namespace Reaparr.Application;

public class GetAllLogsEndpoint : EndpointWithoutRequest<LiveLogEventDTO[]>
{
    private readonly ILogger _log;
    private readonly ILogBufferService _logBufferService;

    public GetAllLogsEndpoint(ILogger log, ILogBufferService logBufferService)
    {
        _log = log.ForContext<GetAllLogsEndpoint>();
        _logBufferService = logBufferService;
    }

    public override void Configure()
    {
        Get(ApiRoutes.DebugController + "/logs/");

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<LiveLogEventDTO[]>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().VerboseApiCall(HttpContext);
        await Send.FluentResult(Result.Ok(_logBufferService.GetAll()), ct);
    }
}
