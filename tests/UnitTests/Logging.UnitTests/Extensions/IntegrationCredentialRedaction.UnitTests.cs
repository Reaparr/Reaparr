using Autofac;
using Microsoft.AspNetCore.Http;
using Reaparr.Application;
using Reaparr.Environment;
using Serilog.Sinks.TestCorrelator;

namespace Reaparr.Logging.UnitTests;

public class IntegrationCredentialRedactionUnitTests : BaseUnitTest
{
    [Test]
    public void ShouldMaskIntegrationCredentialPropertiesHeadersAndUrlQueryValues()
    {
        // Arrange
        try
        {
            SetAppRuntimeInfo(x => x.IsUnmasked = false);
            var runtimeInfo = Mock.Container.Resolve<IAppRuntimeInfo>();
            var pathProvider = Mock.Container.Resolve<IPathProvider>();
            LogFactory.SetupLogging(new TestLogConfig(runtimeInfo, pathProvider), runtimeInfo);
            var log = LogFactory.Create<IntegrationCredentialRedactionUnitTests>();
            var secrets = new[]
            {
                "arr-secret",
                "qbittorrent-secret",
                "torznab-secret",
                "bearer-secret",
                "query-secret",
            };

            using var context = TestCorrelator.CreateContext();

            // Act
            log.Information(
                "{ArrApiKey} {QBittorrentApiKey} {TorznabApiKey} {Authorization}",
                secrets[0],
                secrets[1],
                secrets[2],
                secrets[3]
            );
            log.Information("{Url}", $"https://example.test/api?apikey={secrets[4]}&password={secrets[0]}");

            // Assert
            var messages = TestCorrelator
                .GetLogEventsFromContextId(context.Id)
                .Where(x =>
                    x.MessageTemplate.Text
                        is "{ArrApiKey} {QBittorrentApiKey} {TorznabApiKey} {Authorization}"
                            or "{Url}"
                )
                .Select(x => x.RenderMessage())
                .ToList();
            messages.Count.ShouldBe(2);
            messages.ShouldAllBe(x => x.Contains("***MASKED***"));
            foreach (var secret in secrets)
                messages.ShouldAllBe(x => !x.Contains(secret, StringComparison.Ordinal));
        }
        finally
        {
            LogFactory.CloseAndFlush();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void ShouldRespectMaskingModeForPlexTokenFields_WhenLoggingNestedAccountRequests(bool unmasked)
    {
        // Arrange
        SetAppRuntimeInfo(x => x.IsUnmasked = unmasked);
        var runtimeInfo = Mock.Container.Resolve<IAppRuntimeInfo>();
        var pathProvider = Mock.Container.Resolve<IPathProvider>();
        using var log = new TestLogConfig(runtimeInfo, pathProvider).GetLogger();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost", 5000);
        httpContext.Request.Path = "/api/PlexAccount";
        var request = new
        {
            Validation = new ValidatePlexTokenEndpointRequest
            {
                PlexAccountId = 0,
                DisplayName = "Visible-account-label",
                ManualAuthenticationToken = "manual-plex-secret-41",
            },
            Account = new CreatePlexAccountEndpointRequest
            {
                DisplayName = "Visible-account-label",
                Username = "account-user",
                Password = "Password-secret-41!",
                IsEnabled = true,
                IsMain = true,
                IsValidated = true,
                ValidatedAt = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc),
                Uuid = "account-uuid",
                PlexId = 41,
                Email = "account@example.test",
                Title = "Account",
                ClientId = "account-client",
                Is2Fa = false,
                CustomAuthenticationToken = "custom-plex-secret-41",
                AuthenticationToken = "automatic-plex-secret-41",
            },
        };
        var secrets = new[]
        {
            request.Validation.ManualAuthenticationToken,
            request.Account.AuthenticationToken,
            request.Account.CustomAuthenticationToken,
            request.Account.Password,
        };
        using var context = TestCorrelator.CreateContext();

        // Act
        log.Here().DebugApiCall(httpContext, request);

        // Assert
        var logEvent = TestCorrelator.GetLogEventsFromContextId(context.Id).Single();
        var message = logEvent.RenderMessage();
        message.ShouldContain("POST");
        message.ShouldContain("/api/PlexAccount");
        message.ShouldContain("Visible-account-label");
        foreach (var secret in secrets)
        {
            if (unmasked)
                message.ShouldContain(secret);
            else
                message.ShouldNotContain(secret);
        }

        if (!unmasked)
            message.ShouldContain("***MASKED***");
    }
}
