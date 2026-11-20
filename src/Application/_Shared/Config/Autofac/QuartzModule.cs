using Module = Autofac.Module;

namespace Reaparr.Application;

public class QuartzModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AllJobListener>().SingleInstance();
        builder.RegisterType<DownloadJobListener>().SingleInstance();
        builder.RegisterType<LibrarySyncJobListener>().SingleInstance();
        builder.RegisterType<SchedulerListener>().SingleInstance();
        builder.RegisterType<BackgroundJobsSetup>().As<IBackgroundJobsSetup>().SingleInstance();
        builder.RegisterType<MediaOverviewRebuildCoordinator>().As<IMediaOverviewRebuildCoordinator>().SingleInstance();
    }
}
