namespace Reaparr.SignalR.Contracts;

public interface INotificationHubService
{
    /// <summary>Sends a notification to the front-end.</summary>
    /// <param name="notification">The notification to send.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendNotificationAsync(Notification notification);

    /// <summary>
    /// Sends a refresh data notification to the front-end. Refresh notifications are intentionally
    /// non-cancellable so callers can notify clients after committing state changes.
    /// </summary>
    /// <param name="dataType">The data category to refresh.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendRefreshNotificationAsync(RefreshDataType dataType);

    /// <summary>
    /// Sends multiple refresh data notifications to the front-end. Refresh notifications are intentionally
    /// non-cancellable so callers can notify clients after committing state changes.
    /// </summary>
    /// <param name="dataTypes">The data categories to refresh.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendRefreshNotificationAsync(List<RefreshDataType> dataTypes);
}
