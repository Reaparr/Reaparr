using System.Runtime.CompilerServices;
using Autofac;
using Reaparr.Environment;

namespace Reaparr.Data.UnitTests;

public class ReaparrDbContextFactoryUnitTests
{
    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "MA0042",
        Justification = "This test deliberately compares the synchronous and asynchronous factory APIs."
    )]
    public async Task ShouldCreateIndependentConfiguredContexts_WhenUsingBothCreationMethods()
    {
        // Arrange
        await using var container = CreateContainer();
        var factory = container.Resolve<IReaparrDbContextFactory>();
        var pathProvider = container.Resolve<IPathProvider>();

        // Act
        using var syncContext = factory.Create();
        using var asyncContext = await factory.CreateAsync();

        // Assert
        syncContext.ShouldNotBeSameAs(asyncContext);
        syncContext.DatabaseName.ShouldBe(pathProvider.DatabaseName);
        asyncContext.DatabaseName.ShouldBe(pathProvider.DatabaseName);
    }

    [Test]
    public void ShouldCollectDisposedSyncContext_WhenFactoryAndRootContainerRemainAlive()
    {
        // Arrange
        using var container = CreateContainer();
        var factory = container.Resolve<IReaparrDbContextFactory>();

        // Act
        var contextReference = CreateAndDisposeSyncContext(factory);
        ForceCollection();
        GC.KeepAlive(factory);
        GC.KeepAlive(container);

        // Assert
        contextReference.TryGetTarget(out _).ShouldBeFalse();
    }

    [Test]
    public void ShouldCollectDisposedAsyncContext_WhenFactoryAndRootContainerRemainAlive()
    {
        // Arrange
        using var container = CreateContainer();
        var factory = container.Resolve<IReaparrDbContextFactory>();

        // Act
        var contextReference = CreateAndDisposeAsyncContext(factory).GetAwaiter().GetResult();
        ForceCollection();
        GC.KeepAlive(factory);
        GC.KeepAlive(container);

        // Assert
        contextReference.TryGetTarget(out _).ShouldBeFalse();
    }

    [Test]
    public void ShouldDisposeDirectlyResolvedContext_WhenLifetimeScopeIsDisposed()
    {
        // Arrange
        using var container = CreateContainer();
        var scope = container.BeginLifetimeScope();
        var context = scope.Resolve<IReaparrDbContext>();
        _ = context.PlexAccounts;
        context.ClearChangeTracker();

        // Act
        scope.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => context.ClearChangeTracker());
    }

    private static IContainer CreateContainer()
    {
        var runtimeInfo = new MockAppRuntimeInfo();
        var pathProvider = new MockPathProvider($"factory-{Guid.NewGuid():N}", appRuntimeInfo: runtimeInfo);
        var builder = new ContainerBuilder();
        builder.RegisterInstance(LogFactory.Create()).As<ILogger>().SingleInstance();
        builder.RegisterInstance(runtimeInfo).As<IAppRuntimeInfo>().SingleInstance();
        builder.RegisterInstance(pathProvider).As<IPathProvider>().SingleInstance();
        builder.RegisterModule<DataModule>();
        return builder.Build();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<IReaparrDbContext> CreateAndDisposeSyncContext(IReaparrDbContextFactory factory)
    {
        var context = factory.Create();
        _ = context.PlexAccounts;
        context.ClearChangeTracker();
        var reference = new WeakReference<IReaparrDbContext>(context);
        context.Dispose();
        return reference;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference<IReaparrDbContext>> CreateAndDisposeAsyncContext(
        IReaparrDbContextFactory factory
    )
    {
        var context = await factory.CreateAsync();
        _ = context.PlexAccounts;
        context.ClearChangeTracker();
        var reference = new WeakReference<IReaparrDbContext>(context);
        context.Dispose();
        return reference;
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
