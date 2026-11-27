using System.Net.Http.Headers;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Reaparr.FileSystem.Contracts;
using Reaparr.Settings.Contracts;

namespace Reaparr.IntegrationTests;

public class BaseContainer : IDisposable
{
    private readonly ReaparrWebApplicationFactory _factory;

    private readonly ILogger _log;

    private readonly ILifetimeScope _lifeTimeScope;
    private readonly string _databasePath;

    public string DatabaseName => _factory.MemoryDbName;

    public string TestFileSystemRootPath { get; }

    /// <summary>
    /// Creates an Autofac container and sets up a test database.
    /// </summary>
    private BaseContainer(
        ILogger log,
        ReaparrWebApplicationFactory factory,
        string testFileSystemRootPath,
        string databasePath
    )
    {
        _log = log.ForContext<BaseContainer>();

        TestFileSystemRootPath = testFileSystemRootPath;
        _databasePath = databasePath;

        _factory = factory;

        // Create a separate scope as not to interfere with tests running in parallel
        _lifeTimeScope = _factory.Services.GetAutofacRoot().BeginLifetimeScope();
    }

    public static async Task<BaseContainer> Create(
        ILogger log,
        Seed seed,
        Action<UnitTestDataConfig>? options = null,
        bool useTestAuthentication = true
    )
    {
        // Program.Main owns the process runtime outside DI, so integration startup must set this before it can exit the test process.
        System.Environment.SetEnvironmentVariable(EnvKeys.IntegrationTestMode, bool.TrueString);

        var config = UnitTestDataConfig.FromOptions(options);
        var memoryDbName = MockDatabase.GetMemoryDatabaseName();
        var mockAppRuntimeInfo = ResolveRuntimeInfo(config);
        var mockAppBuildInfo = config.OverrideAppBuildInfo ?? new MockAppBuildInfo();
        var mockPathProvider = new MockPathProvider(memoryDbName, mockAppBuildInfo, mockAppRuntimeInfo);

        var testFileSystemRootPath = IntegrationTestFileSystemSandbox.GetSandboxFolder(memoryDbName);

        ReaparrWebApplicationFactory? factory = null;
        BaseContainer? container = null;
        try
        {
            testFileSystemRootPath = IntegrationTestFileSystemSandbox.Create(memoryDbName, log, mockPathProvider);
            log.Information("Initialized integration test with database name: {DatabaseName}", memoryDbName);
            var (reaparrContext, authContext) = MockDatabase.GetMemoryDbContext(
                log,
                mockPathProvider,
                mockAppRuntimeInfo,
                memoryDbName
            );
            await using (reaparrContext)
            await using (authContext)
            {
                await (reaparrContext, authContext).Setup(seed, mockPathProvider, mockAppRuntimeInfo, config.DatabaseOptions);
            }

            factory = new ReaparrWebApplicationFactory(seed, memoryDbName, options, useTestAuthentication);
            container = new BaseContainer(log, factory, testFileSystemRootPath, mockPathProvider.DatabasePath);

            if (config.FileSystemOptions is not null)
            {
                var fileSystem = container.Resolve<IFileSystem>();
                var dbContext = container.Resolve<IReaparrDbContext>();
                config.FileSystemOptions.Invoke(fileSystem, dbContext);
            }

            if (config.DownloadSpeedLimitInKib > 0)
                await container.SetDownloadSpeedLimit(options);

            return container;
        }
        catch
        {
            try
            {
                try
                {
                    if (container is not null)
                        await container._lifeTimeScope.DisposeAsync();
                }
                finally
                {
                    if (factory is not null)
                        await factory.DisposeAsync();
                }
            }
            catch (Exception cleanupFailure)
            {
                log.Here().Error(cleanupFailure, "Failed to dispose incomplete fixture {DatabaseName}", memoryDbName);
            }
            finally
            {
                DeleteSandbox(log, memoryDbName, testFileSystemRootPath, mockPathProvider.DatabasePath);
            }

            throw;
        }
    }

