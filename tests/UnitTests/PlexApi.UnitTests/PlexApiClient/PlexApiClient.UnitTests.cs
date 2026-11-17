using System.Net;
using System.Text;
using Moq.Contrib.HttpClient;
using Moq.Protected;
using Reaparr.Application.Contracts;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class PlexApiClientUnitTests : BaseUnitTest<Func<PlexApiClientOptions?, PlexApiClient>>
{
    private readonly PlexApiHttpClientFixture _httpClientFixture;
    [After(Test)]
    public void CleanupHttpClientFixture() => _httpClientFixture.Dispose();

    public PlexApiClientUnitTests()
    {
        _httpClientFixture = new PlexApiHttpClientFixture(Log);
    }
    private PlexApiClient CreateClient(PlexApiClientOptions options) =>
        new(_httpClientFixture.Create(HttpHandlerMock.Object), options);



    [Test]
    public async Task ShouldPreserveOriginal401HtmlResponse_WhenPlexApiReturns401HtmlResponse()
    {
        const string expectedHtml = "<html><head><title>Unauthorized</title></head><body><h1>401 Unauthorized</h1></body></html>";
        HttpHandlerMock
            .SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.Unauthorized, x =>
            {
                x.Content = new StringContent(expectedHtml, Encoding.UTF8, ContentType.TextHtml);
            });

        // Arrange
        var client = CreateClient(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null, RetryCount = 0 });

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

        // Assert
        responseMessage.ShouldNotBeNull();
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        responseMessage.Content.Headers.ContentType!.MediaType.ShouldBe(ContentType.TextHtml);
        var html = await responseMessage.Content.ReadAsStringAsync(CancellationToken);
        html.ShouldBe(expectedHtml);
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldHandleTimeoutException_WhenHttpRequestTimesOut()
    {
        // Arrange
        var originalTimeoutException = new TaskCanceledException("Request timed out");
        HttpHandlerMock.SetupAnyRequest().ThrowsAsync(originalTimeoutException);
        var client = CreateClient(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null, RetryCount = 0 });

        // Act
        await Should.ThrowAsync<TaskCanceledException>(() =>
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"))
        );

        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(x => x.RequestUri == new Uri("http://localhost")), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldPreserveOriginal500HtmlResponse_WhenInternalServerErrorOccurs()
    {
        const string expectedHtml = "<html><head><title>Internal Server Error</title></head><body><h1>500 Internal Server Error</h1></body></html>";
        HttpHandlerMock
            .SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.InternalServerError, x =>
            {
                x.Content = new StringContent(expectedHtml, Encoding.UTF8, ContentType.TextHtml);
            });

        var client = CreateClient(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null, RetryCount = 0 });
        var responseMessage = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

        // Assert
        responseMessage.ShouldNotBeNull();
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        responseMessage.Content.Headers.ContentType!.MediaType.ShouldBe(ContentType.TextHtml);
        var html = await responseMessage.Content.ReadAsStringAsync(CancellationToken);
        html.ShouldBe(expectedHtml);
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldReturnValid200Response_WhenRequestIsSuccessful()
    {
        // Set up the mocked HttpClient to return a 200 OK response
        HttpHandlerMock
            .SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.OK, x => x.Content = "{ \"message\": \"Success\" }".ToStringContent());

        // Arrange
        var client = CreateClient(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null, RetryCount = 0 });

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

        // Assert
        responseMessage.ShouldNotBeNull();
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await responseMessage.Content.ReadAsStringAsync(CancellationToken);
        json.ShouldNotBeNullOrEmpty();
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldReturn503Response_WhenServiceUnavailableIsReceived()
    {
        HttpHandlerMock.SetupAnyRequest().ReturnsResponse(HttpStatusCode.ServiceUnavailable);

        var client = CreateClient(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null, RetryCount = 0 });
        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"));

        // Assert
        responseMessage.ShouldNotBeNull();
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

        // PlexApiClient forwards the raw HttpClient response; retries are handled by the registered pipeline.
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldReturnBadGatewayResponse_WhenHttpRequestExceptionOccurs()
    {
        // Set up the mocked HttpClient to throw an HttpRequestException
        HttpHandlerMock.SetupAnyRequest().ThrowsAsync(new HttpRequestException("Network error"));

        // Arrange
        var progressUpdates = new List<HttpRequestRetryProgress>();
        var client = CreateClient(
            new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = progressUpdates.Add, RetryCount = 0 }
        );

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(() =>
            client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://localhost"))
        );

        // Assert
        exception.Message.ShouldBe("Network error");
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(x => x.RequestUri == new Uri("http://localhost")), ItExpr.IsAny<CancellationToken>());
    }
}
