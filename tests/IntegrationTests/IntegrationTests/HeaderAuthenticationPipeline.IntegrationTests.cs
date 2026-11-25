using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;
using Reaparr.Settings.Contracts;

namespace Reaparr.IntegrationTests;

public class HeaderAuthenticationPipelineIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldAuthorizeFirstProtectedRequest_WithValidTrustedHeaderAndNoCookie()
    {
        // Arrange
        using var container = await CreateRealAuthenticationContainer(17016, ["127.0.0.1", "::1"]);
        using var client = CreateUnauthenticatedClient(container);
        client.DefaultRequestHeaders.Add("X-Auth-User", DefaultUserAppCredentials.DefaultUsername);

        // Act
        var response = await client.GetAsync(ApiRoutes.SettingsController + "/");

        response.IsSuccessStatusCode.ShouldBeTrue();
        GetSetCookieHeaders(response).Count.ShouldBe(1);
        var setCookie = GetAuthenticationCookies(response).Single();
        var ticket = UnprotectCookie(container, setCookie);
        ticket.Principal.Identity.ShouldNotBeNull();
        ticket.Principal.Identity.IsAuthenticated.ShouldBeTrue();
        ticket.Principal.Identity.Name.ShouldBe(DefaultUserAppCredentials.DefaultUsername);
        ticket.Principal.IsInRole(DefaultUserAppCredentials.DefaultAdminRole).ShouldBeTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldKeepIssuedCookieUsableAndRevokeItAfterPasswordChange_WhenHeaderAssertionsStop(
        bool disableHeaderAuthentication
    )
    {
        // Arrange
        using var container = await CreateRealAuthenticationContainer(17020, ["127.0.0.1", "::1"]);
        using var headerClient = CreateUnauthenticatedClient(container);
        headerClient.DefaultRequestHeaders.Add("X-Auth-User", DefaultUserAppCredentials.DefaultUsername);
        var headerResponse = await headerClient.GetAsync(ApiRoutes.SettingsController + "/");
        headerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var setCookie = GetAuthenticationCookies(headerResponse).Single();
        var headerCookie = setCookie[..setCookie.IndexOf(';')];
        var ticket = UnprotectCookie(container, setCookie);
        ticket.Properties.IsPersistent.ShouldBeTrue();
        ticket.Principal.FindFirst("HeaderAuth")!.Value.ShouldBe("true");
        ticket.Principal.Identity!.Name.ShouldBe(DefaultUserAppCredentials.DefaultUsername);
        var userService = container.Resolve<IUserService>();
        var before = (await userService.FindByNameAsync(DefaultUserAppCredentials.DefaultUsername)).ShouldNotBeNull();
        var originalSecurityStamp = before.SecurityStamp;
        container.Resolve<IAuthenticationSettings>().HeaderAuthentication.Enabled = !disableHeaderAuthentication;

        using var cookieClient = CreateUnauthenticatedClient(container);
        cookieClient.DefaultRequestHeaders.Add("Cookie", headerCookie);

        // Act
        var cookieResponse = await cookieClient.GetAsync(ApiRoutes.SettingsController + "/");
        using var statusResponse = await cookieClient.GetAsync(
            ApiRoutes.AuthenticatedController + "/status",
            CancellationToken
        );
        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var status = (
            await statusResponse.Content.ReadFromJsonAsync<ResultDTO<UserClaimsDTO>>(CancellationToken)
        ).ShouldNotBeNull();
        status.IsSuccess.ShouldBeTrue();
        status.Errors.Count.ShouldBe(0);
        var claims = status.Value.ShouldNotBeNull();
        claims.IsLoggedIn.ShouldBeTrue();
        claims.UserName.ShouldBe(DefaultUserAppCredentials.DefaultUsername);
        var updateResponse = await cookieClient.PutAsJsonAsync(
            ApiRoutes.AuthenticatedController,
            new UpdateCredentialsEndpointRequest { Username = null, Password = "Replacement-password-43!" }
        );

        // Assert
        cookieResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<BaseResultDTO>();
        updateResult.ShouldNotBeNull();
        updateResult.IsSuccess.ShouldBeTrue();
        updateResult.Errors.Count.ShouldBe(0);

        using var staleCookieClient = CreateUnauthenticatedClient(container);
        staleCookieClient.DefaultRequestHeaders.Add("Cookie", headerCookie);
        var staleResponse = await staleCookieClient.GetAsync(ApiRoutes.SettingsController + "/");
        staleResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var staleMutationResponse = await staleCookieClient.PutAsJsonAsync(
            ApiRoutes.AuthenticatedController,
            new UpdateCredentialsEndpointRequest { Username = null, Password = "Another-password-54!" }
        );
        staleMutationResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var after = (await userService.FindByNameAsync(DefaultUserAppCredentials.DefaultUsername)).ShouldNotBeNull();
        after.Id.ShouldBe(before.Id);
        after.SecurityStamp.ShouldNotBe(originalSecurityStamp);
        using var anonymousClient = CreateUnauthenticatedClient(container);
        using var anonymousResponse = await anonymousClient.GetAsync(
            ApiRoutes.SettingsController + "/",
            CancellationToken
        );
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var freshLoginClient = CreateUnauthenticatedClient(container);
        using var freshFormData = new MultipartFormDataContent
        {
            { new StringContent(DefaultUserAppCredentials.DefaultUsername), "Username" },
            { new StringContent("Replacement-password-43!"), "Password" },
            { new StringContent("false"), "RememberMe" },
        };
        var freshLoginResponse = await freshLoginClient.PostAsync(ApiRoutes.LoginEndpoint, freshFormData);
        freshLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task ShouldRejectFirstProtectedRequest_WhenHeaderUserIsUnknown()
    {
        // Arrange
        using var container = await CreateRealAuthenticationContainer(17017, ["127.0.0.1", "::1"]);
        using var client = CreateUnauthenticatedClient(container);
        client.DefaultRequestHeaders.Add("X-Auth-User", "unknown-header-user");

        // Act
        var response = await client.GetAsync(ApiRoutes.SettingsController + "/");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        GetAuthenticationCookies(response).Count.ShouldBe(0);
    }

    [Test]
    public async Task ShouldRejectFirstProtectedRequest_WhenHeaderComesFromUntrustedProxy()
    {
        // Arrange
        using var container = await CreateRealAuthenticationContainer(17018, ["10.0.0.0/8"]);
        using var client = CreateUnauthenticatedClient(container);
        client.DefaultRequestHeaders.Add("X-Auth-User", DefaultUserAppCredentials.DefaultUsername);

        // Act
        var response = await client.GetAsync(ApiRoutes.SettingsController + "/");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        GetAuthenticationCookies(response).Count.ShouldBe(0);
    }

    private static async Task<BaseContainer> CreateRealAuthenticationContainer(int seed, List<string> trustedProxies) =>
        await BaseContainer.Create(
            LogFactory.Create<HeaderAuthenticationPipelineIntegrationTests>(),
            new Seed(seed),
            ConfigureHeaderAuthentication(trustedProxies),
            useTestAuthentication: false
        );

    private static Action<UnitTestDataConfig> ConfigureHeaderAuthentication(List<string> trustedProxies) =>
        config =>
        {
            var authenticationSettings = AuthenticationModule.Create();
            authenticationSettings.HeaderAuthentication = new HeaderAuthenticationSettings
            {
                Enabled = true,
                MappingType = HeaderMappingType.Username,
                TrustedProxies = trustedProxies,
                EnableLogging = false,
                MaxHeaderLength = 256,
                RequireHttps = false,
            };

            config.OverrideServices = builder =>
                builder.RegisterInstance(authenticationSettings).As<IAuthenticationSettings>().SingleInstance();
        };

    private static HttpClient CreateUnauthenticatedClient(BaseContainer container)
    {
        var server = (TestServer)container.Resolve<IServer>();
        return new HttpClient(server.CreateHandler(context => context.Connection.RemoteIpAddress = IPAddress.Loopback))
        {
            BaseAddress = server.BaseAddress,
        };
    }

    private static List<string> GetSetCookieHeaders(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];

    private static List<string> GetAuthenticationCookies(HttpResponseMessage response) =>
        GetSetCookieHeaders(response)
            .Where(x => x.StartsWith($"{DefaultUserAppCredentials.DefaultCookieName}=", StringComparison.Ordinal))
            .ToList();

    private static AuthenticationTicket UnprotectCookie(BaseContainer container, string setCookie)
    {
        var cookiePair = setCookie[..setCookie.IndexOf(';')];
        var cookieValue = cookiePair[(cookiePair.IndexOf('=') + 1)..];
        var options = container
            .Resolve<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        return options.TicketDataFormat.Unprotect(cookieValue).ShouldNotBeNull();
    }
}
