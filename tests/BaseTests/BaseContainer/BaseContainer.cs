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
    private readonly SqliteConnection? _memoryDatabaseConnection;

    public string DatabaseName => _factory.MemoryDbName;

    public string TestFileSystemRootPath { get; }

    /// <summary>
    /// Creates an Autofac container and sets up a test database.
    /// </summary>
    private BaseContainer(
        ILogger log,
        ReaparrWebApplicationFactory factory,
        string testFileSystemRootPath,
        string databasePath,
        SqliteConnection? memoryDatabaseConnection
    )
    {
        _log = log.ForContext<BaseContainer>();

        TestFileSystemRootPath = testFileSystemRootPath;
        _databasePath = databasePath;
        _memoryDatabaseConnection = memoryDatabaseConnection;

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
        SqliteConnection? memoryDatabaseConnection = null;
        try
        {
            testFileSystemRootPath = IntegrationTestFileSystemSandbox.Create(memoryDbName, log, mockPathProvider);
            log.Information("Initialized integration test with database name: {DatabaseName}", memoryDbName);
            if (!mockAppRuntimeInfo.IsIntegrationTestMode)
            {
                // The named in-memory database disappears when its last connection closes.
                memoryDatabaseConnection = new SqliteConnection(
                    DbContextConnections.GetConnectionString(memoryDbName, SqliteOpenMode.Memory)
                );
                await memoryDatabaseConnection.OpenAsync();
            }

            var (reaparrContext, authContext) = MockDatabase.GetMemoryDbContext(
                log,
                mockPathProvider,
                mockAppRuntimeInfo,
                memoryDbName
            );
            await using (reaparrContext)
            await using (authContext)
            {
                await (reaparrContext, authContext).Setup(
                    seed,
                    mockPathProvider,
                    mockAppRuntimeInfo,
                    config.DatabaseOptions
                );
            }

            factory = new ReaparrWebApplicationFactory(
                seed,
                memoryDbName,
                config,
                mockAppBuildInfo,
                mockAppRuntimeInfo,
                useTestAuthentication
            );
            container = new BaseContainer(
                log,
                factory,
                testFileSystemRootPath,
                mockPathProvider.DatabasePath,
                memoryDatabaseConnection
            );

            if (config.FileSystemOptions is not null)
            {
                var fileSystem = container.Resolve<IFileSystem>();
                var dbContext = container.Resolve<IReaparrDbContext>();
                config.FileSystemOptions.Invoke(fileSystem, dbContext);
            }

            if (config.DownloadSpeedLimitInKib > 0)
                await container.SetDownloadSpeedLimit(config.DownloadSpeedLimitInKib);

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
                if (memoryDatabaseConnection is not null)
                {
                    var cleanup = await Result.Try(memoryDatabaseConnection.DisposeAsync);
                    cleanup.LogIfFailed();
                }

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

    private async Task SetDownloadSpeedLimit(int downloadSpeedLimitInKib)
    {
        var machineIdentifiers = await ReaparrDbContext
            .PlexServers.Select(server => server.MachineIdentifier)
            .ToListAsync();
        var settings = GetServerSettings;
        foreach (var machineIdentifier in machineIdentifiers)
            settings.SetDownloadSpeedLimit(machineIdentifier, downloadSpeedLimitInKib);
    }

    public T Resolve<T>()
        where T : notnull => _lifeTimeScope.Resolve<T>();

    public void Dispose()
    {
        Exception? firstFailure = null;
        try
        {
            _factory.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }

        try
        {
            _lifeTimeScope.Dispose();
        }
        catch (Exception exception)
        {
            if (firstFailure is not null)
                _log.Here().Error(exception, "Failed to release fixture scope for {DatabaseName}", DatabaseName);
            firstFailure ??= exception;
        }

        if (_memoryDatabaseConnection is not null)
        {
            try
            {
                _memoryDatabaseConnection.Dispose();
            }
            catch (Exception exception)
            {
                if (firstFailure is not null)
                    _log.Here()
                        .Error(exception, "Failed to close the memory database for {DatabaseName}", DatabaseName);
                firstFailure ??= exception;
            }
        }

        DeleteSandbox(_log, DatabaseName, TestFileSystemRootPath, _databasePath);

        if (firstFailure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
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
