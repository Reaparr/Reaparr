using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using CliWrap;
using CliWrap.EventStream;

namespace Reaparr.External;

/// <summary>
/// Native wrapper around the dash-mpd-cli binary for downloading MPEG-DASH media.
/// Manages process lifecycle with start, stop, and clean disposal using CliWrap.
/// </summary>
public class DashMpdCliWrapper : IDashMpdCliWrapper
{
    private const string NETWORK_ERROR_TOKEN = "network error";
    private const string MAX_NETWORK_ERROR_TOKEN = "max_error_count";

    private readonly ILogger _log;
    private readonly IFile _file;
    private readonly CancellationTokenSource _gracefulCts = new();
    private readonly CancellationTokenSource _forcefulCts = new();
    private readonly string _binaryPath;
    private readonly object _lifecycleGate = new();

    private readonly Subject<string> _stdoutSubject = new();
    private readonly Subject<DashDownloadProgress> _progressSubject = new();
    private readonly Subject<DashDownloadCompletedEventArgs> _downloadCompletedSubject = new();

    private Task<Result>? _runTask;
    private Task<Result>? _stopTask;
    private Task? _disposeTask;
    private bool _disposed;

    private bool _hasNetworkError;
    private string? _networkErrorLine;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashMpdCliWrapper"/> class.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
    public DashMpdCliWrapper(ILogger logger, IFile file)
    {
        _log = logger.ForContext<DashMpdCliWrapper>();
        _file = file;
        _binaryPath = GetDefaultBinaryPath();
    }

    /// <summary>
    /// Observable stream of standard output lines (including ANSI codes and carriage returns).
    /// </summary>
    public IObservable<string> StandardOutput => _stdoutSubject.AsObservable();

    /// <summary>
    /// Observable stream of parsed download progress updates.
    /// </summary>
    public IObservable<DashDownloadProgress> Progress => _progressSubject.AsObservable();

    /// <inheritdoc/>
    public IObservable<DashDownloadCompletedEventArgs> DownloadCompleted => _downloadCompletedSubject.Take(1);

    /// <inheritdoc/>
    public async Task<Result> StartAsync(DashMpdCliOptions options)
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
                return Result.Fail("Cannot start dash-mpd-cli after the wrapper has been disposed.");

            if (_runTask is { IsCompleted: false })
                throw new InvalidOperationException("dash-mpd-cli is already running.");

