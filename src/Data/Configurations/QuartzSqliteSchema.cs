using AppAny.Quartz.EntityFrameworkCore.Migrations;

namespace Reaparr.Data;

/// <summary>
/// Completes the SQLite schema supplied by AppAny 0.6.2 for Quartz 4.7.3.
/// </summary>
/// <remarks>
/// <para>
/// Apply this after AppAny registers its SQLite model. The four scheduler columns support normal
/// persistent scheduling. The execution-history fields, lookup indexes, and job-status table support
/// persistent execution history. The job-status entity exists only in the EF model; Quartz reads and
/// writes its rows directly.
/// </para>
/// <para>
/// TODO Tracks <see href="https://github.com/appany/AppAny.Quartz.EntityFrameworkCore.Migrations/pull/85">AppAny PR #85</see>.
/// Once a released AppAny SQLite package supplies equivalent mappings, upgrade to that package, remove
/// this class and its call, and check the EF model against the snapshot. Do not recreate the existing
/// tables or add the same columns again if any mapping differences need to be reconciled. Leave the
/// applied Reaparr migrations intact.
/// </para>
/// </remarks>
internal static class QuartzSqliteSchema
{
    internal static void Configure(ModelBuilder builder)
    {
        builder
            .Entity<QuartzTrigger>()
            .Property<long?>("MisfireThreshold")
            .HasColumnName("MISFIRE_THRESHOLD")
            .HasColumnType("bigint");

        builder.Entity<QuartzTrigger>().Property<bool?>("FixedDelay").HasColumnName("FIXED_DELAY").HasColumnType("bit");

        builder
            .Entity<QuartzFiredTrigger>()
            .Property<long?>("InterruptRequestedTime")
            .HasColumnName("INTERRUPT_REQUESTED_TIME")
            .HasColumnType("bigint");

        builder
            .Entity<QuartzSchedulerState>()
            .Property<string>("QuartzVersion")
            .IsRequired(false)
            .HasColumnName("QUARTZ_VERSION")
            .HasColumnType("text");

        ConfigureQuartzExecutionHistory(builder);
        ConfigureQuartzJobStatus(builder);
    }

    private static void ConfigureQuartzExecutionHistory(ModelBuilder builder)
    {
        var entity = builder.Entity<QuartzExecutionHistory>();
        entity.Property<int?>("Result").HasColumnName("RESULT").HasColumnType("integer");
        entity.Property<string>("Summary").HasColumnName("SUMMARY").HasColumnType("nvarchar(1000)");
        entity.Property<string>("Metrics").HasColumnName("METRICS").HasColumnType("text");
        entity.Property<bool?>("Manual").HasColumnName("MANUAL").HasColumnType("bit");
        entity.Property<string>("FireInstanceId").HasColumnName("FIRE_INSTANCE_ID").HasColumnType("nvarchar(140)");
        entity.Property<string>("JobInput").HasColumnName("JOB_INPUT").HasColumnType("text");
        entity.Property<bool?>("JobInputTooLarge").HasColumnName("JOB_INPUT_TOO_LARGE").HasColumnType("bit");

        entity.HasIndex("SchedulerName", "JobGroup", "JobName", "FiredTime").HasDatabaseName("IDX_QRTZ_EH_JOB_TIME");
        entity.HasIndex("SchedulerName", "FireInstanceId").HasDatabaseName("IDX_QRTZ_EH_FIRE_INST");
        entity
            .HasIndex("SchedulerName", "TriggerGroup", "TriggerName", "FiredTime")
            .HasDatabaseName("IDX_QRTZ_EH_TRIG_TIME");
    }

    private static void ConfigureQuartzJobStatus(ModelBuilder builder)
    {
        builder.SharedTypeEntity<Dictionary<string, object>>(
            "AppAny.Quartz.EntityFrameworkCore.Migrations.QuartzJobStatus",
            entity =>
            {
                entity
                    .Property<string>("SchedulerName")
                    .IsRequired()
                    .HasColumnName("SCHED_NAME")
                    .HasColumnType("nvarchar(120)");
                entity
                    .Property<string>("JobGroup")
                    .IsRequired()
                    .HasColumnName("JOB_GROUP")
                    .HasColumnType("nvarchar(150)");
                entity
                    .Property<string>("JobName")
                    .IsRequired()
                    .HasColumnName("JOB_NAME")
                    .HasColumnType("nvarchar(150)");
                entity
                    .Property<long>("FirstFiredTime")
                    .IsRequired()
                    .HasColumnName("FIRST_FIRED_TIME")
                    .HasColumnType("bigint");
                entity
                    .Property<long>("LastFiredTime")
                    .IsRequired()
                    .HasColumnName("LAST_FIRED_TIME")
                    .HasColumnType("bigint");
                entity.Property<int>("LastResult").IsRequired().HasColumnName("LAST_RESULT").HasColumnType("integer");
                entity
                    .Property<long>("LastRunTime")
                    .IsRequired()
                    .HasColumnName("LAST_RUN_TIME")
                    .HasColumnType("bigint");
                entity
                    .Property<string>("LastInstanceName")
                    .IsRequired()
                    .HasColumnName("LAST_INSTANCE_NAME")
                    .HasColumnType("nvarchar(200)");
                entity.Property<string>("LastEntryId").HasColumnName("LAST_ENTRY_ID").HasColumnType("nvarchar(140)");
                entity.Property<string>("LastSummary").HasColumnName("LAST_SUMMARY").HasColumnType("nvarchar(1000)");
                entity.Property<long?>("LastSuccessTime").HasColumnName("LAST_SUCCESS_TIME").HasColumnType("bigint");
                entity.Property<long?>("LastFailureTime").HasColumnName("LAST_FAILURE_TIME").HasColumnType("bigint");
                entity
                    .Property<string>("LastFailureMessage")
                    .HasColumnName("LAST_FAILURE_MESSAGE")
                    .HasColumnType("nvarchar(1000)");
                entity
                    .Property<int>("ConsecutiveFailures")
                    .IsRequired()
                    .ValueGeneratedOnAdd()
                    .HasDefaultValue(0)
                    .HasColumnName("CONSECUTIVE_FAILURES")
                    .HasColumnType("integer");
                entity
                    .Property<long>("RunCount")
                    .IsRequired()
                    .ValueGeneratedOnAdd()
                    .HasDefaultValue(0L)
                    .HasColumnName("RUN_COUNT")
                    .HasColumnType("bigint");
                entity
                    .Property<long>("FailureCount")
                    .IsRequired()
                    .ValueGeneratedOnAdd()
                    .HasDefaultValue(0L)
                    .HasColumnName("FAILURE_COUNT")
                    .HasColumnType("bigint");
                entity.HasKey("SchedulerName", "JobGroup", "JobName");
                entity.ToTable("QRTZ_JOB_STATUS");
            }
        );
    }
}
