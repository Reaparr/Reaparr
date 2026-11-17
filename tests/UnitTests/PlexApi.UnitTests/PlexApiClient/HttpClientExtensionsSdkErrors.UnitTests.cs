using System.Net;
using System.Text;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.PlexApi.UnitTests;

public class HttpClientExtensionsSdkErrorsUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(1001)]
    [Arguments(PlexErrorCodes.EnterVerificationCode)]
    [Arguments(PlexErrorCodes.InvalidVerificationCode)]
    public async Task ShouldPreserveUnauthorizedAndPlexErrorCode_WhenGeneratedAuthenticationFails(int code)
    {
        using var httpClientFixture = new PlexApiHttpClientFixture(Log);
        using var httpClient = httpClientFixture.Create(new UnauthorizedHandler(code));
        var transport = new PlexApiClient(httpClient, new PlexApiClientOptions { ConnectionUrl = "http://plex.test/api/v2" });
        var sdk = new PlexAPI(client: transport, serverUrl: "http://plex.test/api/v2");

        // Act
        var exception = await Should.ThrowAsync<Exception>(() => sdk.Authentication.PostUsersSignInDataAsync(
            new PostUsersSignInDataRequest
            {
                ClientIdentifier = "test-client",
                RequestBody = new PostUsersSignInDataRequestBody
                {
                    Login = "test-user",
                    Password = "test-password",
                },
            }));
        var result = await Task.FromException<object>(exception).ToResponse(CancellationToken.None);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(4);
        result.HasPlex401UnauthorizedError().ShouldBeTrue();
        var error = result.Errors.OfType<PlexError>().Single();
        error.Code.ShouldBe(code);
        error.Status.ShouldBe(401);
        error.Message.ShouldBe("Authentication rejected");
        result.Errors.OfType<ExceptionalError>().Single().Exception.ShouldBeSameAs(exception);
    }

    private sealed class UnauthorizedHandler(int code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                RequestMessage = request,
                Content = new StringContent(
                    $"{{\"errors\":[{{\"code\":{code},\"message\":\"Authentication rejected\",\"status\":401}}]}}",
                    Encoding.UTF8,
                    "application/json"),
            });
    }
}
