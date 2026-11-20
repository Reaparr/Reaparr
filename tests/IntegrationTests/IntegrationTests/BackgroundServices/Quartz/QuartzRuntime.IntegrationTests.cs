using System.Collections.Concurrent;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Reaparr.AppHost;

namespace Reaparr.IntegrationTests;

public class QuartzRuntimeIntegrationTests : BaseIntegrationTests
{
    [Test]
    public async Task ShouldConfigureEveryQuartzConnection_WhenProviderCreatesAndReopensConnections()
    {
        // Arrange
        var paths = new Mock<IPathProvider>();
        paths
            .SetupGet(x => x.DatabasePath)
            .Returns(Path.Combine(Path.GetTempPath(), $"reaparr-quartz-pragmas-{Guid.NewGuid():N}.db"));
        var provider = new QuartzSqliteConnectionProvider(paths.Object);
        await using var connection = provider.CreateConnection();

        // Act
        await connection.OpenAsync(CancellationToken);
        await connection.CloseAsync();
        await connection.OpenAsync(CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT (SELECT temp_store FROM pragma_temp_store), (SELECT cache_size FROM pragma_cache_size), (SELECT synchronous FROM pragma_synchronous), (SELECT foreign_keys FROM pragma_foreign_keys)";
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);

        // Assert
        (await reader.ReadAsync(CancellationToken)).ShouldBeTrue();
        reader.GetInt64(0).ShouldBe(2);
        reader.GetInt64(1).ShouldBe(-20000);
        reader.GetInt64(2).ShouldBe(1);
        reader.GetInt64(3).ShouldBe(1);
        ((SqliteConnection)connection).DefaultTimeout.ShouldBe(120);
    }

