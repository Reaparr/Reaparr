using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace Reaparr.IntegrationTests;

public class AuthenticationCookiePipelineIntegrationTests : BaseIntegrationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldIssueAuthoritativeCookieWithIdentityAndPersistence_WhenPasswordLoginSucceeds(
        bool rememberMe
    )
    {
        // Arrange
        using var container = await CreateRealAuthenticationContainer(17014);
        using var client = CreateUnauthenticatedClient(container);
        using var formData = new MultipartFormDataContent
        {
            { new StringContent(DefaultUserAppCredentials.DefaultUsername), "Username" },
            { new StringContent(DefaultUserAppCredentials.DefaultPassword), "Password" },
            { new StringContent(rememberMe.ToString()), "RememberMe" },
        };
        var seededUser = await container
            .Resolve<IUserService>()
            .FindByNameAsync(DefaultUserAppCredentials.DefaultUsername);
        seededUser.ShouldNotBeNull();

        // Act
        var response = await client.PostAsync(ApiRoutes.LoginEndpoint, formData);

        response.IsSuccessStatusCode.ShouldBeTrue();
        GetSetCookieHeaders(response).Count.ShouldBe(1);
        var cookie = GetCookiePair(GetSingleAuthenticationCookie(response));
        var ticket = UnprotectCookie(container, cookie);

        ticket.Principal.Identity.ShouldNotBeNull();
        ticket.Principal.Identity.IsAuthenticated.ShouldBeTrue();
        ticket.Principal.Identity.Name.ShouldBe(DefaultUserAppCredentials.DefaultUsername);
        var nameIdentifierClaim = ticket.Principal.FindFirst(ClaimTypes.NameIdentifier);
        nameIdentifierClaim.ShouldNotBeNull();
        nameIdentifierClaim!.Value.ShouldBe(seededUser!.Id);
        ticket.Principal.IsInRole(DefaultUserAppCredentials.DefaultAdminRole).ShouldBeTrue();
        ticket.Properties.IsPersistent.ShouldBe(rememberMe);

        using var authenticatedClient = CreateUnauthenticatedClient(container);
        authenticatedClient.DefaultRequestHeaders.Add("Cookie", cookie);
        var protectedResponse = await authenticatedClient.GetAsync(ApiRoutes.SettingsController + "/");
        protectedResponse.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldClearAuthoritativeCookie_WhenLogoutSucceeds()
    {
        // Arrange
        using var container = await CreateRealAuthenticationContainer(17015);
        using var loginClient = CreateUnauthenticatedClient(container);
        using var formData = new MultipartFormDataContent
        {
            { new StringContent(DefaultUserAppCredentials.DefaultUsername), "Username" },
            { new StringContent(DefaultUserAppCredentials.DefaultPassword), "Password" },
            { new StringContent("false"), "RememberMe" },
        };
        var loginResponse = await loginClient.PostAsync(ApiRoutes.LoginEndpoint, formData);
        loginResponse.IsSuccessStatusCode.ShouldBeTrue();
        var cookie = GetCookiePair(GetSingleAuthenticationCookie(loginResponse));

        using var logoutClient = CreateUnauthenticatedClient(container);
        logoutClient.DefaultRequestHeaders.Add("Cookie", cookie);

        // Act
        var logoutResponse = await logoutClient.PostAsync(ApiRoutes.LogOutEndpoint, content: null);

        // Assert
        logoutResponse.IsSuccessStatusCode.ShouldBeTrue();
        var deletedCookie = GetSingleAuthenticationCookie(logoutResponse);
        var deletedHeader = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(deletedCookie);
        deletedHeader.Value.ToString().ShouldBeEmpty();
        deletedHeader.Expires.ShouldNotBeNull();
        deletedHeader.Expires.Value.ShouldBeLessThan(DateTimeOffset.UtcNow);

        using var unauthenticatedClient = CreateUnauthenticatedClient(container);
        var statusResponse = await unauthenticatedClient.GetAsync(ApiRoutes.AuthenticatedController + "/status");
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<BaseContainer> CreateRealAuthenticationContainer(int seed) =>
        await BaseContainer.Create(
            LogFactory.Create<AuthenticationCookiePipelineIntegrationTests>(),
            new Seed(seed),
            useTestAuthentication: false
        );

    private static HttpClient CreateUnauthenticatedClient(BaseContainer container)
    {
        var client = container.GetApiClient();
        client.DefaultRequestHeaders.Remove("Authorization");
        return client;
    }

    private static List<string> GetSetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];

    private static string GetSingleAuthenticationCookie(HttpResponseMessage response)
    {
        var cookies = GetSetCookieHeaders(response)
            .Where(x => x.StartsWith($"{DefaultUserAppCredentials.DefaultCookieName}=", StringComparison.Ordinal))
            .ToList();

        cookies.Count.ShouldBe(1);
        return cookies[0];
    }

    private static string GetCookiePair(string setCookie) => setCookie[..setCookie.IndexOf(';')];

    private static AuthenticationTicket UnprotectCookie(BaseContainer container, string cookie)
    {
        var cookieValue = cookie[(cookie.IndexOf('=') + 1)..];
        var options = container
            .Resolve<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        return options.TicketDataFormat!.Unprotect(cookieValue).ShouldNotBeNull();
    }
}
