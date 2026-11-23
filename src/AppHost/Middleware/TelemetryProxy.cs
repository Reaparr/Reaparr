using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using OpenTelemetry;
using Yarp.ReverseProxy.Forwarder;

namespace Reaparr.AppHost;

/// <summary>
/// Forwards the browser's native PostHog traffic without entering the application request pipeline.
/// </summary>
public sealed class TelemetryProxy : HttpTransformer
{
    internal const long MaxRequestBodySize = 64L * 1024 * 1024;

    private readonly IHttpForwarder _forwarder;
    private readonly HttpMessageInvoker _httpClient;
    private readonly bool _telemetryEnabled;
    private readonly string _telemetryHost;
    private readonly string? _assetDestinationPrefix;
    private readonly string _projectToken;
    private readonly string _trustedHeaderName;

    /// <summary>
    /// Creates the native telemetry proxy.
    /// </summary>
    public TelemetryProxy(
        IHttpForwarder forwarder,
        HttpMessageInvoker httpClient,
        IAppBuildInfo appBuildInfo,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        _forwarder = forwarder;
        _httpClient = httpClient;
        _telemetryEnabled = appBuildInfo.HasValidTelemetry;
        _telemetryHost = appBuildInfo.TelemetryHost;
        _assetDestinationPrefix = Uri.TryCreate(_telemetryHost, UriKind.Absolute, out var ingestionOrigin)
            ? GetAssetOrigin(ingestionOrigin)?.GetLeftPart(UriPartial.Authority)
            : null;
        _projectToken = appBuildInfo.TelemetryProjectToken;
        _trustedHeaderName = appRuntimeInfo.HeaderAuthTokenName;
    }

    /// <summary>
    /// Handles one native browser request using YARP's streaming forwarder.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } requestBodySize)
            requestBodySize.MaxRequestBodySize = MaxRequestBodySize;

        if (!_telemetryEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers.Allow = $"{HttpMethods.Get}, {HttpMethods.Post}";
            return;
        }

        var destinationPrefix = IsAssetPath(context.Request.Path)
            ? _assetDestinationPrefix ?? _telemetryHost
            : _telemetryHost;
        using var suppression = SuppressInstrumentationScope.Begin();
        await _forwarder.SendAsync(
            context,
            destinationPrefix,
            _httpClient,
            ForwarderRequestConfig.Empty,
            this,
            context.RequestAborted
        );
    }

    internal static Uri? GetAssetOrigin(Uri ingestionOrigin)
    {
        if (ingestionOrigin.Scheme != Uri.UriSchemeHttps || !ingestionOrigin.IsDefaultPort)
            return null;

        return ingestionOrigin.Host switch
        {
            TelemetryProtocol.EuIngestionHost => new Uri($"{Uri.UriSchemeHttps}://{TelemetryProtocol.EuAssetHost}"),
            TelemetryProtocol.UsIngestionHost => new Uri($"{Uri.UriSchemeHttps}://{TelemetryProtocol.UsAssetHost}"),
            _ => null,
        };
    }

    internal static bool IsTelemetryDestination(Uri target, Uri ingestionOrigin, Uri? assetOrigin)
    {
        if (HasSameAuthority(target, ingestionOrigin))
            return true;

        return assetOrigin is not null
            && IsAssetPath(new PathString(target.AbsolutePath))
            && HasSameAuthority(target, assetOrigin);
    }

    private static bool IsAssetPath(PathString path) =>
        path.StartsWithSegments("/static") || path.StartsWithSegments("/array");

    private static bool HasSameAuthority(Uri left, Uri right) =>
        left.Scheme == right.Scheme && left.Host == right.Host && left.Port == right.Port;

    /// <inheritdoc />
    public override ValueTask TransformRequestAsync(
        HttpContext httpContext,
        HttpRequestMessage proxyRequest,
        string destinationPrefix,
        CancellationToken cancellationToken
    )
    {
        // YARP's default request transform copies application headers; forward only the SDK allowlist instead.
        var isAssetRequest = IsAssetPath(httpContext.Request.Path);
        foreach (var header in httpContext.Request.Headers)
        {
            if (!ShouldForwardRequestHeader(header.Key, header.Value, isAssetRequest))
                continue;

            if (!proxyRequest.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string?>)header.Value))
                proxyRequest.Content?.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string?>)header.Value);
        }

        if (httpContext.Connection.RemoteIpAddress is { } remoteAddress)
            proxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", remoteAddress.ToString());

        var upstreamPath = httpContext.Request.Path.Value switch
        {
            "/submit/" => new PathString("/i/v0/e/"),
            "/submit-batch/" => new PathString("/e/"),
            _ => httpContext.Request.Path,
        };
        proxyRequest.RequestUri = RequestUtilities.MakeDestinationAddress(
            destinationPrefix,
            upstreamPath,
            httpContext.Request.QueryString
        );

        return default;
    }

    /// <inheritdoc />
    public override async ValueTask<bool> TransformResponseAsync(
        HttpContext httpContext,
        HttpResponseMessage? proxyResponse,
        CancellationToken cancellationToken
    )
    {
        var shouldProxy = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
        httpContext.Response.Headers.Remove(HeaderNames.SetCookie);
        httpContext.Response.Headers.Remove(HeaderNames.Location);
        return shouldProxy;
    }

    private bool ShouldForwardRequestHeader(string name, StringValues value, bool isAssetRequest)
    {
        if (string.Equals(name, HeaderNames.Authorization, StringComparison.OrdinalIgnoreCase))
        {
            return value.Count == 1
                && AuthenticationHeaderValue.TryParse(value[0], out var authorization)
                && string.Equals(
                    authorization.Scheme,
                    TelemetryProtocol.BearerScheme,
                    StringComparison.OrdinalIgnoreCase
                )
                && authorization.Parameter == _projectToken;
        }

        if (string.Equals(name, _trustedHeaderName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (
            name.StartsWith("PostHog-", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("X-PostHog-", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        if (
            string.Equals(name, HeaderNames.ContentType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, HeaderNames.ContentEncoding, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, HeaderNames.UserAgent, StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return isAssetRequest
            && (
                string.Equals(name, HeaderNames.Accept, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, HeaderNames.AcceptEncoding, StringComparison.OrdinalIgnoreCase)
            );
    }
}
