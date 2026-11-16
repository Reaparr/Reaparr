using System.Collections.Concurrent;
using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Reaparr.Environment;
using Reaparr.SignalR;
using Serilog.Events;
using Serilog.Sinks.AspNetCore.App.SignalR.Extensions;

namespace Reaparr.AppHost.UnitTests;

public class SignalRLogConfigUnitTests : BaseUnitTest
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldPreserveExistingSinksAndLoggers_WhenSignalRIsAttachedWithoutTelemetry(bool enableSeq)
    {
        // Arrange
        // The real Serilog file sink does not use IFileSystem, so isolate its output outside the repository.
        var directory = Directory.CreateTempSubdirectory(nameof(SignalRLogConfigUnitTests));
        var seqBodies = new ConcurrentQueue<string>();
        var seqReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var liveEvents = new ConcurrentQueue<LiveLogEventDTO>();
        var client = new Mock<IClientProxy>();
        client
            .Setup(x => x.SendCoreAsync(nameof(ILogHub.LogEvent), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>(
                (_, arguments, _) => liveEvents.Enqueue((LiveLogEventDTO)arguments[0]!)
            )
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        var clients = new Mock<IHubClients>();
        clients.SetupGet(x => x.All).Returns(client.Object);
        var hub = new Mock<IHubContext<LogHub>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSerilogHub<LogHub>();
        builder.Services.AddSingleton(hub.Object);
        await using var app = builder.Build();
        app.Run(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            seqBodies.Enqueue(await reader.ReadToEndAsync());
            context.Response.StatusCode = StatusCodes.Status201Created;
            seqReceived.TrySetResult();
        });
        await app.StartAsync();
        SetAppBuildInfo(x =>
        {
            x.TelemetryHost = string.Empty;
            x.TelemetryProjectToken = string.Empty;
        });
        var seqUrl = enableSeq ? app.Urls.Single() : string.Empty;
        SetAppRuntimeInfo(x => x.SEQ_Url = seqUrl);
        var pathProvider = new Mock<IPathProvider>();
        pathProvider.SetupGet(x => x.LogsDirectory).Returns(directory.FullName);
        var buffer = new LogBufferService();
        var config = new SignalRLogConfig(
            Mock.Container.Resolve<IAppRuntimeInfo>(),
            pathProvider.Object,
            buffer,
            Mock.Container.Resolve<IAppBuildInfo>()
        );
        await using var logger = config.GetLogger();
        var contextualLogger = logger.ForContext("Regression", true);
        contextualLogger.Debug("Before SignalR attachment");

        try
        {
            // Act
            config.AttachSignalR(app, LogEventLevel.Debug);
            contextualLogger.Debug("After SignalR attachment");
            if (enableSeq)
                await seqReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            buffer.GetAll().Select(x => x.Message).ShouldBe(["Before SignalR attachment", "After SignalR attachment"]);
            liveEvents.Select(x => x.Message).ShouldBe(["After SignalR attachment"]);
            client.Verify();
            var file = Directory.GetFiles(directory.FullName, "log*.txt").Single();
            var contents = await File.ReadAllTextAsync(file);
            contents.ShouldContain("Before SignalR attachment");
            contents.ShouldContain("After SignalR attachment");
            if (enableSeq)
            {
                var exported = string.Join("\n", seqBodies);
                exported.ShouldContain("Before SignalR attachment");
                exported.ShouldContain("After SignalR attachment");
            }
            else
                seqBodies.ShouldBeEmpty();
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
