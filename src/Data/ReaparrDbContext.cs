using System.Data;
using System.Diagnostics;
using System.Reflection;
using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.SQLite;
using EFCore.BulkExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Reaparr.Data;

public sealed class ReaparrDbContext : DbContext, IReaparrDbContext, IReaparrDbContextDatabase
{
    private const int MAX_TRANSACTION_ATTEMPTS = 2;
    private readonly ILogger _log;
    private readonly IPathProvider _pathProvider;
    private readonly IAppRuntimeInfo _appRuntimeInfo;
    public DbSet<PlexAccount> PlexAccounts { get; set; }

    public DbSet<FolderPath> FolderPaths { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    public DbSet<PlexLibrary> PlexLibraries { get; set; }

    public DbSet<PlexLibraryAccessHistoryEvent> PlexLibraryAccessHistoryEvents { get; set; }

    public DbSet<PlexActor> PlexActors { get; set; }

    public DbSet<PlexGenre> PlexGenres { get; set; }

    public DbSet<PlexCountry> PlexCountries { get; set; }

    public DbSet<PlexMovie> PlexMovies { get; set; }

    public DbSet<PlexMovieMediaData> PlexMovieData { get; set; }

    public DbSet<MediaOverviewMovieSnapshot> MediaOverviewMovieSnapshots { get; set; }

    public DbSet<MediaOverviewTvShowSnapshot> MediaOverviewTvShowSnapshots { get; set; }

    public DbSet<PlexTvShow> PlexTvShows { get; set; }

    public DbSet<PlexTvShowMediaQuality> PlexTvShowMediaQualities { get; set; }

    public DbSet<PlexTvShowSeason> PlexTvShowSeason { get; set; }

    public DbSet<PlexTvShowSeasonMediaQuality> PlexTvShowSeasonMediaQualities { get; set; }

    public DbSet<PlexTvShowEpisode> PlexTvShowEpisodes { get; set; }

    public DbSet<PlexTvShowEpisodeMediaData> PlexTvShowEpisodeData { get; set; }

    public DbSet<PlexServer> PlexServers { get; set; }

    public DbSet<PlexServerConnection> PlexServerConnections { get; set; }

    public DbSet<PlexServerStatus> PlexServerStatuses { get; set; }

    public DbSet<LibrarySyncJobQueue> LibrarySyncJobQueues { get; set; }

    public DbSet<SonarrIntegration> SonarrIntegrations { get; set; }

    public DbSet<RadarrIntegration> RadarrIntegrations { get; set; }

    public DbSet<DownloadTaskMovie> DownloadTaskMovie { get; set; }

    public DbSet<DownloadTaskMovieFile> DownloadTaskMovieFile { get; set; }

    public DbSet<DownloadTaskMovieFileLog> DownloadTaskMovieFileLogs { get; set; }

    public DbSet<DownloadTaskTvShow> DownloadTaskTvShow { get; set; }

    public DbSet<DownloadTaskTvShowSeason> DownloadTaskTvShowSeason { get; set; }

    public DbSet<DownloadTaskTvShowEpisode> DownloadTaskTvShowEpisode { get; set; }

    public DbSet<DownloadTaskTvShowEpisodeFile> DownloadTaskTvShowEpisodeFile { get; set; }

    public DbSet<DownloadTaskTvShowEpisodeFileLog> DownloadTaskTvShowEpisodeFileLogs { get; set; }

    public DbSet<PlexAccountServer> PlexAccountServers { get; set; }

    public DbSet<PlexAccountLibrary> PlexAccountLibraries { get; set; }

    public DbSet<PlexLibraryActors> PlexLibraryActors { get; set; }

    public DbSet<PlexLibraryCountries> PlexLibraryCountries { get; set; }

    public DbSet<PlexLibraryGenres> PlexLibraryGenres { get; set; }

    public DbSet<PlexMovieActors> PlexMovieActors { get; set; }

    public DbSet<PlexMovieCountries> PlexMovieCountries { get; set; }

    public DbSet<PlexMovieGenres> PlexMovieGenres { get; set; }

    public DbSet<PlexTvShowActors> PlexTvShowActors { get; set; }

    public DbSet<PlexTvShowGenres> PlexTvShowGenres { get; set; }

    public DbSet<PlexTvShowCountries> PlexTvShowCountries { get; set; }

    #region Comparison

    public DbSet<PlexComparisonState> PlexComparisonScopes { get; set; }

    public DbSet<PlexMovieComparison> PlexMovieComparisons { get; set; }

    public DbSet<PlexTvShowComparison> PlexTvShowComparisons { get; set; }

    public DbSet<PlexSeasonComparison> PlexSeasonComparisons { get; set; }

    public DbSet<PlexEpisodeComparison> PlexEpisodeComparisons { get; set; }

    #endregion

    public string DatabaseName { get; }

    /// <inheritdoc/>
    public async Task BulkInsertAsync<T>(
        IList<T> entities,
        BulkConfig? bulkConfig = null,
        CancellationToken cancellationToken = default
    )
        where T : class =>
        await ExecuteBulkAsync(
            () =>
                DbContextBulkExtensions.BulkInsertAsync(
                    this,
                    entities,
                    bulkConfig,
                    cancellationToken: cancellationToken
                ),
            cancellationToken
        );

    /// <inheritdoc/>
    public async Task BulkUpdateAsync<T>(
        IList<T> entities,
        BulkConfig? bulkConfig = null,
        CancellationToken cancellationToken = default
    )
        where T : class =>
        await ExecuteBulkAsync(
            () =>
                DbContextBulkExtensions.BulkUpdateAsync(
                    this,
                    entities,
                    bulkConfig,
                    cancellationToken: cancellationToken
                ),
            cancellationToken
        );

    /// <inheritdoc/>
    public Task<Result<T>> ExecuteTransactionAsync<T>(
        Func<IReaparrDbContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default
    ) => Result.Try(() => ExecuteTransactionWithRetryAsync(operation, cancellationToken));

    /// <inheritdoc/>
    public Task<Result> ExecuteTransactionAsync(
        Func<IReaparrDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default
    ) => Result.Try(() => ExecuteTransactionWithRetryAsync(operation, cancellationToken));

    /// <inheritdoc/>
    public Task<int> ExecuteSqlInterpolatedAsync(
        FormattableString sql,
        CancellationToken cancellationToken = default
    ) => Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);

