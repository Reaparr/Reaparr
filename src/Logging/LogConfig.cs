using Serilog.Enrichers.Sensitive;
using Serilog.Sinks.OpenTelemetry;

namespace Reaparr.Logging;

public class LogConfig : SlimLogConfig
{
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    private readonly IPathProvider _pathProvider;
    private readonly IAppBuildInfo _appBuildInfo;

    protected LogConfig(IAppRuntimeInfo appRuntimeInfo, IPathProvider pathProvider, IAppBuildInfo appBuildInfo)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        _appRuntimeInfo = appRuntimeInfo;
        _pathProvider = pathProvider;
        _appBuildInfo = appBuildInfo;
    }

    /// <summary>
    /// Provides a base configuration with console and debug sinks, and allows for extension by derived classes (e.g. to add file or Seq sinks).
    /// </summary>
    /// <param name="minimumLogLevel">The global minimum log level used before sink-specific filtering.</param>
    /// <returns></returns>
    protected override LoggerConfiguration GetBaseConfiguration(LogEventLevel minimumLogLevel = LogEventLevel.Debug)
    {
        var config = base.GetBaseConfiguration(minimumLogLevel);

        // Do not mask data when debugging
        if (!_appRuntimeInfo.IsUnmasked)
        {
            config.Enrich.WithSensitiveDataMasking(options =>
            {
                options.MaskingOperators.Clear();
                options.MaskingOperators = [new EmailAddressMaskingOperator(), new UrlMaskingOperator()];
                var sensitiveProperties = new[]
                {
                    "PlexLibraryTitle",
                    "PlexAccountDisplayName",
                    "PlexLibraryName",
                    "PlexServerName",
                    "UserName",
                    "PublicAddress",
                    "PlexServerConnectionUrl",
                    "PlexServerConnection",
                    "PlexServerStatus",
                    "DownloadUrl",
                    "AuthToken",
                    "AccessToken",
                    "RefreshToken",
                    "PlexAuthToken",
                    "ManualAuthenticationToken",
                    "AuthenticationToken",
                    "CustomAuthenticationToken",
                    "Token",
                    "Password",
                    "ApiKey",
                    "ArrApiKey",
                    "QBittorrentApiKey",
                    "TorznabApiKey",
                    "VerificationCode",
                    "Authorization",
                    "SID",
                    "MachineIdentifier",
                };

                foreach (var property in sensitiveProperties)
                {
                    options.MaskProperties.Add(MaskProperty.WithDefaults(property));
                }
            });
        }

        return config;
    }

    /// <summary>
    /// Provides an extended sink configuration with file, Seq, and telemetry sinks.
    /// </summary>
    /// <param name="minimumLogLevel">The global minimum log level used before sink-specific filtering.</param>
    protected virtual LoggerConfiguration GetExtendedConfiguration(LogEventLevel minimumLogLevel = LogEventLevel.Debug)
    {
        var config = GetBaseConfiguration(minimumLogLevel).Enrich.WithProperty("AppRunId", _appRuntimeInfo.AppRunId);

        if (_appBuildInfo.HasValidTelemetry)
        {
            config.WriteTo.OpenTelemetry(
                options =>
                {
                    options.Endpoint = null;
                    options.LogsEndpoint = $"{_appBuildInfo.TelemetryHost.TrimEnd('/')}/i/v1/logs";
                    options.TracesEndpoint = null;
                    options.Protocol = OtlpProtocol.HttpProtobuf;
                    options.Headers["Authorization"] = $"Bearer {_appBuildInfo.TelemetryProjectToken}";
                    options.IncludedData = IncludedData.MessageTemplateTextAttribute;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = "reaparr",
                        ["service.version"] = _appBuildInfo.InformationalVersion,
                        ["environment"] = _appBuildInfo.IsDevRelease ? "dev" : "stable",
                        ["runtime_mode"] = _appBuildInfo.RuntimeMode,
                        ["platform"] = _appBuildInfo.CurrentOS.ToString(),
                        ["runtime_identifier"] = _appBuildInfo.RuntimeIdentifier,
                    };
                },
                ignoreEnvironment: true
            );
        }

        if (!string.IsNullOrEmpty(_appRuntimeInfo.SEQ_Url))
            config = config.WriteTo.Seq(
                _appRuntimeInfo.SEQ_Url,
                apiKey: !string.IsNullOrEmpty(_appRuntimeInfo.SEQ_ApiKey) ? _appRuntimeInfo.SEQ_ApiKey : null,
                restrictedToMinimumLevel: minimumLogLevel
            );

        return config.WriteTo.File(
            FileTemplate, // This should always be plain file as not to log ASCII characters
            Path.Combine(_pathProvider.LogsDirectory, "log.txt"),
            restrictedToMinimumLevel: minimumLogLevel,
            rollingInterval: RollingInterval.Day,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: 7
        );
    }

    public override Logger GetLogger(LogEventLevel minimumLogLevel = LogEventLevel.Debug) =>
        GetExtendedConfiguration(minimumLogLevel).CreateLogger();
}
