namespace Reaparr.AppHost;

/// <summary>
///  The ContainerConfig class contains all the Autofac modules.
/// </summary>
public static class ContainerConfig
{
    /// <summary>
    /// Autofac container builder, Serilog registration is left out due to being context-dependent.
    /// Integration tests have a different configuration than the application.
    /// </summary>
    /// <param name="builder">The builder through which components can be registered.</param>
    public static void ConfigureContainer(ContainerBuilder builder)
    {
        // Environment
        builder.RegisterModule<EnvironmentModule>();

        // Application
        builder.RegisterModule<ApplicationModule>();
        builder.RegisterModule<PublicApiModule>();
        builder.RegisterModule<LogModule>();

        // Infrastructure
        builder.RegisterModule<DataModule>();
        builder.RegisterModule<IdentityModule>();
        builder.RegisterModule<ExternalModule>();
        builder.RegisterModule<FileSystemModule>();
        builder.RegisterModule<PlexApiModule>();
        builder.RegisterModule<SettingsModule>();
        builder.RegisterModule<SignalrModule>();

        // Presentation
        builder.RegisterModule<AppHostModule>();

        // Packages
        builder.RegisterModule<FastEndpointsModule>();
        builder.RegisterModule<QuartzModule>();
    }
}
