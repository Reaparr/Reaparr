using System.Net;
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
    public async Task ShouldReturnNotFound_WhenPlexThumbnailIsMissing()
    {
        // Arrange
        await SetupDatabase(
            1001,
            config =>
            {
                config.PlexAccountCount = 1;
            }
        );
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
        result.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        handler
            .RequestUris.Select(x => x.AbsolutePath)
            .ShouldBe(["/photo/:/transcode", $"/library/metadata/{request.PlexKey}/thumb/{request.MetaDataKey}"]);
        clientFactory.Verify();
    }

    private sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

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
