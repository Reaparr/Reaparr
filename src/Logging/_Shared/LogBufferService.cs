namespace Reaparr.Logging;

/// <summary>
/// In-memory cache for live log events and functions as a sink.
/// </summary>
public class LogBufferService : ILogBufferService
{
    internal const int MaxRetainedEvents = 10_000;
    internal const long MaxRetainedTextBytes = 8L * 1024 * 1024;

    private readonly Lock _sync = new();
    private readonly Queue<LiveLogEventDTO> _logEvents = new();
    private long _retainedTextBytes;

    /// <inheritdoc />
    public void Add(LiveLogEventDTO logEvent)
    {
        var textBytes = GetTextBytes(logEvent);
        if (textBytes > MaxRetainedTextBytes)
            return;

        lock (_sync)
        {
            while (_logEvents.Count >= MaxRetainedEvents || _retainedTextBytes + textBytes > MaxRetainedTextBytes)
            {
                var evictedEvent = _logEvents.Dequeue();
                _retainedTextBytes -= GetTextBytes(evictedEvent);
            }

            _logEvents.Enqueue(logEvent);
            _retainedTextBytes += textBytes;
        }
    }

    /// <inheritdoc />
    public LiveLogEventDTO[] GetAll()
    {
        lock (_sync)
        {
            return _logEvents.ToArray();
        }
    }

    public void Emit(LogEvent logEvent)
    {
        Add(logEvent.ToLiveLogEvent());
    }

    private static long GetTextBytes(LiveLogEventDTO logEvent)
    {
        long textLength = logEvent.Message.Length;
        textLength += logEvent.Exception?.Length ?? 0;
        textLength += logEvent.SourceContext?.Length ?? 0;
        return textLength * sizeof(char);
    }
}
