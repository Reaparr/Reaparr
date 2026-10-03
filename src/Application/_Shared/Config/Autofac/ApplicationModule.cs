using Downloader;
using Microsoft.Extensions.Hosting;
using Velopack.Sources;
using Module = Autofac.Module;

namespace Reaparr.Application;

/// <summary>
/// Used to register all dependencies in Autofac for the Application project.
/// </summary>
public class ApplicationModule : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterInstance(TimeProvider.System).As<TimeProvider>();
        builder.RegisterType<DownloadQueue>().As<IDownloadQueue>().SingleInstance();
        builder.RegisterType<DownloadTaskScheduler>().As<IDownloadTaskScheduler>().SingleInstance();
        builder.RegisterType<MoveDownloadFileJobScheduler>().As<IMoveDownloadFileScheduler>().SingleInstance();
        builder.RegisterType<MoveDownloadFileJobQueue>().As<IMoveDownloadFileQueue>().SingleInstance();
        builder.RegisterType<SonarrHttpClientFactory>().As<ISonarrHttpClientFactory>().InstancePerDependency();
        builder.RegisterType<RadarrHttpClientFactory>().As<IRadarrHttpClientFactory>().InstancePerDependency();

        builder
            .RegisterType<DirectPlexDownloadClient>()
            .Keyed<IPlexDownloadClient>(PlexDownloadClientType.Direct)
            .InstancePerDependency();

        builder
            .Register<Func<DownloadConfiguration, IDownloadService>>(_ =>
                config => new DownloadService(config, loggerFactory: null) // no internal library logging should happen
            )
            .InstancePerDependency();

        builder
            .RegisterType<DashPlexDownloadClient>()
            .Keyed<IPlexDownloadClient>(PlexDownloadClientType.Dash)
            .InstancePerDependency();

        builder
            .RegisterType<DownloadTaskUpdateDispatcher>()
            .As<IDownloadTaskUpdateDispatcher>()
            .As<IHostedService>()
            .SingleInstance();

        builder
            .Register(context => new UpdateManager(
                new GithubSource(
                    "https://github.com/Reaparr/Reaparr",
                    context.Resolve<IAppRuntimeInfo>().GitHubToken,
                    context.Resolve<IAppBuildInfo>().IsDevRelease
                )
            ))
            .SingleInstance();

        builder.RegisterType<LibrarySyncProgressStore>().As<ILibrarySyncProgressStore>().SingleInstance();
        builder.RegisterType<DownloadSpeedLimitProvider>().As<IDownloadSpeedLimitProvider>().SingleInstance();
    }
}
