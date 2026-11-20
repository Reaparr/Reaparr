using Module = Autofac.Module;

namespace Reaparr.Application;

public class FastEndpointsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register the command executor and event publisher
        builder.RegisterType<CommandExecutor>().As<ICommandExecutor>().InstancePerLifetimeScope();
        builder.RegisterType<CommandServiceResolver>().As<IServiceResolver>().SingleInstance();
        builder.RegisterType<EventPublisher>().As<IEventPublisher>().InstancePerLifetimeScope();
    }
}
