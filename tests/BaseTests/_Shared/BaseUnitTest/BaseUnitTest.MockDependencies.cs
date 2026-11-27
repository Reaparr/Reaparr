using System.IO.Abstractions.TestingHelpers;

namespace Reaparr.BaseTests;

public partial class BaseUnitTest
{
    private Action<ContainerBuilder>? _fileSystemSetup;
    private Action<ContainerBuilder>? _httpClientSetup;
    private Action<ContainerBuilder>? _appBuildInfoSetup;
    private Action<ContainerBuilder>? _appRuntimeInfoSetup;
    private Action<ContainerBuilder>? _dependenciesSetup;
    private MockFileSystem? _fileSystem;
    private AutoMock? _mock;
    private List<AutoMock>? _ownedMocks;

    protected AutoMock Mock => _mock ??= Build();

    protected long DefaultAvailableSpace = (long)ByteSize.FromGigaBytes(1000).Bytes;

    private AutoMock Build()
    {
        var mock = AutoMock.GetStrict(builder =>
        {
            SetDefaultBuilder(builder);

            if (_appBuildInfoSetup is not null)
                _appBuildInfoSetup.Invoke(builder);

            if (_appRuntimeInfoSetup is not null)
                _appRuntimeInfoSetup.Invoke(builder);

            if (_fileSystemSetup is not null)
            {
                _fileSystemSetup.Invoke(builder);

                builder.Register(ctx => ctx.Resolve<IFileSystem>().Path).As<IPath>().SingleInstance();
                builder.Register(ctx => ctx.Resolve<IFileSystem>().File).As<IFile>().SingleInstance();
                builder.Register(ctx => ctx.Resolve<IFileSystem>().Directory).As<IDirectory>().SingleInstance();
            }

            if (_dependenciesSetup is not null)
                _dependenciesSetup.Invoke(builder);

            if (_httpClientSetup is not null)
                _httpClientSetup.Invoke(builder);
        });

        // Already-resolved services can still depend on older containers until the test ends.
        (_ownedMocks ??= []).Add(mock);

        return mock;
    }

    private void InvalidateMock()
    {
        if (_fileSystemSetup is null)
        {
            _mock = null;
            return;
        }

        // Filesystem callbacks run eagerly and use the paths from the outgoing container.
        SetDefaultFileSystemDirectories(Mock.Container.Resolve<IPathProvider>());
        _mock = Build();
    }

    private void DisposeMocks()
    {
        if (_ownedMocks is null)
            return;

        List<Exception>? failures = null;
        for (var i = _ownedMocks.Count - 1; i >= 0; i--)
        {
            try
            {
                _ownedMocks[i].Dispose();
            }
            catch (Exception ex)
            {
                // AutoMock also verifies expectations on disposal. Still release the other containers.
                (failures ??= []).Add(ex);
            }
        }

        _ownedMocks.Clear();
        _mock = null;
        if (failures is not null)
            throw new AggregateException(failures);
    }

