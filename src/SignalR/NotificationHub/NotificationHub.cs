namespace Reaparr.SignalR;

/// <summary>
///  The NotificationHub class is a SignalR hub that sends notifications to the front-end.
/// </summary>
public class NotificationHub : Hub<INotificationHub>, INotificationHub
{
    private readonly ILogger _log;

    /// <summary>
    ///  Initializes a new instance of the <see cref="NotificationHub"/> class.
    /// </summary>
    /// <param name="log">  The <see cref="Serilog.ILogger"/> instance to use for logging.</param>
    public NotificationHub(ILogger log)
    {
        _log = log.ForContext<NotificationHub>();
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        _log.Here()
            .Debug("Client connected to {HubName}: {ConnectionId}", nameof(NotificationHub), Context.ConnectionId);
    }

    /// <inheritdoc/>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _log.Here()
                .Error(
                    exception,
                    "Client disconnected with error from {HubName}: {ConnectionId}",
                    nameof(NotificationHub),
                    Context.ConnectionId
                );
        }
        else
        {
            _log.Here()
                .Debug(
                    "Client disconnected from {HubName}: {ConnectionId}",
                    nameof(NotificationHub),
                    Context.ConnectionId
                );
        }

        return base.OnDisconnectedAsync(exception);
    }

    /// <inheritdoc/>
    public async Task Notification(NotificationDTO notification)
    {
        _log.Here()
            .Debug(
                "Sending notification: {MessageTypesNotification} => {@NotificationDto}",
                nameof(MessageTypes.Notification),
                notification
            );
        await Clients.All.Notification(notification);
    }

    /// <inheritdoc/>
    public async Task RefreshNotification(RefreshDataType dataType)
    {
        _log.Here().Debug("Sending refresh notification: {DataType}", dataType);
        await Clients.All.RefreshNotification(dataType);
    }
}
