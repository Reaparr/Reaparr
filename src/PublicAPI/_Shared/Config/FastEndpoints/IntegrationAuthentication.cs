using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Reaparr.PublicAPI;

public static class IntegrationAuthentication
{
    private static readonly ILogger _log = LogFactory.Create(typeof(IntegrationAuthentication));

    public static async Task<IntegrationIdentity?> AuthenticateQueryKeyAsync(
        this HttpContext httpContext,
        IReaparrDbContext dbContext,
        CancellationToken ct
    )
    {
        var integrationId = GetRouteIntegrationId(httpContext);
        var apiKey = httpContext.Request.Query[IntegrationDefinitions.INDEXER_API_KEY].ToString();

        return await AuthenticateAsync(
            dbContext,
            httpContext,
            integrationId,
            apiKey,
            x => x.TorznabApiKey,
            x => x.TorznabApiKey,
            ct
        );
    }

    public static async Task<IntegrationIdentity?> AuthenticateBearerAsync(
        this HttpContext httpContext,
        IReaparrDbContext dbContext,
        CancellationToken ct
    )
    {
        var integrationId = GetRouteIntegrationId(httpContext);
        var apiKey =
            AuthenticationHeaderValue.TryParse(httpContext.Request.Headers.Authorization, out var authorization)
            && string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                ? authorization.Parameter ?? string.Empty
                : string.Empty;

        return await AuthenticateAsync(
            dbContext,
            httpContext,
            integrationId,
            apiKey,
            x => x.QBittorrentApiKey,
            x => x.QBittorrentApiKey,
            ct
        );
    }

    public static IntegrationIdentity GetIntegrationIdentity(this HttpContext httpContext) =>
        httpContext.Items.TryGetValue(IntegrationDefinitions.IntegrationIdentityItemKey, out var identity)
        && identity is IntegrationIdentity integrationIdentity
            ? integrationIdentity
            : throw new InvalidOperationException("The request has not been authenticated for an integration.");

    public static async Task<IntegrationSettings> GetIntegrationSettings(
        this HttpContext httpContext,
        IReaparrDbContext dbContext,
        IntegrationIdentity identity,
        CancellationToken ct
    )
    {
        _log.Here()
            .Debug(
                "Loading settings for {IntegrationType} integration {IntegrationId} for request to '{RequestPath}'",
                identity.Type,
                identity.Id,
                httpContext.Request.Path
            );

        return identity.Type == IntegrationType.Sonarr
            ? await dbContext
                .SonarrIntegrations.AsNoTracking()
                .Where(x => x.Id == identity.Id)
                .Select(x => new IntegrationSettings(x.Category))
                .SingleAsync(ct)
            : await dbContext
                .RadarrIntegrations.AsNoTracking()
                .Where(x => x.Id == identity.Id)
                .Select(x => new IntegrationSettings(x.Category))
                .SingleAsync(ct);
    }

    private static Guid? GetRouteIntegrationId(HttpContext httpContext) =>
        Guid.TryParse(httpContext.Request.RouteValues["integrationId"]?.ToString(), out var integrationId)
            ? integrationId
            : null;

    private static async Task<IntegrationIdentity?> AuthenticateAsync(
        IReaparrDbContext dbContext,
        HttpContext httpContext,
        Guid? integrationId,
        string apiKey,
        Expression<Func<SonarrIntegration, string>> sonarrApiKeySelector,
        Expression<Func<RadarrIntegration, string>> radarrApiKeySelector,
        CancellationToken ct
    )
    {
        if (integrationId is null)
        {
            _log.Here()
                .Warning(
                    "Integration authentication rejected because route integration ID is missing or invalid for request to '{RequestPath}' from {UserAgent}",
                    httpContext.Request.Path,
                    httpContext.Request.Headers.UserAgent.ToString()
                );
            return null;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.Here()
                .Warning(
                    "Integration authentication rejected because the credential is missing for integration {IntegrationId} and request to '{RequestPath}' from {UserAgent}",
                    integrationId,
                    httpContext.Request.Path,
                    httpContext.Request.Headers.UserAgent.ToString()
                );
            return null;
        }

        var sonarrApiKey = await dbContext
            .SonarrIntegrations.Where(x => x.Id == integrationId.Value)
            .Select(sonarrApiKeySelector)
            .SingleOrDefaultAsync(ct);
        if (sonarrApiKey is not null && FixedTimeEquals(sonarrApiKey, apiKey))
        {
            var identity = integrationId.Value.ToSonarrIdentity();
            httpContext.Items[IntegrationDefinitions.IntegrationIdentityItemKey] = identity;
            _log.Here()
                .Debug(
                    "Authenticated {IntegrationType} integration {IntegrationId} for request to '{RequestPath}'",
                    identity.Type,
                    identity.Id,
                    httpContext.Request.Path
                );
            return identity;
        }

        var radarrApiKey = await dbContext
            .RadarrIntegrations.Where(x => x.Id == integrationId.Value)
            .Select(radarrApiKeySelector)
            .SingleOrDefaultAsync(ct);
        if (radarrApiKey is not null && FixedTimeEquals(radarrApiKey, apiKey))
        {
            var identity = integrationId.Value.ToRadarrIdentity();
            httpContext.Items[IntegrationDefinitions.IntegrationIdentityItemKey] = identity;
            _log.Here()
                .Debug(
                    "Authenticated {IntegrationType} integration {IntegrationId} for request to '{RequestPath}'",
                    identity.Type,
                    identity.Id,
                    httpContext.Request.Path
                );
            return identity;
        }

        _log.Here()
            .Warning(
                "Integration authentication rejected because the credential did not match integration {IntegrationId} for request to '{RequestPath}' from {UserAgent}",
                integrationId,
                httpContext.Request.Path,
                httpContext.Request.Headers.UserAgent.ToString()
            );
        return null;
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);

        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

public readonly record struct IntegrationSettings(string Category);
