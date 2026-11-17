using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application.UnitTests;

public class GetPlexMediaThumbnailImageEndpointUnitTests
    : BaseEndpointUnitTest<GetPlexMediaThumbnailImageEndpoint, GetPlexMediaThumbnailImageEndpointRequest, byte[]>
{
    [Test]
    public void ShouldBuildTranscodeUrl_WithEncodedOriginalThumbnailUrl()
    {
        const string token = "token+with&reserved=characters";

        var url = GetPlexMediaThumbnailImageEndpoint.BuildTranscodeUrl(
            "https://plex.example:32400/",
            "/library/metadata/481523/thumb/1772632750",
            token,
            300,
            450
        );

        var uri = new Uri(url);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        query["width"].ToString().ShouldBe("300");
        query["height"].ToString().ShouldBe("450");
        query["url"]
            .ToString()
            .ShouldBe("/library/metadata/481523/thumb/1772632750?X-Plex-Token=token%2Bwith%26reserved%3Dcharacters");
        query["X-Plex-Token"].ToString().ShouldBe(token);
    }

    [Test]
    public void ShouldBuildDirectThumbnailUrl_WithTokenAndWithoutDoubleSlash()
    {
        var url = GetPlexMediaThumbnailImageEndpoint.BuildDirectThumbnailUrl(
            "https://plex.example:32400/",
            "/library/metadata/481523/thumb/1772632750",
            "plex-token"
        );

        url.ShouldBe("https://plex.example:32400/library/metadata/481523/thumb/1772632750?X-Plex-Token=plex-token");
    }

    [Test]
    public async Task ShouldReturnOriginalThumbnail_WhenPlexTranscodeFails()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }
        );
        using var client = new HttpClient(handler);
        var endpoint = new GetPlexMediaThumbnailImageEndpoint(
            Mock.Create<ILogger>(),
            Mock.Create<IAppRuntimeInfo>(),
            Mock.Create<IReaparrDbContext>(),
            new Mock<IHttpClientFactory>().Object,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        using var response = await endpoint.GetThumbnailResponseAsync(
            client,
            "https://plex.example/photo/:/transcode",
            "https://plex.example/library/metadata/1/thumb/2",
            CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestUris.ShouldBe([
            new Uri("https://plex.example/photo/:/transcode"),
            new Uri("https://plex.example/library/metadata/1/thumb/2"),
        ]);
    }

    [Test]
    public async Task ShouldNotRequestOriginalThumbnail_WhenPlexRejectsAccessToken()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var client = new HttpClient(handler);
        var endpoint = new GetPlexMediaThumbnailImageEndpoint(
            Mock.Create<ILogger>(),
            Mock.Create<IAppRuntimeInfo>(),
            Mock.Create<IReaparrDbContext>(),
            new Mock<IHttpClientFactory>().Object,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        using var response = await endpoint.GetThumbnailResponseAsync(
            client,
            "https://plex.example/photo/:/transcode",
            "https://plex.example/library/metadata/1/thumb/2",
            CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        handler.RequestUris.ShouldBe([new Uri("https://plex.example/photo/:/transcode")]);
    }

    [Test]
    public async Task ShouldNotRequestOriginalThumbnail_WhenPlexTranscodeSucceeds()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var endpoint = new GetPlexMediaThumbnailImageEndpoint(
            Mock.Create<ILogger>(),
            Mock.Create<IAppRuntimeInfo>(),
            Mock.Create<IReaparrDbContext>(),
            new Mock<IHttpClientFactory>().Object,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        using var response = await endpoint.GetThumbnailResponseAsync(
            client,
            "https://plex.example/photo/:/transcode",
            "https://plex.example/library/metadata/1/thumb/2",
            CancellationToken
        );

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestUris.ShouldBe([new Uri("https://plex.example/photo/:/transcode")]);
    }

    [Test]
    [Arguments("image/jpeg", "image/jpeg")]
    [Arguments("image/png", "image/png")]
    [Arguments("IMAGE/PNG; charset=binary", "image/png")]
    public async Task ShouldStreamPassiveThumbnailWithCanonicalContentType_WhenUpstreamResponseIsSuccessful(
        string upstreamContentType,
        string expectedMediaType
    )
    {
        // Arrange
        await SetupDatabase(
            1002,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );
        var plexServerId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var payload = new byte[] { 1, 2, 3 };
        const long upstreamContentLength = 42;
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }
        );
        handler.Responses[0].Content.Headers.ContentType = MediaTypeHeaderValue.Parse(upstreamContentType);
        handler.Responses[0].Content.Headers.ContentLength = upstreamContentLength;
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Once());
        var request = new GetPlexMediaThumbnailImageEndpointRequest
        {
            PlexServerId = plexServerId,
            PlexKey = 1,
            MetaDataKey = 2,
            Width = 300,
            Height = 450,
        };

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services =>
            {
                services.AddSingleton(Mock.Container.Resolve<IAppRuntimeInfo>());
                services.AddSingleton<IMemoryCache>(cache);
                services.AddSingleton(clientFactory.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.ContentType.ShouldBe(expectedMediaType);
        result.Endpoint.HttpContext.Response.ContentLength.ShouldBe(upstreamContentLength);
        result.Endpoint.HttpContext.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray().ShouldBe(payload);
        handler.RequestUris.Select(x => x.AbsolutePath).ShouldBe(["/photo/:/transcode"]);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldUseJpegFallback_WhenUpstreamOmitsContentType()
    {
        // Arrange
        await SetupDatabase(
            1003,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );
        var plexServerId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var payload = new byte[] { 4, 5, 6 };
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) }
        );
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Once());
        var request = new GetPlexMediaThumbnailImageEndpointRequest
        {
            PlexServerId = plexServerId,
            PlexKey = 1,
            MetaDataKey = 2,
            Width = 300,
            Height = 450,
        };

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services =>
            {
                services.AddSingleton(Mock.Container.Resolve<IAppRuntimeInfo>());
                services.AddSingleton<IMemoryCache>(cache);
                services.AddSingleton(clientFactory.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.ContentType.ShouldBe("image/jpeg");
        result.Endpoint.HttpContext.Response.ContentLength.ShouldBe(payload.Length);
        result.Endpoint.HttpContext.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray().ShouldBe(payload);
        handler.RequestUris.Select(x => x.AbsolutePath).ShouldBe([
            "/photo/:/transcode",
            $"/library/metadata/{request.PlexKey}/thumb/{request.MetaDataKey}",
        ]);
        clientFactory.Verify();
    }

    [Test]
    [Arguments("text/html")]
    [Arguments("image/svg+xml")]
    [Arguments("image/x-unknown")]
    [Arguments("image/svg+xml; charset=utf-8")]
    public async Task ShouldRejectUnsupportedUpstreamContentType_WhenTranscodeSucceeds(string upstreamContentType)
    {
        // Arrange
        await SetupDatabase(
            1004,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );
        var plexServerId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var attackerPayload = System.Text.Encoding.UTF8.GetBytes("attacker");
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(attackerPayload) }
        );
        handler.Responses[0].Content.Headers.ContentType = MediaTypeHeaderValue.Parse(upstreamContentType);
        handler.Responses[0].Content.Headers.ContentLength = attackerPayload.Length;
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Once());
        var request = new GetPlexMediaThumbnailImageEndpointRequest
        {
            PlexServerId = plexServerId,
            PlexKey = 1,
            MetaDataKey = 2,
            Width = 300,
            Height = 450,
        };

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services =>
            {
                services.AddSingleton(Mock.Container.Resolve<IAppRuntimeInfo>());
                services.AddSingleton<IMemoryCache>(cache);
                services.AddSingleton(clientFactory.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        result.Endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        result.Endpoint.HttpContext.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        result.Endpoint.HttpContext.Response.ContentLength.ShouldBeNull();
        var responseBody = ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray();
        responseBody.AsSpan().IndexOf(attackerPayload).ShouldBe(-1);
        result.Endpoint.HttpContext.Response.Body.Position = 0;
        var failure = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                result.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        failure.IsSuccess.ShouldBeFalse();
        failure.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        failure.Errors.Count.ShouldBe(1);
        handler.RequestUris.Select(x => x.AbsolutePath).ShouldBe(["/photo/:/transcode"]);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldRejectUnsupportedContentType_WhenDirectThumbnailFallbackSucceeds()
    {
        // Arrange
        await SetupDatabase(
            1005,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );
        var plexServerId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var attackerPayload = System.Text.Encoding.UTF8.GetBytes("attacker");
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(attackerPayload) }
        );
        handler.Responses[1].Content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/svg+xml");
        handler.Responses[1].Content.Headers.ContentLength = attackerPayload.Length;
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Once());
        var request = new GetPlexMediaThumbnailImageEndpointRequest
        {
            PlexServerId = plexServerId,
            PlexKey = 1,
            MetaDataKey = 2,
            Width = 300,
            Height = 450,
        };

        // Act
        var result = await TestEndpointHandleAsync(
            request,
            services =>
            {
                services.AddSingleton(Mock.Container.Resolve<IAppRuntimeInfo>());
                services.AddSingleton<IMemoryCache>(cache);
                services.AddSingleton(clientFactory.Object);
            }
        );

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        result.Endpoint.HttpContext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        result.Endpoint.HttpContext.Response.Headers["X-Content-Type-Options"].ToString().ShouldBe("nosniff");
        result.Endpoint.HttpContext.Response.ContentLength.ShouldBeNull();
        var responseBody = ((MemoryStream)result.Endpoint.HttpContext.Response.Body).ToArray();
        responseBody.AsSpan().IndexOf(attackerPayload).ShouldBe(-1);
        result.Endpoint.HttpContext.Response.Body.Position = 0;
        var failure = (
            await JsonSerializer.DeserializeAsync<BaseResultDTO>(
                result.Endpoint.HttpContext.Response.Body,
                DefaultJsonSerializerOptions.ConfigStandard,
                CancellationToken
            )
        ).ShouldNotBeNull();
        failure.IsSuccess.ShouldBeFalse();
        failure.StatusCode.ShouldBe(StatusCodes.Status502BadGateway);
        failure.Errors.Count.ShouldBe(1);
        handler.RequestUris.Select(x => x.AbsolutePath).ShouldBe([
            "/photo/:/transcode",
            $"/library/metadata/{request.PlexKey}/thumb/{request.MetaDataKey}",
        ]);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldFetchUpstream_WhenIfNoneMatchContainsPreVersionThumbnailETag()
    {
        // Arrange
        await SetupDatabase(
            1006,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );
        var plexServerId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([2]) }
        );
        handler.Responses[0].Content.Headers.ContentType = new("image/jpeg");
        handler.Responses[1].Content.Headers.ContentType = new("image/jpeg");
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Exactly(2));
        var request = new GetPlexMediaThumbnailImageEndpointRequest
        {
            PlexServerId = plexServerId,
            PlexKey = 1,
            MetaDataKey = 2,
            Width = 300,
            Height = 450,
        };
        var oldEtag = $"\"{request.PlexServerId}-{request.PlexKey}-{request.MetaDataKey}-{request.Width}x{request.Height}\"";

        var initial = await TestEndpointHandleAsync(
            request,
            services =>
            {
                services.AddSingleton(Mock.Container.Resolve<IAppRuntimeInfo>());
                services.AddSingleton<IMemoryCache>(cache);
                services.AddSingleton(clientFactory.Object);
            }
        );
        initial.StatusCode.ShouldBe(StatusCodes.Status200OK);
        initial.Endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldContain("\"v2-");
        initial.Endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldNotBe(oldEtag);
        initial.Endpoint.HttpContext.Response.Clear();
        initial.Endpoint.HttpContext.Request.Headers.IfNoneMatch = oldEtag;

        // Act
        await initial.Endpoint.HandleAsync(request, CancellationToken);

        // Assert
        initial.Endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        initial.Endpoint.HttpContext.Response.Headers.ETag.ToString().ShouldNotBe(oldEtag);
        ((MemoryStream)initial.Endpoint.HttpContext.Response.Body).ToArray().ShouldBe([2]);
        handler.RequestUris.Count.ShouldBe(2);
        clientFactory.Verify();
    }

    [Test]
    public async Task ShouldReturnNotFound_WhenPlexThumbnailIsMissing()
    {
        // Arrange
        await SetupDatabase(1001, config =>
        {
            config.PlexAccountCount = 1;
        });
        var plexServerId = await IDbContext.PlexServers.Select(x => x.Id).SingleAsync(CancellationToken);
        var handler = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound),
            new HttpResponseMessage(HttpStatusCode.NotFound)
        );
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory
            .Setup(x => x.CreateClient(HttpClientModule.PlexThumbnailClientName))
            .Returns(client)
            .Verifiable(Times.Once());

        var request = new GetPlexMediaThumbnailImageEndpointRequest
        {
            PlexServerId = plexServerId,
            PlexKey = 1,
            MetaDataKey = 2,
            Width = 300,
            Height = 450,
        };

        // Act
        var result = await TestEndpointHandleAsync(request, services =>
        {
            services.AddSingleton(Mock.Container.Resolve<IAppRuntimeInfo>());
            services.AddSingleton<IMemoryCache>(cache);
            services.AddSingleton(clientFactory.Object);
        });

        // Assert
        result.IsValid.ShouldBeTrue();
        result.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        handler
            .RequestUris.Select(x => x.AbsolutePath)
            .ShouldBe(["/photo/:/transcode", $"/library/metadata/{request.PlexKey}/thumb/{request.MetaDataKey}"]);
        clientFactory.Verify();
    }

    private sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public IReadOnlyList<HttpResponseMessage> Responses { get; } = responses;

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
