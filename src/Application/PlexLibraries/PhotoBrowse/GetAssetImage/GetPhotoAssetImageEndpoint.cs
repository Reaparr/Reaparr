using System.Globalization;
using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Net.Http.Headers;

namespace Reaparr.Application;

public sealed record GetPhotoAssetImageRequest
{
    [RouteParam]
    public required int PlexLibraryId { get; init; }

    [RouteParam]
    public required int AssetId { get; init; }

    [QueryParam, BindFrom("representation")]
    public required PhotoImageRepresentation Representation { get; init; }

    [QueryParam, BindFrom("width")]
    public int? Width { get; init; }

    [QueryParam, BindFrom("height")]
    public int? Height { get; init; }
}

public sealed class GetPhotoAssetImageRequestValidator : Validator<GetPhotoAssetImageRequest>
{
    public GetPhotoAssetImageRequestValidator()
    {
        RuleFor(x => x.PlexLibraryId).GreaterThan(0);
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.Representation).IsInEnum();
        RuleFor(x => x.Width)
            .Must(width => width is >= 1 and <= 4096)
            .When(x => x.Representation == PhotoImageRepresentation.Converted)
            .WithMessage("Width must be between 1 and 4096 for converted photo delivery.");
        RuleFor(x => x.Height)
            .Must(height => height is >= 1 and <= 4096)
            .When(x => x.Representation == PhotoImageRepresentation.Converted)
            .WithMessage("Height must be between 1 and 4096 for converted photo delivery.");
    }
}

/// <summary>Streams a scoped photo asset's original bytes or a Plex-side browser-compatible conversion.</summary>
public sealed class GetPhotoAssetImageEndpoint : Endpoint<GetPhotoAssetImageRequest, byte[]>
{
    private readonly ILogger _log;
    private readonly IReaparrDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    private const int RESPONSE_CACHE_DURATION_SECONDS = 259200;
    private const string IMAGE_ENTITY_TAG_VERSION = "v2";
    private static readonly TimeSpan _connectionCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly string _responseCacheControl = FormattableString.Invariant(
        $"private,max-age={RESPONSE_CACHE_DURATION_SECONDS}"
    );

