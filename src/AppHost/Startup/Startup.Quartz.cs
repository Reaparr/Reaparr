using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Reaparr.AppHost;

public static partial class Startup
{
    /// <summary>
    /// Registers Application jobs and configures the native Quartz scheduler and job store.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="integrationTestMode">Whether to use an isolated in-memory store instead of persistent SQLite.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection ConfigureQuartz(this IServiceCollection services, bool integrationTestMode)
    {
        var jobTypes = typeof(BackgroundJobsSetup)
            .Assembly.GetTypes()
            .Where(type => typeof(IJob).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .ToArray();
        foreach (var jobType in jobTypes)
            services.AddScoped(jobType);

        var schedulerName = integrationTestMode ? $"TestReaparr Scheduler_{Guid.NewGuid()}" : "Reaparr Scheduler";

        if (!integrationTestMode)
            services.AddQuartzExecutionHistory(options => options.Retention = TimeSpan.FromDays(7));

        services.AddQuartz(quartz =>
        {
            quartz.ConfigureScheduler(options =>
            {
                options.InstanceName = schedulerName;
                options.InstanceId = Guid.NewGuid().ToString();
                options.ShutdownJobInterruption = ShutdownJobInterruption.WhenWaitingForJobs;
                options.PropagateTraceContext = false;
            });

            quartz.UseDefaultThreadPool(10);

            if (integrationTestMode)
            {
                quartz.UseInMemoryStore(options => options.MisfireThreshold = TimeSpan.FromMinutes(5));
            }
            else
            {
                quartz.UsePersistentStore(store =>
                {
                    store.UseDriverDelegate<SQLiteDelegate>();
                    store.UseConnectionProvider(sp => new QuartzSqliteConnectionProvider(
                        sp.GetRequiredService<IPathProvider>()
                    ));
                    store.UseSystemTextJsonSerializer();
                    store.UseExecutionHistory();
                    store.ConfigureStore(options =>
                    {
                        options.TablePrefix = "QRTZ_";
                        options.StoreJobDataAsStrings = false;
                        options.MisfireThreshold = TimeSpan.FromMinutes(5);
                        options.DbRetryInterval = TimeSpan.FromMinutes(2);
                        options.SchemaProvisioning = SchemaProvisioning.Validate;
                    });
                });
            }
        });

        return services;
    }
}
