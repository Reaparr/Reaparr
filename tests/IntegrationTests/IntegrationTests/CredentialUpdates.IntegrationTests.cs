using System.Net.Http.Json;
using Reaparr.FluentResultExtensions;

namespace Reaparr.IntegrationTests;

public class CredentialUpdatesIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldRetainStoredCredentialsAndOldLogin_WhenNewPasswordViolatesPolicy(bool changeUsername)
    {
        // Arrange
        using var container = await BaseContainer.Create(
            LogFactory.Create<CredentialUpdatesIntegrationTests>(),
            new Seed(17431),
            useTestAuthentication: false
        );
        using var client = container.GetApiClient();
        client.DefaultRequestHeaders.Remove("Authorization");
        var loginResponse = await LoginAsync(
            client,
            DefaultUserAppCredentials.DefaultUsername,
            DefaultUserAppCredentials.DefaultPassword
        );
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = loginResponse
            .Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith(DefaultUserAppCredentials.DefaultCookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var beforeContext = container.Resolve<AuthDbContext>();
        var before = await beforeContext
            .Users.Select(x => new
            {
                x.Id,
                x.UserName,
                x.NormalizedUserName,
                x.PasswordHash,
                x.SecurityStamp,
            })
            .SingleAsync();
        before.UserName.ShouldBe(DefaultUserAppCredentials.DefaultUsername);
        var request = new UpdateCredentialsEndpointRequest
        {
            Username = changeUsername ? "replacement-user" : null,
            Password = "abcdefgh",
        };

        // Act
        var response = await client.PutAsJsonAsync(ApiRoutes.AuthenticatedController, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<BaseResultDTO>();
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeFalse();
        result.Errors.Count.ShouldBe(3);
        using var afterContext = container.Resolve<AuthDbContext>();
        var after = await afterContext
            .Users.Select(x => new
            {
                x.Id,
                x.UserName,
                x.NormalizedUserName,
                x.PasswordHash,
                x.SecurityStamp,
            })
            .SingleAsync();
        after.ShouldBe(before);
        using var oldLoginClient = container.GetApiClient();
        oldLoginClient.DefaultRequestHeaders.Remove("Authorization");
        var oldLoginResponse = await LoginAsync(
            oldLoginClient,
            DefaultUserAppCredentials.DefaultUsername,
            DefaultUserAppCredentials.DefaultPassword
        );
        oldLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var oldLoginResult = await oldLoginResponse.Content.ReadFromJsonAsync<BaseResultDTO>();
        oldLoginResult.ShouldNotBeNull();
        oldLoginResult.IsSuccess.ShouldBeTrue();
        oldLoginResult.Errors.Count.ShouldBe(0);
    }

    [Test]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ShouldPersistRequestedCredentialsAndAuthenticate_WhenUpdateIsValid(
        bool changeUsername,
        bool changePassword
    )
    {
        // Arrange
        using var container = await BaseContainer.Create(
            LogFactory.Create<CredentialUpdatesIntegrationTests>(),
            new Seed(17432),
            useTestAuthentication: false
        );
        using var client = container.GetApiClient();
        client.DefaultRequestHeaders.Remove("Authorization");
        var loginResponse = await LoginAsync(
            client,
            DefaultUserAppCredentials.DefaultUsername,
            DefaultUserAppCredentials.DefaultPassword
        );
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = loginResponse
            .Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith(DefaultUserAppCredentials.DefaultCookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var beforeContext = container.Resolve<AuthDbContext>();
        var before = await beforeContext
            .Users.Select(x => new
            {
                x.Id,
                x.UserName,
                x.PasswordHash,
                x.SecurityStamp,
            })
            .SingleAsync();
        before.UserName.ShouldBe(DefaultUserAppCredentials.DefaultUsername);
        var username = changeUsername ? "replacement-user" : DefaultUserAppCredentials.DefaultUsername;
        var password = changePassword ? "Replacement-password-43!" : DefaultUserAppCredentials.DefaultPassword;
        var request = new UpdateCredentialsEndpointRequest
        {
            Username = changeUsername ? username : null,
            Password = changePassword ? password : null,
        };

        // Act
        var response = await client.PutAsJsonAsync(ApiRoutes.AuthenticatedController, request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BaseResultDTO>();
        result.ShouldNotBeNull();
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        using var afterContext = container.Resolve<AuthDbContext>();
        var after = await afterContext.Users.SingleAsync();
        after.Id.ShouldBe(before.Id);
        after.UserName.ShouldBe(username);
        after.NormalizedUserName.ShouldBe(username.ToUpperInvariant());
        if (changePassword)
        {
            after.PasswordHash.ShouldNotBe(before.PasswordHash);
            after.SecurityStamp.ShouldNotBe(before.SecurityStamp);
        }
        else
        {
            after.PasswordHash.ShouldBe(before.PasswordHash);
            after.SecurityStamp.ShouldBe(before.SecurityStamp);
        }

        using var updatedLoginClient = container.GetApiClient();
        updatedLoginClient.DefaultRequestHeaders.Remove("Authorization");
        var updatedLoginResponse = await LoginAsync(updatedLoginClient, username, password);
        updatedLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedLoginResult = await updatedLoginResponse.Content.ReadFromJsonAsync<BaseResultDTO>();
        updatedLoginResult.ShouldNotBeNull();
        updatedLoginResult.IsSuccess.ShouldBeTrue();
        updatedLoginResult.Errors.Count.ShouldBe(0);
        using var oldLoginClient = container.GetApiClient();
        oldLoginClient.DefaultRequestHeaders.Remove("Authorization");
        var oldLoginResponse = await LoginAsync(
            oldLoginClient,
            DefaultUserAppCredentials.DefaultUsername,
            DefaultUserAppCredentials.DefaultPassword
        );
        oldLoginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(username), "Username" },
            { new StringContent(password), "Password" },
            { new StringContent("false"), "RememberMe" },
        };
        return await client.PostAsync(ApiRoutes.LoginEndpoint, form);
    }
}
