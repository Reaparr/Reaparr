using System.Diagnostics;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Reaparr.Application;
using Reaparr.Application.Contracts;
using Reaparr.Data.Contracts;
using Reaparr.Environment;
using Reaparr.Identity.Contracts;
using Reaparr.Settings.Contracts;
using Reaparr.SignalR.Contracts;

namespace Reaparr.AppHost.UnitTests;

// These tests replace FastEndpoints' process-wide resolver and therefore cannot run concurrently with other resolver tests.
[NotInParallel("FastEndpointsResolver")]
public class LifecycleCommandOwnershipUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldDisposeDirectContexts_WhenBootDispatchesThroughTheRealCommandExecutor()
    {
        // Arrange
        await SetupDatabase(71601);
        SetAppRuntimeInfo(runtime => runtime.IsIntegrationTestMode = false);
        var probe = new ContextProbe();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(x => x.ApplicationStopping).Returns(CancellationToken.None);
        applicationLifetime.SetupGet(x => x.ApplicationStarted).Returns(CancellationToken.None);
        applicationLifetime.SetupGet(x => x.ApplicationStopped).Returns(CancellationToken.None);
        var scheduler = new Mock<IScheduler>(MockBehavior.Strict);
        scheduler
            .Setup(x =>
                x.QueryJobs(
                    It.Is<JobQuery>(query =>
                        query.Group != null
                        && query.Group.CompareToValue == nameof(JobTypes.LibrarySyncJob)
                        && query.Skip == 0
                        && query.Take == PagedQuery.DefaultTake
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(new PagedResult<JobHeader>([], false))
            .Verifiable(Times.Once());
        var notificationHub = new Mock<INotificationHubService>(MockBehavior.Strict);
        notificationHub
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        var userService = new Mock<IUserService>(MockBehavior.Strict);
        userService
            .Setup(x => x.GetFirstUserAsync(CancellationToken.None))
            .ReturnsAsync((AppUser?)null)
            .Verifiable(Times.Once());
        userService
            .Setup(x => x.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable(Times.Once());
        userService
            .Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Verifiable(Times.Once());
        var roleService = new Mock<IRoleService>(MockBehavior.Strict);
        roleService.Setup(x => x.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true).Verifiable(Times.Once());
        var downloadQueue = new Mock<IDownloadQueue>(MockBehavior.Strict);
        downloadQueue.Setup(x => x.Setup(CancellationToken.None)).Returns(Result.Ok()).Verifiable(Times.Once());
        downloadQueue
            .Setup(x => x.CheckDownloadQueueForAllServers(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var backgroundJobs = new Mock<IBackgroundJobsSetup>(MockBehavior.Strict);
        backgroundJobs
            .Setup(x => x.SetupAsync(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var telemetry = new Mock<ITelemetry>(MockBehavior.Strict);
        telemetry.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask).Verifiable(Times.Once());
        var moveQueue = new Mock<IMoveDownloadFileQueue>(MockBehavior.Strict);
        moveQueue
            .Setup(x => x.CheckMoveDownloadFileJobQueue(CancellationToken.None))
            .ReturnsAsync(Result.Ok())
            .Verifiable(Times.Once());
        var authenticationSettings = AuthenticationModule.Create();

        SetupRealCommandPipeline(
            probe,
            [
                typeof(CreateDefaultAppUserCommandHandler),
                typeof(RecoverInterruptedDownloadsCommandHandler),
                typeof(CleanupLibrarySyncJobQueueCommandHandler),
                typeof(CheckQueuedPlexLibraryToSyncCommandHandler),
            ],
            builder =>
            {
                builder.RegisterInstance(applicationLifetime.Object).As<IHostApplicationLifetime>();
                builder.RegisterInstance(scheduler.Object).As<IScheduler>();
                builder.RegisterInstance(notificationHub.Object).As<INotificationHubService>();
                builder.RegisterInstance(userService.Object).As<IUserService>();
                builder.RegisterInstance(roleService.Object).As<IRoleService>();
                builder.RegisterInstance(authenticationSettings).As<IAuthenticationSettings>();
                builder.RegisterInstance(downloadQueue.Object).As<IDownloadQueue>();
                builder.RegisterInstance(backgroundJobs.Object).As<IBackgroundJobsSetup>();
                builder.RegisterInstance(telemetry.Object).As<ITelemetry>();
                builder.RegisterInstance(moveQueue.Object).As<IMoveDownloadFileQueue>();
                builder.RegisterType<Boot>();
            }
        );

        // Act
        await Mock.Container.Resolve<Boot>().StartAsync(CancellationToken.None);

        // Assert
        probe.MiddlewareCalls.ShouldBe(2);
        probe.Contexts.Count.ShouldBe(2);
        foreach (var context in probe.Contexts)
            Should.Throw<ObjectDisposedException>(() => context.ClearChangeTracker());
        scheduler.Verify();
        notificationHub.Verify();
        userService.Verify();
        roleService.Verify();
        downloadQueue.Verify();
        backgroundJobs.Verify();
        telemetry.Verify();
        moveQueue.Verify();
    }

    [Test]
    public async Task ShouldDisposeDirectContexts_WhenSingletonLibrarySyncListenerDispatchesACommand()
    {
        // Arrange
        await SetupDatabase(71602);
        var probe = new ContextProbe();
        var context = CreateJobContext(new LibrarySyncJobPayload(1, 1));
        SetupRealCommandPipeline(probe, [typeof(CheckQueuedPlexLibraryToSyncCommandHandler)], builder =>
            builder.RegisterType<LibrarySyncJobListener>().SingleInstance()
        );

        // Act
        await Mock
            .Container.Resolve<LibrarySyncJobListener>()
            .JobWasExecuted(context.Object, null, CancellationToken.None);

        // Assert
        probe.MiddlewareCalls.ShouldBe(1);
        probe.Contexts.ShouldHaveSingleItem();
        Should.Throw<ObjectDisposedException>(() => probe.Contexts[0].ClearChangeTracker());
    }

    [Test]
    public async Task ShouldDisposeJobAndNestedCommandContexts_WhenQuartzJobUsesTheRealCommandExecutor()
    {
        // Arrange
        await SetupDatabase(71603, config =>
        {
            config.PlexServerCount = 1;
            config.PlexMovieLibraryCount = 1;
        });
        var setupContext = IDbContext;
        var library = await setupContext.PlexLibraries.SingleAsync(CancellationToken);
        await setupContext.PlexServers.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsEnabled, true),
            CancellationToken
        );
        await setupContext.PlexLibraries.ExecuteUpdateAsync(
            x => x.SetProperty(y => y.IsEnabled, true),
            CancellationToken
        );
        setupContext.LibrarySyncJobQueues.Add(
            new LibrarySyncJobQueue
            {
                PlexServerId = library.PlexServerId,
                PlexLibraryId = library.Id,
                Priority = 1,
                Status = LibrarySyncJobStatus.Processing,
                CreatedAt = DateTime.UtcNow,
            }
        );
        await setupContext.SaveChangesAsync(CancellationToken);
        await setupContext
            .PlexServerStatuses.Where(x => x.PlexServerId == library.PlexServerId)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsSuccessful, true), CancellationToken);

        var probe = new ContextProbe();
        var databaseName = setupContext.DatabaseName;
        var telemetry = Mock.Mock<ITelemetry>();
        telemetry
            .Setup(x =>
                x.BeginJob(
                    TelemetryActivity.LibrarySync,
                    It.IsAny<(ActivityContext Parent, string? SessionId, string JobKey, string FireInstanceId)>()
                )
            )
            .Returns((IDisposable?)null)
            .Verifiable(Times.Once());
        Mock.Mock<INotificationHubService>()
            .Setup(x => x.SendRefreshNotificationAsync(It.IsAny<List<RefreshDataType>>()))
            .Returns(Task.CompletedTask)
            .Verifiable(Times.Once());
        Mock.Mock<IMediaOverviewRebuildCoordinator>()
            .Setup(x => x.AcquireLibrarySyncLeaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LifetimeMarker())
            .Verifiable(Times.Once());
        var notificationHub = Mock.Mock<INotificationHubService>();
        var coordinator = Mock.Mock<IMediaOverviewRebuildCoordinator>();
        SetupRealCommandPipeline(probe, [typeof(TestRefreshLibraryMediaCommandHandler)], builder =>
        {
            builder.RegisterInstance(telemetry.Object).As<ITelemetry>();
            builder.RegisterInstance(notificationHub.Object).As<INotificationHubService>();
            builder.RegisterInstance(coordinator.Object).As<IMediaOverviewRebuildCoordinator>();
            builder.RegisterType<RefreshLibraryMediaCommandValidator>().As<IValidator<RefreshLibraryMediaCommand>>();
            builder.RegisterType<LibrarySyncJob>();
            builder.RegisterType<TestRefreshLibraryMediaCommandHandler>();
            builder
                .Register(context =>
                {
                    var dbContext = MockDatabase.GetMemoryReaparrDbContext(
                        context.Resolve<ILogger>(),
                        context.Resolve<IPathProvider>(),
                        context.Resolve<IAppRuntimeInfo>(),
                        databaseName
                    );
                    probe.Contexts.Add(dbContext);
                    return dbContext;
                })
                .As<IReaparrDbContext>()
                .InstancePerLifetimeScope();
        });

        var jobContext = CreateJobContext(new LibrarySyncJobPayload(library.PlexServerId, library.Id));
        await using (var scope = Mock.Container.BeginLifetimeScope())
        {
            // Act
            await scope.Resolve<LibrarySyncJob>().Execute(jobContext.Object);

            // Assert
            probe.Contexts.Distinct().Count().ShouldBe(2);
            probe.Contexts[0].ClearChangeTracker();
            Should.Throw<ObjectDisposedException>(() => probe.Contexts[^1].ClearChangeTracker());
            jobContext.Object.Result.ShouldBeOfType<BackgroundJobResult>().Status.ShouldBe(JobStatus.Completed);
        }

        // Assert
        foreach (var context in probe.Contexts.Distinct())
            Should.Throw<ObjectDisposedException>(() => context.ClearChangeTracker());
        telemetry.Verify();
        notificationHub.Verify();
        coordinator.Verify();
    }

    private void SetupRealCommandPipeline(
        ContextProbe probe,
        IReadOnlyList<Type> handlerTypes,
        Action<ContainerBuilder> registerDependencies
    )
    {
        SetupDependencies(builder =>
        {
            var services = new ServiceCollection();
            services.AddHttpContextAccessor();
            services.AddMessaging([handlerTypes.ToList()]);
            services.RegisterTestCommandReceivers();
            services.AddCommandMiddleware(middleware =>
            {
                middleware.Register(typeof(ValidationPipeline<,>));
                if (handlerTypes.Contains(typeof(CleanupLibrarySyncJobQueueCommandHandler)))
                    middleware.Register<
                        CleanupLibrarySyncJobQueueCommand,
                        Result,
                        ContextProbeMiddleware<CleanupLibrarySyncJobQueueCommand, Result>
                    >();
                if (handlerTypes.Contains(typeof(CheckQueuedPlexLibraryToSyncCommandHandler)))
                    middleware.Register<
                        CheckQueuedPlexLibraryToSyncCommand,
                        Result,
                        ContextProbeMiddleware<CheckQueuedPlexLibraryToSyncCommand, Result>
                    >();
                if (handlerTypes.Contains(typeof(TestRefreshLibraryMediaCommandHandler)))
                    middleware.Register<
                        RefreshLibraryMediaCommand,
                        Result<PlexLibrary>,
                        ContextProbeMiddleware<RefreshLibraryMediaCommand, Result<PlexLibrary>>
                    >();
            });
            builder.Populate(services);
            builder.RegisterModule<FastEndpointsModule>();
            builder
                .RegisterAssemblyTypes(typeof(CheckQueuedPlexLibraryToSyncCommandValidator).Assembly)
                .Where(type =>
                    !type.IsAbstract
                    && type.GetInterfaces()
                        .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>))
                )
                .AsImplementedInterfaces();
            builder.RegisterInstance(probe).SingleInstance();
            builder.RegisterType<ContextProbeMiddleware<CleanupLibrarySyncJobQueueCommand, Result>>();
            builder.RegisterType<ContextProbeMiddleware<CheckQueuedPlexLibraryToSyncCommand, Result>>();
            builder.RegisterType<ContextProbeMiddleware<RefreshLibraryMediaCommand, Result<PlexLibrary>>>();
            registerDependencies(builder);
        });
        Mock.Container.Resolve<IServiceProvider>().UseMessaging();
    }

    private static Mock<IJobExecutionContext> CreateJobContext(LibrarySyncJobPayload payload)
    {
        var data = payload.ToJobDataMap();
        var jobDetail = new Mock<IJobDetail>();
        jobDetail.SetupGet(x => x.Key).Returns(LibrarySyncJob.GetJobKey(payload.ServerId, payload.LibraryId));
        jobDetail.SetupGet(x => x.JobDataMap).Returns(data);
        var context = new Mock<IJobExecutionContext>();
        context.SetupGet(x => x.JobDetail).Returns(jobDetail.Object);
        context.SetupGet(x => x.MergedJobDataMap).Returns(data);
        context.SetupGet(x => x.FireInstanceId).Returns("lifecycle-test");
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        context.SetupProperty(x => x.Result);
        return context;
    }

    private sealed class ContextProbe
    {
        public List<IReaparrDbContext> Contexts { get; } = [];
        public int MiddlewareCalls { get; set; }
    }

    private sealed class ContextProbeMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>
        where TCommand : ICommand<TResult>
        where TResult : ResultBase, new()
    {
        private readonly IReaparrDbContext _context;
        private readonly ContextProbe _probe;

        public ContextProbeMiddleware(IReaparrDbContext context, ContextProbe probe)
        {
            _context = context;
            _probe = probe;
        }

        public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
        {
            _probe.Contexts.Add(_context);
            _probe.MiddlewareCalls++;
            _context.ClearChangeTracker();
            var result = await next();
            _context.ClearChangeTracker();
            return result;
        }
    }

    private sealed class TestRefreshLibraryMediaCommandHandler
        : ICommandHandler<RefreshLibraryMediaCommand, Result<PlexLibrary>>
    {
        private readonly IReaparrDbContext _context;

        public TestRefreshLibraryMediaCommandHandler(IReaparrDbContext context)
        {
            _context = context;
        }

        public async Task<Result<PlexLibrary>> ExecuteAsync(RefreshLibraryMediaCommand command, CancellationToken ct)
        {
            _context.ClearChangeTracker();
            var library = await _context.PlexLibraries.FirstAsync(x => x.Id == command.PlexLibraryId, ct);
            return Result.Ok(library);
        }
    }

    private sealed class LifetimeMarker : IDisposable
    {
        public void Dispose() { }
    }
}
