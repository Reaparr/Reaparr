namespace Reaparr.Application;

public class GetAllDownloadTasksEndpoint : EndpointWithoutRequest<DownloadSnapshotDTO>
{
    private readonly ILogger _log;
    private readonly IDownloadTaskUpdateDispatcher _downloadTaskUpdateDispatcher;

    public GetAllDownloadTasksEndpoint(ILogger log, IDownloadTaskUpdateDispatcher downloadTaskUpdateDispatcher)
    {
        _log = log.ForContext<GetAllDownloadTasksEndpoint>();
        _downloadTaskUpdateDispatcher = downloadTaskUpdateDispatcher;
    }

    public override void Configure()
    {
        Get(ApiRoutes.DownloadController);

        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<DownloadSnapshotDTO>))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext);
        var snapshot = await _downloadTaskUpdateDispatcher.GetSnapshotAsync(ct);
        await Send.FluentResult(Result.Ok(snapshot), ct);
    }
}
