using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PostHog;
using PostHog.Config;

namespace Reaparr.AppHost;

public static partial class Startup
{
    internal static void ConfigureTelemetry(
        this IServiceCollection services,
        IAppBuildInfo appBuildInfo,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        services.AddOptions<PostHogOptions>().Configure(options =>
        {
            options.Disabled = !appBuildInfo.HasValidTelemetry;
            if (!appBuildInfo.HasValidTelemetry)
                return;

            options.HostUrl = new Uri(appBuildInfo.TelemetryHost, UriKind.Absolute);
            options.ProjectToken = appBuildInfo.TelemetryProjectToken;
        });
        services.AddPostHog();

        if (!appBuildInfo.HasValidTelemetry)
            return;

        var ingestionHost = new Uri(appBuildInfo.TelemetryHost, UriKind.Absolute);
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(TelemetryProtocol.Service, serviceVersion: appBuildInfo.InformationalVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    [TelemetryProtocol.ReleaseChannel] = appBuildInfo.IsDevRelease ? TelemetryProtocol.DevelopmentEnvironment : TelemetryProtocol.StableEnvironment,
                    [TelemetryProtocol.OperatingSystem] = appBuildInfo.CurrentOS.ToString(),
                    [TelemetryProtocol.RuntimePlatform] = appBuildInfo.RuntimeMode,
                    [TelemetryProtocol.RuntimeEnvironment] = appRuntimeInfo.DotnetEnvironment,
                    [TelemetryProtocol.RuntimeIdentifier] = appBuildInfo.RuntimeIdentifier,
                    [TelemetryProtocol.ApplicationId] = appRuntimeInfo.AppRunId,
                }))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
                .AddSource(TelemetryProtocol.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options => options.RecordException = false)
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = false;
                    options.FilterHttpRequestMessage = request => request.RequestUri is not { } target
                        || target.Scheme != ingestionHost.Scheme
                        || target.Host != ingestionHost.Host
                        || target.Port != ingestionHost.Port;
                })
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri($"{appBuildInfo.TelemetryHost.TrimEnd('/')}{TelemetryProtocol.TracesPath}");
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"{TelemetryProtocol.AuthorizationHeader}={TelemetryProtocol.BearerScheme} {appBuildInfo.TelemetryProjectToken}";
                }));
    }
}
