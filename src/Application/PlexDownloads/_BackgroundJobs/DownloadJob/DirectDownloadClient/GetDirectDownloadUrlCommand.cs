using System.Net;

namespace Reaparr.Application;

/// <summary>
/// Some <see cref="PlexServer"/> need a download=1 param, and some explicitly don't.
/// This command returns the valid DownloadURL containing either download=1 query or not
/// </summary>
public record GetDirectDownloadUrlCommand(int PlexServerId, string FileLocationUrl) : ICommand<Result<string>>;

public class GetDirectDownloadUrlCommandValidator : AbstractValidator<GetDirectDownloadUrlCommand>
{
    public GetDirectDownloadUrlCommandValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.FileLocationUrl).NotEmpty();
    }
}

public class GetDirectDownloadUrlCommandHandler : ICommandHandler<GetDirectDownloadUrlCommand, Result<string>>
{
    private readonly HttpClient _httpClient;
    private readonly IReaparrDbContext _dbContext;

    public GetDirectDownloadUrlCommandHandler(IHttpClientFactory httpClientFactory, IReaparrDbContext dbContext)
    {
        _httpClient = httpClientFactory.CreateClient();
        _dbContext = dbContext;
    }

    public async Task<Result<string>> ExecuteAsync(
        GetDirectDownloadUrlCommand command,
        CancellationToken cancellationToken
    )
    {
        var downloadUrlResult = await _dbContext.GetDownloadUrl(
            command.PlexServerId,
            command.FileLocationUrl,
            cancellationToken
        );
        if (downloadUrlResult.IsFailed)
            return downloadUrlResult.ToResult();

        var defaultDownloadUrl = downloadUrlResult.Value;
        var downloadUrlWithFlag = defaultDownloadUrl.SetQueryParam("download", 1).ToString();
        using var fallbackProbeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        var initialProbeTask = ProbeDownloadUrl(defaultDownloadUrl, cancellationToken);
        var fallbackProbeTask = ProbeDownloadUrl(downloadUrlWithFlag, fallbackProbeCancellationTokenSource.Token);

        var initialProbeResult = await initialProbeTask;
        if (initialProbeResult.IsFailed)
        {
            fallbackProbeCancellationTokenSource.Cancel();
            return initialProbeResult.ToResult();
        }

        if (initialProbeResult.Value.IsSuccessStatusCode)
        {
            fallbackProbeCancellationTokenSource.Cancel();
            return Result.Ok(defaultDownloadUrl);
        }

        var statusCode = initialProbeResult.Value.StatusCode;

        if (statusCode != HttpStatusCode.Forbidden)
        {
            fallbackProbeCancellationTokenSource.Cancel();
            return Result
                .Fail(
                    "Plex download URL probe failed with status {StatusCode} ({StatusCodeInt})",
                    (int)statusCode,
                    statusCode
                )
                .AddStatusCode(statusCode)
                .LogError();
        }

        var fallbackProbeResult = await fallbackProbeTask;
        if (fallbackProbeResult.IsFailed)
            return fallbackProbeResult.ToResult();

        if (fallbackProbeResult.Value.IsSuccessStatusCode)
            return Result.Ok(downloadUrlWithFlag);

        return Result
            .Fail(
                "Plex download URL probe failed for both default URL and URL with download=1 flag. Default URL status: {InitialStatusCode} ({InitialStatusCodeInt}). Fallback URL status: {FallbackStatusCodeInt} ({FallbackStatusCode})",
                (int)initialProbeResult.Value.StatusCode,
                initialProbeResult.Value.StatusCode,
                (int)fallbackProbeResult.Value.StatusCode,
                fallbackProbeResult.Value.StatusCode
            )
            .AddStatusCode(fallbackProbeResult.Value.StatusCode)
            .LogError();
    }

    private async Task<Result<ProbeResult>> ProbeDownloadUrl(string downloadUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        var responseResult = await _httpClient.SendResultAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (responseResult.IsFailed && responseResult.IsServerUnreachable())
            return Result.Fail<ProbeResult>(responseResult.Errors);

        if (responseResult.IsFailed)
        {
            var statusCode = (HttpStatusCode)responseResult.ToResult().FindStatusCode();
            return Result.Ok(new ProbeResult(statusCode, (int)statusCode is >= 200 and <= 299));
        }

        using var response = responseResult.Value;
        return Result.Ok(new ProbeResult(response.StatusCode, response.IsSuccessStatusCode));
    }

    private sealed record ProbeResult(HttpStatusCode StatusCode, bool IsSuccessStatusCode);
}
