namespace Reaparr.Domain;

public class EventPublisher : IEventPublisher
{
    private readonly ILogger _log;

    public EventPublisher(ILogger log)
    {
        _log = log.ForContext<EventPublisher>();
    }

    /// <inheritdoc/>
    public Task PublishAsync(IEvent @event, CancellationToken ct = default) =>
        PublishAsync(@event, Mode.WaitForNone, ct);

    /// <inheritdoc/>
    public async Task PublishAsync(IEvent @event, Mode mode = Mode.WaitForNone, CancellationToken ct = default)
    {
        try
        {
            await @event.PublishAsync(mode, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _log.Here().Warning("Publish of {EventType} canceled", @event.GetType().Name);
        }
        catch (Exception e)
        {
            // Swallow exception to avoid breaking the fire and forget
            _log.Here().Error(e, "Error publishing event {EventType}", @event);
            _log.Here().ErrorResult(e);
        }
    }
}
