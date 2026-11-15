using System.Diagnostics;

namespace Reaparr.Application.Contracts;

public partial interface ITelemetry
{
    /// <summary>
    /// Initializes telemetry for the current application.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Returns the authenticated browser telemetry bootstrap from embedded build metadata and the cached startup identity.
    /// </summary>
    BrowserTelemetryBootstrapDTO? GetBrowserBootstrap();

    /// <summary>
    /// Sends a diagnostic telemetry event.
    /// </summary>
    Result SendDiagnostic(TelemetryDiagnosticOrigin origin);

    /// <summary>
    /// Records an exception for the specified operation.
    /// </summary>
    void CaptureException(Exception exception, TelemetryActivity operation);

    /// <summary>
    /// Records telemetry for errors in a result.
    /// </summary>
    void CaptureErrors(ResultBase result, TelemetryActivity operation);

    /// <summary>
    /// Records a failed HTTP request.
    /// </summary>
    void HttpRequestFailed(Exception exception, string method, string? route);

    /// <summary>
    /// Enriches the current request activity and scoped logs with the cached identity and optional browser session.
    /// </summary>
    IDisposable? BeginRequest(string? sessionId);

    /// <summary>
    /// Begins telemetry scope for a background job.
    /// </summary>
    IDisposable? BeginJob(
        TelemetryActivity operation,
        (ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId) context
    );

    /// <summary>
    /// Starts a telemetry activity with an optional parent.
    /// </summary>
    Activity? StartActivity(TelemetryActivity operation, ActivityContext parentContext = default);
}
