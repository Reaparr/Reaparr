using System.Reactive.Linq;
using Microsoft.Extensions.DependencyInjection;
using PostHog;
using Serilog.Context;
using Serilog.Core.Enrichers;

namespace Reaparr.Application;

public sealed partial class Telemetry : ITelemetry, IDisposable
{
    private static readonly Dictionary<string, object> EmptyProperties = [];
    private static readonly JsonSerializerOptions SettingsJsonOptions = new(DefaultJsonSerializerOptions.ConfigStandard)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly IPostHogClient _client;
    private readonly IAppBuildInfo _appBuildInfo;
    private readonly IUserService _userService;
    private readonly ActivitySource _activitySource;
    private readonly IUserSettings _settings;
    private string _telemetryId = string.Empty;
    private IDisposable? _settingsSubscription;

    /// <summary>
    /// Creates the native telemetry facade; identity loading and settings publication wait until initialization.
    /// </summary>
    public Telemetry(
        IPostHogClient client,
        IAppBuildInfo appBuildInfo,
        IUserService userService,
        ActivitySource activitySource,
        IUserSettings settings
    )
    {
        _client = client;
        _appBuildInfo = appBuildInfo;
        _userService = userService;
        _activitySource = activitySource;
        _settings = settings;
    }

    /// <summary>
    /// Loads the existing account identity after boot readiness, publishes current settings, and observes later changes.
    /// Unconfigured builds do no identity or SDK work.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return;

