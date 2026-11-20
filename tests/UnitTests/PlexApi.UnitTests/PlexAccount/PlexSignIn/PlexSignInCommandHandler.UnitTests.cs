using System.Net;
using LukeHagar.PlexAPI.SDK;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class PlexSignInCommandHandlerUnitTests : BaseUnitTest<PlexSignInCommandHandler>
{
    [Test]
    public async Task ShouldUseConfiguredAbsoluteServerUrl_WhenSigningInThroughGeneratedSdk()
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
        var sdk = new PlexAPI(client: transport.Object, serverUrl: "https://plex.tv/api/v2", token: string.Empty);

        Mock.Mock<IPlexApiClientFactory>()
            .Setup(x => x.CreateTvClient(string.Empty, null, cancellation.Token))
            .Returns<string, PlexApiClientOptions?, CancellationToken>((_, _, _) => sdk)
            .Verifiable(Times.Once());

        // Act
        var result = await Sut.ExecuteAsync(
            new PlexSignInCommand
            {
                ClientId = "client-id",
                Username = "user@example.com",
                Password = "password",
                VerificationCode = string.Empty,
            },
            cancellation.Token
        );

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(3);
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Method.ShouldBe(HttpMethod.Post);
        capturedRequest.RequestUri.ShouldBe(new Uri("https://plex.tv/api/v2/users/signin"));
        capturedRequest.Headers.Contains("X-Plex-Token").ShouldBeFalse();
        Mock.Mock<IPlexApiClientFactory>().Verify();
    }
}
