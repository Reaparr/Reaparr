using System.Net;
using LukeHagar.PlexAPI.SDK;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class ValidatePlexTvTokenCommandHandlerUnitTests : BaseUnitTest<ValidatePlexTvTokenCommandHandler>
{
    [Test]
    public async Task ShouldUseConfiguredAbsoluteServerUrl_WhenGettingTokenDetailsThroughGeneratedSdk()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        HttpRequestMessage? capturedRequest = null;
        var transport = new Mock<IPlexApiClient>(MockBehavior.Strict);
        transport
            .Setup(x => x.CloneAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync((HttpRequestMessage request) => request);
        transport
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .Callback<HttpRequestMessage>(request => capturedRequest = request)
            .ReturnsAsync(
                (HttpRequestMessage request) =>
                    new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        RequestMessage = request,
                        Content = new StringContent("{\"error\":\"offline\"}"),
                    }
            );
        var sdk = new PlexAPI(client: transport.Object, serverUrl: "https://plex.tv/api/v2", token: "auth-token");

        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateTvClient("auth-token", null, cancellation.Token))
            .Returns<string, PlexApiClientOptions?, CancellationToken>((_, _, _) => sdk)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(new ValidatePlexTokenCommand("auth-token"), cancellation.Token);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Method.ShouldBe(HttpMethod.Get);
        capturedRequest.RequestUri.ShouldBe(new Uri("https://plex.tv/api/v2/user"));
        capturedRequest.Headers.GetValues("X-Plex-Token").Single().ShouldBe("auth-token");
        Mock.Mock<IPlexApiClientFactory>().Verify();
    }
}
