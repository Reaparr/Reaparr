using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace Reaparr.AppHost;

/// <summary>
/// Named mutex and named pipe implementation of desktop single-instance coordination.
/// </summary>
public sealed class DesktopSingleInstanceCoordinator : IDesktopSingleInstanceCoordinator
{
    private const string DEFAULT_SINGLE_INSTANCE_NAME = "Reaparr.Desktop.SingleInstance";
    private const string SIGNAL_MESSAGE = "show";
    private static readonly TimeSpan _signalRetryTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _initialSignalRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan _maxSignalRetryDelay = TimeSpan.FromMilliseconds(250);

    private readonly Serilog.ILogger _log;
    private readonly string _instanceName;
    private readonly SemaphoreSlim _listenerStartLock = new(1, 1);

    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCancellationTokenSource;
    private Task? _listenerTask;
    private bool _ownsMutex;

    /// <summary>Initializes a new instance of <see cref="DesktopSingleInstanceCoordinator"/>.</summary>
    public DesktopSingleInstanceCoordinator(
        Serilog.ILogger log,
        IAppRuntimeInfo appRuntimeInfo,
        string instanceName = DEFAULT_SINGLE_INSTANCE_NAME,
        Func<string?>? appImagePathAccessor = null,
        Func<string>? appBaseDirectoryAccessor = null
    )
    {
        _log = log.ForContext<DesktopSingleInstanceCoordinator>();
        var appImagePathAccessor1 = appImagePathAccessor ?? (() => appRuntimeInfo.AppImage);
        var appBaseDirectoryAccessor1 = appBaseDirectoryAccessor ?? (() => AppContext.BaseDirectory);
        _instanceName = BuildScopedInstanceName(instanceName, appImagePathAccessor1(), appBaseDirectoryAccessor1());
    }

    /// <inheritdoc />
    public bool TryAcquirePrimaryOwnership()
    {
        if (_ownsMutex)
            return true;

        if (_mutex is not null)
            return false;

        _mutex = new Mutex(initiallyOwned: true, _instanceName, out var createdNew);
        _ownsMutex = createdNew;
        return createdNew;
    }

    /// <inheritdoc />
    public async Task<Result> SignalPrimaryInstanceAsync(CancellationToken cancellationToken)
    {
        return await Result.Try(async Task<Result> () =>
        {
            await using var client = new NamedPipeClientStream(
                ".",
                _instanceName,
                PipeDirection.Out,
                PipeOptions.Asynchronous
            );
            var connectResult = await ConnectToPrimaryInstanceAsync(client, cancellationToken);
            if (connectResult.IsFailed)
                return connectResult.LogIfFailed();

            await client.WriteAsync(Encoding.UTF8.GetBytes(SIGNAL_MESSAGE), cancellationToken);
            await client.FlushAsync(cancellationToken);
            return Result.Ok();
        });
    }

    /// <inheritdoc />
    public Result StartListener(Func<CancellationToken, Task<Result>> onSignal, CancellationToken cancellationToken)
    {
        _listenerStartLock.Wait(cancellationToken);
        try
        {
            if (_listenerTask is not null)
                return Result.Ok();

            _listenerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenerTask = Task.Run(
                () => ListenAsync(onSignal, _listenerCancellationTokenSource.Token),
                CancellationToken.None
            );
            return Result.Ok();
        }
        finally
        {
            _listenerStartLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _listenerCancellationTokenSource?.Cancel();
        _listenerCancellationTokenSource?.Dispose();
        _listenerStartLock.Dispose();

        try
        {
            if (_ownsMutex)
                _mutex?.ReleaseMutex();
        }
        catch (ObjectDisposedException e)
        {
            _log.Here().Debug(e, "The desktop single-instance mutex was already disposed before release");
        }

        _mutex?.Dispose();
    }

    private static string BuildScopedInstanceName(
        string baseInstanceName,
        string? appImagePath,
        string appBaseDirectory
    )
    {
        var executionIdentity = !string.IsNullOrWhiteSpace(appImagePath) ? appImagePath : appBaseDirectory;
        var normalizedIdentity = Path.GetFullPath(executionIdentity)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedIdentity));
        var hash = Convert.ToHexString(hashBytes[..8]);
        return $"{baseInstanceName}.{hash}";
    }

    private async Task<Result> ConnectToPrimaryInstanceAsync(
        NamedPipeClientStream client,
        CancellationToken cancellationToken
    )
    {
        var retryDelay = _initialSignalRetryDelay;
        using var timeoutTokenSource = new CancellationTokenSource(_signalRetryTimeout);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutTokenSource.Token
        );

        while (!linkedTokenSource.Token.IsCancellationRequested)
        {
            var connectResult = await Result.Try(async Task () =>
                await client.ConnectAsync(retryDelay, linkedTokenSource.Token)
            );

            if (connectResult.IsSuccess || connectResult.IsCancelled)
                return connectResult;

            await Result.Try(async Task () => await Task.Delay(retryDelay, cancellationToken));

            retryDelay = TimeSpan.FromMilliseconds(
                Math.Min(retryDelay.TotalMilliseconds * 2, _maxSignalRetryDelay.TotalMilliseconds)
            );
        }

        if (cancellationToken.IsCancellationRequested)
            return ResultExtensions.TaskIsCancelled(nameof(ConnectToPrimaryInstanceAsync));

        return Result.Fail("Timed out while connecting to the primary desktop instance");
    }

    internal static async ValueTask<bool> IsRelaunchSignalAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[SIGNAL_MESSAGE.Length];
        var bytesRead = await stream.ReadAtLeastAsync(
            buffer,
            buffer.Length,
            throwOnEndOfStream: false,
            cancellationToken
        );
        return bytesRead == buffer.Length && Encoding.UTF8.GetString(buffer) == SIGNAL_MESSAGE;
    }

    private async Task ListenAsync(Func<CancellationToken, Task<Result>> onSignal, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _instanceName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );

                await server.WaitForConnectionAsync(cancellationToken);
                if (!await IsRelaunchSignalAsync(server, cancellationToken))
                {
                    _log.Here().Warning("Received unknown or incomplete desktop relaunch signal");
                    continue;
                }

                var result = await onSignal(cancellationToken);
                if (result.IsFailed)
                    result.LogError();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                Result.Fail(new ExceptionalError(e)).LogError();
            }
        }
    }
}