    private static MockAppRuntimeInfo ResolveRuntimeInfo(UnitTestDataConfig config)
    {
        var runtimeInfo = new MockAppRuntimeInfo { IsUnmasked = true, IsIntegrationTestMode = true };
        config.OverrideAppRuntimeInfo?.Invoke(runtimeInfo);
        return runtimeInfo;
    }

    public HttpClient GetApiClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(scheme: "TestScheme");
        return client;
    }

    public IDownloadQueue GetDownloadQueue => Resolve<IDownloadQueue>();

    public IPathProvider PathProvider => Resolve<IPathProvider>();

    public ReaparrDbContext ReaparrDbContext => Resolve<ReaparrDbContext>();

    public IScheduler BackgroundJobScheduler => Resolve<IScheduler>();

    public IDownloadTaskScheduler DownloadTaskScheduler => Resolve<IDownloadTaskScheduler>();

    public IMoveDownloadFileScheduler MoveDownloadFileScheduler => Resolve<IMoveDownloadFileScheduler>();

    public MockDownloadHubService MockDownloadHubService => (MockDownloadHubService)Resolve<IDownloadHubService>();

    public MockProgressHubService MockProgressHubService => (MockProgressHubService)Resolve<IProgressHubService>();

    public IServerSettingsModule GetServerSettings => Resolve<IServerSettingsModule>();

    public IReaparrDbContext DbContext => Resolve<IReaparrDbContext>();

    public async Task SetDownloadSpeedLimit(Action<UnitTestDataConfig>? options = null)
    {
        var config = new UnitTestDataConfig();
        options?.Invoke(config);

        var plexServers = await ReaparrDbContext.PlexServers.ToListAsync();
        foreach (var plexServer in plexServers)
            GetServerSettings.SetDownloadSpeedLimit(plexServer.MachineIdentifier, config.DownloadSpeedLimitInKib);
    }

    public T Resolve<T>()
        where T : notnull => _lifeTimeScope.Resolve<T>();

    /// <summary>
    /// Waits for a download task to reach one of the specified statuses by monitoring SignalR progress updates.
    /// This is event-driven rather than polling-based, making tests more reliable.
    /// </summary>
    /// <param name="downloadTaskId">The GUID of the download task to monitor.</param>
    /// <param name="targetStatuses">The statuses to wait for.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final download progress DTO when the target status is reached.</returns>
    public async Task<DownloadProgressDTO?> WaitForDownloadStatusAsync(
        Guid downloadTaskId,
        DownloadStatus[] targetStatuses,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow.Add(timeout.Value);
        var pollInterval = TimeSpan.FromMilliseconds(500);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            // Check SignalR events (non-blocking with short timeout)
            try
            {
                if (MockDownloadHubService.ServerDownloadProgressList.TryTake(out var progress, 100, cancellationToken))
                {
                    var matchingDownload = FindDownloadById(progress.Downloads, downloadTaskId);
                    if (matchingDownload != null && targetStatuses.Contains(matchingDownload.Status))
                    {
                        _log.Here()
                            .Debug(
                                "Download task {DownloadTaskId} reached status {Status} via SignalR",
                                downloadTaskId,
                                matchingDownload.Status
                            );
                        return matchingDownload;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Poll database as fallback using a fresh DbContext so status changes made by
            // background jobs are observed even when the shared test scope is tracking older entities.
            using var dbContext = await Resolve<IReaparrDbContextFactory>().CreateAsync();
            var dbTask = await dbContext.GetDownloadTaskAsync(
                downloadTaskId,
                cancellationToken: CancellationToken.None
            );
            if (dbTask != null && targetStatuses.Contains(dbTask.DownloadStatus))
            {
                _log.Here()
                    .Debug(
                        "Download task {DownloadTaskId} reached status {Status} via database poll",
                        downloadTaskId,
                        dbTask.DownloadStatus
                    );
                return new DownloadProgressDTO
                {
                    Id = dbTask.Id,
                    Title = dbTask.Title,
                    MediaType = dbTask.MediaType,
                    Status = dbTask.DownloadStatus,
                    Percentage = dbTask.Percentage,
                    DataReceived = dbTask.DataReceived,
                    DataTotal = dbTask.DataTotal,
                    DownloadSpeed = dbTask.DownloadSpeed,
                    TimeRemaining = 0,
                    Children = [],
                };
            }

            await Task.Delay(pollInterval, cancellationToken);
        }

        // Final database check with current status logging
        using var finalDbContext = await Resolve<IReaparrDbContextFactory>().CreateAsync();
        var finalDbTask = await finalDbContext.GetDownloadTaskAsync(
            downloadTaskId,
            cancellationToken: CancellationToken.None
        );
        if (finalDbTask != null)
        {
            _log.Here()
                .Warning(
                    "Timeout waiting for download task {DownloadTaskId} to reach status {Statuses}. Current status: {CurrentStatus}",
                    downloadTaskId,
                    string.Join(", ", targetStatuses),
                    finalDbTask.DownloadStatus
                );
        }

        return null;
    }

    /// <summary>
    /// Waits for all specified download tasks to reach one of the target statuses.
    /// </summary>
    public bool WaitForAllDownloadsStatusAsync(
        Guid[] downloadTaskIds,
        DownloadStatus[] targetStatuses,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var completedTasks = new HashSet<Guid>();

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout.Value);

        try
        {
            while (!cts.Token.IsCancellationRequested && completedTasks.Count < downloadTaskIds.Length)
            {
                if (MockDownloadHubService.ServerDownloadProgressList.TryTake(out var progress, 100, cts.Token))
                {
                    foreach (var taskId in downloadTaskIds.Where(id => !completedTasks.Contains(id)))
                    {
                        var download = FindDownloadById(progress.Downloads, taskId);
                        if (download != null && targetStatuses.Contains(download.Status))
                        {
                            completedTasks.Add(taskId);
                            _log.Here()
                                .Debug(
                                    "Download task {DownloadTaskId} reached status {Status} ({Completed}/{Total})",
                                    taskId,
                                    download.Status,
                                    completedTasks.Count,
                                    downloadTaskIds.Length
                                );
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _log.Here()
                .Warning(
                    "Timeout waiting for downloads. Completed: {Completed}/{Total}",
                    completedTasks.Count,
                    downloadTaskIds.Length
                );
        }

        return completedTasks.Count == downloadTaskIds.Length;
    }

    private static DownloadProgressDTO? FindDownloadById(List<DownloadProgressDTO> downloads, Guid id)
    {
        foreach (var download in downloads)
        {
            if (download.Id == id)
                return download;

            var child = FindDownloadById(download.Children, id);
            if (child != null)
                return child;
        }

        return null;
    }

    public void Dispose()
    {
        // WebApplicationFactory.Dispose joins IHost.StopAsync before returning.
        try
        {
            _factory.Dispose();
        }
        finally
        {
            _lifeTimeScope.Dispose();
        }

        DeleteSandbox(_log, DatabaseName, TestFileSystemRootPath, _databasePath);
    }

    private static void DeleteSandbox(ILogger log, string dbName, string sandboxPath, string databasePath)
    {
        var cleanupResult = Result.Try(() =>
        {
            var expectedSandboxRoot = Path.GetFullPath(IntegrationTestFileSystemSandbox.GetSandboxFolder(dbName));
            var pathToDelete = Path.GetFullPath(sandboxPath);

            if (!pathToDelete.StartsWith(expectedSandboxRoot, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Refusing to delete '{pathToDelete}': path is not inside the expected sandbox root '{expectedSandboxRoot}'."
                );

            if (Directory.Exists(pathToDelete))
            {
                using var connection = new SqliteConnection(
                    DbContextConnections.GetConnectionString(databasePath, SqliteOpenMode.ReadWriteCreate)
                );
                SqliteConnection.ClearPool(connection);

                Directory.Delete(pathToDelete, recursive: true);
                log.Here()
                    .Information(
                        "Deleted test filesystem sandbox for container {DatabaseName}: {Path}",
                        dbName,
                        pathToDelete
                    );
            }
        });

        if (cleanupResult.IsFailed)
        {
            log.Here().Error("Failed to delete test filesystem sandbox for container {DatabaseName}", dbName);
            cleanupResult.LogError();
        }
    }
}
