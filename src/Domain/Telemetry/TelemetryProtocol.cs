namespace Reaparr.Domain;

/// <summary>
/// Defines PostHog and OpenTelemetry protocol constants and request/job correlation keys. Used by
/// telemetry capture, context propagation, and log/trace export to keep their wire formats consistent.
/// </summary>
public static class TelemetryProtocol
{
    public const string ActivitySourceName = "Reaparr";
    public const string SessionIdHeader = "X-POSTHOG-SESSION-ID";
    public const string SessionIdTag = "sessionId";
    public const string DistinctIdTag = "posthogDistinctId";
    public const string MetadataPrefix = "telemetry.";
    public const string TraceParentMetadata = MetadataPrefix + "traceparent";
    public const string TraceStateMetadata = MetadataPrefix + "tracestate";
    public const string SessionIdMetadata = MetadataPrefix + SessionIdTag;
    public const string DiagnosticEvent = "reaparr diagnostic";
    public const string ProfileEvent = "$set";
    public const string ExceptionEvent = "$exception";
    public const string PersonProperties = "$set";
    public const string ProcessPersonProfile = "$process_person_profile";
    public const string TraceId = "$trace_id";
    public const string SpanId = "$span_id";
    public const string SessionId = "$session_id";
    public const string DistinctId = "distinct_id";
    public const string BackendOrigin = "backend";
    public const string FrontendOrigin = "frontend";
    public const string DevelopmentEnvironment = "dev";
    public const string StableEnvironment = "stable";
    public const string EuIngestionHost = "eu.i.posthog.com";
    public const string EuAssetHost = "eu-assets.i.posthog.com";
    public const string UsIngestionHost = "us.i.posthog.com";
    public const string UsAssetHost = "us-assets.i.posthog.com";
    public const string ProxyPath = "/e";
    public const string LogsPath = "/i/v1/logs";
    public const string TracesPath = "/i/v1/traces";
    public const string AuthorizationHeader = "Authorization";
    public const string BearerScheme = "Bearer";
    public const string ServiceName = "service.name";
    public const string ReleaseChannel = "release_channel";
    public const string OperatingSystem = "operating_system";
    public const string RuntimePlatform = "runtime_platform";
    public const string RuntimeEnvironment = "runtime_environment";
    public const string RuntimeIdentifier = "runtime_identifier";
    public const string ApplicationId = "application_id";
    public const string Service = "reaparr";
}