    private async Task<T> ExecuteTransactionWithRetryAsync<T>(
        Func<IReaparrDbContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);
        var stopwatch = Stopwatch.StartNew();

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var openedHere = Database.GetDbConnection().State != ConnectionState.Open;

            try
            {
                if (openedHere)
                    await Database.OpenConnectionAsync(cancellationToken);

                await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    var result = await operation(this, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
                finally
                {
                    ChangeTracker.Clear();
                }
            }
            catch (SqliteException ex) when (IsRetryableLockFailure(ex) && attempt < MAX_TRANSACTION_ATTEMPTS)
            {
                await Task.Delay(Random.Shared.Next(75, 151), cancellationToken);
            }
            catch (SqliteException ex) when (IsRetryableLockFailure(ex))
            {
                _log.Error(
                    ex,
                    "SQLite {OperationCategory} failed after {AttemptCount} transaction attempts and {ElapsedMilliseconds} ms",
                    "write transaction",
                    attempt,
                    stopwatch.ElapsedMilliseconds
                );
                throw;
            }
            finally
            {
                if (openedHere)
                    await Database.CloseConnectionAsync();
            }
        }
    }

    private Task ExecuteTransactionWithRetryAsync(
        Func<IReaparrDbContext, CancellationToken, Task> operation,
        CancellationToken cancellationToken
    ) =>
        ExecuteTransactionWithRetryAsync(
            async (context, ct) =>
            {
                await operation(context, ct);
                return 0;
            },
            cancellationToken
        );

    private static bool IsRetryableLockFailure(SqliteException exception)
    {
        const int sqliteBusy = 5;
        const int sqliteLocked = 6;
        return exception.SqliteErrorCode is sqliteBusy or sqliteLocked;
    }

    /// <inheritdoc/>
    public void ClearChangeTracker() => ChangeTracker.Clear();

    public ReaparrDbContext(ILogger log, IPathProvider pathProvider, IAppRuntimeInfo appRuntimeInfo)
    {
        _log = log.ForContext<ReaparrDbContext>();
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;
        DatabaseName = pathProvider.DatabaseName;
    }

    /// <summary>
    /// Constructor for DbContextFactory - accepts pre-configured options.
    /// This is required for AddDbContextFactory to work properly.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public ReaparrDbContext(
        DbContextOptions<ReaparrDbContext> options,
        ILogger log,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
        : base(options)
    {
        _log = log.ForContext<ReaparrDbContext>();
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;
        DatabaseName = pathProvider.DatabaseName;
    }

    /// <summary>
    /// Constructor for DbContextFactory with explicit database name - accepts pre-configured options used in unit and integration testing.
    /// </summary>
    public ReaparrDbContext(
        DbContextOptions<ReaparrDbContext> options,
        ILogger log,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        string databaseName
    )
        : base(options)
    {
        _log = log.ForContext<ReaparrDbContext>();
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;
        DatabaseName = databaseName;

        Database.OpenConnection();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.DefaultConfiguration(_pathProvider, _appRuntimeInfo, typeof(ReaparrDbContext));
        }

        optionsBuilder.UseSeeding(ReaparrDBContextSeed.Seed(_pathProvider));

        optionsBuilder.UseAsyncSeeding(ReaparrDBContextSeed.SeedAsync(_pathProvider));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.UseCollation(OrderByNaturalExtensions.CollationName);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        builder.AddQuartz(x => x.UseSqlite());

        base.OnModelCreating(builder);
    }

    /// <summary>
    /// Executes a bulk operation with an open database connection.
    /// EFCore.BulkExtensions requires this for SQLite to avoid "Prepare can only be called when the connection is open."
    /// </summary>
    private async Task ExecuteBulkAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var conn = Database.GetDbConnection();
        var openedHere = conn.State != ConnectionState.Open;
        if (openedHere)
            await Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await operation();
        }
        finally
        {
            if (openedHere)
                await Database.CloseConnectionAsync();
        }
    }

    /// <inheritdoc/>
    public bool CanConnect()
    {
        if (!Database.CanConnect())
        {
            _log.Error("Database {DatabaseName} is not connectable", DatabaseName);
            return false;
        }

        var result = Result.Try(() =>
        {
            DbContextConnections.InitializeDatabase(Database.GetDbConnection());
            DbContextConnections.EnableWriteAheadLogging(Database.GetDbConnection());
        });
        result.LogIfFailed();

        return result.IsSuccess;
    }

    /// <inheritdoc/>
    public bool IsInMemory() => Database.IsInMemory();

    /// <inheritdoc/>
    public void CloseConnection() => Database.CloseConnection();

    /// <inheritdoc/>
    public Result<bool> EnsureDeleted()
    {
        try
        {
            return Result.Ok(Database.EnsureDeleted());
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    /// <inheritdoc/>
    public Result Migrate() =>
        Result.Try(() =>
        {
            DbContextConnections.InitializeDatabase(Database.GetDbConnection());
            DbContextConnections.EnableWriteAheadLogging(Database.GetDbConnection());
            Database.Migrate();
        });

    /// <inheritdoc/>
    public Task<Result<int>> Optimize(CancellationToken cancellationToken = default) =>
        Result.Try(async Task<int> () => await Database.ExecuteSqlRawAsync("PRAGMA optimize;", cancellationToken));

    /// <inheritdoc/>
    public IEnumerable<string> GetPendingMigrations() => Database.GetPendingMigrations();

    /// <inheritdoc/>
    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
