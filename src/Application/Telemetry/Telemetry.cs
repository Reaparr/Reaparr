using PostHog;

namespace Reaparr.Application;

public sealed class Telemetry : ITelemetry
{
    private readonly IPostHogClient _client;
    private readonly IAppBuildInfo _appBuildInfo;
    private readonly IUserService _userService;

    public Telemetry(IPostHogClient client, IAppBuildInfo appBuildInfo, IUserService userService)
    {
        _client = client;
        _appBuildInfo = appBuildInfo;
        _userService = userService;
    }

    public async Task<Result> TrackAsync(string eventName, IReadOnlyDictionary<string, object?> parameters)
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return ResultExtensions.Create503ServiceUnavailableResult("Telemetry is unconfigured or disabled");

        parameters.TryGetValue("origin", out var origin);
        if (
            (eventName, parameters.Count, origin)
            is not
            ("reaparr diagnostic", 1, TelemetryDiagnosticOrigin.Backend or TelemetryDiagnosticOrigin.Frontend)
        )
            return ResultExtensions.Create400BadRequestResult("Invalid telemetry event");

        var telemetryId = await _userService.GetTelemetryId();

        var properties = new Dictionary<string, object>
        {
            ["$process_person_profile"] = false,
            ["$geoip_disable"] = true,
            ["origin"] = origin is TelemetryDiagnosticOrigin.Backend ? "backend" : "frontend",
            ["environment"] = _appBuildInfo.IsDevRelease ? "dev" : "stable",
            ["release"] = _appBuildInfo.InformationalVersion,
            ["runtime_mode"] = _appBuildInfo.RuntimeMode,
            ["platform"] = _appBuildInfo.CurrentOS.ToString(),
            ["runtime_identifier"] = _appBuildInfo.RuntimeIdentifier,
        };
        // SDK capture acknowledges queue admission, not ingestion or delivery.
        var capture = Result.Try(() => _client.Capture(telemetryId, eventName, properties, groups: null, flags: null));
        return capture.IsSuccess && capture.Value
            ? Result.Ok().WithSuccess("Diagnostic event accepted for sending")
            : ResultExtensions.Create503ServiceUnavailableResult("Telemetry could not accept the event");
    }
}