    private void SetDefaultBuilder(ContainerBuilder builder)
    {
        builder.Register<ILogger>((_, _) => LogFactory.Create()).SingleInstance().ExternallyOwned();

        builder
            .Register(_ =>
            {
                var telemetry = new Mock<ITelemetry>();
                telemetry.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
                telemetry.Setup(x => x.SendDiagnostic(It.IsAny<TelemetryDiagnosticOrigin>())).Returns(Result.Ok());
                return telemetry.Object;
            })
            .As<ITelemetry>()
            .SingleInstance();

        // Database context can be set up once and then retrieved by its DB name.
        builder
            .Register(
                (ctx, _) =>
                    MockDatabase.GetMemoryReaparrDbContext(
                        ctx.Resolve<ILogger>(),
                        ctx.Resolve<IPathProvider>(),
                        ctx.Resolve<IAppRuntimeInfo>(),
                        _databaseName
                    )
            )
            .As<ReaparrDbContext>()
            .InstancePerDependency();

        builder
            .Register(
                (ctx, _) =>
                    MockDatabase.GetMemoryReaparrDbContext(
                        ctx.Resolve<ILogger>(),
                        ctx.Resolve<IPathProvider>(),
                        ctx.Resolve<IAppRuntimeInfo>(),
                        _databaseName
                    )
            )
            .As<IReaparrDbContext>()
            .InstancePerDependency();

        builder
            .Register(
                (ctx, _) =>
                    MockDatabase.GetMemoryAuthDbContext(
                        ctx.Resolve<ILogger>(),
                        ctx.Resolve<IPathProvider>(),
                        ctx.Resolve<IAppRuntimeInfo>(),
                        _databaseName
                    )
            )
            .As<AuthDbContext>()
            .InstancePerDependency();

        builder
            .Register(
                (ctx, _) =>
                    MockDatabase.GetMemoryAuthDbContext(
                        ctx.Resolve<ILogger>(),
                        ctx.Resolve<IPathProvider>(),
                        ctx.Resolve<IAppRuntimeInfo>(),
                        _databaseName
                    )
            )
            .As<IAuthDbContext>()
            .InstancePerDependency();

        builder
            .Register(
                (ctx, _) =>
                {
                    var logger = ctx.Resolve<ILogger>();
                    var pathProvider = ctx.Resolve<IPathProvider>();
                    var appRuntimeInfo = ctx.Resolve<IAppRuntimeInfo>();
                    var factoryMock = new Mock<IReaparrDbContextFactory>(MockBehavior.Strict);
                    factoryMock
                        .Setup(x => x.Create())
                        .Returns(() =>
                            MockDatabase.GetMemoryReaparrDbContext(logger, pathProvider, appRuntimeInfo, _databaseName)
                        );
                    factoryMock
                        .Setup(x => x.CreateAsync())
                        .Returns(() =>
                            Task.FromResult<IReaparrDbContext>(
                                MockDatabase.GetMemoryReaparrDbContext(
                                    logger,
                                    pathProvider,
                                    appRuntimeInfo,
                                    _databaseName
                                )
                            )
                        );
                    return factoryMock.Object;
                }
            )
            .As<IReaparrDbContextFactory>()
            .InstancePerDependency();

        builder
            .Register(
                (ctx, _) =>
                {
                    var logger = ctx.Resolve<ILogger>();
                    var pathProvider = ctx.Resolve<IPathProvider>();
                    var appRuntimeInfo = ctx.Resolve<IAppRuntimeInfo>();
                    var factoryMock = new Mock<IAuthDbContextFactory>(MockBehavior.Strict);
                    factoryMock
                        .Setup(x => x.Create())
                        .Returns(() =>
                            MockDatabase.GetMemoryAuthDbContext(logger, pathProvider, appRuntimeInfo, _databaseName)
                        );
                    factoryMock
                        .Setup(x => x.CreateAsync())
                        .Returns(() =>
                            Task.FromResult<IAuthDbContext>(
                                MockDatabase.GetMemoryAuthDbContext(logger, pathProvider, appRuntimeInfo, _databaseName)
                            )
                        );
                    return factoryMock.Object;
                }
            )
            .As<IAuthDbContextFactory>()
            .InstancePerDependency();

        builder.RegisterType<MockAppBuildInfo>().As<IAppBuildInfo>().SingleInstance();
        builder.RegisterType<MockAppRuntimeInfo>().As<IAppRuntimeInfo>().SingleInstance();

        builder
            .Register(ctx => new MockPathProvider(
                _databaseName,
                ctx.Resolve<IAppBuildInfo>(),
                ctx.Resolve<IAppRuntimeInfo>()
            ))
            .As<IPathProvider>()
            .SingleInstance();
    }

