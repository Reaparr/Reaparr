using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Reaparr.Environment;
using Serilog.Events;

namespace Reaparr.Logging.UnitTests;

public class LogConfigUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldPersistOnlyAcceptedEvents_WhenTelemetryAndSeqAreAbsent()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory(nameof(LogConfigUnitTests));
        var pathProvider = new Moq.Mock<IPathProvider>();
        pathProvider.SetupGet(x => x.LogsDirectory).Returns(directory.FullName);
        var runtimeInfo = new MockAppRuntimeInfo { SEQ_Url = string.Empty, SEQ_ApiKey = string.Empty };
        var buildInfo = new MockAppBuildInfo { TelemetryHost = string.Empty, TelemetryProjectToken = string.Empty };
        const string marker = "I108-file-without-telemetry";

        // Act
        await using (var logger = new ProductionLogConfig(runtimeInfo, pathProvider.Object, buildInfo).GetLogger(LogEventLevel.Warning))
        {
            logger.Information("Filtered-out I108 event");
            logger.Warning("Persisted {Marker}", marker);
        }

        // Assert
        var file = Directory.GetFiles(directory.FullName, "log*.txt").ShouldHaveSingleItem();
        var content = await File.ReadAllTextAsync(file, CancellationToken);
        content.ShouldContain(marker);
        content.ShouldNotContain("Filtered-out I108 event");
        content.ShouldNotContain("\u001b");
    }

    [Test]
    public async Task ShouldDeliverToSeqAndFile_WhenTelemetryCredentialsAreAbsent()
    {
        // Arrange
        var directory = Directory.CreateTempSubdirectory(nameof(LogConfigUnitTests));
        var pathProvider = new Moq.Mock<IPathProvider>();
        pathProvider.SetupGet(x => x.LogsDirectory).Returns(directory.FullName);
        var received = new ConcurrentQueue<(string ApiKey, string Body)>();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receiver = builder.Build();
        receiver.MapPost("/{**path}", async (HttpRequest request) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
            received.Enqueue((request.Headers["X-Seq-ApiKey"].ToString(), body));
            return Results.Content(
                "{\"MinimumLevelAccepted\":null}",
                "application/json",
                statusCode: StatusCodes.Status201Created
            );
        });
        await receiver.StartAsync(CancellationToken);
        var runtimeInfo = new MockAppRuntimeInfo { SEQ_Url = receiver.Urls.Single(), SEQ_ApiKey = "local-seq-test-key" };
        var buildInfo = new MockAppBuildInfo { TelemetryHost = string.Empty, TelemetryProjectToken = string.Empty };
        const string marker = "I108-seq-and-file-without-telemetry";

        // Act
        await using (var logger = new ProductionLogConfig(runtimeInfo, pathProvider.Object, buildInfo).GetLogger(LogEventLevel.Warning))
        {
            logger.Information("Filtered-out I108 event");
            logger.Warning("Persisted {Marker}", marker);
        }
        await receiver.StopAsync(CancellationToken);

        // Assert
        var delivery = received.ShouldHaveSingleItem();
        delivery.ApiKey.ShouldBe(runtimeInfo.SEQ_ApiKey);
        var eventJson = delivery.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries).ShouldHaveSingleItem();
        using var logEvent = JsonDocument.Parse(eventJson);
        logEvent.RootElement.GetProperty("@mt").GetString().ShouldBe("Persisted {Marker}");
        logEvent.RootElement.GetProperty("@l").GetString().ShouldBe("Warning");
        logEvent.RootElement.GetProperty("Marker").GetString().ShouldBe(marker);
        logEvent.RootElement.GetProperty("AppRunId").GetString().ShouldBe(runtimeInfo.AppRunId);
        var file = Directory.GetFiles(directory.FullName, "log*.txt").ShouldHaveSingleItem();
        var content = await File.ReadAllTextAsync(file, CancellationToken);
        content.ShouldContain(marker);
        content.ShouldNotContain("Filtered-out I108 event");
        content.ShouldNotContain("\u001b");
    }

    private sealed class ProductionLogConfig : LogConfig
    {
        public ProductionLogConfig(IAppRuntimeInfo runtimeInfo, IPathProvider pathProvider, IAppBuildInfo buildInfo)
            : base(runtimeInfo, pathProvider, buildInfo) { }
    }
}
