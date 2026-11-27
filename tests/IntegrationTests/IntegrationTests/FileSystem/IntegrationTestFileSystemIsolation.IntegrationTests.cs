using Autofac;
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

    private sealed class ShutdownProbe : IHostedService
    {
        public ShutdownProbe() { }

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
