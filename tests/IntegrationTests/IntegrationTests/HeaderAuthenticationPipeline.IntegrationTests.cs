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

        return options.TicketDataFormat!.Unprotect(cookieValue).ShouldNotBeNull();
    }
}
