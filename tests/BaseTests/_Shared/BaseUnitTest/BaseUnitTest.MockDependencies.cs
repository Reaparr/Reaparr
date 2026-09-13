using System.IO.Abstractions.TestingHelpers;
using Reaparr.PlexApi.Contracts;

namespace Reaparr.BaseTests;

public partial class BaseUnitTest
{
    private Action<ContainerBuilder>? _fileSystemSetup;
    private Action<ContainerBuilder>? _httpClientSetup;
    private Action<ContainerBuilder>? _appBuildInfoSetup;
    private Action<ContainerBuilder>? _appRuntimeInfoSetup;
    private Action<ContainerBuilder>? _dependenciesSetup;
    private readonly MockFileSystem _fileSystem = new();
    protected AutoMock Mock { get; set; }

    protected long DefaultAvailableSpace = (long)ByteSize.FromGigaBytes(1000).Bytes;

    private void Build()
    {
        Mock = AutoMock.GetStrict(builder =>
        {
            SetDefaultBuilder(builder);

            if (_appBuildInfoSetup is not null)
                _appBuildInfoSetup.Invoke(builder);

            if (_appRuntimeInfoSetup is not null)
                _appRuntimeInfoSetup.Invoke(builder);

            if (_fileSystemSetup is not null)
            {
                SetDefaultFileSystemDirectories();
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

        // Mock to avoid HttpClient.Dispose() not mocked exception
        Mock.Mock<IPlexApiClient>().Setup(x => x.Dispose());
    }

    private void SetDefaultBuilder(ContainerBuilder builder)
    {
        builder.Register<ILogger>((_, _) => LogFactory.Create()).SingleInstance();


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
        if (Mock.Container.Resolve<IAppBuildInfo>() is MockAppBuildInfo existingAppBuildInfo)
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

        Build();
    }

    protected void SetAppRuntimeInfo(Action<MockAppRuntimeInfo> action)
    {
        // Apply to the current singleton for already-resolved SUT instances, then
        // persist the same override for any future container rebuilds.
        if (Mock.Container.Resolve<IAppRuntimeInfo>() is MockAppRuntimeInfo existingAppRuntimeInfo)
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

        Build();
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

        Build();
    }

    private void SetDefaultFileSystemDirectories()
    {
        var pathProvider = Mock.Container.Resolve<IPathProvider>();

        _fileSystem.AddDrive(
            "/",
            new MockDriveData
            {
                IsReady = true,
                DriveType = DriveType.Fixed,
                AvailableFreeSpace = DefaultAvailableSpace,
            }
        );
        _fileSystem.AddDirectory(pathProvider.ConfigDirectory);
        _fileSystem.AddDirectory(pathProvider.DefaultDownloadsDestinationFolder);
        _fileSystem.AddDirectory(pathProvider.DefaultMovieDestinationFolder);
        _fileSystem.AddDirectory(pathProvider.DefaultTvShowsDestinationFolder);
        _fileSystem.AddDirectory(pathProvider.DefaultMusicDestinationFolder);
        _fileSystem.AddDirectory(pathProvider.DefaultPhotosDestinationFolder);
        _fileSystem.AddDirectory(pathProvider.DefaultOtherDestinationFolder);
        _fileSystem.AddDirectory(pathProvider.DefaultGamesDestinationFolder);
    }

    protected void SetupFileSystem(Action<MockFileSystem>? action = default)
    {
        _fileSystemSetup = builder =>
        {
            if (action is not null)
            {
                action.Invoke(_fileSystem);
            }

            builder.Register<MockFileSystem>(_ => _fileSystem).As<IFileSystem>().SingleInstance();
        };

        Build();
    }

    protected void SetupDependencies(Action<ContainerBuilder> action)
    {
        if (_dependenciesSetup is not null)
            throw new InvalidOperationException("SetupDependencies should not be called more than once.");

        _dependenciesSetup = action;

        Build();
    }

    protected void SetupFileSystem(Action<MockFileSystem, IReaparrDbContext> action)
    {
        _fileSystemSetup = builder =>
        {
            builder
                .Register<MockFileSystem>(ctx =>
                {
                    DataBaseSetupGuard();

                    var dbContext = ctx.Resolve<IReaparrDbContext>();

                    action.Invoke(_fileSystem, dbContext);

                    return _fileSystem;
                })
                .As<IFileSystem>()
                .SingleInstance();
        };

        Build();
    }
}
