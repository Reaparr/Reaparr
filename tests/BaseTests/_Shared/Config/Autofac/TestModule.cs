using Downloader;
using Moq.Contrib.HttpClient;
using Reaparr.Settings.Contracts;

namespace Reaparr.BaseTests;

/// <summary>
/// Add the default test mock modules here which can later be overridden
/// </summary>
public class TestModule : Module
{
    public required string MemoryDbName { get; init; }
    public required UnitTestDataConfig Config { get; init; }

    protected override void Load(ContainerBuilder builder)
    {
        // Database context can be setup once and then retrieved by its DB name.
        builder
            .Register(
                (ctx, _) =>
                    MockDatabase.GetMemoryReaparrDbContext(
                        ctx.Resolve<ILogger>(),
                        ctx.Resolve<IPathProvider>(),
                        ctx.Resolve<IAppRuntimeInfo>(),
                        MemoryDbName
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
                        MemoryDbName
                    )
            )
            .As<IReaparrDbContext>()
            .As<IReaparrDbContextDatabase>()
            .InstancePerDependency();

        builder
            .Register(
                (ctx, _) =>
                    MockDatabase.GetMemoryAuthDbContext(
                        ctx.Resolve<ILogger>(),
                        ctx.Resolve<IPathProvider>(),
                        ctx.Resolve<IAppRuntimeInfo>(),
                        MemoryDbName
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
                        MemoryDbName
                    )
            )
            .As<IAuthDbContext>()
            .As<IAuthDbContextDatabase>()
            .InstancePerDependency();

        builder.RegisterType<MockProgressHubService>().As<IProgressHubService>().SingleInstance();
        builder.RegisterType<MockDownloadHubService>().As<IDownloadHubService>().SingleInstance();
        builder.RegisterType<MockNotificationHubService>().As<INotificationHubService>().SingleInstance();
        builder.RegisterType<MockPlexApiServer>().As<IMockPlexApiServer>().SingleInstance();

        builder
            .Register(ctx => Config.MockDownloadServiceFactory(ctx.Resolve<IFileSystem>().File))
            .As<Func<DownloadConfiguration, IDownloadService>>()
            .SingleInstance();

        builder
            .Register((_, _) => Config.OverrideAppBuildInfo ?? new MockAppBuildInfo())
            .As<IAppBuildInfo>()
            .SingleInstance();

        builder
            .Register(
                (_, _) =>
                {
                    var runtimeInfo = new MockAppRuntimeInfo { IsIntegrationTestMode = true, IsUnmasked = true };
                    Config.OverrideAppRuntimeInfo?.Invoke(runtimeInfo);
                    return runtimeInfo;
                }
            )
            .As<IAppRuntimeInfo>()
            .SingleInstance();

        builder
            .Register(
                (ctx, _) =>
                    new MockPathProvider(MemoryDbName, ctx.Resolve<IAppBuildInfo>(), ctx.Resolve<IAppRuntimeInfo>())
            )
            .As<IPathProvider>()
            .SingleInstance();

        SetMockedDependencies(builder);
    }

    private void SetMockedDependencies(ContainerBuilder builder)
    {
        if (Config.BaseMockHttpClientOptions is not null || Config.HttpClientOptions is not null)
        {
            builder
                .Register(context =>
                {
                    var handler = new Mock<HttpMessageHandler>(MockBehavior.Loose);

                    if (Config.BaseMockHttpClientOptions is not null)
                    {
                        var mockPlexApiServer = context.Resolve<IMockPlexApiServer>();
                        mockPlexApiServer.Setup(handler, Config.BaseMockHttpClientOptions);
                    }

                    // We do not invoke Config.HttpClientOptions here because it requires a per-dependency service (like dbContext)
                    return handler;
                })
                .SingleInstance();

            builder
                .Register(context =>
                {
                    var handler = context.Resolve<Mock<HttpMessageHandler>>();

                    if (Config.HttpClientOptions is not null)
                    {
                        var dbContext = context.Resolve<IReaparrDbContext>();
                        Config.HttpClientOptions.Invoke(handler, dbContext);
                    }

                    handler
                        .SetupRequest(x => x.RequestUri?.AbsolutePath == "/library/parts/653125/119385313456/file.mp4")
                        .ReturnsAsync(
                            new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) }
                        );

                    var httpClientFactory = new Mock<IHttpClientFactory>();
                    httpClientFactory
                        .Setup(x => x.CreateClient(It.IsAny<string>()))
                        .Returns(() => CreateMockHttpClient(handler));
                    return httpClientFactory.Object;
                })
                .As<IHttpClientFactory>()
                .SingleInstance();

            builder
                .Register(context => CreateMockHttpClient(context.Resolve<Mock<HttpMessageHandler>>()))
                .As<HttpClient>()
                .InstancePerDependency();
        }

        if (Config.MockConfigManager is not null)
            builder.RegisterInstance(Config.MockConfigManager).As<IConfigManager>();
    }

    private static HttpClient CreateMockHttpClient(Mock<HttpMessageHandler> handler)
    {
        var client = new HttpClient(handler.Object, disposeHandler: false);
        client.DefaultRequestHeaders.Add("User-Agent", "MockHttpClient");
        return client;
    }
}
