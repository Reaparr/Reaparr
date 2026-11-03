namespace Reaparr.Domain;

public interface ITelemetry
{
    Task<Result> TrackAsync(
        string eventName,
        IReadOnlyDictionary<string, object?> parameters
    );
}
