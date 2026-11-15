namespace Reaparr.PublicAPI.UnitTests.Config;

public class ScopedIntegrationAuthenticationUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldResolveIntegration_WhenRouteAndQueryKeyMatch()
    {
        // Arrange
        await SetupDatabase(626201, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var httpContext = CreateHttpContext(integration.Id);
        httpContext.Request.QueryString = new QueryString($"?apikey={integration.TorznabApiKey}");

        // Act
        var result = await httpContext.AuthenticateQueryKeyAsync(IDbContext, CancellationToken);

        // Assert
        result.ShouldBe(integration.Id.ToRadarrIdentity());
        httpContext.GetIntegrationIdentity().ShouldBe(integration.Id.ToRadarrIdentity());
    }

    [Test]
    public async Task ShouldRejectDifferentQueryKeyCasing()
    {
        // Arrange
        await SetupDatabase(626205, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var httpContext = CreateHttpContext(integration.Id);
        httpContext.Request.QueryString = new QueryString($"?ApiKey={integration.QBittorrentApiKey}");

        // Act
        var result = await httpContext.AuthenticateQueryKeyAsync(IDbContext, CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Test]
    public async Task ShouldRejectQueryKey_WhenItBelongsToAnotherRouteIntegration()
    {
        // Arrange
        await SetupDatabase(626202, config => config.RadarrIntegrationCount = 2);
        var integrations = await IDbContext.RadarrIntegrations.OrderBy(x => x.Id).ToListAsync(CancellationToken);
        var routeIntegration = integrations[0];
        var otherIntegration = integrations[1];
        var httpContext = CreateHttpContext(routeIntegration.Id);
        httpContext.Request.QueryString = new QueryString($"?apikey={otherIntegration.TorznabApiKey}");

        // Act
        var result = await httpContext.AuthenticateQueryKeyAsync(IDbContext, CancellationToken);

        // Assert
        result.ShouldBeNull();
        httpContext.Items.ContainsKey(IntegrationDefinitions.IntegrationIdentityItemKey).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldResolveIntegration_WhenRouteAndBearerKeyMatch()
    {
        // Arrange
        await SetupDatabase(626203, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var httpContext = CreateHttpContext(integration.Id);
        httpContext.Request.Headers.Authorization = $"Bearer {integration.QBittorrentApiKey}";

        // Act
        var result = await httpContext.AuthenticateBearerAsync(IDbContext, CancellationToken);

        // Assert
        result.ShouldBe(integration.Id.ToRadarrIdentity());
        httpContext.GetIntegrationIdentity().ShouldBe(integration.Id.ToRadarrIdentity());
    }

    [Test]
    public async Task ShouldRejectNonBearerAuthenticationAndSidCookies()
    {
        // Arrange
        await SetupDatabase(626204, config => config.RadarrIntegrationCount = 1);
        var integration = await IDbContext.RadarrIntegrations.SingleAsync(CancellationToken);
        var httpContext = CreateHttpContext(integration.Id);
        httpContext.Request.Headers.Authorization = $"Basic {integration.QBittorrentApiKey}";
        httpContext.Request.Headers.Cookie = $"SID={integration.QBittorrentApiKey}";

        // Act
        var result = await httpContext.AuthenticateBearerAsync(IDbContext, CancellationToken);

        // Assert
        result.ShouldBeNull();
        httpContext.Items.ContainsKey(IntegrationDefinitions.IntegrationIdentityItemKey).ShouldBeFalse();
    }

    private static DefaultHttpContext CreateHttpContext(Guid integrationId)
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["integrationId"] = integrationId.ToString();
        return context;
    }
}
