namespace Reaparr.Logging;

/// <summary>
/// Stores live log events in memory for retrieval and streaming.
/// </summary>
public interface ILogBufferService : ILogEventSink
{
    /// <summary>
    /// Stores a log event in the in-memory cache.
    /// </summary>
    void Add(LiveLogEventDTO logEvent);

    /// <summary>
    /// Returns all cached log events.
    /// </summary>
    LiveLogEventDTO[] GetAll();
}
