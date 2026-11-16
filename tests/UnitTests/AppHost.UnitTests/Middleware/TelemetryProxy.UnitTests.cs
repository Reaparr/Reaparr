using System.Net;
using System.Reactive.Disposables;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using Reaparr.Application.Contracts;
using Yarp.ReverseProxy.Forwarder;
using MediaTypeHeaderValue = System.Net.Http.Headers.MediaTypeHeaderValue;

namespace Reaparr.AppHost.UnitTests;

public class TelemetryProxyUnitTests : BaseUnitTest
{
    [Test]
    [Arguments("X-Trusted-Proxy")]
    [Arguments("Authorization")]
    public async Task ShouldStreamNativeIngestion_AndPreserveBodyStatusEncodingQueryAndSafeHeaders(
        string trustedHeaderName
    )
    {
        // Arrange
        var requestBytes = new byte[] { 0x1F, 0x8B, 0x08, 0x00, 0xFF, 0x00 };
        var responseBytes = new byte[] { 0x00, 0x9F, 0xFF, 0x01 };
        byte[]? capturedRequestBody = null;
        var handler = new RecordingHandler(
            async (request, cancellationToken) =>
            {
                capturedRequestBody = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                var response = new HttpResponseMessage(HttpStatusCode.MultiStatus)
                {
                    Content = new ByteArrayContent(responseBytes),
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                response.Content.Headers.ContentEncoding.Add("br");
                response.Headers.TryAddWithoutValidation(HeaderNames.SetCookie, "reaparr=must-not-reach-browser");
                response.Headers.TryAddWithoutValidation(HeaderNames.Location, "https://evil.example.test/login");
                return response;
            }
        );
        using var serviceProvider = CreateForwarderServiceProvider();
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        var appBuildInfo = new MockAppBuildInfo
        {
            TelemetryHost = "https://telemetry.example.test",
            TelemetryProjectToken = "phc_public",
        };
        var appRuntimeInfo = new MockAppRuntimeInfo { HeaderAuthTokenName = trustedHeaderName };
        var proxy = new TelemetryProxy(
            serviceProvider.GetRequiredService<IHttpForwarder>(),
            invoker,
            appBuildInfo,
            appRuntimeInfo
        );
        var context = CreateContext(HttpMethods.Post, "/i/v1/e//evil.example.test", requestBytes);
        context.Request.PathBase = "/client/e";
        context.Request.QueryString = new QueryString("?batch=1&compression=gzip&next=https%3A%2F%2Fevil.example.test");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.15");
        context.Request.ContentType = "application/json";
        context.Request.Headers[HeaderNames.ContentEncoding] = "gzip";
        context.Request.Headers[HeaderNames.UserAgent] = "Reaparr Native PostHog";
        context.Request.Headers["PostHog-Session-Id"] = "native-session";
        context.Request.Headers[HeaderNames.Authorization] = "Bearer phc_public";
        context.Request.Headers[HeaderNames.Cookie] = "reaparr=private";
        context.Request.Headers["X-Trusted-Proxy"] = "application-credential";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.99";
        context.Request.Headers[HeaderNames.Baggage] = "private=must-not-forward";
        context.Request.Headers["traceparent"] = "00-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-bbbbbbbbbbbbbbbb-01";
        context.Request.Headers[HeaderNames.Accept] = "application/octet-stream";
        context.Request.Headers[HeaderNames.AcceptEncoding] = "gzip";

        // Act
        await proxy.InvokeAsync(context);

        // Assert
        handler.CallCount.ShouldBe(1);
        handler.Request.ShouldNotBeNull();
        handler.Request!.RequestUri!.AbsoluteUri.ShouldBe(
            "https://telemetry.example.test/i/v1/e//evil.example.test?batch=1&compression=gzip&next=https%3A%2F%2Fevil.example.test"
        );
        capturedRequestBody.ShouldBe(requestBytes);
        handler.Request.Headers.GetValues("PostHog-Session-Id").Single().ShouldBe("native-session");
        handler.Request.Headers.UserAgent.ToString().ShouldBe("Reaparr Native PostHog");
        handler.Request.Headers.Authorization!.ToString().ShouldBe("Bearer phc_public");
        handler.Request.Headers.GetValues("X-Forwarded-For").Single().ShouldBe("192.0.2.15");
        handler.Request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
        handler.Request.Content.Headers.ContentEncoding.Single().ShouldBe("gzip");
        handler.Request.Content.Headers.ContentLength.ShouldBe(requestBytes.LongLength);
        handler.Request.Headers.Contains(HeaderNames.Cookie).ShouldBeFalse();
        handler.Request.Headers.Contains("X-Trusted-Proxy").ShouldBeFalse();
        handler.Request.Headers.Contains(HeaderNames.Baggage).ShouldBeFalse();
        handler.Request.Headers.Contains("traceparent").ShouldBeFalse();
        handler.Request.Headers.Contains("X-Forwarded-For").ShouldBeTrue();
        handler.Request.Headers.Contains(HeaderNames.Accept).ShouldBeFalse();
        handler.Request.Headers.Contains(HeaderNames.AcceptEncoding).ShouldBeFalse();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status207MultiStatus);
        ((MemoryStream)context.Response.Body).ToArray().ShouldBe(responseBytes);
        context.Response.Headers[HeaderNames.ContentEncoding].ToString().ShouldBe("br");
        context.Response.Headers.ContainsKey(HeaderNames.SetCookie).ShouldBeFalse();
        context.Response.Headers.ContainsKey(HeaderNames.Location).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldNotForwardArbitraryApplicationAuthorization()
    {
        // Arrange
        var handler = new RecordingHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))
        );
        using var serviceProvider = CreateForwarderServiceProvider();
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        var context = CreateContext(HttpMethods.Get, "/i/v1/e");
        context.Request.Headers[HeaderNames.Authorization] = "Bearer application-secret";
        var proxy = new TelemetryProxy(
            serviceProvider.GetRequiredService<IHttpForwarder>(),
            invoker,
            new MockAppBuildInfo
            {
                TelemetryHost = "https://telemetry.example.test",
                TelemetryProjectToken = "phc_public",
            },
            new MockAppRuntimeInfo()
        );

        // Act
        await proxy.InvokeAsync(context);

        // Assert
        handler.CallCount.ShouldBe(1);
        handler.Request.ShouldNotBeNull();
        handler.Request!.Headers.Authorization.ShouldBeNull();
    }

    [Test]
    public async Task ShouldRouteRegionalBinaryAssets_AndKeepCustomConfiguredOrigin()
    {
        // Arrange
        var euHandler = new RecordingHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))
        );
        var usHandler = new RecordingHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))
        );
        var customHandler = new RecordingHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))
        );
        using var euProvider = CreateForwarderServiceProvider();
        using var usProvider = CreateForwarderServiceProvider();
        using var customProvider = CreateForwarderServiceProvider();
        using var euInvoker = new HttpMessageInvoker(euHandler, disposeHandler: false);
        using var usInvoker = new HttpMessageInvoker(usHandler, disposeHandler: false);
        using var customInvoker = new HttpMessageInvoker(customHandler, disposeHandler: false);
        var runtime = new MockAppRuntimeInfo();

        // Act
        var euContext = CreateContext(HttpMethods.Get, "/static/app.js");
        euContext.Request.Headers[HeaderNames.Accept] = "application/javascript";
        euContext.Request.Headers[HeaderNames.AcceptEncoding] = "gzip";

        await new TelemetryProxy(
            euProvider.GetRequiredService<IHttpForwarder>(),
            euInvoker,
            new MockAppBuildInfo { TelemetryHost = "https://eu.i.posthog.com", TelemetryProjectToken = "phc_eu" },
            runtime
        ).InvokeAsync(euContext);

        var usContext = CreateContext(HttpMethods.Get, "/array/config.js");
        await new TelemetryProxy(
            usProvider.GetRequiredService<IHttpForwarder>(),
            usInvoker,
            new MockAppBuildInfo { TelemetryHost = "https://us.i.posthog.com", TelemetryProjectToken = "phc_us" },
            runtime
        ).InvokeAsync(usContext);

        var customContext = CreateContext(HttpMethods.Get, "/static/app.js");
        await new TelemetryProxy(
            customProvider.GetRequiredService<IHttpForwarder>(),
            customInvoker,
            new MockAppBuildInfo { TelemetryHost = "https://proxy.example.test", TelemetryProjectToken = "phc_custom" },
            runtime
        ).InvokeAsync(customContext);

        // Assert
        euHandler.Request!.RequestUri!.AbsoluteUri.ShouldBe("https://eu-assets.i.posthog.com/static/app.js");
        euHandler.Request.Headers.GetValues(HeaderNames.Accept).Single().ShouldBe("application/javascript");
        euHandler.Request.Headers.GetValues(HeaderNames.AcceptEncoding).Single().ShouldBe("gzip");
        usHandler.Request!.RequestUri!.AbsoluteUri.ShouldBe("https://us-assets.i.posthog.com/array/config.js");
        customHandler.Request!.RequestUri!.AbsoluteUri.ShouldBe("https://proxy.example.test/static/app.js");
    }

    [Test]
    public void ShouldExcludeNativeProxyAndRegionalAssetsFromOpenTelemetry()
    {
        // Arrange
        var build = new MockAppBuildInfo
        {
            TelemetryHost = "https://eu.i.posthog.com",
            TelemetryProjectToken = "phc_public",
        };
        var runtime = new MockAppRuntimeInfo();
        var services = new ServiceCollection();
        services.ConfigureTelemetry(build, runtime);
        using var provider = services.BuildServiceProvider();
        var aspNetOptions = provider
            .GetRequiredService<IOptionsMonitor<AspNetCoreTraceInstrumentationOptions>>()
            .CurrentValue;
        var httpOptions = provider
            .GetRequiredService<IOptionsMonitor<HttpClientTraceInstrumentationOptions>>()
            .CurrentValue;

        // Assert
        aspNetOptions.Filter.ShouldNotBeNull();
        aspNetOptions.Filter!(CreateContext(HttpMethods.Get, "/e/i/v1/e")).ShouldBeFalse();
        aspNetOptions.Filter!(CreateContext(HttpMethods.Get, "/api/work")).ShouldBeTrue();
        httpOptions.FilterHttpRequestMessage.ShouldNotBeNull();
        httpOptions
            .FilterHttpRequestMessage!(new HttpRequestMessage(HttpMethod.Get, "https://eu.i.posthog.com/i/v1/e"))
            .ShouldBeFalse();
        httpOptions
            .FilterHttpRequestMessage!(
                new HttpRequestMessage(HttpMethod.Get, "https://eu-assets.i.posthog.com/static/app.js")
            )
            .ShouldBeFalse();
        httpOptions
            .FilterHttpRequestMessage!(new HttpRequestMessage(HttpMethod.Get, "https://eu-assets.i.posthog.com/other"))
            .ShouldBeTrue();
    }

    [Test]
    public async Task ShouldReturnNotFoundWithoutTransmission_WhenTelemetryIsDisabled()
    {
        // Arrange
        var handler = new RecordingHandler(
            (_, _) =>
                Task.FromException<HttpResponseMessage>(new InvalidOperationException("upstream must not be contacted"))
        );
        using var serviceProvider = CreateForwarderServiceProvider();
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        var feature = new MaxRequestBodySizeFeature();
        var context = CreateContext(HttpMethods.Post, "/i/v1/e", [1, 2, 3]);
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        // Act
        await new TelemetryProxy(
            serviceProvider.GetRequiredService<IHttpForwarder>(),
            invoker,
            new MockAppBuildInfo(),
            new MockAppRuntimeInfo()
        ).InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        feature.MaxRequestBodySize.ShouldBe(64L * 1024 * 1024);
        handler.CallCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldRejectUnsupportedMethodsWithoutTransmission()
    {
        // Arrange
        var handler = new RecordingHandler(
            (_, _) =>
                Task.FromException<HttpResponseMessage>(new InvalidOperationException("upstream must not be contacted"))
        );
        using var serviceProvider = CreateForwarderServiceProvider();
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        var feature = new MaxRequestBodySizeFeature();
        var context = CreateContext(HttpMethods.Put, "/i/v1/e", [1, 2, 3]);
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        // Act
        await new TelemetryProxy(
            serviceProvider.GetRequiredService<IHttpForwarder>(),
            invoker,
            new MockAppBuildInfo
            {
                TelemetryHost = "https://telemetry.example.test",
                TelemetryProjectToken = "phc_public",
            },
            new MockAppRuntimeInfo()
        ).InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(StatusCodes.Status405MethodNotAllowed);
        context.Response.Headers.Allow.ToString().ShouldBe("GET, POST");
        feature.MaxRequestBodySize.ShouldBe(64L * 1024 * 1024);
        handler.CallCount.ShouldBe(0);
    }

    [Test]
    public async Task ShouldPreserveNativeFailureResponses_ForUpstreamFailureAndCancellation()
    {
        // Arrange
        var failureHandler = new RecordingHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("upstream unavailable"))
        );
        using var failureProvider = CreateForwarderServiceProvider();
        using var failureInvoker = new HttpMessageInvoker(failureHandler, disposeHandler: false);
        var failureContext = CreateContext(HttpMethods.Get, "/i/v1/e");
        var failureProxy = new TelemetryProxy(
            failureProvider.GetRequiredService<IHttpForwarder>(),
            failureInvoker,
            new MockAppBuildInfo
            {
                TelemetryHost = "https://telemetry.example.test",
                TelemetryProjectToken = "phc_public",
            },
            new MockAppRuntimeInfo()
        );

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancellationHandler = new RecordingHandler(
            (_, _) =>
                Task.FromException<HttpResponseMessage>(
                    new InvalidOperationException("canceled request must not complete")
                )
        );
        using var cancellationProvider = CreateForwarderServiceProvider();
        using var cancellationInvoker = new HttpMessageInvoker(cancellationHandler, disposeHandler: false);
        var cancellationContext = CreateContext(HttpMethods.Get, "/i/v1/e");
        cancellationContext.RequestAborted = cancellation.Token;
        var cancellationProxy = new TelemetryProxy(
            cancellationProvider.GetRequiredService<IHttpForwarder>(),
            cancellationInvoker,
            new MockAppBuildInfo
            {
                TelemetryHost = "https://telemetry.example.test",
                TelemetryProjectToken = "phc_public",
            },
            new MockAppRuntimeInfo()
        );

        // Act
        await failureProxy.InvokeAsync(failureContext);
        await cancellationProxy.InvokeAsync(cancellationContext);

        // Assert
        failureContext.Response.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        failureHandler.CallCount.ShouldBe(1);
        cancellationContext.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task ShouldBypassBusinessTelemetryForProxyBranch_AndKeepOrdinaryPathsInPipeline()
    {
        // Arrange
        var handler = new RecordingHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))
        );
        using var forwarderProvider = CreateForwarderServiceProvider();
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        var telemetry = new Mock<ITelemetry>(MockBehavior.Strict);
        telemetry.Setup(x => x.BeginRequest(null)).Returns(Disposable.Empty).Verifiable(Times.Once());
        var services = new ServiceCollection()
            .AddSingleton(
                new TelemetryProxy(
                    forwarderProvider.GetRequiredService<IHttpForwarder>(),
                    invoker,
                    new MockAppBuildInfo
                    {
                        TelemetryHost = "https://telemetry.example.test",
                        TelemetryProjectToken = "phc_public",
                    },
                    new MockAppRuntimeInfo()
                )
            )
            .AddSingleton(telemetry.Object)
            .BuildServiceProvider();
        using (services)
        {
            var ordinaryPathReached = false;
            var application = new ApplicationBuilder(services);
            application.MapTelemetryProxy();
            application.UseMiddleware<TelemetryContextMiddleware>();
            application.Run(_ =>
            {
                ordinaryPathReached = true;
                return Task.CompletedTask;
            });
            var pipeline = application.Build();

            var proxyContext = CreateContext(HttpMethods.Get, "/e/native");
            proxyContext.RequestServices = services;
            proxyContext.Request.PathBase = "/client";
            var ordinaryContext = CreateContext(HttpMethods.Get, "/api/work");
            ordinaryContext.RequestServices = services;

            // Act
            await pipeline(proxyContext);
            await pipeline(ordinaryContext);

            // Assert
            handler.CallCount.ShouldBe(1);
            handler.Request!.RequestUri!.AbsoluteUri.ShouldBe("https://telemetry.example.test/native");
            ordinaryPathReached.ShouldBeTrue();
            telemetry.Verify();
        }
    }

    private static ServiceProvider CreateForwarderServiceProvider() =>
        new ServiceCollection().AddLogging(ConfigureLogging).AddHttpForwarder().BuildServiceProvider();

    private static void ConfigureLogging(ILoggingBuilder builder) => builder.SetMinimumLevel(LogLevel.None);

    private static DefaultHttpContext CreateContext(string method, string path, byte[]? body = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Protocol = "HTTP/1.1";
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(body ?? []);
        if (body is not null)
            context.Request.ContentLength = body.LongLength;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        {
            _send = send;
        }

        public int CallCount { get; private set; }
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            CallCount++;
            Request = request;
            return _send(request, cancellationToken);
        }
    }

    private sealed class MaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}
