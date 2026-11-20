using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Reaparr.Logging.UnitTests;

public class LogBufferServiceUnitTests
{
    [Test]
    public void ShouldEvictOldestEvents_WhenCountLimitIsReached()
    {
        // Arrange
        var buffer = new LogBufferService();
        for (var index = 0; index < LogBufferService.MaxRetainedEvents; index++)
            buffer.Add(CreateEvent(index.ToString()));

        // Act
        buffer.Add(CreateEvent("latest"));

        // Assert
        var history = buffer.GetAll();
        history.Length.ShouldBe(LogBufferService.MaxRetainedEvents);
        history.First().Message.ShouldBe("1");
        history.Last().Message.ShouldBe("latest");
    }

    [Test]
    public void ShouldEvictOldestEvents_WhenTextByteLimitIsReached()
    {
        // Arrange
        var buffer = new LogBufferService();
        var payload = new string('x', 1_500_000);
        buffer.Add(CreateEvent($"first-{payload}"));
        buffer.Add(CreateEvent($"second-{payload}"));

        // Act
        buffer.Add(CreateEvent($"latest-{payload}"));

        // Assert
        var history = buffer.GetAll();
        history.Select(x => x.Message[..x.Message.IndexOf('-')]).ShouldBe(["second", "latest"]);
    }

    [Test]
    public void ShouldDropOversizedEvent_WithoutEvictingExistingHistory()
    {
        // Arrange
        var buffer = new LogBufferService();
        buffer.Add(CreateEvent("retained"));

        // Act
        var oversizedMessage = new string('x', (int)(LogBufferService.MaxRetainedTextBytes / sizeof(char)) + 1);
        buffer.Add(CreateEvent(oversizedMessage));

        // Assert
        buffer.GetAll().Select(x => x.Message).ShouldBe(["retained"]);
    }

    [Test]
    public async Task ShouldReturnConsistentFifoSnapshots_WhenWritersAndSnapshotsRunConcurrently()
    {
        // Arrange
        var buffer = new LogBufferService();
        var snapshots = new ConcurrentQueue<string[]>();
        var writers = Enumerable
            .Range(0, 64)
            .Select(index =>
                Task.Run(() =>
                {
                    buffer.Add(CreateEvent(index.ToString()));
                    snapshots.Enqueue(buffer.GetAll().Select(x => x.Message).ToArray());
                })
            )
            .ToArray();

        // Act
        await Task.WhenAll(writers);

        // Assert
        var history = buffer.GetAll();
        history.Length.ShouldBe(64);
        history.Select(x => x.Message).Distinct().Count().ShouldBe(64);
        snapshots.ShouldNotBeEmpty();
        foreach (var snapshot in snapshots)
        {
            snapshot.ShouldBe(history.Take(snapshot.Length).Select(x => x.Message).ToArray());
        }
    }

    [Test]
    public void ShouldNotRetainEvictedPayload_WhenBufferRemainsAlive()
    {
        // Arrange
        var buffer = new LogBufferService();
        var weakReferences = AddEvictedEvents(buffer);

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        weakReferences.Event.TryGetTarget(out _).ShouldBeFalse();
        weakReferences.Message.TryGetTarget(out _).ShouldBeFalse();
        buffer.GetAll().Length.ShouldBe(2);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<LiveLogEventDTO> Event, WeakReference<string> Message) AddEvictedEvents(
        LogBufferService buffer
    )
    {
        var evicted = CreateEvent($"evicted-{new string('x', 2_000_000)}");
        var weakReferences = (new WeakReference<LiveLogEventDTO>(evicted), new WeakReference<string>(evicted.Message));
        buffer.Add(evicted);
        buffer.Add(CreateEvent($"retained-one-{new string('x', 2_000_000)}"));
        buffer.Add(CreateEvent($"retained-two-{new string('x', 2_000_000)}"));
        return weakReferences;
    }

    private static LiveLogEventDTO CreateEvent(string message) =>
        new()
        {
            Sequence = 1,
            Timestamp = DateTimeOffset.UnixEpoch,
            Level = LogSeverity.Information,
            Message = message,
        };
}