    public GetPhotoAssetImageEndpoint(
        ILogger log,
        IReaparrDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache
    )
    {
        _log = log.ForContext<GetPhotoAssetImageEndpoint>();
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public override void Configure()
    {
        Get(ApiRoutes.PlexPhotoController + "/libraries/{PlexLibraryId}/assets/{AssetId}/image");
        ResponseCache(
            RESPONSE_CACHE_DURATION_SECONDS,
            ResponseCacheLocation.Client,
            varyByHeader: HeaderNames.Cookie,
            varyByQueryKeys: ["representation", "width", "height"]
        );
        Summary(s =>
        {
            s.Summary = "Deliver a photo image";
            s.Description = "Streams the original photo part or a Plex-side browser-compatible conversion.";
            s.ExampleRequest = new GetPhotoAssetImageRequest
            {
                PlexLibraryId = 1,
                AssetId = 1,
                Representation = PhotoImageRepresentation.Original,
            };
        });
        Description(x =>
            x.Produces<byte[]>(StatusCodes.Status200OK, MediaTypeNames.Image.Jpeg)
                .Produces<byte[]>(StatusCodes.Status200OK, MediaTypeNames.Image.Png)
                .Produces(StatusCodes.Status304NotModified)
                .Produces<BaseResultDTO>(StatusCodes.Status400BadRequest)
                .Produces<BaseResultDTO>(StatusCodes.Status404NotFound)
                .Produces<BaseResultDTO>(StatusCodes.Status502BadGateway)
                .Produces<BaseResultDTO>(HttpCodes.Status601PlexAuthenticationFailed)
        );
    }

    public override async Task HandleAsync(GetPhotoAssetImageRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        HttpContext.Response.Headers.CacheControl = "no-store";
        var scopeResult = await _dbContext.ResolvePlexPhotoBrowseScopeAsync(req.PlexLibraryId, ct);
        if (scopeResult.IsCancelledOrFailed)
        {
            await Send.FluentResult(scopeResult.ToResult(), ct);
            return;
        }

        var asset = await _dbContext
            .PlexPhotoMediaData.Where(x =>
                x.Id == req.AssetId && x.PlexLibraryId == req.PlexLibraryId && x.AssetKind == PhotoAssetKind.Image
            )
            .FirstOrDefaultAsync(ct);
        if (asset is null || string.IsNullOrWhiteSpace(asset.Key))
        {
            await Send.FluentResult(ResultExtensions.EntityNotFound(nameof(PlexPhotoMediaData), req.AssetId), ct);
            return;
        }

        var tokenTask = _dbContext.GetPlexServerTokenAsync(asset.PlexServerId, ct);
        var connectionTask = GetCachedConnectionAsync(asset.PlexServerId, ct);
        await Task.WhenAll(tokenTask, connectionTask);

        var tokenResult = await tokenTask;
        if (tokenResult.IsCancelledOrFailed)
        {
            await Send.FluentResult(
                Result.Fail("Photo server authentication is unavailable").AddPlex401UnauthorizedError(),
                ct
            );
            return;
        }

        var connectionResult = await connectionTask;
        if (connectionResult.IsCancelledOrFailed)
        {
            await Send.FluentResult(connectionResult.ToResult(), ct);
            return;
        }

        var etag = CreateImageEntityTag(asset, req);

        // Validate current library access and asset metadata before accepting a cached image.
        if (
            HttpContext
                .Request.GetTypedHeaders()
                .IfNoneMatch.Any(value =>
                    value.Equals(EntityTagHeaderValue.Any) || value.Compare(etag, useStrongComparison: false)
                )
        )
        {
            HttpContext.Response.Headers.ETag = etag.ToString();
            HttpContext.Response.Headers.CacheControl = _responseCacheControl;
            HttpContext.Response.Headers.Vary = HeaderNames.Cookie;
            HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        var token = tokenResult.Value;
        var connection = connectionResult.Value;
        var client = _httpClientFactory.CreateClient(HttpClientModule.PlexThumbnailClientName);
        var upstreamUrl =
            req.Representation == PhotoImageRepresentation.Original
                ? BuildOriginalUrl(connection, asset.Key, token)
                : BuildTranscodeUrl(
                    connection.Url,
                    asset.Key,
                    token,
                    req.Width!.Value,
                    req.Height!.Value,
                    GetConversionFormat(asset.Container)
                );

        try
        {
            using var response = await client.GetAsync(upstreamUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                await SendUpstreamFailureAsync(response.StatusCode, ct);
                return;
            }

            var contentType = ResolveImageContentType(
                response.Content.Headers.ContentType?.MediaType,
                req.Representation == PhotoImageRepresentation.Converted
                    ? GetConversionFormat(asset.Container)
                    : asset.Container
            );
            if (contentType is null)
            {
                await SendTransportFailureAsync("Plex returned a non-image response.", ct);
                return;
            }

            HttpContext.Response.Headers.ETag = etag.ToString();
            HttpContext.Response.Headers.CacheControl = _responseCacheControl;
            HttpContext.Response.Headers.Vary = HeaderNames.Cookie;
            HttpContext.Response.ContentType = contentType;
            HttpContext.Response.ContentLength = null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await stream.CopyToAsync(HttpContext.Response.Body, ct);
        }
        catch (HttpRequestException exception) when (!ct.IsCancellationRequested)
        {
            await HandleTransportFailureAsync(exception, "Failed to connect to the Plex server.", ct);
        }
        catch (TaskCanceledException exception) when (!ct.IsCancellationRequested)
        {
            await HandleTransportFailureAsync(exception, "The Plex image request timed out.", ct);
        }
        catch (IOException exception) when (!ct.IsCancellationRequested)
        {
            await HandleTransportFailureAsync(exception, "The Plex image response could not be read.", ct);
        }
    }

    private async Task<Result<PlexServerConnection>> GetCachedConnectionAsync(int plexServerId, CancellationToken ct)
    {
        var cacheKey = $"plex_connection_{plexServerId}";
        if (
            _cache.TryGetValue<Result<PlexServerConnection>>(cacheKey, out var cachedResult) && cachedResult is not null
        )
            return cachedResult;

        var connectionResult = await _dbContext.ChoosePlexServerConnection(plexServerId, ct);
        if (connectionResult.IsSuccess)
            _cache.Set(cacheKey, connectionResult, _connectionCacheDuration);

        return connectionResult;
    }

    private static EntityTagHeaderValue CreateImageEntityTag(PlexPhotoMediaData asset, GetPhotoAssetImageRequest req)
    {
        var targetSize =
            req.Representation == PhotoImageRepresentation.Converted
                ? FormattableString.Invariant($"-{req.Width!.Value}x{req.Height!.Value}")
                : string.Empty;
        return new EntityTagHeaderValue(
            FormattableString.Invariant(
                $"\"{IMAGE_ENTITY_TAG_VERSION}-{asset.PlexServerId}-{asset.PlexLibraryId}-{asset.Id}-{asset.PlexApiPartId}-{asset.PlexApiRatingKey}-{asset.UpdatedAt?.Ticks ?? asset.AddedAt.Ticks}-{asset.Size}-{asset.Width}x{asset.Height}-{req.Representation}{targetSize}\""
            )
        );
    }

    internal static string BuildOriginalUrl(PlexServerConnection connection, string partKey, string token) =>
        connection.GetDownloadUrl(partKey, token);

    internal static string BuildTranscodeUrl(
        string connectionUrl,
        string partKey,
        string token,
        int width,
        int height,
        string format
    )
    {
        var sourceUrl = QueryHelpers.AddQueryString(partKey, "X-Plex-Token", token);
        var query = new Dictionary<string, string?>
        {
            ["url"] = sourceUrl,
            ["format"] = format,
            ["width"] = width.ToString(CultureInfo.InvariantCulture),
            ["height"] = height.ToString(CultureInfo.InvariantCulture),
            ["quality"] = "-1",
            ["upscale"] = "0",
            ["minSize"] = "0",
            ["rotate"] = "1",
            ["X-Plex-Token"] = token,
        };

        return QueryHelpers.AddQueryString($"{connectionUrl.TrimEnd('/')}/photo/:/transcode", query);
    }

    internal static string GetConversionFormat(string? container)
    {
        var normalizedContainer = container?.Trim().TrimStart('.').ToLowerInvariant();
        return
            normalizedContainer
                is "png"
                    or "apng"
                    or "webp"
                    or "gif"
                    or "bmp"
                    or "tif"
                    or "tiff"
                    or "avif"
                    or "heic"
                    or "heif"
                    or "svg"
                    or "svgz"
                    or "jxl"
            ? "png"
            : "jpg";
    }

    private async Task SendUpstreamFailureAsync(HttpStatusCode statusCode, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var result = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => Result
                .Fail("Plex rejected photo access")
                .AddPlex401UnauthorizedError(),
            HttpStatusCode.NotFound or HttpStatusCode.NoContent => Result
                .Fail("Plex could not return the photo image")
                .Add502BadGatewayError(),
            _ => Result.Fail("Plex failed to return the photo image").Add502BadGatewayError(),
        };
        await Send.FluentResult(result, ct);
    }

    private async Task HandleTransportFailureAsync(Exception exception, string message, CancellationToken ct)
    {
        if (HttpContext.Response.HasStarted)
        {
            _log.Here().Warning(exception, "Photo image transport failed after response started: {Message}", message);
            HttpContext.Abort();
            return;
        }

        await SendTransportFailureAsync(message, ct, exception);
    }

    private async Task SendTransportFailureAsync(string message, CancellationToken ct, Exception? exception = null)
    {
        _log.Here().Warning(exception, "{Message}", message);
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Remove(HeaderNames.ETag);
        await Send.FluentResult(Result.Fail(new Error(message)).Add502BadGatewayError(), ct);
    }

    private static string? ResolveImageContentType(string? upstreamContentType, string? container)
    {
        if (!string.IsNullOrWhiteSpace(upstreamContentType))
        {
            if (Domain.ContentType.IsPassiveImage(upstreamContentType))
                return upstreamContentType;

            if (!upstreamContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        var normalizedContainer = container?.Trim().TrimStart('.').ToLowerInvariant();
        return normalizedContainer switch
        {
            "jpg" or "jpeg" => MediaTypeNames.Image.Jpeg,
            "png" or "apng" => MediaTypeNames.Image.Png,
            "gif" => "image/gif",
            "webp" => "image/webp",
            "bmp" => "image/bmp",
            "heic" => "image/heic",
            "heif" => "image/heif",
            "avif" => "image/avif",
            "jxl" => "image/jxl",
            _ => null,
        };
    }
}
