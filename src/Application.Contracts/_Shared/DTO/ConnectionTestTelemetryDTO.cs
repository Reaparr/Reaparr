namespace Reaparr.Application.Contracts;

/// <summary>
/// Connection-test details projected for telemetry without credentials or raw upstream error messages.
/// </summary>
/// <remarks>
/// A successful result envelope can still describe an unhealthy connection; <see cref="Status"/> determines that outcome.
/// </remarks>
public sealed record ConnectionTestTelemetryDTO
{
    /// <summary>The connection health or rejection reason reported by the probe.</summary>
    public required TestConnectionStatus Status { get; init; }

    /// <summary>The HTTP response status observed by the probe, or null when none was recorded.</summary>
    public int? HttpStatusCode { get; init; }
}
