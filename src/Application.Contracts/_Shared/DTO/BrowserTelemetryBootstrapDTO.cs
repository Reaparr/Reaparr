namespace Reaparr.Application.Contracts;

/// <summary>
/// Embedded browser telemetry configuration and the cached authenticated application identity.
/// </summary>
public sealed record BrowserTelemetryBootstrapDTO
{
    /// <summary>Embedded telemetry ingestion host.</summary>
    public required string ApiHost { get; init; }

    /// <summary>Embedded public telemetry project token.</summary>
    public required string ProjectToken { get; init; }

    /// <summary>Cached application identity used by backend telemetry.</summary>
    public required string DistinctId { get; init; }

    /// <summary>Application informational release version.</summary>
    public required string Release { get; init; }

    /// <summary>Application release environment.</summary>
    public required string Environment { get; init; }

    /// <summary>Application runtime mode.</summary>
    public required string RuntimeMode { get; init; }

    /// <summary>Application runtime identifier.</summary>
    public required string RuntimeIdentifier { get; init; }

    /// <summary>Operating system platform.</summary>
    public required string Platform { get; init; }
}
