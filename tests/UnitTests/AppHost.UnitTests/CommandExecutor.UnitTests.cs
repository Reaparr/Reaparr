using System.Runtime.CompilerServices;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Reaparr.Application;
using Reaparr.Application.Contracts;
using Reaparr.Data;
using Reaparr.Data.Contracts;
using Reaparr.Environment;

namespace Reaparr.AppHost.UnitTests;

// FastEndpoints has one process-wide resolver; these tests must not replace it concurrently.
[NotInParallel("FastEndpointsResolver")]
public class CommandExecutorUnitTests
{
    [Test]
    public async Task ShouldDisposeHandlerAndMiddlewareDependencies_WhenBackgroundDispatchCompletes()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();

        // Act
        var result = await executor.Send(new LifetimeCommand());

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        state.ValidationCalls.ShouldBe(1);
        state.HandlerCalls.ShouldBe(1);
        state.Markers.Select(x => x.Disposed).ShouldAllBe(x => x);
        state.HandlerMarker!.Disposed.ShouldBeTrue();
        state.MiddlewareMarker!.Disposed.ShouldBeTrue();
        state.HandlerScope.ShouldBe(state.MiddlewareScope);
        state.HandlerContext.ShouldNotBeSameAs(state.MiddlewareContext);
        Should.Throw<ObjectDisposedException>(() => state.HandlerContext!.ClearChangeTracker());
        Should.Throw<ObjectDisposedException>(() => state.MiddlewareContext!.ClearChangeTracker());
    }

    [Test]
    public async Task ShouldRecoverAfterFirstMiddlewareActivationFails_WhenNextCommandIsDispatched()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();
        state.ThrowMiddlewareActivation = true;

        // Act
        var failed = await executor.Send(new LifetimeCommand());
        state.ThrowMiddlewareActivation = false;
        var recovered = await executor.Send(new LifetimeCommand());

        // Assert
        failed.IsFailed.ShouldBeTrue();
        failed.Errors.Count.ShouldBe(1);
        recovered.IsSuccess.ShouldBeTrue();
        recovered.Errors.Count.ShouldBe(0);
        state.HandlerCalls.ShouldBe(1);
        state.ValidationCalls.ShouldBe(1);
        state.Markers.ShouldAllBe(x => x.Disposed);
    }

    [Test]
    [Arguments("failure")]
    [Arguments("handler")]
    [Arguments("middleware")]
    [Arguments("activation")]
    [Arguments("cancel")]
    [Arguments("invalid")]
    public async Task ShouldReleaseScopeAndRestoreResolution_WhenDispatchDoesNotSucceed(string outcome)
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();
        using var cancellation = new CancellationTokenSource();
        state.ThrowActivation = outcome == "activation";
        if (outcome == "cancel")
            await cancellation.CancelAsync();

        // Act
        var result = await executor.Send(new LifetimeCommand(outcome), cancellation.Token);
        var firstMarkers = state.Markers.ToArray();
        state.ThrowActivation = false;
        var nextResult = await executor.Send(new LifetimeCommand());

        // Assert
        result.IsFailed.ShouldBeTrue();
        result.Errors.Count.ShouldBe(1);
        firstMarkers.ShouldAllBe(x => x.Disposed);
        nextResult.IsSuccess.ShouldBeTrue();
        nextResult.Errors.Count.ShouldBe(0);
        state.Markers.ShouldAllBe(x => x.Disposed);
        state.HandlerCalls.ShouldBe(outcome is "invalid" or "activation" or "middleware" or "cancel" ? 1 : 2);
    }

    [Test]
    public async Task ShouldDisposeScopeAndPreserveCancellation_WhenRunningHandlerIsCancelled()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var execution = executor.Send(new LifetimeCommand("wait", entered, release), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        state.HandlerCalls.ShouldBe(1);
        var context = state.HandlerContext!;
        context.ClearChangeTracker();
        await cancellation.CancelAsync();
        var cancelled = await execution;
        var cancelledScope = state.HandlerScope;
        var next = await executor.Send(new LifetimeCommand());

        // Assert
        cancelled.IsCancelled.ShouldBeTrue();
        cancelled.IsFailed.ShouldBeTrue();
        cancelled.Errors.Count.ShouldBe(1);
        cancelled
            .Errors.Single()
            .ShouldBeOfType<ExceptionalError>()
            .Exception.ShouldBeAssignableTo<OperationCanceledException>()
            .CancellationToken.ShouldBe(cancellation.Token);
        Should.Throw<ObjectDisposedException>(() => context.ClearChangeTracker());
        next.IsSuccess.ShouldBeTrue();
        next.Errors.Count.ShouldBe(0);
        state.HandlerCalls.ShouldBe(2);
        state.HandlerScope.ShouldNotBe(cancelledScope);
        state.Markers.ShouldAllBe(marker => marker.Disposed);
    }

    [Test]
    public async Task ShouldKeepOperationAliveUntilEventCommandsFinish_WhenPublishingWithWaitForAll()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var execution = executor.Send(new LifetimeCommand("event", entered, release));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        execution.IsCompleted.ShouldBeFalse();
        state.HandlerContext!.ClearChangeTracker();
        release.SetResult();
        var result = await execution;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        state.HandlerCalls.ShouldBe(2);
        state.ScopeIds.Distinct().Count().ShouldBe(1);
        state.Markers.ShouldAllBe(marker => marker.Disposed);
    }

    [Test]
    public async Task ShouldReuseOuterScope_WhenCommandDispatchesNestedCommand()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();

        // Act
        var result = await executor.Send(new LifetimeCommand("nested"));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        state.HandlerCalls.ShouldBe(2);
        state.ValidationCalls.ShouldBe(2);
        state.ScopeIds.Distinct().Count().ShouldBe(1);
        state.Markers.ShouldAllBe(x => x.Disposed);
    }

    [Test]
    public async Task ShouldKeepRequestDependenciesAlive_WhenCommandCompletesInsideHttpRequest()
    {
        // Arrange
        await using var container = CreateContainer();
        var accessor = container.Resolve<IHttpContextAccessor>();
        var scope = container.BeginLifetimeScope();
        accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.Resolve<IServiceProvider>() };
        var state = container.Resolve<ExecutionState>();

        // Act
        var result = await container.Resolve<ICommandExecutor>().Send(new LifetimeCommand());
        accessor.HttpContext = null;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        state.Markers.ShouldAllBe(x => !x.Disposed);
        state.HandlerContext!.ClearChangeTracker();
        await scope.DisposeAsync();
        state.Markers.ShouldAllBe(x => x.Disposed);
        Should.Throw<ObjectDisposedException>(() => state.HandlerContext.ClearChangeTracker());
    }

    [Test]
    public async Task ShouldIsolateScopes_WhenIndependentCommandsOverlap()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = container.Resolve<ExecutionState>();

        // Act
        var first = executor.Send(new LifetimeCommand("wait", entered, release));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var firstScope = state.HandlerScope;
        var second = await executor.Send(new LifetimeCommand());
        var secondScope = state.HandlerScope;
        release.SetResult();
        var firstResult = await first;

        // Assert
        firstResult.IsSuccess.ShouldBeTrue();
        firstResult.Errors.Count.ShouldBe(0);
        second.IsSuccess.ShouldBeTrue();
        second.Errors.Count.ShouldBe(0);
        firstScope.ShouldNotBe(secondScope);
        state.Markers.ShouldAllBe(x => x.Disposed);
    }

    [Test]
    public async Task ShouldRejectInheritedProviderAfterDisposal_WhenUnawaitedWorkAttemptsDispatch()
    {
        // Arrange
        await using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();
        var state = container.Resolve<ExecutionState>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        var result = await executor.Send(new LifetimeCommand("escape", Release: release));
        release.SetResult();
        var escaped = await state.EscapedDispatch!;

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Errors.Count.ShouldBe(0);
        escaped.IsFailed.ShouldBeTrue();
        escaped.Errors.Count.ShouldBe(1);
        state.HandlerCalls.ShouldBe(1);
        state.Markers.ShouldAllBe(x => x.Disposed);
    }

    [Test]
    public void ShouldCollectCompletedContexts_WhenRootAndExecutorRemainAliveAcrossIntervals()
    {
        // Arrange
        using var container = CreateContainer();
        var executor = container.Resolve<ICommandExecutor>();

        // Act
        var references = new List<WeakReference<IReaparrDbContext>>();
        for (var interval = 0; interval < 3; interval++)
        {
            references.AddRange(RunCompletedOperations(executor, container.Resolve<ExecutionState>()));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert completed contexts do not accumulate while the same root remains alive.
            foreach (var reference in references)
                reference.TryGetTarget(out _).ShouldBeFalse();
        }
        GC.KeepAlive(executor);
        GC.KeepAlive(container);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference<IReaparrDbContext>> RunCompletedOperations(
        ICommandExecutor executor,
        ExecutionState state
    )
    {
        var references = new List<WeakReference<IReaparrDbContext>>();
        for (var i = 0; i < 12; i++)
        {
            var result = executor.Send(new LifetimeCommand()).GetAwaiter().GetResult();
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
            references.Add(new(state.HandlerContext!));
            references.Add(new(state.MiddlewareContext!));
            state.HandlerContext = null;
            state.MiddlewareContext = null;
        }
        return references;
    }

    private static IContainer CreateContainer()
    {
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddMessaging([new List<Type> { typeof(LifetimeHandler), typeof(LifetimeEventHandler) }]);
        services.AddCommandMiddleware(c =>
        {
            c.Register(typeof(ValidationPipeline<,>));
            c.Register<LifetimeCommand, Result, LifetimeMiddleware>();
        });
        var runtime = new MockAppRuntimeInfo();
        var builder = new ContainerBuilder();
        builder.Populate(services);
        builder.RegisterModule<FastEndpointsModule>();
        builder.RegisterModule<DataModule>();
        builder.RegisterInstance(LogFactory.Create()).As<ILogger>();
        builder.RegisterInstance(runtime).As<IAppRuntimeInfo>();
        builder.RegisterInstance(new MockPathProvider("command-lifetime", appRuntimeInfo: runtime)).As<IPathProvider>();
        builder.RegisterType<ExecutionState>().SingleInstance();
        builder.RegisterType<ScopeIdentity>().InstancePerLifetimeScope();
        builder.RegisterType<DisposalMarker>().InstancePerDependency();
        builder.RegisterType<LifetimeValidator>().As<IValidator<LifetimeCommand>>();
        var container = builder.Build();
        container.Resolve<IServiceProvider>().UseMessaging();
        return container;
    }

    public sealed record LifetimeCommand(
        string Outcome = "success",
        TaskCompletionSource? Entered = null,
        TaskCompletionSource? Release = null
    ) : ICommand<Result>;

    public sealed class ExecutionState
    {
        public ExecutionState() { }

        public int ValidationCalls;
        public int HandlerCalls;
        public bool ThrowActivation;
        public bool ThrowMiddlewareActivation;
        public Guid HandlerScope;
        public Guid MiddlewareScope;
        public IReaparrDbContext? HandlerContext;
        public IReaparrDbContext? MiddlewareContext;
        public Task<Result>? EscapedDispatch;
        public List<Guid> ScopeIds { get; } = [];
        public List<DisposalMarker> Markers { get; } = [];
        public DisposalMarker? HandlerMarker;
        public DisposalMarker? MiddlewareMarker;
    }

    public sealed class ScopeIdentity
    {
        public ScopeIdentity() { }

        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class DisposalMarker : IDisposable
    {
        public DisposalMarker(ExecutionState state) => state.Markers.Add(this);

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    public sealed class LifetimeValidator : AbstractValidator<LifetimeCommand>
    {
        public LifetimeValidator(ExecutionState state)
        {
            RuleFor(x => x.Outcome)
                .Must(value =>
                {
                    state.ValidationCalls++;
                    return value != "invalid";
                });
        }
    }

    public sealed record LifetimeEvent(TaskCompletionSource Entered, TaskCompletionSource Release) : IEvent;

    public sealed class LifetimeEventHandler : IEventHandler<LifetimeEvent>
    {
        private readonly ICommandExecutor _executor;

        public LifetimeEventHandler(ICommandExecutor executor)
        {
            _executor = executor;
        }

        public async Task HandleAsync(LifetimeEvent notification, CancellationToken ct)
        {
            notification.Entered.SetResult();
            await notification.Release.Task.WaitAsync(ct);
            var result = await _executor.Send(new LifetimeCommand(), ct);
            result.IsSuccess.ShouldBeTrue();
            result.Errors.Count.ShouldBe(0);
        }
    }

    public sealed class LifetimeHandler : ICommandHandler<LifetimeCommand, Result>
    {
        private readonly IReaparrDbContext _context;
        private readonly ScopeIdentity _scope;
        private readonly ExecutionState _state;
        private readonly ICommandExecutor _executor;
        private readonly IEventPublisher _eventPublisher;

        public LifetimeHandler(
            IReaparrDbContext context,
            ScopeIdentity scope,
            ExecutionState state,
            ICommandExecutor executor,
            DisposalMarker marker,
            IEventPublisher eventPublisher
        )
        {
            _context = context;
            _scope = scope;
            _state = state;
            _executor = executor;
            _eventPublisher = eventPublisher;
            state.HandlerMarker = marker;
            if (state.ThrowActivation)
                throw new InvalidOperationException("activation");
        }

        public async Task<Result> ExecuteAsync(LifetimeCommand command, CancellationToken ct)
        {
            _state.HandlerCalls++;
            _state.HandlerScope = _scope.Id;
            _state.ScopeIds.Add(_scope.Id);
            _state.HandlerContext = _context;
            _context.ClearChangeTracker();
            if (command.Outcome == "handler")
                throw new InvalidOperationException("handler");
            if (command.Outcome == "cancel")
                ct.ThrowIfCancellationRequested();
            if (command.Outcome == "failure")
                return Result.Fail("expected");
            if (command.Outcome == "event")
                await _eventPublisher.PublishAsync(
                    new LifetimeEvent(command.Entered!, command.Release!),
                    Mode.WaitForAll,
                    ct
                );
            if (command.Outcome == "nested")
            {
                var nested = await _executor.Send(new LifetimeCommand(), ct);
                _context.ClearChangeTracker();
                return nested;
            }
            if (command.Outcome == "escape")
            {
                _state.EscapedDispatch = Task.Run(
                    async () =>
                    {
                        await command.Release!.Task;
                        return await _executor.Send(new LifetimeCommand());
                    },
                    CancellationToken.None
                );
            }
            if (command.Outcome == "wait")
            {
                command.Entered!.SetResult();
                await command.Release!.Task.WaitAsync(ct);
                _context.ClearChangeTracker();
            }
            return Result.Ok();
        }
    }

    public sealed class LifetimeMiddleware : ICommandMiddleware<LifetimeCommand, Result>
    {
        private readonly IReaparrDbContext _context;
        private readonly ScopeIdentity _scope;
        private readonly ExecutionState _state;

        public LifetimeMiddleware(
            IReaparrDbContext context,
            ScopeIdentity scope,
            ExecutionState state,
            DisposalMarker marker
        )
        {
            _context = context;
            _scope = scope;
            _state = state;
            state.MiddlewareMarker = marker;
            if (state.ThrowMiddlewareActivation)
                throw new InvalidOperationException("middleware activation");
        }

        public async Task<Result> ExecuteAsync(
            LifetimeCommand command,
            CommandDelegate<Result> next,
            CancellationToken ct
        )
        {
            _state.MiddlewareContext = _context;
            _state.MiddlewareScope = _scope.Id;
            _context.ClearChangeTracker();
            if (command.Outcome == "middleware")
                throw new InvalidOperationException("middleware");
            var result = await next();
            _context.ClearChangeTracker();
            return result;
        }
    }
}
