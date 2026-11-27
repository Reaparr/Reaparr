using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace Reaparr.IntegrationTests;

public class IntegrationTestFileSystemIsolationIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldUseUniqueSandboxAndCleanupWhenContainerIsDisposed()
    {
        string firstSandboxPath;
        string secondSandboxPath;

        using (var firstContainer = await CreateContainer(new Seed(101)))
        {
            firstSandboxPath = firstContainer.TestFileSystemRootPath;
            Directory.Exists(firstSandboxPath).ShouldBeTrue();

            using (var secondContainer = await CreateContainer(new Seed(202)))
            {
                secondSandboxPath = secondContainer.TestFileSystemRootPath;
                secondSandboxPath.ShouldNotBe(firstSandboxPath);
                Directory.Exists(secondSandboxPath).ShouldBeTrue();
            }

            Directory.Exists(secondSandboxPath).ShouldBeFalse();
        }

        Directory.Exists(firstSandboxPath).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldRemoveOnlyFailedSandbox_WhenFilesystemSetupThrows()
    {
        // Arrange
        using var retained = await CreateContainer(303);
        var failure = new InvalidOperationException("Fixture setup failed");
        string? failedSandbox = null;

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            CreateContainer(404, config => config.FileSystemOptions = (_, db) =>
            {
                failedSandbox = IntegrationTestFileSystemSandbox.GetSandboxFolder(db.DatabaseName);
                Directory.Exists(failedSandbox).ShouldBeTrue();
                throw failure;
            })
        );

        // Assert
        exception.ShouldBeSameAs(failure);
        Directory.Exists(failedSandbox).ShouldBeFalse();
        Directory.Exists(retained.TestFileSystemRootPath).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldKeepSandboxUntilHostedShutdownCompletes()
    {
        // Arrange
        var probe = new ShutdownProbe();
        var container = await CreateContainer(505, config =>
            config.OverrideServices = builder => builder.RegisterInstance(probe).As<IHostedService>()
        );
        var sandbox = container.TestFileSystemRootPath;
        Directory.Exists(sandbox).ShouldBeTrue();

        // Act
        var disposal = Task.Run(container.Dispose);
        try
        {
            await probe.Stopping.Task.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            Directory.Exists(sandbox).ShouldBeTrue();
            disposal.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            probe.Release.TrySetResult();
            await disposal.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken);
        }

        // Assert
        Directory.Exists(sandbox).ShouldBeFalse();
    }

    [Test]
    public async Task ShouldReleaseHostAndRemoveOnlyItsSandbox_WhenHostedStopThrows()
    {
        // Arrange
        using var neighbor = await CreateContainer(606);
        var failure = new InvalidOperationException("Hosted stop failed");
        var probe = new FailingShutdownProbe(failure);
        var container = await CreateContainer(707, config =>
            config.OverrideServices = builder => builder.RegisterInstance(probe).As<IHostedService>()
        );
        var sandbox = container.TestFileSystemRootPath;
        Directory.Exists(sandbox).ShouldBeTrue();

        // Act
        var exception = Should.Throw<InvalidOperationException>(container.Dispose);

        // Assert
        exception.ShouldBeSameAs(failure);
        probe.Disposed.ShouldBeTrue();
        Directory.Exists(sandbox).ShouldBeFalse();
        Directory.Exists(neighbor.TestFileSystemRootPath).ShouldBeTrue();
    }

    [Test]
    public async Task ShouldUseOneConfigurationForSeedingAndHosting_WhenOptionsHaveState()
    {
        // Arrange
        var revision = 0;

        // Act
        using var container = await CreateContainer(808, config =>
        {
            var configuredRevision = ++revision;
            config.DatabaseOptions = database => database.PlexServerCount = configuredRevision;
            config.DownloadSpeedLimitInKib = 100 * configuredRevision;
            config.OverrideServices = builder => builder.RegisterInstance(new Seed(configuredRevision));
        });

        // Assert
        var server = await container.DbContext.PlexServers.SingleAsync(CancellationToken);
        container.GetServerSettings.GetDownloadSpeedLimit(server.MachineIdentifier).ShouldBe(100);
        container.Resolve<Seed>().Next().ShouldBe(new Seed(1).Next());
        revision.ShouldBe(1);
    }

    [Test]
    public async Task ShouldKeepSeededMemoryDatabaseUntilFixtureDisposal()
    {
        // Arrange
        string databaseName;

        // Act
        using (var container = await CreateContainer(909, config =>
        {
            config.OverrideAppRuntimeInfo = runtime => runtime.IsIntegrationTestMode = false;
            config.DatabaseOptions = database => database.PlexServerCount = 2;
        }))
        {
            databaseName = container.DatabaseName;
            (await container.DbContext.PlexServers.CountAsync(CancellationToken)).ShouldBe(2);
        }

        // Assert
        await using var connection = new SqliteConnection(
            DbContextConnections.GetConnectionString(databaseName, SqliteOpenMode.Memory)
        );
        await connection.OpenAsync(CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'";
        (await command.ExecuteScalarAsync(CancellationToken)).ShouldBe(0L);
    }

    [Test]
    public async Task ShouldKeepScopeOwnedContextUsableAfterApplyingSpeedLimit()
    {
        // Arrange
        const int speedLimit = 128;

        // Act
        using var container = await CreateContainer(910, config =>
        {
            config.DownloadSpeedLimitInKib = speedLimit;
            config.OverrideServices = builder => builder
                .Register(context => MockDatabase.GetMemoryReaparrDbContext(
                    context.Resolve<ILogger>(),
                    context.Resolve<IPathProvider>(),
                    context.Resolve<IAppRuntimeInfo>()
                ))
                .InstancePerLifetimeScope();
        });

        // Assert
        var server = await container.ReaparrDbContext.PlexServers.SingleAsync(CancellationToken);
        container.GetServerSettings.GetDownloadSpeedLimit(server.MachineIdentifier).ShouldBe(speedLimit);
    }

    private sealed class FailingShutdownProbe : IHostedService, IDisposable
    {
        private readonly Exception _failure;

        public FailingShutdownProbe(Exception failure) => _failure = failure;

        public bool Disposed { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.FromException(_failure);

        public void Dispose() => Disposed = true;
    }

    private sealed class ShutdownProbe : IHostedService
    {
        public TaskCompletionSource Stopping { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopping.TrySetResult();
            return Release.Task.WaitAsync(cancellationToken);
        }
    }
}
