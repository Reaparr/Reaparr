using System.Collections.Concurrent;

namespace Reaparr.BaseTests;

public class MockNotificationHubService : INotificationHubService
{
    private readonly ILogger _log;

    public BlockingCollection<Notification> NotificationList { get; } = new();

    public BlockingCollection<RefreshDataType> RefreshNotificationList { get; } = new();

    public MockNotificationHubService(ILogger log)
    {
        _log = log.ForContext<MockNotificationHubService>();
    }

    public Task SendNotificationAsync(Notification notification)
    {
        NotificationList.Add(notification);
        _log.Here().Verbose("{ClassName} => {@Notification}", nameof(MockNotificationHubService), notification);
        return Task.CompletedTask;
    }

    public Task SendRefreshNotificationAsync(RefreshDataType dataType)
    {
        RefreshNotificationList.Add(dataType);
        _log.Here().Verbose("{ClassName} => {DataType}", nameof(MockNotificationHubService), dataType);

        return Task.CompletedTask;
    }

    public async Task SendRefreshNotificationAsync(List<RefreshDataType> dataTypes)
    {
        foreach (var dataType in dataTypes)
            await SendRefreshNotificationAsync(dataType);
    }
}
