using System.Text;

namespace Reaparr.PublicAPI;

public sealed record GetTorrentFileRequest
{
    [RouteParam]
    public required string Hash { get; init; }
}

public sealed class GetTorrentFileRequestValidator : Validator<GetTorrentFileRequest>
{
    public GetTorrentFileRequestValidator()
    {
        RuleFor(x => x.Hash).NotEmpty().WithMessage("Hash is required.");
    }
}

public sealed class GetTorrentFileEndpoint : Endpoint<GetTorrentFileRequest>
{
    private readonly ILogger _log;

    public GetTorrentFileEndpoint(ILogger logger)
    {
        _log = logger.ForContext<GetTorrentFileEndpoint>();
    }

    public override void Configure()
    {
        Get(PublicApiRoutes.DownloadClient + "/torrents/file/{hash}.torrent");
        Description(x => x.IsDownloadClient());
        AllowAnonymous();
        PreProcessor<DownloadClientAuthenticationPreProcessor<GetTorrentFileRequest>>();
    }

    public override async Task HandleAsync(GetTorrentFileRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);

        _log.Here().Warning("GetTorrentFileEndpoint called but not implemented");

        var bytes = "d8:announce0:e"u8.ToArray();
        HttpContext.Response.ContentType = "application/x-bittorrent";
        await HttpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length, ct);
    }
}
