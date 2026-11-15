using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Trace;
using PostHog;
using PostHog.Config;
using Reaparr.Application;
using Reaparr.Application.Contracts;
using Reaparr.Environment;
using Reaparr.Identity.Contracts;
using Reaparr.Settings.Contracts;
using Serilog.Events;

namespace Reaparr.AppHost.UnitTests;

// Native OTel instrumentation is process-wide; isolate this fixture's two collector/provider pairs.
[NotInParallel(nameof(TelemetryLogConfigUnitTests))]
public class TelemetryLogConfigUnitTests : BaseUnitTest
{
    private const string IDENTITY = "0e3a7eb1-2e25-4b37-af7f-e58263f1742c";
    private const string SESSION = "0197af2c-8533-71b3-a351-80da30db084a";
    private const string PROJECT_TOKEN = "phc_loopback_test";
    private const string PROBE_PATH = "/telemetry-native-probe";
    private const string LOG_SECRET = "intentional-operational-log-secret";
    private const string FAILURE_PATH = "/telemetry-native-failure";
    private const string EXCEPTION_SECRET = "accepted-original-exception-text";

    [Test]
    public void ShouldResolveOneDisabledPostHogClient_WhenAutofacBuildsApplicationContainer()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>(MockBehavior.Strict);
        loggerFactory
            .Setup(x =>
                x.CreateLogger(It.Is<string>(category => !category.StartsWith("PostHog", StringComparison.Ordinal)))
            )
            .Returns(NullLogger.Instance);
        loggerFactory.Setup(x => x.Dispose());
        var services = new ServiceCollection();
        services.AddSingleton(loggerFactory.Object);
        services.AddOptions<PostHogOptions>().Configure(options => options.Disabled = true);
        services.AddPostHog();

        using var container = BuildPostHogContainer(services);

        // Act
        var client = container.Resolve<IPostHogClient>();
        var secondClient = container.Resolve<IPostHogClient>();
        var accepted = client.Capture(IDENTITY, nameof(TelemetryEvent.NotificationsCleared), null, null, null);

