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
        await using var beforeContext = container.Resolve<AuthDbContext>();
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
        await using var afterContext = container.Resolve<AuthDbContext>();
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
    public async Task ShouldRejectOldCookieAndProtectedMutation_WhenPasswordIsUpdated()
    {
        // Arrange
        using var container = await BaseContainer.Create(
            LogFactory.Create<CredentialUpdatesIntegrationTests>(),
            new Seed(17433),
            useTestAuthentication: false
        );
        using var loginClient = container.GetApiClient();
        loginClient.DefaultRequestHeaders.Remove("Authorization");
        var loginResponse = await LoginAsync(
            loginClient,
            DefaultUserAppCredentials.DefaultUsername,
            DefaultUserAppCredentials.DefaultPassword
        );
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var oldCookie = loginResponse
            .Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith(DefaultUserAppCredentials.DefaultCookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];

        using var authenticatedClient = container.GetApiClient();
        authenticatedClient.DefaultRequestHeaders.Remove("Authorization");
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", oldCookie);
        var updateRequest = new UpdateCredentialsEndpointRequest
        {
            Username = null,
            Password = "Replacement-password-43!",
        };

        // Act
        var updateResponse = await authenticatedClient.PutAsJsonAsync(ApiRoutes.AuthenticatedController, updateRequest);

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<BaseResultDTO>();
        updateResult.ShouldNotBeNull();
        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Errors.Count.ShouldBe(0);

        using var staleSessionClient = container.GetApiClient();
        staleSessionClient.DefaultRequestHeaders.Remove("Authorization");
        staleSessionClient.DefaultRequestHeaders.Add("Cookie", oldCookie);
        var staleProtectedResponse = await staleSessionClient.GetAsync(ApiRoutes.SettingsController + "/");
        staleProtectedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var staleMutationResponse = await staleSessionClient.PutAsJsonAsync(
            ApiRoutes.AuthenticatedController,
            new UpdateCredentialsEndpointRequest { Username = null, Password = "Another-password-54!" }
        );
        staleMutationResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var freshLoginClient = container.GetApiClient();
        freshLoginClient.DefaultRequestHeaders.Remove("Authorization");
        var freshLoginResponse = await LoginAsync(
            freshLoginClient,
            DefaultUserAppCredentials.DefaultUsername,
            "Replacement-password-43!"
        );
        freshLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var freshCookie = freshLoginResponse
            .Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith(DefaultUserAppCredentials.DefaultCookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];

        using var freshSessionClient = container.GetApiClient();
        freshSessionClient.DefaultRequestHeaders.Remove("Authorization");
        freshSessionClient.DefaultRequestHeaders.Add("Cookie", freshCookie);
        var freshProtectedResponse = await freshSessionClient.GetAsync(ApiRoutes.SettingsController + "/");
        freshProtectedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
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
        await using var beforeContext = container.Resolve<AuthDbContext>();
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
        await using var afterContext = container.Resolve<AuthDbContext>();
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
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(username), "Username");
        form.Add(new StringContent(password), "Password");
        form.Add(new StringContent("false"), "RememberMe");
        return await client.PostAsync(ApiRoutes.LoginEndpoint, form);
    }
}
