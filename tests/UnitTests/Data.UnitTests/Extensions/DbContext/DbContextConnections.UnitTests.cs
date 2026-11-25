using Microsoft.Data.Sqlite;
using Reaparr.Identity;

namespace Reaparr.Data.UnitTests;

public class DbContextConnectionsUnitTests : BaseUnitTest
{
    [Test]
    public async Task ShouldKeepBusyTimeoutOnEveryPhysicalConnection()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"reaparr-sqlite-config-{Guid.NewGuid():N}.db");
        var pathProvider = new MockPathProvider($"sqlite-config-{Guid.NewGuid():N}");
        var appRuntimeInfo = new MockAppRuntimeInfo();
        var reaparrOptions = new DbContextOptionsBuilder<ReaparrDbContext>();
        reaparrOptions.ConfigureSqlite(databasePath, SqliteOpenMode.ReadWriteCreate);
        var authOptions = new DbContextOptionsBuilder<AuthDbContext>();
        authOptions.ConfigureSqlite(databasePath, SqliteOpenMode.ReadWriteCreate);
        await using var setupContext = new ReaparrDbContext(reaparrOptions.Options, Log, pathProvider, appRuntimeInfo);
        DbContextConnections.InitializeDatabase(setupContext.Database.GetDbConnection());
        DbContextConnections.EnableWriteAheadLogging(setupContext.Database.GetDbConnection());

        // Act
        await using var reaparrContext1 = new ReaparrDbContext(
            reaparrOptions.Options,
            Log,
            pathProvider,
            appRuntimeInfo
        );
        await using var authContext = new AuthDbContext(authOptions.Options, Log, pathProvider, appRuntimeInfo);
        await using var reaparrContext2 = new ReaparrDbContext(
            reaparrOptions.Options,
            Log,
            pathProvider,
            appRuntimeInfo
        );

        var configurations = await Task.WhenAll(
            ReadConfiguration(reaparrContext1),
            ReadConfiguration(authContext),
            ReadConfiguration(reaparrContext2)
        );

        // Assert
        configurations.ShouldAllBe(x => x == _expectedConfiguration);
    }

    [Test]
    public async Task ShouldApplyConnectionPragmasWhenOpeningConnections()
    {
        // Arrange
        var databasePath = Path.Combine(Path.GetTempPath(), $"reaparr-sqlite-config-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<ReaparrDbContext>();
        options.ConfigureSqlite(databasePath, SqliteOpenMode.ReadWriteCreate);
        var pathProvider = new MockPathProvider($"sqlite-config-{Guid.NewGuid():N}");
        var appRuntimeInfo = new MockAppRuntimeInfo();

        await using var setupContext = new ReaparrDbContext(options.Options, Log, pathProvider, appRuntimeInfo);
        DbContextConnections.InitializeDatabase(setupContext.Database.GetDbConnection());
        DbContextConnections.EnableWriteAheadLogging(setupContext.Database.GetDbConnection());

        await using var command = setupContext.Database.GetDbConnection().CreateCommand();
        await setupContext.Database.OpenConnectionAsync();
        command.CommandText = "PRAGMA cache_size = -1234;";
        await command.ExecuteNonQueryAsync();
        await setupContext.Database.CloseConnectionAsync();

        // Act
        await using var context = new ReaparrDbContext(options.Options, Log, pathProvider, appRuntimeInfo);
        var configuration = await ReadConfiguration(context);
        var cacheSize = await ReadInt64(context.Database.GetDbConnection(), "cache_size");
        await using var queryCommand = context.Database.GetDbConnection().CreateCommand();
        queryCommand.CommandText = "SELECT 1;";
        var result = await queryCommand.ExecuteScalarAsync();

        // Assert
        result.ShouldBe(1L);
        configuration.DefaultCommandTimeout.ShouldBe(120);
        cacheSize.ShouldBe(-20000);
    }

    [Test]
    public void ShouldUseSharedCacheForInMemoryDatabases()
    {
        // Arrange

        // Act
        var connectionString = DbContextConnections.GetConnectionString(
            $"reaparr-in-memory-{Guid.NewGuid():N}",
            SqliteOpenMode.Memory
        );

        // Assert
        new SqliteConnectionStringBuilder(connectionString).Cache.ShouldBe(SqliteCacheMode.Shared);
    }

    private static readonly SqliteConfiguration _expectedConfiguration = new(120, "wal");

    private static async Task<SqliteConfiguration> ReadConfiguration(DbContext context)
    {
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();

        return new SqliteConfiguration(
            ((SqliteConnection)connection).DefaultTimeout,
            await ReadText(connection, "journal_mode")
        );
    }

    private static async Task<long> ReadInt64(System.Data.Common.DbConnection connection, string pragma)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string> ReadText(System.Data.Common.DbConnection connection, string pragma)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed record SqliteConfiguration(long DefaultCommandTimeout, string JournalMode);
}
