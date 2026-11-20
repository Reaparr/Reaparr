using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Reaparr.Data.Contracts;

namespace Reaparr.AppHost.UnitTests;

public class StartupDatabaseUnitTests
{
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ShouldKeepDatabaseDependenciesAliveUntilSetupCompletes_AndDisposeThemAfterward(bool succeeds)
    {
        // Arrange
        var probe = new StartupDatabaseProbe(succeeds ? Result.Ok() : Result.Fail("database setup failed"));
        await using var app = CreateStartupApp(probe);

        // Act
        var setupTask = app.SetupDatabase();
        await probe.SetupStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        probe.DependencyDisposed.ShouldBeFalse();
        probe.ReleaseSetup.TrySetResult();

        var result = await setupTask;
        result.IsSuccess.ShouldBe(succeeds);
        result.Errors.Count.ShouldBe(succeeds ? 0 : 1);
        probe.DependencyDisposed.ShouldBeTrue();
    }

    [Test]
    public async Task ShouldDisposeDatabaseDependencies_WhenSetupThrows()
    {
        // Arrange
        var probe = new StartupDatabaseProbe(Result.Ok())
        {
            SetupException = new InvalidOperationException("database setup failed unexpectedly"),
        };
        await using var app = CreateStartupApp(probe);

        // Act
        var setupTask = app.SetupDatabase();
        await probe.SetupStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        probe.ReleaseSetup.TrySetResult();
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => setupTask);

        // Assert
        exception.Message.ShouldBe("database setup failed unexpectedly");
        probe.DependencyDisposed.ShouldBeTrue();
    }

    private static WebApplication CreateStartupApp(StartupDatabaseProbe probe)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
        {
            containerBuilder.RegisterInstance(probe).SingleInstance();
            containerBuilder.RegisterType<StartupDatabaseDependency>().InstancePerDependency();
            containerBuilder
                .RegisterType<StartupDatabaseManager>()
                .As<IReaparrDbContextManager>()
                .InstancePerDependency();
        });
        return builder.Build();
    }

    private sealed class StartupDatabaseManager : IReaparrDbContextManager
    {
        private readonly StartupDatabaseDependency _dependency;

        public StartupDatabaseManager(StartupDatabaseDependency dependency)
        {
            _dependency = dependency;
        }

        public async Task<Result> SetupAsync(CancellationToken cancellationToken = default)
        {
            _dependency.Probe.SetupStarted.TrySetResult();
            await _dependency.Probe.ReleaseSetup.Task.WaitAsync(cancellationToken);
            if (_dependency.Probe.SetupException is not null)
                throw _dependency.Probe.SetupException;

            return _dependency.Probe.SetupResult;
        }
    }

    private sealed class StartupDatabaseDependency : IDisposable
    {
        public StartupDatabaseDependency(StartupDatabaseProbe probe)
        {
            Probe = probe;
        }

        public StartupDatabaseProbe Probe { get; }

        public void Dispose()
        {
            Probe.DependencyDisposed = true;
        }
    }

    private sealed class StartupDatabaseProbe
    {
        public StartupDatabaseProbe(Result setupResult)
        {
            SetupResult = setupResult;
        }

        public Result SetupResult { get; }
        public Exception? SetupException { get; init; }

        public TaskCompletionSource SetupStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSetup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DependencyDisposed { get; set; }
    }
}
