using System.Diagnostics;
using Velopack;

namespace Reaparr.AppHost;

/// <summary>
///  The main class entry point for the application.
/// </summary>
public class Program
{
    private static readonly AppRuntimeInfo _appRuntimeInfo = new();
    private static readonly AppBuildInfo _appBuildInfo = new();
    private static PathProvider? _pathProvider;
    private static DesktopStartupFailureDialog? _failureDialog;

    // ReSharper disable once InconsistentNaming
    private static Serilog.ILogger _log => Log.ForContext(typeof(Program));

    /// <summary>
    ///  The main method entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    [STAThread]
    public static async Task Main(string[] args)
    {
        FluentResultConfiguration.Setup();

        _pathProvider = new PathProvider(_appBuildInfo, _appRuntimeInfo);
        var logBuffer = new LogBufferService();
        var signalRLogConfig = new SignalRLogConfig(_appRuntimeInfo, _pathProvider, logBuffer, _appBuildInfo);
        _failureDialog = new DesktopStartupFailureDialog(_appBuildInfo, _pathProvider, logBuffer);

        // Skip logger setup in integration test mode to preserve test logger
        if (!_appRuntimeInfo.IsIntegrationTestMode)
            LogFactory.SetupLogging(signalRLogConfig, _appRuntimeInfo, _appRuntimeInfo.LogLevel);

        // This should be run at the very start before anything is initiated
        var velopackResult = Result.Try(() => VelopackApp.Build().Run());
        if (velopackResult.IsFailed)
        {
            FailedToStart(velopackResult);
            return;
        }

        _log.Here().Information("Initiating Reaparr boot process");

        _log.Here()
            .Information(
                "Starting Reaparr {Version} ({Channel}) in {RuntimeMode} mode on {CurrentOS} ({RuntimeIdentifier})",
                _appBuildInfo.InformationalVersion,
                _appBuildInfo.IsDevRelease ? "DEVELOPMENT" : "STABLE",
                _appBuildInfo.RuntimeMode,
                _appBuildInfo.CurrentOS,
                _appBuildInfo.RuntimeIdentifier
            );
        try
        {
            AppExtensions.LogIdentity(_appBuildInfo, _appRuntimeInfo);

            var builder = WebApplication.CreateBuilder(args);

            builder.Host.ConfigureAutofacBuilder(logBuffer);
            builder.Services.ConfigureServices(builder.Environment, _appRuntimeInfo);
            var app = builder.Build();

            if (!_appRuntimeInfo.IsIntegrationTestMode)
                signalRLogConfig.AttachSignalR(app, LogFactory.MinimumLogLevel);

            var applicationResult = await RunApplicationAsync(app, _appBuildInfo, _appRuntimeInfo);
            if (applicationResult.IsFailed)
                FailedToStart(applicationResult);
        }
        catch (Exception e)
        {
            _log.Here().Fatal("Reaparr crashed due to an exception!");
            FailedToStart(Result.Fail(new ExceptionalError(e)));
        }
        finally
        {
            // Ensure to flush and stop internal timers/threads before application-exit (Avoid segmentation fault on Linux)
            LogFactory.CloseAndFlush();
        }
    }

    internal static async Task<Result> RunApplicationAsync(
        WebApplication app,
        IAppBuildInfo appBuildInfo,
        IAppRuntimeInfo appRuntimeInfo,
        CancellationToken cancellationToken = default
    )
    {
        var isDesktopProcess = appBuildInfo.IsDesktopMode && !appRuntimeInfo.IsIntegrationTestMode;
        if (isDesktopProcess)
        {
            var coordinator = app.Services.GetRequiredService<IDesktopSingleInstanceCoordinator>();
            if (!coordinator.TryAcquirePrimaryOwnership())
            {
                _log.Here().Debug("Signaling the existing Reaparr desktop instance");
                return await coordinator.SignalPrimaryInstanceAsync(cancellationToken);
            }
        }

        var configResult = app.SetupConfigFile();
        if (configResult.IsFailed)
            return configResult;

        var databaseResult = await app.SetupDatabase();
        if (databaseResult.IsFailed)
            return databaseResult;

        app.ApplyForwardedHeaders();
        app.ConfigureApplication(app.Environment, appBuildInfo, appRuntimeInfo);

        if (isDesktopProcess)
            return await RunDesktopLifecycleAsync(app, cancellationToken);

        await app.RunAsync(cancellationToken);
        return Result.Ok();
    }

