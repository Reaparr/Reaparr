using Autofac;

namespace Reaparr.Identity;

public class IdentityModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AuthDbContext>().As<IAuthDbContext>().AsSelf().InstancePerDependency();

        builder.RegisterType<AuthDbContext>().As<IAuthDbContextDatabase>().InstancePerDependency();

        builder.RegisterType<AuthDbContextFactory>().As<IAuthDbContextFactory>().InstancePerDependency();

        builder.RegisterType<IdentityUserService>().As<IUserService>().InstancePerLifetimeScope();

        builder.RegisterType<IdentityRoleService>().As<IRoleService>().InstancePerLifetimeScope();

        builder.RegisterType<IdentityIdentitySignInService>().As<IIdentitySignInService>().InstancePerLifetimeScope();
    }
}