        // Assert
        client.ShouldBeOfType<PostHogClient>();
        client.ShouldBeSameAs(secondClient);
        accepted.ShouldBeFalse();
        loggerFactory.Verify(
            x => x.CreateLogger(It.Is<string>(category => category.StartsWith("PostHog", StringComparison.Ordinal))),
            Times.Never()
        );
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCorrelateRealSdkTracesAndLogs_WhilePreservingExistingMaskPolicy(bool unmasked)
    {
        // Arrange: the native exporters send to an isolated receiver, never to PostHog.
        var received = new ConcurrentQueue<(string Path, string Authorization, string? ContentType, byte[] Body)>();
        var receiverBuilder = WebApplication.CreateBuilder();
        receiverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receiver = receiverBuilder.Build();
        receiver.Run(async context =>
        {
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body);
            received.Enqueue(
                (
                    context.Request.Path.Value!,
                    context.Request.Headers.Authorization.ToString(),
                    context.Request.ContentType,
                    body.ToArray()
                )
            );
            if (
                context.Request.Path == TelemetryProtocol.TracesPath
                || context.Request.Path == TelemetryProtocol.LogsPath
            )
                context.Response.ContentType = "application/x-protobuf";
            else
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{}");
            }
        });
        await receiver.StartAsync();
        var ingestionHost = receiver.Urls.Single();
        var ingestionAuthority = new Uri(ingestionHost).Authority;
        var build = new Mock<IAppBuildInfo>();
        build.SetupGet(x => x.HasValidTelemetry).Returns(true);
        build.SetupGet(x => x.TelemetryHost).Returns(ingestionHost);
        build.SetupGet(x => x.TelemetryProjectToken).Returns(PROJECT_TOKEN);
        build.SetupGet(x => x.InformationalVersion).Returns("1.0.0-test");
        build.SetupGet(x => x.RuntimeMode).Returns("docker");
        build.SetupGet(x => x.RuntimeIdentifier).Returns("linux-x64");
        SetAppRuntimeInfo(runtime =>
        {
            runtime.IsUnmasked = unmasked;
            runtime.SEQ_Url = string.Empty;
        });
        var runtime = Mock.Container.Resolve<IAppRuntimeInfo>();
        var directory = Directory.CreateTempSubdirectory(nameof(TelemetryLogConfigUnitTests));
        var paths = new Mock<IPathProvider>();
        paths.SetupGet(x => x.LogsDirectory).Returns(directory.FullName);
        var buffer = new LogBufferService();
        var logConfig = new SignalRLogConfig(runtime, paths.Object, buffer, build.Object);
        using var logger = logConfig.GetLogger(LogEventLevel.Debug);
        var user = new Mock<IUserService>();
        user.Setup(x => x.GetTelemetryId()).ReturnsAsync(IDENTITY);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["PostHog:HostUrl"] = "https://runtime-override.invalid",
                ["PostHog:ProjectToken"] = "runtime-override",
            }
        );
        builder.Services.AddSingleton(build.Object);
        builder.Services.AddScoped(_ => user.Object);
        builder.Services.AddSingleton(_ => new ActivitySource(TelemetryProtocol.ActivitySourceName));
        builder.Services.AddSingleton<IUserSettings>(new UserSettings());
        builder.Services.AddSingleton<ITelemetry, Telemetry>();
        // Observe real SDK logging through the application logger without including unrelated host logs.
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            logging.AddSerilog(logger);
            logging.AddFilter<Serilog.Extensions.Logging.SerilogLoggerProvider>(
                (category, _) => category is not null && category.StartsWith("PostHog", StringComparison.Ordinal)
            );
        });
        builder.Services.ConfigureTelemetry(build.Object, runtime);
        builder.Services.PostConfigure<PostHogOptions>(options => options.EnableCompression = false);
        using var postHogContainer = BuildPostHogContainer(builder.Services);
        builder.Services.AddSingleton(postHogContainer.Resolve<IPostHogClient>());
        // Both hosts share a process: exclude the collector's own incoming requests from instrumentation.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context => context.Request.Host.Value != ingestionAuthority
        );
        await using var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<TelemetryContextMiddleware>();
        ActivityTraceId operationTrace = default;
        ActivitySpanId operationSpan = default;
        app.MapGet(
            PROBE_PATH,
            (ITelemetry telemetry) =>
            {
                using var operation = telemetry.StartActivity(TelemetryActivity.LibrarySync);
                operation.ShouldNotBeNull();
                operationTrace = operation.TraceId;
                operationSpan = operation.SpanId;
                logger.Verbose("Below the configured log threshold");
                logger.Information("Native telemetry operation {ApiKey}", LOG_SECRET);
                telemetry.NotificationsCleared(17);
                return Results.Accepted();
            }
        );
        app.MapGet(
            FAILURE_PATH,
            (HttpContext _) =>
                Task.FromException(new InvalidOperationException($"Native failure token={EXCEPTION_SECRET}"))
        );
        await app.StartAsync();
        await app.Services.GetRequiredService<ITelemetry>().InitializeAsync();

        try
        {
            // Act: exercise a real incoming request and native Activity propagation across HttpClient/ASP.NET.
            using var initiatingActivity = new Activity(
                nameof(ShouldCorrelateRealSdkTracesAndLogs_WhilePreservingExistingMaskPolicy)
            )
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            initiatingActivity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            using var http = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, app.Urls.Single() + PROBE_PATH);
            request.Headers.Add(TelemetryProtocol.SessionIdHeader, SESSION);
            using var response = await http.SendAsync(request, CancellationToken);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted);
            using var failedRequest = new HttpRequestMessage(HttpMethod.Get, app.Urls.Single() + FAILURE_PATH);
            failedRequest.Headers.Add(TelemetryProtocol.SessionIdHeader, SESSION);
            using var failedResponse = await http.SendAsync(failedRequest, CancellationToken);
            failedResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.InternalServerError);
            await app.Services.GetRequiredService<IPostHogClient>().FlushAsync();
            app.Services.GetRequiredService<TracerProvider>().ForceFlush().ShouldBeTrue();
            logger.Dispose();

            // Assert native wire identities, not a mock echo of a dictionary passed to a sink.
            operationTrace.ShouldBe(initiatingActivity.TraceId);
            var batches = received
                .Where(item => item.Path != TelemetryProtocol.TracesPath && item.Path != TelemetryProtocol.LogsPath)
                .ToArray();
            var sdkEvents = new List<JsonElement>();
            foreach (var batch in batches)
            {
                using var document = JsonDocument.Parse(batch.Body);
                sdkEvents.AddRange(
                    document.RootElement.GetProperty("batch").EnumerateArray().Select(item => item.Clone())
                );
            }
            var properties = sdkEvents
                .Single(item => item.GetProperty("event").GetString() == nameof(TelemetryEvent.NotificationsCleared))
                .GetProperty("properties");
            properties.GetProperty(TelemetryProtocol.TraceId).GetString().ShouldBe(operationTrace.ToHexString());
            properties.GetProperty(TelemetryProtocol.SpanId).GetString().ShouldBe(operationSpan.ToHexString());
            properties.GetProperty(TelemetryProtocol.SessionId).GetString().ShouldBe(SESSION);
            properties.GetProperty(TelemetryProtocol.DistinctId).GetString().ShouldBe(IDENTITY);
            properties.GetProperty(TelemetryProperty.DeletedCount).GetInt32().ShouldBe(17);
            properties.GetProperty(TelemetryProperty.Outcome).GetString().ShouldBe(nameof(TelemetryOutcome.Succeeded));
            var nativeError = sdkEvents
                .Single(item => item.GetProperty("event").GetString() == TelemetryProtocol.ExceptionEvent)
                .GetProperty("properties");
            nativeError.GetProperty(TelemetryProtocol.TraceId).GetString().ShouldBe(operationTrace.ToHexString());
            nativeError.GetProperty(TelemetryProtocol.SessionId).GetString().ShouldBe(SESSION);
            nativeError.GetProperty(TelemetryProperty.HttpRoute).GetString().ShouldBe(FAILURE_PATH);
            nativeError.GetRawText().ShouldContain(typeof(InvalidOperationException).FullName!);
            nativeError.GetRawText().ShouldContain(EXCEPTION_SECRET);
            foreach (var traceExport in received.Where(item => item.Path == TelemetryProtocol.TracesPath))
                Encoding.UTF8.GetString(traceExport.Body).ShouldNotContain(EXCEPTION_SECRET);

            foreach (var endpoint in new[] { TelemetryProtocol.TracesPath, TelemetryProtocol.LogsPath })
            {
                var exports = received.Where(item => item.Path == endpoint).ToArray();
                var correlated = exports.Single(item =>
                    item.Body.AsSpan().IndexOf(Convert.FromHexString(operationSpan.ToHexString())) >= 0
                );
                correlated.Authorization.ShouldBe($"{TelemetryProtocol.BearerScheme} {PROJECT_TOKEN}");
                correlated.ContentType.ShouldBe("application/x-protobuf");
                correlated
                    .Body.AsSpan()
                    .IndexOf(Convert.FromHexString(operationTrace.ToHexString()))
                    .ShouldBeGreaterThanOrEqualTo(0);
                var attributes = Encoding.UTF8.GetString(correlated.Body);
                attributes.ShouldContain(TelemetryProtocol.SessionIdTag);
                attributes.ShouldContain(SESSION);
                attributes.ShouldContain(TelemetryProtocol.DistinctIdTag);
                attributes.ShouldContain(IDENTITY);
                attributes.ShouldContain(TelemetryProtocol.ServiceName);
                attributes.ShouldContain(TelemetryProtocol.Service);
                attributes.ShouldContain("1.0.0-test");
                if (endpoint == TelemetryProtocol.LogsPath)
                {
                    attributes.Contains(LOG_SECRET, StringComparison.Ordinal).ShouldBe(unmasked);
                    attributes.ShouldNotContain("Below the configured log threshold");
                }
            }

            var localLog = await File.ReadAllTextAsync(
                Directory.GetFiles(directory.FullName, "log*.txt").Single(),
                CancellationToken
            );
            localLog.Contains(LOG_SECRET, StringComparison.Ordinal).ShouldBe(unmasked);
            localLog.ShouldNotContain("Below the configured log threshold");
            buffer.GetAll().Single().Message.Contains(LOG_SECRET, StringComparison.Ordinal).ShouldBe(unmasked);
        }
        finally
        {
            await app.StopAsync();
            await receiver.StopAsync();
        }
    }

    [Test]
    public async Task ShouldKeepUnconfiguredBuildNonTransmitting_WhileRequestsAndLocalLogsContinue()
    {
        var received = new ConcurrentQueue<string>();
        var receiverBuilder = WebApplication.CreateBuilder();
        receiverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receiver = receiverBuilder.Build();
        receiver.Run(context =>
        {
            received.Enqueue(context.Request.Path.Value!);
            return Task.CompletedTask;
        });
        await receiver.StartAsync();
        var build = new Mock<IAppBuildInfo>();
        build.SetupGet(x => x.HasValidTelemetry).Returns(false);
        build.SetupGet(x => x.TelemetryHost).Returns(string.Empty);
        build.SetupGet(x => x.TelemetryProjectToken).Returns(string.Empty);
        SetAppRuntimeInfo(runtime => runtime.SEQ_Url = string.Empty);
        var runtime = Mock.Container.Resolve<IAppRuntimeInfo>();
        var directory = Directory.CreateTempSubdirectory(nameof(TelemetryLogConfigUnitTests));
        var paths = new Mock<IPathProvider>();
        paths.SetupGet(x => x.LogsDirectory).Returns(directory.FullName);
        var buffer = new LogBufferService();
        using var logger = new SignalRLogConfig(runtime, paths.Object, buffer, build.Object).GetLogger(
            LogEventLevel.Information
        );
        var user = new Mock<IUserService>(MockBehavior.Strict);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["PostHog:HostUrl"] = receiver.Urls.Single(),
                ["PostHog:ProjectToken"] = PROJECT_TOKEN,
                ["PostHog:Disabled"] = bool.FalseString,
            }
        );
        builder.Services.AddSingleton(build.Object);
        builder.Services.AddScoped(_ => user.Object);
        builder.Services.AddSingleton(_ => new ActivitySource(TelemetryProtocol.ActivitySourceName));
        builder.Services.AddSingleton<IUserSettings>(new UserSettings());
        builder.Services.AddSingleton<ITelemetry, Telemetry>();
        builder.Services.ConfigureTelemetry(build.Object, runtime);
        using var postHogContainer = BuildPostHogContainer(builder.Services);
        builder.Services.AddSingleton(postHogContainer.Resolve<IPostHogClient>());
        await using var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<TelemetryContextMiddleware>();
        app.MapGet(
            PROBE_PATH,
            (ITelemetry telemetry) =>
            {
                using var operation = telemetry.StartActivity(TelemetryActivity.LibrarySync);
                operation.ShouldBeNull();
                telemetry.NotificationsCleared(17);
                telemetry.CaptureException(
                    new IOException("Unavailable optional service"),
                    TelemetryActivity.LibrarySync
                );
                logger.Information("Business operation completed without telemetry configuration");
                return Results.NoContent();
            }
        );
        await app.StartAsync();
        await app.Services.GetRequiredService<ITelemetry>().InitializeAsync();
        try
        {
            using var http = new HttpClient();
            using var response = await http.GetAsync(app.Urls.Single() + PROBE_PATH, CancellationToken);
            response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);
            await app.Services.GetRequiredService<IPostHogClient>().FlushAsync();
            app.Services.GetService<TracerProvider>().ShouldBeNull();
            logger.Dispose();
            received.ShouldBeEmpty();
            user.Verify(x => x.GetTelemetryId(), Times.Never());
            buffer.GetAll().Single().Message.ShouldContain("Business operation completed");
            (
                await File.ReadAllTextAsync(
                    Directory.GetFiles(directory.FullName, "log*.txt").Single(),
                    CancellationToken
                )
            ).ShouldContain("Business operation completed");
        }
        finally
        {
            await app.StopAsync();
            await receiver.StopAsync();
        }
    }

    private static IContainer BuildPostHogContainer(IServiceCollection services)
    {
        var containerBuilder = new ContainerBuilder();
        containerBuilder.Populate(services);
        containerBuilder.RegisterModule<AppHostModule>();
        return containerBuilder.Build();
    }
}
