using Reaparr.Application.Contracts;

namespace Reaparr.AppHost;

/// <summary>
/// Attaches native request context and reports unexpected request failures.
/// </summary>
public sealed class TelemetryContextMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Creates middleware around the next request delegate.
    /// </summary>
    public TelemetryContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Runs the request with telemetry context, preserving its original exception.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, ITelemetry telemetry)
    {
        using var scope = telemetry.BeginRequest(
            context.Request.Headers[TelemetryProtocol.SessionIdHeader].FirstOrDefault()
        );

        try
        {
            await _next(context);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            telemetry.HttpRequestFailed(
                exception,
                context.Request.Method,
                (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? context.Request.Path.Value
            );
            throw;
        }
    }
}
