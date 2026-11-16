using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NaturalSort.Extension;
using Reaparr.Environment;

namespace Reaparr.Data.Contracts;

public static class DbContextConnections
{
    private const int BUSY_TIMEOUT_SECONDS = 120;
    private const int WAL_AUTO_CHECKPOINT_PAGES = 1000;
    private const int JOURNAL_SIZE_LIMIT_BYTES = 134217728;
    private const int MEMORY_MAP_SIZE_BYTES = 268435456;
    private const int PAGE_CACHE_SIZE_KIBIBYTES = 20000;

    private static readonly string _databaseInitializationPragmas = $"""
        PRAGMA wal_autocheckpoint = {WAL_AUTO_CHECKPOINT_PAGES};
        PRAGMA journal_size_limit = {JOURNAL_SIZE_LIMIT_BYTES};
        PRAGMA mmap_size = {MEMORY_MAP_SIZE_BYTES};
        PRAGMA locking_mode = NORMAL;
        PRAGMA secure_delete = OFF;
        """;

    private static readonly string _connectionPragmas = $"""
        PRAGMA temp_store = MEMORY;
        PRAGMA cache_size = -{PAGE_CACHE_SIZE_KIBIBYTES};
        PRAGMA synchronous = NORMAL;
        """;

    private static readonly NaturalSortComparer _naturalSortComparer = new(StringComparison.OrdinalIgnoreCase);
    private static readonly OnConnectionOpenInterceptor _connectionOpenInterceptor = new();

    public static string GetConnectionString(string dataSource, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder
        {
            Mode = mode,
            ForeignKeys = true,
            DataSource = dataSource,
            Cache = mode == SqliteOpenMode.Memory ? SqliteCacheMode.Shared : SqliteCacheMode.Default,
            Pooling = true,
            DefaultTimeout = BUSY_TIMEOUT_SECONDS,
        }.ToString();

    public static void ConfigureSqlite(
        this DbContextOptionsBuilder optionsBuilder,
        string dataSource,
        SqliteOpenMode mode
    )
    {
        optionsBuilder.AddInterceptors(_connectionOpenInterceptor);
        optionsBuilder.UseSqlite(GetConnectionString(dataSource, mode));
    }

    /// <summary>
    /// Configures requirements that must exist on every physical SQLite connection.
    /// Busy timeout is supplied by <see cref="GetConnectionString"/> and verified here;
    /// no PRAGMAs are reset when a pooled connection is opened.
    /// </summary>
    public static void ConfigureOpenedSqliteConnection(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
            return;

        using var command = sqliteConnection.CreateCommand();
        command.CommandText = _connectionPragmas;
        command.ExecuteNonQuery();

        sqliteConnection.CreateCollation(OrderByNaturalExtensions.CollationName, (x, y) =>
            _naturalSortComparer.Compare(x, y)
        );
    }

    /// <summary>
    /// Applies database tuning once during startup, migration, or connectivity validation.
    /// </summary>
    public static void InitializeDatabase(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
            throw new InvalidOperationException("SQLite configuration can only be applied to a SQLite connection.");

        var openedHere = sqliteConnection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            sqliteConnection.Open();

        try
        {
            ConfigureOpenedSqliteConnection(sqliteConnection);

            using var command = sqliteConnection.CreateCommand();
            command.CommandText = _databaseInitializationPragmas;
            command.ExecuteNonQuery();
        }
        finally
        {
            if (openedHere)
                sqliteConnection.Close();
        }
    }

    public static void EnableWriteAheadLogging(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
            throw new InvalidOperationException("WAL mode can only be enabled for a SQLite connection.");

        if (new SqliteConnectionStringBuilder(sqliteConnection.ConnectionString).Mode == SqliteOpenMode.Memory)
            return;

        var openedHere = sqliteConnection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            sqliteConnection.Open();

        try
        {
            using var command = sqliteConnection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode = WAL;";
            var journalMode = command.ExecuteScalar()?.ToString();
            if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQLite did not enable WAL mode for the configured database.");
        }
        finally
        {
            if (openedHere)
                sqliteConnection.Close();
        }
    }

    public static void DefaultConfiguration(
        this DbContextOptionsBuilder optionsBuilder,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Type contextType
    )
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        if (appRuntimeInfo.IsDevelopmentEnvironment || appRuntimeInfo.IsIntegrationTestMode)
        {
            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging();
        }

        optionsBuilder.ConfigureSqlite(pathProvider.DatabasePath, SqliteOpenMode.ReadWriteCreate);

        var sqliteOptionsBuilder = new SqliteDbContextOptionsBuilder(optionsBuilder);
        sqliteOptionsBuilder.MigrationsAssembly(contextType.Assembly.FullName);

        // Use split queries for multiple collection includes to avoid cartesian explosion.
        // This resolves: "Compiling a query which loads related collections for more than one
        // collection navigation... no 'QuerySplittingBehavior' has been configured"
        // See: https://go.microsoft.com/fwlink/?linkid=2134277
        sqliteOptionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }

    private sealed class OnConnectionOpenInterceptor : DbConnectionInterceptor
    {
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            ConfigureOpenedSqliteConnection(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            ConfigureOpenedSqliteConnection(connection);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
