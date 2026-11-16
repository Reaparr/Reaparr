using System.Net.Http.Headers;
using System.Security.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Application;

public static class HttpClientModule
{
    public static readonly string DefaultClientName = string.Empty;
    public static readonly string PlexThumbnailClientName = "PlexThumbnail";
    internal static readonly string GitHubClientName = "GitHub";

    public static void RegisterDefaultHttpClient(this IServiceCollection services)
    {
        services.AddTransient<DefaultHttpClientRetryHandler>();

        services
            .AddHttpClient(DefaultClientName)
            .AddHttpMessageHandler<DefaultHttpClientRetryHandler>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.None,
                    },
                }
            );
    }

    public static void RegisterPlexThumbnailHttpClient(this IServiceCollection services)
    {
        services
            .AddHttpClient(PlexThumbnailClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(45);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
            })
            .AddHttpMessageHandler<DefaultHttpClientRetryHandler>()
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 10,
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    },

                    // Increase connect timeout to handle slow connections
                    ConnectTimeout = TimeSpan.FromSeconds(15),
                }
            );
    }

    public static void RegisterGitHubHttpClient(this IServiceCollection services, IAppRuntimeInfo appRuntimeInfo)
    {
        services
            .AddHttpClient(GitHubClientName, client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
                );
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Reaparr");

                if (!string.IsNullOrWhiteSpace(appRuntimeInfo.GitHubToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Bearer",
                        appRuntimeInfo.GitHubToken
                    );
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    }

    public static HttpClient CreateGitHubHttpClient(this IHttpClientFactory factory) =>
        factory.CreateClient(GitHubClientName);
}
