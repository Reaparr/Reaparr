using System.Collections.Concurrent;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task ShouldDeferPersistentStoreConstructionUntilAfterMigration_AndPreservePausedJobsAndPayloads()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"reaparr-quartz-{Guid.NewGuid():N}.db");
        var databasePathReads = 0;
        var paths = new Mock<IPathProvider>();
        paths
            .SetupGet(x => x.DatabasePath)
            .Returns(() =>
            {
                Interlocked.Increment(ref databasePathReads);
                return databasePath;
            });
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
        var completions = new ProbeState();
        var restartedServices = new ServiceCollection();
        restartedServices.AddLogging();
        restartedServices.AddSingleton(paths.Object);
        restartedServices.AddSingleton(completions);
        restartedServices.AddScoped<ProbeScope>();
        restartedServices.AddScoped<ProbeJob>();
        restartedServices.ConfigureQuartz(integrationTestMode: false);
        var applicationJobTypes = typeof(Reaparr.Application.BackgroundJobsSetup)
            .Assembly.GetTypes()
            .Where(type => typeof(IJob).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .OrderBy(type => type.FullName)
            .ToArray();
        var registeredApplicationJobTypes = restartedServices
            .Where(descriptor => descriptor.ImplementationType is not null)
            .Select(descriptor => descriptor.ImplementationType!)
            .Where(applicationJobTypes.Contains)
            .OrderBy(type => type.FullName)
            .ToArray();
        var restartedBuilder = new ContainerBuilder();
        restartedBuilder.Populate(restartedServices);
        await using var restartedContainer = restartedBuilder.Build();
        databasePathReads.ShouldBe(0);
        File.Exists(databasePath).ShouldBeFalse();
        registeredApplicationJobTypes.ShouldBe(applicationJobTypes);
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
        var serializer = new Quartz.Impl.SystemTextJsonObjectSerializer();
        var payload = new Dictionary<string, object> { ["payload"] = "original", ["fail"] = false };
        var serializedPayload = serializer.Serialize(payload);
        var jobClass = typeof(ProbeJob).AssemblyQualifiedName!;
        await database.Database.ExecuteSqlAsync(
            $"INSERT INTO QRTZ_JOB_DETAILS (SCHED_NAME, JOB_NAME, JOB_GROUP, JOB_CLASS_NAME, IS_DURABLE, IS_NONCONCURRENT, IS_UPDATE_DATA, REQUESTS_RECOVERY, JOB_DATA) VALUES ('Reaparr Scheduler', {jobKey.Name}, {jobKey.Group}, {jobClass}, 1, 0, 0, 0, {serializedPayload})",
            CancellationToken
        );
        await database.Database.ExecuteSqlAsync(
            $"INSERT INTO QRTZ_JOB_DETAILS (SCHED_NAME, JOB_NAME, JOB_GROUP, JOB_CLASS_NAME, IS_DURABLE, IS_NONCONCURRENT, IS_UPDATE_DATA, REQUESTS_RECOVERY) VALUES ('Reaparr Scheduler', {controlKey.Name}, {controlKey.Group}, {jobClass}, 1, 0, 0, 0)",
            CancellationToken
        );
        var futureTime = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();
        await database.Database.ExecuteSqlAsync(
            $"INSERT INTO QRTZ_TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, JOB_NAME, JOB_GROUP, NEXT_FIRE_TIME, PRIORITY, TRIGGER_STATE, TRIGGER_TYPE, START_TIME, MISFIRE_INSTR) VALUES ('Reaparr Scheduler', {triggerKey.Name}, {triggerKey.Group}, {jobKey.Name}, {jobKey.Group}, {futureTime}, 5, 'PAUSED', 'SIMPLE', {futureTime}, 0)",
            CancellationToken
        );
        await database.Database.ExecuteSqlAsync(
            $"INSERT INTO QRTZ_SIMPLE_TRIGGERS (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP, REPEAT_COUNT, REPEAT_INTERVAL, TIMES_TRIGGERED) VALUES ('Reaparr Scheduler', {triggerKey.Name}, {triggerKey.Group}, 0, 0, 0)",
            CancellationToken
        );
        await database.Database.MigrateAsync(CancellationToken);
        var now = DateTimeOffset.UtcNow;
        await database.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO QRTZ_EXECUTION_HISTORY
                (SCHED_NAME, ENTRY_ID, INSTANCE_NAME, JOB_NAME, JOB_GROUP, TRIGGER_NAME, TRIGGER_GROUP, FIRED_TIME, RUN_TIME, SUCCEEDED)
            VALUES
                ('Reaparr Scheduler', 'within-week', 'old-node', 'persisted', 'upgrade-regression', 'paused', 'upgrade-regression', {now.AddDays(
                -6
            ).UtcTicks}, 0, 1),
                ('Reaparr Scheduler', 'expired', 'old-node', 'persisted', 'upgrade-regression', 'paused', 'upgrade-regression', {now.AddDays(
                -8
            ).UtcTicks}, 0, 1)
            """,
            CancellationToken
        );
        (
            await database
                .Database.SqlQueryRaw<string>("SELECT ENTRY_ID AS Value FROM QRTZ_EXECUTION_HISTORY ORDER BY ENTRY_ID")
                .ToListAsync(CancellationToken)
        ).ShouldBe(["expired", "within-week"]);
        Interlocked.Exchange(ref databasePathReads, 0);
        databasePathReads.ShouldBe(0);
        var schemaConnection = database.Database.GetDbConnection();
        await database.Database.OpenConnectionAsync(CancellationToken);
        await using (var schemaCommand = schemaConnection.CreateCommand())
        {
            schemaCommand.CommandText = "PRAGMA table_info('QRTZ_TRIGGERS')";
            await using var schemaReader = await schemaCommand.ExecuteReaderAsync(CancellationToken);
            var triggerColumns = new Dictionary<string, bool>();
            while (await schemaReader.ReadAsync(CancellationToken))
                triggerColumns.Add(schemaReader.GetString(1), schemaReader.GetInt64(3) == 0);

            triggerColumns["MISFIRE_THRESHOLD"].ShouldBeTrue();
            triggerColumns["FIXED_DELAY"].ShouldBeTrue();
        }
        await using (var schedulerColumnsCommand = schemaConnection.CreateCommand())
        {
            schedulerColumnsCommand.CommandText = "PRAGMA table_info('QRTZ_SCHEDULER_STATE')";
            await using var schedulerColumnsReader = await schedulerColumnsCommand.ExecuteReaderAsync(
                CancellationToken
            );
            var schedulerColumns = new Dictionary<string, bool>();
            while (await schedulerColumnsReader.ReadAsync(CancellationToken))
                schedulerColumns.Add(schedulerColumnsReader.GetString(1), schedulerColumnsReader.GetInt64(3) == 0);

            schedulerColumns["QUARTZ_VERSION"].ShouldBeTrue();
        }
        await using (var firedTriggerColumnsCommand = schemaConnection.CreateCommand())
        {
            firedTriggerColumnsCommand.CommandText = "PRAGMA table_info('QRTZ_FIRED_TRIGGERS')";
            await using var firedTriggerColumnsReader = await firedTriggerColumnsCommand.ExecuteReaderAsync(
                CancellationToken
            );
            var firedTriggerColumns = new Dictionary<string, bool>();
            while (await firedTriggerColumnsReader.ReadAsync(CancellationToken))
                firedTriggerColumns.Add(
                    firedTriggerColumnsReader.GetString(1),
                    firedTriggerColumnsReader.GetInt64(3) == 0
                );

            firedTriggerColumns["INTERRUPT_REQUESTED_TIME"].ShouldBeTrue();
        }
        await using (var oldRowsCommand = schemaConnection.CreateCommand())
        {
            oldRowsCommand.CommandText = "SELECT MISFIRE_THRESHOLD, FIXED_DELAY FROM QRTZ_TRIGGERS";
            await using var oldRowsReader = await oldRowsCommand.ExecuteReaderAsync(CancellationToken);
            (await oldRowsReader.ReadAsync(CancellationToken)).ShouldBeTrue();
            (await oldRowsReader.IsDBNullAsync(0, CancellationToken)).ShouldBeTrue();
            (await oldRowsReader.IsDBNullAsync(1, CancellationToken)).ShouldBeTrue();
        }
        await database.Database.CloseConnectionAsync();
        databasePathReads.ShouldBe(0);

        // Act
        var restarted = restartedContainer.Resolve<IScheduler>();
        var restoredJob = await restarted.GetJobDetail(jobKey, CancellationToken);
        var restoredState = await restarted.GetTriggerState(triggerKey, CancellationToken);
        await restarted.Start(CancellationToken);
        databasePathReads.ShouldBe(1);
        await restarted.TriggerJob(jobKey, cancellationToken: CancellationToken);
        await completions.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken);
        foreach (var status in new[] { JobStatus.Failed, JobStatus.Cancelled })
        {
            await restarted.TriggerJob(
                jobKey,
                new JobDataMap { ["outcome"] = (int)status, [SchedulerConstants.JobInput] = "\"private input\"" },
                CancellationToken
            );
        }

        await WaitForDatabaseConditionAsync(async () =>
            await database
                .Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM QRTZ_EXECUTION_HISTORY WHERE RESULT IS NOT NULL"
                )
                .SingleAsync(CancellationToken) == 3
            && !await database
                .Database.SqlQueryRaw<string>("SELECT ENTRY_ID AS Value FROM QRTZ_EXECUTION_HISTORY")
                .AnyAsync(id => id == "expired", CancellationToken)
        );
        var pausedAfterFire = await restarted.GetTriggerState(triggerKey, CancellationToken);
        await restarted.Shutdown(true, CancellationToken);

        registeredApplicationJobTypes.ShouldBe(applicationJobTypes);
        databasePathReads.ShouldBe(1);
        restoredJob.ShouldNotBeNull();
        restoredJob.JobDataMap.GetString("payload").ShouldBe("original");
        restoredState.ShouldBe(TriggerState.Paused);
        pausedAfterFire.ShouldBe(TriggerState.Paused);
        completions.Payloads.ToArray().ShouldBe(["original", "original", "original"]);
        completions.Created.ShouldBe(3);
        completions.Released.ShouldBe(3);
        (
            await database
                .Database.SqlQueryRaw<string>(
                    "SELECT ENTRY_ID AS Value FROM QRTZ_EXECUTION_HISTORY WHERE RESULT IS NULL"
                )
                .ToListAsync(CancellationToken)
        ).ShouldBe(["within-week"]);
        var history = await database
            .Database.SqlQueryRaw<int>(
                """
                SELECT RESULT AS Value FROM QRTZ_EXECUTION_HISTORY
                WHERE JOB_NAME = 'persisted' AND RESULT IS NOT NULL AND JOB_INPUT IS NULL
                  AND EXECUTION_LOG IS NULL AND ERROR_MESSAGE IS NULL AND COALESCE(SUMMARY, '') <> 'private failure detail'
                """
            )
            .ToListAsync(CancellationToken);
        history
            .Order()
            .ShouldBe(
                new[] { (int)JobRunResult.Succeeded, (int)JobRunResult.Failed, (int)JobRunResult.Cancelled }.Order()
            );
        (
            await database
                .Database.SqlQueryRaw<long>(
                    "SELECT RUN_COUNT AS Value FROM QRTZ_JOB_STATUS WHERE JOB_NAME = 'persisted'"
                )
                .SingleAsync(CancellationToken)
        ).ShouldBe(3L);
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
            if (context.MergedJobDataMap.TryGetValue("outcome", out var outcome) && outcome is int status)
                context.SetResult((JobStatus)status, "private failure detail");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class CancellableProbeJob(ProbeState state, ProbeScope scope) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            GC.KeepAlive(scope);
            state.TokenMatched = cancellationToken == context.CancellationToken;
            await using var registration = cancellationToken.Register(() => state.Cancelled.TrySetResult());
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