    [Test]
    public async Task ShouldPreservePausedJobsAndPayloads_WhenPersistentSchedulerRestarts()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"reaparr-quartz-{Guid.NewGuid():N}.db");
        var paths = new Mock<IPathProvider>();
        paths.SetupGet(x => x.DatabasePath).Returns(databasePath);
        paths.SetupGet(x => x.DatabaseName).Returns(Path.GetFileNameWithoutExtension(databasePath));
        paths.SetupGet(x => x.DefaultDownloadsDestinationFolder).Returns("/quartz-test/downloads");
        paths.SetupGet(x => x.DefaultMovieDestinationFolder).Returns("/quartz-test/movies");
        paths.SetupGet(x => x.DefaultTvShowsDestinationFolder).Returns("/quartz-test/tv");
        paths.SetupGet(x => x.DefaultMusicDestinationFolder).Returns("/quartz-test/music");
        paths.SetupGet(x => x.DefaultPhotosDestinationFolder).Returns("/quartz-test/photos");
        paths.SetupGet(x => x.DefaultOtherDestinationFolder).Returns("/quartz-test/other");
        paths.SetupGet(x => x.DefaultGamesDestinationFolder).Returns("/quartz-test/games");
        paths.SetupGet(x => x.DataDirectory).Returns("/quartz-test/data");
        var runtime = new Mock<IAppRuntimeInfo>();
        await using var initializer = new SqliteConnection(
            DbContextConnections.GetConnectionString(databasePath, SqliteOpenMode.ReadWriteCreate)
        );
        DbContextConnections.InitializeDatabase(initializer);
        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databasePath, SqliteOpenMode.ReadWriteCreate);
        await using var database = new ReaparrDbContext(
            options.Options,
            LogFactory.Create(),
            paths.Object,
            runtime.Object
        );
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031153443_AddMusicPhotosAndOtherVideos", CancellationToken);
        var jobKey = new JobKey("persisted", "upgrade-regression");
        var controlKey = new JobKey("control", "upgrade-regression");
        var triggerKey = new TriggerKey("paused", "upgrade-regression");
        var completions = new ProbeState();
        var serializer = new Quartz.Impl.SystemTextJsonObjectSerializer();
        var payload = new Dictionary<string, object> { ["payload"] = "original", ["fail"] = false };
        var serializedPayload = serializer.Serialize(payload);
        var jobClass = typeof(ProbeJob).AssemblyQualifiedName!;
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO QRTZ_JOB_DETAILS (SCHED_NAME, JOB_NAME, JOB_GROUP, JOB_CLASS_NAME, IS_DURABLE, IS_NONCONCURRENT, IS_UPDATE_DATA, REQUESTS_RECOVERY, JOB_DATA) VALUES ('Reaparr Scheduler', {jobKey.Name}, {jobKey.Group}, {jobClass}, 1, 0, 0, 0, {serializedPayload})",
            CancellationToken
        );
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO QRTZ_JOB_DETAILS (SCHED_NAME, JOB_NAME, JOB_GROUP, JOB_CLASS_NAME, IS_DURABLE, IS_NONCONCURRENT, IS_UPDATE_DATA, REQUESTS_RECOVERY) VALUES ('Reaparr Scheduler', {controlKey.Name}, {controlKey.Group}, {jobClass}, 1, 0, 0, 0)",
            CancellationToken
        );
        var futureTime = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO QRTZ_TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, JOB_NAME, JOB_GROUP, NEXT_FIRE_TIME, PRIORITY, TRIGGER_STATE, TRIGGER_TYPE, START_TIME, MISFIRE_INSTR) VALUES ('Reaparr Scheduler', {triggerKey.Name}, {triggerKey.Group}, {jobKey.Name}, {jobKey.Group}, {futureTime}, 5, 'PAUSED', 'SIMPLE', {futureTime}, 0)",
            CancellationToken
        );
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO QRTZ_SIMPLE_TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, REPEAT_COUNT, REPEAT_INTERVAL, TIMES_TRIGGERED) VALUES ('Reaparr Scheduler', {triggerKey.Name}, {triggerKey.Group}, 0, 0, 0)",
            CancellationToken
        );
        await migrator.MigrateAsync("20261120180716_UpgradeQuartzSchema", CancellationToken);
        await database.Database.MigrateAsync(CancellationToken);

        // Act
        var restartedServices = new ServiceCollection();
        restartedServices.AddLogging();
        restartedServices.AddSingleton(paths.Object);
        restartedServices.AddSingleton(completions);
        restartedServices.AddScoped<ProbeScope>();
        restartedServices.AddScoped<ProbeJob>();
        restartedServices.ConfigureQuartz(integrationTestMode: false);
        var restartedBuilder = new ContainerBuilder();
        restartedBuilder.Populate(restartedServices);
        await using var restartedContainer = restartedBuilder.Build();
        var restarted = restartedContainer.Resolve<IScheduler>();
        var restoredJob = await restarted.GetJobDetail(jobKey, CancellationToken);
        var restoredState = await restarted.GetTriggerState(triggerKey, CancellationToken);
        await restarted.Start(CancellationToken);
        await restarted.TriggerJob(jobKey, cancellationToken: CancellationToken);
        await completions.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        var pausedAfterFire = await restarted.GetTriggerState(triggerKey, CancellationToken);
        await restarted.Shutdown(true, CancellationToken);

        // Assert
        restoredJob.ShouldNotBeNull();
        restoredJob.JobDataMap.GetString("payload").ShouldBe("original");
        restoredState.ShouldBe(TriggerState.Paused);
        pausedAfterFire.ShouldBe(TriggerState.Paused);
        completions.Payloads.ToArray().ShouldBe(["original"]);
        completions.Created.ShouldBe(1);
        completions.Released.ShouldBe(1);
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT JOB_NAME FROM QRTZ_JOB_DETAILS WHERE SCHED_NAME = 'Reaparr Scheduler' ORDER BY JOB_NAME";
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(CancellationToken))
            names.Add(reader.GetString(0));
        names.ShouldBe(["control", "persisted"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldDisposeJobScopeAndInvokeTerminalListener_WhenJobCompletesOrFails(bool fail)
    {
        // Arrange
        var state = new ProbeState();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(state);
        services.AddScoped<ProbeScope>();
        services.AddScoped<ProbeJob>();
        services.ConfigureQuartz(integrationTestMode: true);
        var builder = new ContainerBuilder();
        builder.Populate(services);
        await using var container = builder.Build();
        var scheduler = container.Resolve<IScheduler>();
        var listener = new ProbeListener();
        scheduler.ListenerManager.AddJobListener(listener);
        var key = new JobKey("scope", "upgrade-regression");

        // Act
        await scheduler.Start(CancellationToken);
        await scheduler.ScheduleJob(
            JobBuilder
                .Create<ProbeJob>()
                .WithIdentity(key)
                .UsingJobData("payload", "scope-data")
                .UsingJobData("fail", fail)
                .Build(),
            TriggerBuilder.Create().ForJob(key).StartNow().Build(),
            cancellationToken: CancellationToken
        );
        var exception = await listener.Completed.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        await state.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        await scheduler.Shutdown(true, CancellationToken);

        // Assert
        state.Created.ShouldBe(1);
        state.Released.ShouldBe(1);
        state.Payloads.ToArray().ShouldBe(["scope-data"]);
        listener.Started.ShouldBe(1);
        listener.Finished.ShouldBe(1);
        if (fail)
            exception.ShouldNotBeNull();
        else
            exception.ShouldBeNull();
    }

    [Test]
    public async Task ShouldCancelExecutionTokenAndReleaseScope_WhenRunningJobIsInterrupted()
    {
        // Arrange
        var state = new ProbeState();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(state);
        services.AddScoped<ProbeScope>();
        services.AddScoped<CancellableProbeJob>();
        services.ConfigureQuartz(integrationTestMode: true);
        var builder = new ContainerBuilder();
        builder.Populate(services);
        await using var container = builder.Build();
        var scheduler = container.Resolve<IScheduler>();
        var key = new JobKey("interrupted", "upgrade-regression");

        // Act
        await scheduler.Start(CancellationToken);
        await scheduler.ScheduleJob(
            JobBuilder.Create<CancellableProbeJob>().WithIdentity(key).Build(),
            TriggerBuilder.Create().ForJob(key).StartNow().Build(),
            cancellationToken: CancellationToken
        );
        await state.Executing.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        var interrupted = await scheduler.Interrupt(key, CancellationToken);
        await state.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        await state.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        await scheduler.Shutdown(true, CancellationToken);

        // Assert
        interrupted.ShouldBeTrue();
        state.Created.ShouldBe(1);
        state.Released.ShouldBe(1);
        state.TokenMatched.ShouldBeTrue();
        state.Payloads.ToArray().ShouldBeEmpty();
    }

    public sealed class ProbeState
    {
        public ConcurrentQueue<string> Payloads { get; } = new();
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Executing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TokenMatched;
        public int Created;
        public int Released;
    }

    public sealed class ProbeScope : IDisposable
    {
        private readonly ProbeState _state;

        public ProbeScope(ProbeState state)
        {
            _state = state;
            Interlocked.Increment(ref state.Created);
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _state.Released);
            _state.Disposed.TrySetResult();
        }
    }

    public sealed class ProbeJob(ProbeState state, ProbeScope scope) : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            GC.KeepAlive(scope);
            state.Payloads.Enqueue(context.MergedJobDataMap.GetString("payload")!);
            if (context.MergedJobDataMap.GetBoolean("fail"))
                throw new InvalidOperationException("Expected Quartz regression failure");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class CancellableProbeJob(ProbeState state, ProbeScope scope) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            GC.KeepAlive(scope);
            state.TokenMatched = cancellationToken == context.CancellationToken;
            using var registration = cancellationToken.Register(() => state.Cancelled.TrySetResult());
            state.Executing.TrySetResult();
            await state.Cancelled.Task;
        }
    }

    private sealed class ProbeListener : IJobListener
    {
        public string Name => nameof(ProbeListener);
        public int Started;
        public int Finished;
        public TaskCompletionSource<JobExecutionException?> Completed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Started);
            return ValueTask.CompletedTask;
        }

        public ValueTask JobWasExecuted(
            IJobExecutionContext context,
            JobExecutionException? jobException,
            CancellationToken cancellationToken = default
        )
        {
            Interlocked.Increment(ref Finished);
            Completed.TrySetResult(jobException);
            return ValueTask.CompletedTask;
        }
    }
}
