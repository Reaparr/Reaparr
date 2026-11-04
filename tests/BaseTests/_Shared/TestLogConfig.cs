using Serilog.Core;

namespace Reaparr.BaseTests;

public class TestLogConfig : LogConfig
{
    public TestLogConfig(IAppRuntimeInfo appRuntimeInfo, IPathProvider pathProvider, IAppBuildInfo appBuildInfo)
        : base(appRuntimeInfo, pathProvider, appBuildInfo) { }

    /// <inheritdoc/>
    protected override bool UseInteractiveSinks => true;

    public override Logger GetLogger(LogEventLevel minimumLogLevel = LogEventLevel.Debug) =>
        GetBaseConfiguration(minimumLogLevel).WriteTo.TestCorrelator(minimumLogLevel).CreateLogger();
}