    internal static async Task<Result> RunDesktopLifecycleAsync(
        WebApplication app,
        CancellationToken cancellationToken = default,
        Action<Uri>? openBrowser = null
    )
    {
        var services = app.Services;
        var singleInstanceCoordinator = services.GetRequiredService<IDesktopSingleInstanceCoordinator>();


        await app.StartAsync(cancellationToken);
        // Observe host shutdown before entering the blocking native window loop.
        var hostShutdownTask = app.WaitForShutdownAsync(cancellationToken);
        try
        {
            var appRuntimeInfo = services.GetRequiredService<IAppRuntimeInfo>();
            if (appRuntimeInfo.IsDesktopEmbeddedDisabled)
            {
                var browserResult = StartBrowserOnlyMode(
                    ResolveBrowserUri(app.Urls, appRuntimeInfo),
                    singleInstanceCoordinator,
                    openBrowser,
                    cancellationToken
                );
                if (browserResult.IsFailed)
                    return browserResult;

                await hostShutdownTask;
                return Result.Ok();
            }

            _log.Here().Debug("Starting the Reaparr desktop single-instance listener");
            var desktopMode = services.GetRequiredService<IDesktopMode>();
            var listenerResult = singleInstanceCoordinator.StartListener(
                ct =>
                {
                    _log.Here().Debug("Showing the Reaparr desktop window");
                    return desktopMode.ShowMainWindowAsync(ct);
                },
                cancellationToken
            );
            if (listenerResult.IsFailed)
                return listenerResult;

            var desktopModeResult = await desktopMode.StartAsync(cancellationToken);
            if (desktopModeResult.IsFailed)
                return desktopModeResult;

            await desktopMode.WaitForExitAsync(cancellationToken);
            return Result.Ok();
        }
        finally
        {
            app.Lifetime.StopApplication();
            await hostShutdownTask;
        }
    }

    private static Result StartBrowserOnlyMode(
        Uri browserUri,
        IDesktopSingleInstanceCoordinator coordinator,
        Action<Uri>? openBrowser,
        CancellationToken cancellationToken
    )
    {
        _log.Here()
            .Warning(
                "Embedded desktop window mode disabled via {EnvVar}. Launching browser-only mode at {Uri}",
                EnvKeys.DesktopEmbeddedDisabled,
                browserUri
            );

        openBrowser ??= static uri => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        var listenerResult = coordinator.StartListener(
            _ =>
            {
                openBrowser(browserUri);
                return Task.FromResult(Result.Ok());
            },
            cancellationToken
        );
        if (listenerResult.IsFailed)
            return listenerResult;

        openBrowser(browserUri);
        return Result.Ok();
    }

    internal static Uri ResolveBrowserUri(IEnumerable<string> serverAddresses, IAppRuntimeInfo appRuntimeInfo)
    {
        if (appRuntimeInfo.IsDevelopmentEnvironment)
            return new Uri("http://localhost:3000/");

        var serverAddress = serverAddresses.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(serverAddress))
            return new Uri($"http://localhost:{appRuntimeInfo.AppPort}/");

        var uri = new Uri(serverAddress);
        if (uri.Host is "0.0.0.0" or "::" or "[::]")
            return new UriBuilder(uri) { Host = "localhost" }.Uri;

        return uri;
    }

    private static void FailedToStart(Result result)
    {
        _log.Here().Fatal("Reaparr failed to start!");

        result.LogFatal();

        LogFactory.CloseAndFlush();

        if (_appBuildInfo.IsDesktopMode && !_appRuntimeInfo.IsIntegrationTestMode)
        {
            try
            {
                _failureDialog?.Show(result);
                _failureDialog = null;
            }
            catch (Exception dialogException)
            {
                _log.Here().Error(dialogException, "Failed to show startup failure dialog");
            }
        }

        // ReSharper disable once LogMessageIsSentenceProblem
        _log.Here().Fatal("Reaparr has been shutdown! R.I.P.");

        if (!_appRuntimeInfo.IsIntegrationTestMode)
            System.Environment.Exit(1);
    }
}
