using Autofac;

namespace Reaparr.Data;

public class DataModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ReaparrDbContext>().As<IReaparrDbContext>().AsSelf().InstancePerDependency();

        builder.RegisterType<ReaparrDbContext>().As<IReaparrDbContextDatabase>().InstancePerDependency();

        builder.RegisterType<ReaparrDbContextManager>().As<IReaparrDbContextManager>().InstancePerDependency();

        builder.RegisterType<ReaparrDbContextFactory>().As<IReaparrDbContextFactory>().InstancePerDependency();
    }
}