            if (_gracefulCts.IsCancellationRequested || _forcefulCts.IsCancellationRequested)
                return ResultExtensions.TaskIsCancelled(nameof(DashMpdCliWrapper));
        }

        _hasNetworkError = false;
        _networkErrorLine = null;

        if (!_file.Exists(_binaryPath))
            return _log.Here()
                .ErrorResult(
                    "dash-mpd-cli binary not found at: {BinaryPath}. Ensure the binary is included in the build output",
                    _binaryPath
                );

        if (string.IsNullOrWhiteSpace(options.MpdUrl))
            return _log.Here().ErrorResult("MPD URL cannot be null or empty");

        if (string.IsNullOrWhiteSpace(options.Output))
            return _log.Here().ErrorResult("Output path cannot be null or empty");

        if (string.IsNullOrWhiteSpace(options.WorkingDirectory))
            return _log.Here().ErrorResult("Working directory cannot be null or empty");

        var arguments = options.ToBuildArguments();

        _log.Here().Information("Starting dash-mpd-cli: {BinaryPath} {Arguments}", _binaryPath, arguments);
        _log.Here().Debug("Working directory: {WorkingDirectory}", options.WorkingDirectory);

        var envVars = options.EnvironmentVariables.ToDictionary(kvp => kvp.Key, string? (kvp) => kvp.Value);

        foreach (var (key, value) in options.EnvironmentVariables)
            _log.Here().Verbose("Environment variable: {Key}={Value}", key, value);

        var command = Cli.Wrap(_binaryPath)
            .WithValidation(CommandResultValidation.None)
            .WithArguments(arguments)
            .WithWorkingDirectory(options.WorkingDirectory)
            .WithEnvironmentVariables(envVars);

        Task<Result> runTask;
        lock (_lifecycleGate)
        {
            if (_disposed)
                return Result.Fail("Cannot start dash-mpd-cli after the wrapper has been disposed.");
            if (_runTask is { IsCompleted: false })
                throw new InvalidOperationException("dash-mpd-cli is already running.");

            if (_gracefulCts.IsCancellationRequested || _forcefulCts.IsCancellationRequested)
                return ResultExtensions.TaskIsCancelled(nameof(DashMpdCliWrapper));

            _stopTask = null;
            runTask = RunEventLoopAsync(command, _forcefulCts.Token, _gracefulCts.Token);
            _runTask = runTask;
        }

        return await runTask;
    }

    private async Task<Result> RunEventLoopAsync(
        Command command,
        CancellationToken forcefulCancellationToken,
        CancellationToken gracefulCancellationToken
    )
    {
        // Do not let synchronous process events invoke observers while StartAsync holds the lifecycle gate.
        await Task.Yield();

        var processResult = Result.Ok();

        var listenResult = await Result.Try(
            (Func<Task>)(
                async () =>
                {
                    await foreach (
                        var cmdEvent in command.ListenAsync(
                            Encoding.Default,
                            Encoding.Default,
                            forcefulCancellationToken,
                            gracefulCancellationToken
                        )
                    )
                    {
                        switch (cmdEvent)
                        {
                            case StartedCommandEvent started:
                                _log.Here()
                                    .Information(
                                        "dash-mpd-cli process started with PID {ProcessId}",
                                        started.ProcessId
                                    );
                                break;

                            // By convention for "data output" (what you want to pipe into another program or save to a file).
                            case StandardOutputCommandEvent stdOut:
                                HandleStdoutLine(stdOut.Text);
                                break;

                            // By convention "diagnostics" (logs, warnings, progress bars, info messages) happen in stderr
                            case StandardErrorCommandEvent stdErr:
                                HandleStdoutLine(stdErr.Text);
                                break;

                            case ExitedCommandEvent exited:
                                _log.Here()
                                    .Information("dash-mpd-cli process exited with code {ExitCode}", exited.ExitCode);

                                var isCancelled = IsCancellationRequested();
                                var result =
                                    isCancelled ? ResultExtensions.TaskIsCancelled(nameof(DashMpdCliWrapper))
                                    : exited.ExitCode == 0 ? Result.Ok()
                                    : CreateFailureResult(exited.ExitCode);

                                processResult = result;

                                _downloadCompletedSubject.OnNext(
                                    new DashDownloadCompletedEventArgs(isCancelled, exited.ExitCode, result)
                                );
                                break;
                        }
                    }
                }
            )
        );

        if (IsCancellationRequested())
            return ResultExtensions.TaskIsCancelled(nameof(DashMpdCliWrapper));

        return Result.Merge(listenResult, processResult);
    }

    private bool IsCancellationRequested() =>
        _gracefulCts.IsCancellationRequested || _forcefulCts.IsCancellationRequested;

    private void HandleStdoutLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;

        _stdoutSubject.OnNext(line);

        var progress = TryParseProgress(line);
        if (progress != null)
        {
            _progressSubject.OnNext(progress);
            return;
        }

        var dashLogEvent = TryParseLogEvent(line);
        if (dashLogEvent == null)
        {
            _log.Here().Error("Failed to parse dash-mpd-cli NDJSON line: {Data}", line);
            return;
        }

        if (IsNetworkErrorLine(dashLogEvent.Message))
        {
            _hasNetworkError = true;
            _networkErrorLine = dashLogEvent.Message;
        }

        if (string.Equals(dashLogEvent.Level, "ERROR", StringComparison.OrdinalIgnoreCase))
        {
            _log.Here().Error("{Data}", line);
            return;
        }

        if (
            string.Equals(dashLogEvent.Level, "WARN", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dashLogEvent.Level, "WARNING", StringComparison.OrdinalIgnoreCase)
        )
        {
            _log.Here().Warning("{Data}", line);
            return;
        }

        _log.Here().Debug("{Data}", line);
    }

    private static DashLogEvent? TryParseLogEvent(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var result = Result.Try(() =>
            JsonSerializer.Deserialize<DashLogEvent>(output, DefaultJsonSerializerOptions.ConfigStandard)
        );

        if (result.IsFailed)
            return null;

        var valueEvent = result.Value;
        if (valueEvent is null)
            return null;

        return valueEvent;
    }

    private Result CreateFailureResult(int exitCode)
    {
        if (!_hasNetworkError)
            return Result.Fail("dash-mpd-cli exited with code {ExitCode}", exitCode);

        return Result
            .Fail("dash-mpd-cli failed with network error: {NetworkErrorLine}", _networkErrorLine ?? string.Empty)
            .Add504GatewayTimeoutError();
    }

    private static bool IsNetworkErrorLine(string line) =>
        line.Contains(NETWORK_ERROR_TOKEN, StringComparison.OrdinalIgnoreCase)
        || line.Contains(MAX_NETWORK_ERROR_TOKEN, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Stops the running process gracefully, then forcefully if it does not exit within the grace period.
    /// </summary>
    /// <returns>A task that completes when the process has stopped.</returns>
    public Task<Result> StopAsync()
    {
        lock (_lifecycleGate)
        {
            _stopTask ??= StopCoreAsync();
            return _stopTask;
        }
    }

    private async Task<Result> StopCoreAsync()
    {
        _log.Here().Information("Stopping dash-mpd-cli process");
        await _gracefulCts.CancelAsync();
        _forcefulCts.CancelAfter(TimeSpan.FromSeconds(10));

        var runTask = _runTask;
        if (runTask is null)
            return Result.Ok();

        return await Result.Try(async Task<Result> () => await runTask);
    }

    /// <summary>
    /// Asynchronously disposes of the wrapper and ensures the process is terminated.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        lock (_lifecycleGate)
        {
            _disposed = true;
            _disposeTask ??= DisposeCoreAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            var stopResult = await StopAsync();
            stopResult.LogIfFailed();
        }
        catch (Exception ex)
        {
            _log.Here().Warning(ex, "Error stopping dash-mpd-cli during dispose");
        }

        try
        {
            _stdoutSubject.OnCompleted();
            _progressSubject.OnCompleted();
            _downloadCompletedSubject.OnCompleted();

            _stdoutSubject.Dispose();
            _progressSubject.Dispose();
            _downloadCompletedSubject.Dispose();
        }
        catch (Exception ex)
        {
            _log.Here().Warning(ex, "Error disposing subjects");
        }

        _gracefulCts.Dispose();
        _forcefulCts.Dispose();
    }

    /// <summary>
    /// Attempts to parse progress information from dash-mpd-cli output.
    /// </summary>
    /// <param name="output">The output line to parse.</param>
    /// <returns>Progress information if successfully parsed, null otherwise.</returns>
    private DashDownloadProgress? TryParseProgress(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var result = Result.Try(() =>
            JsonSerializer.Deserialize<DashProgressEvent>(output, DefaultJsonSerializerOptions.ConfigStandard)
        );

        if (result.IsFailed)
            return null;

        var valueEvent = result.Value;
        if (valueEvent is not { IsProgress: true })
            return null;

        return new DashDownloadProgress
        {
            Eta = valueEvent.EtaSeconds ?? 0,
            Percent = valueEvent.Percent,
            DownloadSpeedInBytes = valueEvent.Bandwidth,
            CurrentStep = valueEvent.Message,
            RawOutput = output,
            DownloadedBytes = valueEvent.DownloadedBytes,
            TotalBytes = valueEvent.TotalBytes ?? 0,
        };
    }

    private string GetDefaultBinaryPath()
    {
        var assemblyDir = AppContext.BaseDirectory;
        var binaryDir = Path.Combine(assemblyDir, "dash-mpd-cli", "binary");

        var binaryName = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "dash-mpd-cli-linux-amd64",
            Architecture.Arm64 => "dash-mpd-cli-linux-aarch64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported architecture: {RuntimeInformation.OSArchitecture}. "
                    + $"Only x64 and ARM64 are supported."
            ),
        };

        return Path.Combine(binaryDir, binaryName);
    }
}
