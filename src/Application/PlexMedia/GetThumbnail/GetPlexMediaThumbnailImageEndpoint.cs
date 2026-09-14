using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace Reaparr.Application;

public record GetPlexMediaThumbnailImageEndpointRequest
{
    [QueryParam, BindFrom("plexServerId")]
    public required int PlexServerId { get; init; }

    [QueryParam, BindFrom("plexKey")]
    public required int PlexKey { get; init; }

    [QueryParam, BindFrom("metaDataKey")]
    public required int MetaDataKey { get; init; }

    [QueryParam, BindFrom("width")]
    public required int Width { get; init; }

    [QueryParam, BindFrom("height")]
    public required int Height { get; init; }
}

public class GetPlexMediaThumbnailImageEndpointRequestValidator : Validator<GetPlexMediaThumbnailImageEndpointRequest>
{
    public GetPlexMediaThumbnailImageEndpointRequestValidator()
    {
        RuleFor(x => x.PlexServerId).GreaterThan(0);
        RuleFor(x => x.PlexKey).GreaterThan(0);
        RuleFor(x => x.MetaDataKey).GreaterThan(0);
        RuleFor(x => x.Width).InclusiveBetween(1, 4096);
        RuleFor(x => x.Height).InclusiveBetween(1, 4096);
    }
}

/// <summary>
/// High-performance thumbnail proxy endpoint with streaming response and in-memory caching
/// for database lookups. Response caching is enabled for downstream caches (3 days).
/// </summary>
public sealed class GetPlexMediaThumbnailImageEndpoint : Endpoint<GetPlexMediaThumbnailImageEndpointRequest, byte[]>
{
    private readonly ILogger _log;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly IReaparrDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    private static readonly TimeSpan _connectionCacheDuration = TimeSpan.FromMinutes(5);

    public GetPlexMediaThumbnailImageEndpoint(
        ILogger log,
        IAppRuntimeInfo appRuntimeInfo,
        IReaparrDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache
    )
    {
        _log = log.ForContext<GetPlexMediaThumbnailImageEndpoint>();
        _appRuntimeInfo = appRuntimeInfo;
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexMediaController + "/thumbnail");

        // Enable response caching headers for downstream caches (proxies, CDNs, browsers)
        // 3 days = 259200 seconds, varied by query parameters
        ResponseCache(259200, varyByQueryKeys: ["plexServerId", "plexKey", "metaDataKey", "width", "height"]);

        Summary(s =>
        {
            s.Summary = "Proxy Plex image";
            s.Description = "Proxies image bytes from Plex servers with CORS headers.";
            s.ExampleRequest = new GetPlexMediaThumbnailImageEndpointRequest
            {
                PlexServerId = 1,
                PlexKey = 1756014789,
                MetaDataKey = 57920,
                Width = 200,
                Height = 400,
            };
        });

