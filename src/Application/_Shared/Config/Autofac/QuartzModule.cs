using System.Collections.Specialized;
using System.Reflection;
using Autofac.Extras.Quartz;
using Microsoft.Data.Sqlite;
using Quartz.Impl.AdoJobStore;
using Reaparr.Domain.Autofac;
using Module = Autofac.Module;

namespace Reaparr.Application;

public class QuartzModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register Quartz dependencies
        // Source: https://www.quartz-scheduler.net/
        builder.RegisterType<AllJobListener>().SingleInstance();
        builder.RegisterType<DownloadJobListener>().SingleInstance();
        builder.RegisterType<LibrarySyncJobListener>().SingleInstance();
        builder.RegisterType<SchedulerListener>().SingleInstance();
        builder.RegisterType<BackgroundJobsSetup>().As<IBackgroundJobsSetup>().SingleInstance();
        builder
            .RegisterType<MediaOverviewRebuildCoordinator>()
            .As<IMediaOverviewRebuildCoordinator>()
            .SingleInstance();

        // Source: https://github.com/alphacloud/Autofac.Extras.Quartz
        builder.RegisterModule(new QuartzAutofacFactoryModule { ConfigurationProvider = ConfigurationProvider });

        // register all Quartz jobs
        builder.RegisterModule(new QuartzAutofacJobsModule(assembly));

        // Source: https://github.com/alphacloud/Autofac.Extras.Quartz/blob/develop/src/Samples/Shared/Bootstrap.cs
        builder.Register(_ => new ScopedDependency("global")).AsImplementedInterfaces().SingleInstance();
    }

    private NameValueCollection ConfigurationProvider(IComponentContext context)
    {
        var pathProvider = context.Resolve<IPathProvider>();
        var runtimeInfo = context.Resolve<IAppRuntimeInfo>();

        var connectionString = DbContextConnections.GetConnectionString(
            pathProvider.DatabasePath,
            SqliteOpenMode.ReadWriteCreate
        );

        var schedulerName = !runtimeInfo.IsIntegrationTestMode
            ? "Reaparr Scheduler"
            : "TestReaparr Scheduler_" + Guid.NewGuid();
        var schedulerBuilder = SchedulerBuilder
            .Create()
            .WithName(schedulerName)
            .WithId(Guid.NewGuid().ToString())
            .UseDefaultThreadPool(10)
            .UsePersistentStore(store =>
            {
                // https://www.quartz-scheduler.net/documentation/quartz-3.x/packages/system-text-json.html#configuring
                store.UseProperties = false;
                store.UseSystemTextJsonSerializer();
                // False because Autofac is initialized before the database. By the time the database is set up, the schema has already been validated.
                store.PerformSchemaValidation = false;
                store.RetryInterval = TimeSpan.FromMinutes(2);
                store.UseGenericDatabase<SQLiteDelegate>(
                    provider: "SQLite-Microsoft",
                    dataSourceName: "default",
                    configurer: database =>
                    {
                        database.ConnectionString = connectionString;
                        database.TablePrefix = "QRTZ_";
                        database.UseConnectionProvider<QuartzSqliteConnectionProvider>();
                    }
                );
            });

        schedulerBuilder.Properties["quartz.dataSource.default.connectionProvider.connectionString"] = connectionString;

        schedulerBuilder.InterruptJobsOnShutdownWithWait = true;
        schedulerBuilder.MisfireThreshold = TimeSpan.FromMinutes(5);

        return schedulerBuilder.Properties;
    }

    public static NameValueCollection TestQuartzConfiguration()
    {
        var schedulerBuilder = SchedulerBuilder
            .Create()
            .WithName("TestReaparr Scheduler_" + Guid.NewGuid())
            .WithId(Guid.NewGuid().ToString())
            .UseDefaultThreadPool(10);

        schedulerBuilder.InterruptJobsOnShutdownWithWait = true;
        schedulerBuilder.MisfireThreshold = TimeSpan.FromMinutes(5);

        return schedulerBuilder.Properties;
    }
}
