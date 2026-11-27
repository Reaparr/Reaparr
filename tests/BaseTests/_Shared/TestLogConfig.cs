using Serilog.Core;

namespace Reaparr.BaseTests;

public class TestLogConfig : LogConfig
{
    public TestLogConfig(IAppRuntimeInfo appRuntimeInfo, IPathProvider pathProvider, IAppBuildInfo appBuildInfo)
        : base(appRuntimeInfo, pathProvider, appBuildInfo) { }

    /// <inheritdoc/>
    protected override bool UseInteractiveSinks => false;

    public override Logger GetLogger(LogEventLevel minimumLogLevel = LogEventLevel.Debug)
    {
        // LOG_LEVEL can restore detailed console output without affecting TestCorrelator assertions.
        var consoleLevel =
            Enum.TryParse<LogEventLevel>(
                System.Environment.GetEnvironmentVariable(EnvKeys.LogLevel),
                ignoreCase: true,
                out var configuredLevel
            ) && Enum.IsDefined(configuredLevel)
                ? configuredLevel
                : LogEventLevel.Warning;
        var template = Console.IsOutputRedirected ? FileTemplate : ConsoleTemplate;
        return GetBaseConfiguration(minimumLogLevel)
            .WriteTo.Console(template, restrictedToMinimumLevel: consoleLevel)
            .WriteTo.TestCorrelator(minimumLogLevel)
            .CreateLogger();
    }
}
