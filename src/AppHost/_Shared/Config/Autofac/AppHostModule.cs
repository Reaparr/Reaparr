using Microsoft.Extensions.Logging.Abstractions;
using PostHog;

namespace Reaparr.AppHost;

/// <summary>
///  Autofac module for the AppHost project.
/// </summary>
public class AppHostModule : Module
{
    /// <inheritdoc/>
    protected override void Load(ContainerBuilder builder)
    {
        builder
            .RegisterType<PostHogClient>()
            .As<IPostHogClient>()
            .SingleInstance()
            .WithParameter(new TypedParameter(typeof(ILoggerFactory), NullLoggerFactory.Instance));
        builder.Register(c => c.Resolve<IHttpClientFactory>().CreateClient()).As<HttpClient>().InstancePerDependency();

        // This needs to be registered in order to fire Boot on Application startup
        builder.RegisterType<Boot>().As<IHostedService>().SingleInstance();

        builder
            .RegisterType<DesktopSingleInstanceCoordinator>()
            .As<IDesktopSingleInstanceCoordinator>()
            .SingleInstance();
        builder.RegisterType<DesktopWindow>().As<IDesktopWindow>().InstancePerDependency();
        builder.RegisterType<DesktopMode>().As<IDesktopMode>().SingleInstance();
    }
}