    protected void SetAppBuildInfo(Action<MockAppBuildInfo> action)
    {
        // Apply to the current singleton for already-resolved SUT instances, then
        // persist the same override for any future container rebuilds.
        if (_mock?.Container.Resolve<IAppBuildInfo>() is MockAppBuildInfo existingAppBuildInfo)
            action.Invoke(existingAppBuildInfo);

        _appBuildInfoSetup = builder =>
        {
            builder
                .Register<MockAppBuildInfo>(_ =>
                {
                    var instance = new MockAppBuildInfo();
                    action.Invoke(instance);
                    return instance;
                })
                .As<IAppBuildInfo>()
                .SingleInstance();
        };

        InvalidateMock();
    }

    protected void SetAppRuntimeInfo(Action<MockAppRuntimeInfo> action)
    {
        // Apply to the current singleton for already-resolved SUT instances, then
        // persist the same override for any future container rebuilds.
        if (_mock?.Container.Resolve<IAppRuntimeInfo>() is MockAppRuntimeInfo existingAppRuntimeInfo)
            action.Invoke(existingAppRuntimeInfo);

        _appRuntimeInfoSetup = builder =>
        {
            builder
                .Register<MockAppRuntimeInfo>(_ =>
                {
                    var instance = new MockAppRuntimeInfo();
                    action.Invoke(instance);
                    return instance;
                })
                .As<IAppRuntimeInfo>()
                .SingleInstance();
        };

        InvalidateMock();
    }

    protected void SetupHttpClient(Action<Mock<HttpMessageHandler>>? action = null)
    {
        _httpClientSetup = builder =>
        {
            builder
                .Register(_ =>
                {
                    action?.Invoke(HttpHandlerMock);

                    return new HttpClient(HttpHandlerMock.Object);
                })
                .As<HttpClient>()
                .SingleInstance();
        };

        InvalidateMock();
    }

    private void SetDefaultFileSystemDirectories(IPathProvider pathProvider)
    {
        var fileSystem = _fileSystem ??= new MockFileSystem();

        fileSystem.AddDrive(
            "/",
            new MockDriveData
            {
                IsReady = true,
                DriveType = DriveType.Fixed,
                AvailableFreeSpace = DefaultAvailableSpace,
            }
        );
        fileSystem.AddDirectory(pathProvider.ConfigDirectory);
        fileSystem.AddDirectory(pathProvider.DefaultDownloadsDestinationFolder);
        fileSystem.AddDirectory(pathProvider.DefaultMovieDestinationFolder);
        fileSystem.AddDirectory(pathProvider.DefaultTvShowsDestinationFolder);
        fileSystem.AddDirectory(pathProvider.DefaultMusicDestinationFolder);
        fileSystem.AddDirectory(pathProvider.DefaultPhotosDestinationFolder);
        fileSystem.AddDirectory(pathProvider.DefaultOtherDestinationFolder);
        fileSystem.AddDirectory(pathProvider.DefaultGamesDestinationFolder);
    }

    protected void SetupFileSystem(Action<MockFileSystem>? action = default)
    {
        _ = Mock;
        var fileSystem = _fileSystem ??= new MockFileSystem();
        _fileSystemSetup = builder =>
        {
            action?.Invoke(fileSystem);
            builder.Register<MockFileSystem>(_ => fileSystem).As<IFileSystem>().SingleInstance();
        };

        InvalidateMock();
    }

    protected void SetupDependencies(Action<ContainerBuilder> action)
    {
        if (_dependenciesSetup is not null)
            throw new InvalidOperationException("SetupDependencies should not be called more than once.");

        _dependenciesSetup = action;

        InvalidateMock();
    }

    protected void SetupFileSystem(Action<MockFileSystem, IReaparrDbContext> action)
    {
        _ = Mock;
        var fileSystem = _fileSystem ??= new MockFileSystem();
        _fileSystemSetup = builder =>
        {
            builder
                .Register<MockFileSystem>(ctx =>
                {
                    DataBaseSetupGuard();

                    var dbContext = ctx.Resolve<IReaparrDbContext>();

                    action.Invoke(fileSystem, dbContext);

                    return fileSystem;
                })
                .As<IFileSystem>()
                .SingleInstance();
        };

        InvalidateMock();
    }
}
