using System.Net;
using System.Text;
using System.Text.Json;
using LukeHagar.PlexAPI.SDK;
using LukeHagar.PlexAPI.SDK.Models.Requests;
using Reaparr.Application.Contracts;
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
        var transport = new PlexApiClient(
            httpClient,
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test/api/v2" }
        );
        var sdk = new PlexAPI(client: transport, serverUrl: "http://plex.test/api/v2");

        // Act
        var exception = await Should.ThrowAsync<Exception>(() =>
            sdk.Authentication.PostUsersSignInDataAsync(
                new PostUsersSignInDataRequest
                {
                    ClientIdentifier = "test-client",
                    RequestBody = new PostUsersSignInDataRequestBody
                    {
                        Login = "test-user",
                        Password = "test-password",
                    },
                }
            )
        );
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

    [Test]
    [Arguments(HttpStatusCode.Unauthorized)]
    [Arguments(HttpStatusCode.InternalServerError)]
    [Arguments(HttpStatusCode.BadGateway)]
    public async Task ShouldPreserveStatusAndCompletion_WhenGeneratedSdkReceivesHtmlError(HttpStatusCode status)
    {
        // Arrange
        var terminal = new HtmlResponseHandler(status);
        var progress = new List<HttpRequestRetryProgress>();
        using var fixture = new PlexApiHttpClientFixture(Log);
        var factory = new PlexApiClientFactory(
            options => new PlexApiClient(fixture.Create(terminal), options!),
            Moq.Mock.Of<Reaparr.Data.Contracts.IReaparrDbContextFactory>()
        );
        var client = factory.CreateTvClient(
            options: new PlexApiClientOptions
            {
                ConnectionUrl = "https://plex.tv/api/v2",
                RetryCount = 1,
                RetryProgressAction = progress.Add,
            }
        );

        // Act
        var result = await client
            .Authentication.PostUsersSignInDataAsync(
                new PostUsersSignInDataRequest
                {
                    ClientIdentifier = "html-regression",
                    RequestBody = new PostUsersSignInDataRequestBody
                    {
                        Login = "test-user",
                        Password = "test-password",
                    },
                }
            )
            .ToResponse(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(status == HttpStatusCode.Unauthorized ? 4 : 3);
        if (status == HttpStatusCode.Unauthorized)
        {
            result.HasPlex401UnauthorizedError().ShouldBeTrue();
            var error = result.Errors.OfType<PlexError>().Single();
            error.Code.ShouldBe(401);
            error.Status.ShouldBe(401);
            error.Message.ShouldBe("HTML upstream error");
        }
        else
            result.GetStatusCodeReason()!.Metadata[ResultExtensions.StatusCodeName].ShouldBe((int)status);
        var exception = result.Errors.OfType<ExceptionalError>().Single().Exception;
        var sdkError = (LukeHagar.PlexAPI.SDK.Models.Errors.PlexAPIError)exception;
        sdkError.RawResponse.ShouldBeSameAs(terminal.Response);
        sdkError.RawResponse.StatusCode.ShouldBe(status);
        sdkError.RawResponse.ReasonPhrase.ShouldBe("HTML upstream error");
        using var body = JsonDocument.Parse(sdkError.Body);
        body.RootElement.GetProperty("errors")
            .EnumerateArray()
            .Single()
            .GetProperty("status")
            .GetInt32()
            .ShouldBe((int)status);
        terminal.RequestCount.ShouldBe(1);
        progress.Count.ShouldBe(1);
        progress.Single().Completed.ShouldBeTrue();
        progress.Single().StatusCode.ShouldBe((int)status);
    }

    [Test]
    public async Task ShouldNotFabricateSuccess_WhenSuccessfulHttpResponseContainsHtml()
    {
        // Arrange
        using var fixture = new PlexApiHttpClientFixture(Log);
        var transport = new PlexApiClient(
            fixture.Create(new HtmlResponseHandler(HttpStatusCode.OK)),
            new PlexApiClientOptions { ConnectionUrl = "http://plex.test", RetryCount = 0 }
        );
        var client = new PlexAPI(client: transport, serverUrl: "http://plex.test");

        // Act
        var result = await client.General.GetIdentityAsync().ToResponse(CancellationToken);

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.OfType<ExceptionalError>().Count().ShouldBe(1);
    }

    private sealed class HtmlResponseHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpResponseMessage? Response { get; private set; }
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            Response = new HttpResponseMessage(status)
            {
                RequestMessage = request,
                ReasonPhrase = "HTML upstream error",
                Content = new StringContent("<html>upstream error</html>", Encoding.UTF8, "text/html"),
            };
            return Task.FromResult(Response);
        }
    }

    private sealed class UnauthorizedHandler(int code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    RequestMessage = request,
                    Content = new StringContent(
                        $"{{\"errors\":[{{\"code\":{code},\"message\":\"Authentication rejected\",\"status\":401}}]}}",
                        Encoding.UTF8,
                        "application/json"
                    ),
                }
            );
    }
}