        Description(x =>
        {
            x.Produces(StatusCodes.Status200OK, typeof(byte[]), MediaTypeNames.Image.Jpeg)
                .Produces(StatusCodes.Status304NotModified)
                .Produces(HttpCodes.Status601PlexAuthenticationFailed, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status404NotFound, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status502BadGateway, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO));
        });
    }

    public override async Task HandleAsync(GetPlexMediaThumbnailImageEndpointRequest req, CancellationToken ct)
    {
#pragma warning disable ASP0015
        HttpContext.Response.Headers["Access-Control-Allow-Origin"] = "*";
        HttpContext.Response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        HttpContext.Response.Headers["Access-Control-Allow-Headers"] = "*";
#pragma warning restore ASP0015

        // Generate ETag based on the unique thumbnail parameters
        var etag = $"\"{req.PlexServerId}-{req.PlexKey}-{req.MetaDataKey}-{req.Width}x{req.Height}\"";
        HttpContext.Response.Headers.ETag = etag;

        // Check If-None-Match header for conditional request (304 Not Modified)
        var ifNoneMatch = HttpContext.Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var plexServerId = req.PlexServerId;

        // Fetch token and connection in parallel for better performance
        var tokenTask = _dbContext.GetPlexServerTokenAsync(plexServerId, ct);
        var connectionTask = GetCachedConnectionAsync(plexServerId, ct);

        await Task.WhenAll(tokenTask, connectionTask);

        var tokenResult = await tokenTask;
        if (tokenResult.IsFailed)
        {
            _log.Here()
                .Warning(
                    "Failed to get Plex server token for server {PlexServerId}: {Error}",
                    plexServerId,
                    tokenResult.Errors.FirstOrDefault()?.Message
                );
            await Send.FluentResult(tokenResult.ToResult(), ct);
            return;
        }

        var connectionResult = await connectionTask;
        if (connectionResult.IsFailed)
        {
            _log.Here()
                .Warning(
                    "Failed to get Plex server connection for server {PlexServerId}: {Error}",
                    plexServerId,
                    connectionResult.Errors.FirstOrDefault()?.Message
                );
            await Send.FluentResult(connectionResult.ToResult(), ct);
            return;
        }

        var token = tokenResult.Value;

        // Use the named PlexThumbnail HttpClient with connection pooling
        var client = _httpClientFactory.CreateClient(HttpClientModule.PlexThumbnailClientName);

        var thumbnailPath = $"/library/metadata/{req.PlexKey}/thumb/{req.MetaDataKey}";
        var transcodeUrl = BuildTranscodeUrl(connectionResult.Value.Url, thumbnailPath, token, req.Width, req.Height);
        var directUrl = BuildDirectThumbnailUrl(connectionResult.Value.Url, thumbnailPath, token);

        try
        {
            // Plex's image transcoder can reject an otherwise valid original thumbnail. In that case,
            // fall back to the same original resource Plex Web can render instead of returning a 502.
            using var response = await GetThumbnailResponseAsync(client, transcodeUrl, directUrl, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);
                _log.Here()
                    .Warning(
                        "Plex rejected the access token while fetching a thumbnail for server {PlexServerName} ({PlexServerId}). Refresh the Plex account's server access",
                        plexServerName,
                        plexServerId
                    );
                HttpContext.Response.Headers.CacheControl = "no-store";
                await Send.FluentResult(
                    Result
                        .Fail("Plex rejected the server access token; refresh the Plex account access")
                        .AddPlex401UnauthorizedError(),
                    ct
                );
                return;
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                _log.Here().Verbose("Plex returned no thumbnail content from {Url}", SanitizeUrl(directUrl));
                HttpContext.Response.Headers.CacheControl = "no-store";
                await Send.FluentResult(
                    Result.Fail("No thumbnail image content returned by Plex").Add404NotFoundError(),
                    ct
                );
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _log.Here()
                    .Warning(
                        "Failed to fetch Plex thumbnail from {Url} - Status: {StatusCode}",
                        SanitizeUrl(directUrl),
                        response.StatusCode
                    );
                HttpContext.Response.Headers.CacheControl = "no-store";
                await Send.FluentResult(Result.Fail("Failed to fetch image").Add502BadGatewayError(), ct);
                return;
            }

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
            var contentLength = response.Content.Headers.ContentLength;

            if (contentLength.HasValue)
            {
                HttpContext.Response.ContentLength = contentLength.Value;
            }

            HttpContext.Response.ContentType = contentType;

            // Stream directly to response without buffering
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await stream.CopyToAsync(HttpContext.Response.Body, ct);
        }
        catch (HttpRequestException ex)
        {
            // Check if this is a connection refused error (server offline) - use Warning instead of Error
            var isConnectionRefused =
                ex.InnerException is SocketException socketEx
                && (socketEx.ErrorCode == 111 || socketEx.ErrorCode == 10061);

            var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);
            if (isConnectionRefused)
            {
                _log.Here()
                    .Warning(
                        "Connection refused while fetching Plex thumbnail for server {PlexServerName} (key {PlexKey}). Server appears to be offline",
                        plexServerName,
                        req.PlexKey
                    );
            }
            else
            {
                _log.Here()
                    .Warning(
                        ex,
                        "HTTP request failed while fetching Plex thumbnail for server {PlexServerName} (key {PlexKey}). URL: {Url}",
                        plexServerName,
                        req.PlexKey,
                        SanitizeUrl(transcodeUrl)
                    );
            }

            await Send.FluentResult(Result.Fail("Failed to connect to Plex server").Add502BadGatewayError(), ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);

            // Timeout occurred (not user cancellation)
            _log.Here()
                .Warning(
                    ex,
                    "Timeout fetching Plex thumbnail for server {PlexServerName} ({PlexServerId}) (key {PlexKey}). URL: {Url}",
                    plexServerName,
                    plexServerId,
                    req.PlexKey,
                    SanitizeUrl(transcodeUrl)
                );
            await Send.FluentResult(Result.Fail("Request timeout").Add502BadGatewayError(), ct);
        }
        catch (IOException ex)
        {
            var plexServerName = await _dbContext.GetPlexServerNameById(plexServerId);

            // SSL handshake failures and other I/O errors - typically due to unreachable/misconfigured servers
            _log.Here()
                .Warning(
                    "I/O error while fetching Plex thumbnail for server {PlexServerName} ({PlexServerId}) (key {PlexKey}). {ErrorMessage}",
                    plexServerName,
                    plexServerId,
                    req.PlexKey,
                    ex.Message
                );
            await Send.FluentResult(Result.Fail("Network error while fetching image").Add502BadGatewayError(), ct);
        }
    }

    internal static string BuildTranscodeUrl(
        string connectionUrl,
        string thumbnailPath,
        string token,
        int width,
        int height
    )
    {
        var query = new Dictionary<string, string?>
        {
            ["width"] = width.ToString(),
            ["height"] = height.ToString(),
            ["minSize"] = "1",
            ["upscale"] = "1",
            ["url"] = QueryHelpers.AddQueryString(thumbnailPath, "X-Plex-Token", token),
            ["X-Plex-Token"] = token,
        };

        return QueryHelpers.AddQueryString($"{connectionUrl.TrimEnd('/')}/photo/:/transcode", query);
    }

    internal static string BuildDirectThumbnailUrl(string connectionUrl, string thumbnailPath, string token) =>
        QueryHelpers.AddQueryString($"{connectionUrl.TrimEnd('/')}{thumbnailPath}", "X-Plex-Token", token);

    internal async Task<HttpResponseMessage> GetThumbnailResponseAsync(
        HttpClient client,
        string transcodeUrl,
        string directUrl,
        CancellationToken ct
    )
    {
        var response = await client.GetAsync(transcodeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (
            response.IsSuccessStatusCode
            || response.StatusCode == HttpStatusCode.NoContent
            || response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Forbidden
        )
            return response;

        _log.Here()
            .Warning(
                "Plex thumbnail transcode failed from {Url} with status {StatusCode}; attempting the original thumbnail",
                SanitizeUrl(transcodeUrl),
                response.StatusCode
            );
        response.Dispose();

        return await client.GetAsync(directUrl, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    private async Task<Result<PlexServerConnection>> GetCachedConnectionAsync(int plexServerId, CancellationToken ct)
    {
        var cacheKey = $"plex_connection_{plexServerId}";

        // Check cache first
        if (
            _cache.TryGetValue<Result<PlexServerConnection>>(cacheKey, out var cachedResult) && cachedResult is not null
        )
            return cachedResult;

        // Fetch from a database
        var connectionResult = await _dbContext.ChoosePlexServerConnection(plexServerId, ct);

        // Only cache successful results
        if (connectionResult.IsSuccess)
        {
            _cache.Set(cacheKey, connectionResult, _connectionCacheDuration);
        }

        return connectionResult;
    }

    /// <summary>
    /// Sanitizes a URL by removing query string parameters to prevent logging sensitive tokens
    /// </summary>
    private string SanitizeUrl(string url)
    {
        if (_appRuntimeInfo.IsUnmasked)
            return url;

        if (string.IsNullOrEmpty(url))
            return url;

        var questionMarkIndex = url.IndexOf('?');
        return questionMarkIndex >= 0 ? url[..questionMarkIndex] : url;
    }
}
