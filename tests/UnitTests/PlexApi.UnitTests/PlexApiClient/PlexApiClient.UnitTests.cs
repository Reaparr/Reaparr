using System.Net;
using System.Text;
using System.Text.Json;
using Moq.Contrib.HttpClient;
using Moq.Protected;
using Reaparr.Application.Contracts;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class PlexApiClientUnitTests : BaseUnitTest<Func<PlexApiClientOptions?, PlexApiClient>>
{
    [Test]
    public async Task ShouldReturnValid401ResponseAsJson_WhenPlexApiReturns401HtmlResponse()
    {
        SetupHttpClient(config =>
        {
            config
                .SetupAnyRequest()
                .ReturnsResponse(HttpStatusCode.Unauthorized, x =>
                {
                    x.Content = new StringContent(
                        "<html><head><title>Unauthorized</title></head><body><h1>401 Unauthorized</h1></body></html>",
                        Encoding.UTF8,
                        ContentType.TextHtml
                    );
                });
        });

        // Arrange
        var client = Sut(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null });

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage());

        // Assert
        responseMessage.ShouldNotBeNull();
        responseMessage
            .Content.Headers.Any(x =>
                x.Key == "Content-Type" && x.Value.Any(y => y.Contains(ContentType.ApplicationJson))
            )
            .ShouldBeTrue();
        var json = await responseMessage.Content.ReadAsStringAsync(CancellationToken);
        json.ShouldNotBeNullOrEmpty();

        // Verify json response
        var result = JsonSerializer.Deserialize<PlexErrorDTO>(json, DefaultJsonSerializerOptions.ConfigStandard);
        result.ShouldNotBeNull();
        result.Code.ShouldBe(401);
        result.Message.ShouldBe("Unauthorized");
        result.Status.ShouldBe(401);
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldHandleTimeoutException_WhenHttpRequestTimesOut()
    {
        // Set up the mocked HttpClient to throw a timeout exception
        SetupHttpClient(config =>
        {
            config.SetupAnyRequest().ThrowsAsync(new TaskCanceledException("Request timed out"));
        });

        // Arrange
        var progressUpdates = new List<HttpRequestRetryProgress>();
        var client = Sut(
            new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = progressUpdates.Add }
        );

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage());

        // Assert
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.RequestTimeout);
        responseMessage.ReasonPhrase.ShouldBe("Request Timeout");
        var progress = progressUpdates.ShouldHaveSingleItem();
        progress.Completed.ShouldBeTrue();
        progress.ConnectionSuccessful.ShouldBeFalse();
        progress.StatusCode.ShouldBe((int)HttpStatusCode.RequestTimeout);
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldReturn500Response_WhenInternalServerErrorOccurs()
    {
        // Set up the mocked HttpClient to return a 500 Internal Server Error response
        SetupHttpClient(config =>
        {
            config
                .SetupAnyRequest()
                .ReturnsResponse(HttpStatusCode.InternalServerError, x =>
                {
                    x.Content = new StringContent(
                        "<html><head><title>Internal Server Error</title></head><body><h1>500 Internal Server Error</h1></body></html>",
                        Encoding.UTF8,
                        ContentType.TextHtml
                    );
                });
        });

        // Arrange
        var client = Sut(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null });

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage());

        // Assert
        responseMessage.ShouldNotBeNull();
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var json = await responseMessage.Content.ReadAsStringAsync(CancellationToken);
        json.ShouldNotBeNullOrEmpty();

        // Verify JSON response content
        var result = JsonSerializer.Deserialize<PlexErrorDTO>(json, DefaultJsonSerializerOptions.ConfigStandard);
        result.ShouldNotBeNull();
        result.Message.ShouldBe("Internal Server Error");
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Test]
    public async Task ShouldReturnValid200Response_WhenRequestIsSuccessful()
    {
        // Set up the mocked HttpClient to return a 200 OK response
        SetupHttpClient(config =>
        {
            config
                .SetupAnyRequest()
                .ReturnsResponse(HttpStatusCode.OK, x => x.Content = "{ \"message\": \"Success\" }".ToStringContent());
        });

        // Arrange
        var client = Sut(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null });

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage());

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
        SetupHttpClient(config =>
        {
            config.SetupAnyRequest().ReturnsResponse(HttpStatusCode.ServiceUnavailable);
        });

        // Arrange
        var client = Sut(new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = null });

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage());

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
        SetupHttpClient(config =>
        {
            config.SetupAnyRequest().ThrowsAsync(new HttpRequestException("Network error"));
        });

        // Arrange
        var progressUpdates = new List<HttpRequestRetryProgress>();
        var client = Sut(
            new PlexApiClientOptions { ConnectionUrl = "http://localhost", RetryProgressAction = progressUpdates.Add }
        );

        // Act
        var responseMessage = await client.SendAsync(new HttpRequestMessage());

        // Assert
        responseMessage.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        responseMessage.ReasonPhrase.ShouldBe("Bad Gateway");
        var progress = progressUpdates.ShouldHaveSingleItem();
        progress.Completed.ShouldBeTrue();
        progress.ConnectionSuccessful.ShouldBeFalse();
        progress.StatusCode.ShouldBe((int)HttpStatusCode.BadGateway);
        HttpHandlerMock
            .Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