        var initializationResult = await Result.Try(async Task () =>
        {
            // Boot calls this after the existing application account has been created.
            _telemetryId = await _userService.GetTelemetryId();
            PublishSettings(changed: false);
            _settingsSubscription = _settings.SettingsUpdated.Subscribe(_ => PublishSettings(changed: true));
        });
        initializationResult.LogIfFailed();
    }

    /// <summary>
    /// Returns the browser bootstrap when telemetry is configured and startup identity loading succeeded.
    /// </summary>
    public BrowserTelemetryBootstrapDTO? GetBrowserBootstrap()
    {
        if (!_appBuildInfo.HasValidTelemetry || string.IsNullOrEmpty(_telemetryId))
            return null;

        return new BrowserTelemetryBootstrapDTO
        {
            ApiHost = _appBuildInfo.TelemetryHost,
            ProjectToken = _appBuildInfo.TelemetryProjectToken,
            DistinctId = _telemetryId,
            Release = _appBuildInfo.InformationalVersion,
            Environment = _appBuildInfo.IsDevRelease
                ? TelemetryProtocol.DevelopmentEnvironment
                : TelemetryProtocol.StableEnvironment,
            RuntimeMode = _appBuildInfo.RuntimeMode,
            RuntimeIdentifier = _appBuildInfo.RuntimeIdentifier,
            Platform = _appBuildInfo.CurrentOS.ToString(),
        };
    }

    /// <summary>
    /// Submits a diagnostic with its backend or frontend origin and reports SDK admission, not remote delivery.
    /// </summary>
    public Result SendDiagnostic(TelemetryDiagnosticOrigin origin)
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return ResultExtensions.Create503ServiceUnavailableResult("Telemetry is unconfigured or disabled");

        return Capture(
            TelemetryEvent.ReaparrDiagnostic,
            new Dictionary<string, object>
            {
                [TelemetryProperty.Origin] =
                    origin == TelemetryDiagnosticOrigin.Frontend
                        ? TelemetryProtocol.FrontendOrigin
                        : TelemetryProtocol.BackendOrigin,
            }
        )
            ? Result.Ok().WithSuccess("Diagnostic event accepted for sending")
            : ResultExtensions.Create503ServiceUnavailableResult("Telemetry could not accept the event");
    }

    /// <summary>
    /// Sends the original exception through native error tracking with the operation and current trace/session context.
    /// </summary>
    public void CaptureException(Exception exception, TelemetryActivity operation) =>
        Capture(
            TelemetryProtocol.ExceptionEvent,
            new Dictionary<string, object> { [TelemetryProperty.Stage] = operation.ToString() },
            exception: exception
        );

    /// <summary>
    /// Marks failed operation spans and captures exceptional errors, including nested causes, unless the result is cancelled.
    /// Expected domain failures do not become error-tracking exceptions.
    /// </summary>
    public void CaptureErrors(ResultBase result, TelemetryActivity operation)
    {
        var cancelled = result.IsCancelled;
        if (result.IsFailed)
            Activity.Current?.SetStatus(cancelled ? ActivityStatusCode.Unset : ActivityStatusCode.Error);

        if (!cancelled && result.HasError<ExceptionalError>(out var errors))
            foreach (var error in errors)
                CaptureException(error.Exception, operation);
    }

    /// <summary>
    /// Captures an unexpected request exception with the HTTP method and matched route supplied by middleware.
    /// </summary>
    public void HttpRequestFailed(Exception exception, string method, string? route) =>
        Capture(
            TelemetryProtocol.ExceptionEvent,
            new Dictionary<string, object>
            {
                [TelemetryProperty.HttpMethod] = method,
                [TelemetryProperty.HttpRoute] = route!,
            },
            exception: exception
        );

    /// <summary>
    /// Enriches the current request activity and scoped logs with the cached identity and optional browser session.
    /// </summary>
    /// <returns>The log-context scope to dispose in the request owner, or null for an unconfigured build.</returns>
    public IDisposable? BeginRequest(string? sessionId)
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return null;

        Activity.Current?.SetTag(TelemetryProtocol.DistinctIdTag, _telemetryId);
        Activity.Current?.SetTag(TelemetryProtocol.SessionIdTag, sessionId);
        return LogContext.Push(
            new PropertyEnricher(TelemetryProtocol.DistinctIdTag, _telemetryId),
            new PropertyEnricher(TelemetryProtocol.SessionIdTag, sessionId)
        );
    }

    /// <summary>
    /// Starts a consumer span from serialized job context and enriches logs with the session and Quartz execution identity.
    /// Unparented jobs are isolated from the scheduler's ambient activity.
    /// </summary>
    /// <returns>A scope restoring the previous activity and log context on disposal, or null for an unconfigured build.</returns>
    public IDisposable? BeginJob(
        TelemetryActivity operation,
        (ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId) context
    )
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return null;

        // An unparented worker must not inherit the scheduler's ambient activity.
        var previous = Activity.Current;
        Activity.Current = null;
        var activity = StartActivityCore(
            operation,
            context.Parent,
            new ActivityTagsCollection
            {
                [TelemetryProtocol.SessionIdTag] = context.SessionId,
                [TelemetryProperty.JobName] = context.JobKey,
                [TelemetryProperty.FireInstanceId] = context.FireInstanceId,
            },
            ActivityKind.Consumer
        );
        var logContext = LogContext.Push(
            new PropertyEnricher(TelemetryProtocol.DistinctIdTag, _telemetryId),
            new PropertyEnricher(TelemetryProtocol.SessionIdTag, context.SessionId),
            new PropertyEnricher(TelemetryProperty.JobName, context.JobKey),
            new PropertyEnricher(TelemetryProperty.FireInstanceId, context.FireInstanceId)
        );
        return new JobTelemetryScope(activity, previous, logContext);
    }

    /// <summary>
    /// Starts an internal operation span using native activity context without adding a product-analytics event.
    /// </summary>
    public Activity? StartActivity(TelemetryActivity operation, ActivityContext parentContext = default) =>
        StartActivityCore(operation, parentContext);

    /// <summary>
    /// Creates a native span with build eligibility, cached identity, and session inheritance from in-process parents.
    /// </summary>
    /// <returns>The sampled activity, or null when telemetry is unconfigured or no listener creates a span.</returns>
    private Activity? StartActivityCore(
        TelemetryActivity operation,
        ActivityContext parentContext = default,
        IEnumerable<KeyValuePair<string, object?>>? tags = null,
        ActivityKind kind = ActivityKind.Internal
    )
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return null;

        var activity = _activitySource.StartActivity(operation.ToString(), kind, parentContext, tags);
        activity?.SetTag(TelemetryProtocol.DistinctIdTag, _telemetryId);
        if (GetActivityTag(activity?.Parent, TelemetryProtocol.SessionIdTag) is { } sessionId)
            activity?.SetTag(TelemetryProtocol.SessionIdTag, sessionId);
        return activity;
    }

    /// <summary>
    /// Maps results to telemetry outcomes: cancellation takes precedence, exceptional failures differ from rejection,
    /// and successful operations can explicitly report no work. Shared Result helpers supply the underlying predicates.
    /// </summary>
    private static string Outcome(ResultBase result, bool noOp = false) =>
        (
            result.IsCancelled ? TelemetryOutcome.Cancelled
            : result.IsFailed
                ? result.HasError<ExceptionalError>() ? TelemetryOutcome.Failed
                    : TelemetryOutcome.Rejected
            : noOp ? TelemetryOutcome.NoOp
            : TelemetryOutcome.Succeeded
        ).ToString();

    /// <summary>
    /// Converts event identifiers to wire names, retaining the native diagnostic event name.
    /// </summary>
    private bool Capture(TelemetryEvent eventName, Dictionary<string, object> properties) =>
        Capture(
            eventName == TelemetryEvent.ReaparrDiagnostic ? TelemetryProtocol.DiagnosticEvent : eventName.ToString(),
            properties
        );

    /// <summary>
    /// Enriches a payload and submits analytics, person properties, or an original exception through the native SDK.
    /// SDK admission failures stay inside this boundary and do not affect business operations.
    /// </summary>
    /// <returns>Whether the SDK accepted the payload; false also covers unconfigured builds and admission exceptions.</returns>
    private bool Capture(
        string eventName,
        Dictionary<string, object> properties,
        Dictionary<string, object>? personProperties = null,
        Exception? exception = null
    )
    {
        if (!_appBuildInfo.HasValidTelemetry)
            return false;

        // Native SDK admission is best effort and must not change the business result.
        var captureResult = Result.Try(() =>
        {
            properties[TelemetryProtocol.ProcessPersonProfile] = true;
            properties[TelemetryProtocol.DisableGeoIp] = true;
            properties.TryAdd(TelemetryProperty.Origin, TelemetryProtocol.BackendOrigin);
            properties[TelemetryProperty.Environment] = _appBuildInfo.IsDevRelease
                ? TelemetryProtocol.DevelopmentEnvironment
                : TelemetryProtocol.StableEnvironment;
            properties[TelemetryProperty.Release] = _appBuildInfo.InformationalVersion;
            properties[TelemetryProperty.RuntimeMode] = _appBuildInfo.RuntimeMode;
            properties[TelemetryProperty.Platform] = _appBuildInfo.CurrentOS.ToString();
            properties[TelemetryProperty.RuntimeIdentifier] = _appBuildInfo.RuntimeIdentifier;
            if (Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity)
            {
                properties[TelemetryProtocol.TraceId] = activity.TraceId.ToHexString();
                properties[TelemetryProtocol.SpanId] = activity.SpanId.ToHexString();
                if (GetActivityTag(activity, TelemetryProtocol.SessionIdTag) is { } sessionId)
                    properties[TelemetryProtocol.SessionId] = sessionId;
            }

            if (exception is not null)
            {
                Activity.Current?.SetStatus(ActivityStatusCode.Error);
                return _client.CaptureException(exception, _telemetryId, properties, groups: null, flags: null);
            }

            return personProperties is null
                ? _client.Capture(_telemetryId, eventName, properties, groups: null, flags: null)
                : _client.Capture(
                    _telemetryId,
                    eventName,
                    properties,
                    personPropertiesToSet: personProperties,
                    personPropertiesToSetOnce: EmptyProperties
                );
        });
        return captureResult.IsSuccess && captureResult.Value;
    }

    /// <summary>
    /// Projects current settings into person properties and, after a settings signal, a change event.
    /// Serialization is contained so a publication failure does not terminate the settings observer.
    /// </summary>
    private void PublishSettings(bool changed)
    {
        // Serialization belongs here too: an observer failure must not terminate SettingsUpdated.
        var settingsResult = Result.Try(() =>
        {
            var settings = JsonSerializer.SerializeToElement(_settings.ToDTO(), SettingsJsonOptions);
            var properties = new Dictionary<string, object>();
            foreach (var module in settings.EnumerateObject())
            foreach (var property in module.Value.EnumerateObject())
                properties[$"{TelemetryProfileProperty.SettingsPrefix}{module.Name}.{property.Name}"] = property.Value;

            var header = _settings.AuthenticationSettings.HeaderAuthentication;
            properties[TelemetryProfileProperty.HeaderAuthentication] = JsonSerializer.SerializeToElement(
                new
                {
                    header.Enabled,
                    header.MappingType,
                    header.TrustedProxies,
                    header.EnableLogging,
                    header.MaxHeaderLength,
                    header.RequireHttps,
                },
                SettingsJsonOptions
            );
            return Capture(
                changed ? TelemetryEvent.SettingsChanged.ToString() : TelemetryProtocol.ProfileEvent,
                new Dictionary<string, object>(properties),
                properties
            );
        });
        settingsResult.LogIfFailed();
    }

    /// <summary>
    /// Finds the nearest string-valued tag on an activity or its in-process parents for session correlation.
    /// </summary>
    internal static string? GetActivityTag(Activity? activity, string name)
    {
        for (var current = activity; current is not null; current = current.Parent)
            if (current.GetTagItem(name) is string value)
                return value;
        return null;
    }

    /// <summary>Stops observing settings changes without taking ownership of the injected native SDK.</summary>
    public void Dispose() => _settingsSubscription?.Dispose();

    private sealed class JobTelemetryScope : IDisposable
    {
        private readonly Activity? _activity;
        private readonly Activity? _previous;
        private readonly IDisposable _logContext;

        /// <summary>Owns the job activity and log scope together with the ambient activity to restore.</summary>
        public JobTelemetryScope(Activity? activity, Activity? previous, IDisposable logContext)
        {
            _activity = activity;
            _previous = previous;
            _logContext = logContext;
        }

        /// <summary>Releases job log/span context and restores the activity that preceded execution.</summary>
        public void Dispose()
        {
            _logContext.Dispose();
            _activity?.Dispose();
            Activity.Current = _previous;
        }
    }
}
