using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Reaparr.Settings.Contracts;

namespace Reaparr.AppHost;

/// <summary>
///  Extension methods for INetworkSettings to build ForwardedHeadersOptions for ASP.NET Core middleware.
/// </summary>
public static class NetworkSettingsExtensions
{
    private static readonly Serilog.ILogger _log = LogFactory.Create(typeof(NetworkSettingsExtensions));

    /// <summary>
    /// Applies forwarded headers middleware based on network settings.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void ApplyForwardedHeaders(this WebApplication app)
    {
        // TLS is terminated upstream; without forwarded headers, Request.Scheme stays http and base URLs are wrong.
        var networkSettings = app.Services.GetRequiredService<INetworkSettings>();

        // Warn in production if forwarded headers are enabled without an allowlist.
        if (networkSettings is { TrustProxyHeaders: true, AllowedProxyIps.Count: 0 } && app.Environment.IsProduction())
        {
            _log.Here().Warning("Production environment with TrustProxyHeaders enabled but no AllowedProxyIps defined");
        }

        // Build forwarding options from settings and register early in the pipeline.
        var forwardedHeadersOptions = networkSettings.BuildForwardedHeadersOptions();
        if (forwardedHeadersOptions.ForwardedHeaders != ForwardedHeaders.None)
            app.UseForwardedHeaders(forwardedHeadersOptions);
    }

    /// <summary>
    /// Builds forwarded headers options from the network settings.
    /// </summary>
    /// <param name="networkSettings">Network settings for proxy configuration.</param>
    /// <returns>The configured forwarded headers options.</returns>
    public static ForwardedHeadersOptions BuildForwardedHeadersOptions(this INetworkSettings networkSettings)
    {
        // Start with safe defaults; keep forwarding disabled unless explicitly enabled.
        var options = new ForwardedHeadersOptions();
        if (!networkSettings.TrustProxyHeaders)
            return options;

        // Enable forwarded headers used by proxies to report client connection data.
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

        // Limit to a single proxy hop to reduce header spoofing risk.
        options.ForwardLimit = 1;

        if (!string.IsNullOrWhiteSpace(networkSettings.ForwardedHostHeader))
            options.ForwardedHostHeaderName = networkSettings.ForwardedHostHeader;

        if (!string.IsNullOrWhiteSpace(networkSettings.ForwardedPathHeader))
            options.ForwardedPrefixHeaderName = networkSettings.ForwardedPathHeader;

        // Only clear defaults when an explicit allowlist is provided.
        if (networkSettings.AllowedProxyIps.Count == 0)
            return options;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        // Populate allowed proxies/networks from configuration.
        foreach (var entry in networkSettings.AllowedProxyIps)
        {
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var trimmedEntry = entry.Trim();
            if (trimmedEntry.Contains('/'))
            {
                try
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(trimmedEntry));
                }
                catch (Exception ex)
                {
                    _log.Here().Warning(ex, "Ignoring invalid proxy network entry: {Entry}", trimmedEntry);
                }

                continue;
            }

            if (!IPAddress.TryParse(trimmedEntry, out var ipAddress))
            {
                _log.Here().Warning("Ignoring invalid proxy IP entry: {Entry}", trimmedEntry);
                continue;
            }

            options.KnownProxies.Add(ipAddress);
        }

        if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            _log.Here()
                .Error("Forwarded headers disabled because AllowedProxyIps contains no valid IP or network entries");
        }

        return options;
    }
}
