using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Reaparr.BaseTests;

public static partial class MockDatabase
{
    private static readonly Serilog.ILogger _log = LogFactory.Create(typeof(MockDatabase));

    private static readonly string _databaseTemplateName = GetMemoryDatabaseName();
    private static SqliteConnection? _databaseTemplateConnection;

    // SQLite migrations and EFCore.BulkExtensions seeding are not reliable when multiple test databases initialize concurrently.
    private static readonly SemaphoreSlim _setupLock = new(1, 1);

    #region Methods

    #region Private

    private static async Task<ReaparrDbContext> AddPlexServers(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        if (!config.ShouldHavePlexServer)
            return context;

        // Generate fake servers
        for (var i = 0; i < config.PlexServerCount; i++)
        {
            var plexServer = FakeData.GetPlexServer(seed, options).Generate();
            context.PlexServers.Add(plexServer);
        }

        await context.SaveChangesAsync();
        var plexServers = await context.PlexServers.ToListAsync();

        // Add Connection to each server
        foreach (var plexServer in plexServers)
        {
            var connections = FakeData
                .GetPlexServerConnections(seed, plexServerId: plexServer.Id)
                .Generate(config.PlexServerConnectionPerServerCount);
            context.PlexServerConnections.AddRange(connections);
        }

        await context.SaveChangesAsync();
        var plexConnections = await context.PlexServerConnections.ToListAsync();

        // Add status to each connection
        foreach (var connection in plexConnections)
        {
            var status = FakeData
                .GetPlexServerStatus(seed, plexServerId: connection.PlexServerId, plexServerConnectionId: connection.Id)
                .Generate();
            context.PlexServerStatuses.Add(status);
        }

        await context.SaveChangesAsync();

        _log.Here()
            .Debug(
                "Added {PlexServerCount} {NameOfPlexServer}s to {NameOfReaparrDbContext}: {DatabaseName}",
                config.PlexServerCount,
                nameof(PlexServer),
                nameof(ReaparrDbContext),
                context.DatabaseName
            );
        return context;
    }

    private static async Task<ReaparrDbContext> AddPlexLibraries(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        if (!config.ShouldHavePlexLibrary)
            return context;

        var plexServers = await context.PlexServers.ToListAsync();
        plexServers.ShouldNotBeEmpty();

        var plexLibrariesToDb = new List<PlexLibrary>();

        foreach (var plexServer in plexServers)
        {
            var plexLibraries = new List<PlexLibrary>();
            if (config.ShouldHaveMoviePlexLibrary)
                plexLibraries.AddRange(
                    FakeData
                        .GetPlexLibrary(seed, PlexMediaType.Movie)
                        .Generate(Math.Max(1, config.PlexMovieLibraryCount))
                );

            if (config.ShouldHaveTvShowPlexLibrary)
                plexLibraries.AddRange(
                    FakeData
                        .GetPlexLibrary(seed, PlexMediaType.TvShow)
                        .Generate(Math.Max(1, config.PlexTvShowLibraryCount))
                );

            foreach (var plexLibrary in plexLibraries)
                plexLibrary.PlexServerId = plexServer.Id;

            plexLibrariesToDb.AddRange(plexLibraries);
        }

        context.PlexLibraries.AddRange(plexLibrariesToDb);
        await context.SaveChangesAsync();
        return context;
    }

    private static async Task<ReaparrDbContext> AddPlexAccount(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        var plexServers = context.PlexServers.Include(x => x.PlexLibraries).ToList();

        for (var i = 0; i < config.PlexAccountCount; i++)
        {
            var plexAccount = FakeData.GetPlexAccount(seed).Generate();

            await context.PlexAccounts.AddAsync(plexAccount);
            await context.SaveChangesAsync();

            _log.Here()
                .Debug(
                    "Added 1 {NameOfPlexAccount}: {PlexAccountTitle} to ReaparrDbContext: {DatabaseName}",
                    nameof(PlexAccount),
                    plexAccount.Title,
                    context.DatabaseName
                );

            var plexAccountServer = plexServers.Select(x => new PlexAccountServer
            {
                AuthTokenCreationDate = DateTime.UtcNow,
                PlexServerId = x.Id,
                PlexAccountId = plexAccount.Id,
                AuthToken = "FAKE_AUTH_TOKEN",
                IsServerOwned = true,
            });

            // Add account -> server relation
            context.PlexAccountServers.AddRange(plexAccountServer);
            await context.SaveChangesAsync();

            // Add account -> library relation
            var plexAccountLibraries = plexServers
                .SelectMany(x => x.PlexLibraries)
                .Select(x => new PlexAccountLibrary
                {
                    PlexAccountId = plexAccount.Id,
                    PlexServerId = x.PlexServerId,
                    PlexLibraryId = x.Id,
                    IsLibraryOwned = true,
                });
            context.PlexAccountLibraries.AddRange(plexAccountLibraries);
        }

        await context.SaveChangesAsync();

        return context;
    }

    private static async Task<ReaparrDbContext> AddPlexAccountLibraries(this ReaparrDbContext context)
    {
        var plexLibraries = await context.PlexLibraries.ToListAsync();
        var plexAccounts = await context.PlexAccounts.ToListAsync();
        plexAccounts.ShouldNotBeEmpty();
        plexLibraries.ShouldNotBeEmpty();

        var plexAccountLibraries = new List<PlexAccountLibrary>();
        foreach (var plexAccount in plexAccounts)
        foreach (var plexLibrary in plexLibraries)
            plexAccountLibraries.Add(
                new PlexAccountLibrary
                {
                    PlexAccountId = plexAccount.Id,
                    PlexServerId = plexLibrary.PlexServerId,
                    PlexLibraryId = plexLibrary.Id,
                    IsLibraryOwned = true,
                }
            );

        context.PlexAccountLibraries.AddRange(plexAccountLibraries);
        await context.SaveChangesAsync();
        return context;
    }

    #endregion

    #region Public

    public static string GetMemoryDatabaseName() => $"memory_database_{Guid.NewGuid():N}";

    public static async Task<ReaparrDbContext> AddRadarrIntegrations(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        if (config.AssignUnownedDownloadTasksToRadarrIntegration && config.RadarrIntegrationCount > 1)
            throw new InvalidOperationException(
                "AssignUnownedDownloadTasksToRadarrIntegration allows at most one Radarr integration."
            );

        for (var i = 0; i < config.RadarrIntegrationCount; i++)
        {
            var integrationId = new Guid(seed.Next(), 0, 0, new byte[8]);
            var id = integrationId.ToString("N");
            context.RadarrIntegrations.Add(
                new RadarrIntegration
                {
                    Id = integrationId,
                    DisplayName = $"Radarr {id[..8]}",
                    BaseUrl = $"https://radarr-{id[..8]}.example.com",
                    RadarrApiKey = id,
                    QBittorrentApiKey = IntegrationApiKeyGenerator.GenerateQBittorrentApiKey(integrationId),
                    TorznabApiKey = IntegrationApiKeyGenerator.GenerateTorznabApiKey(integrationId),
                    Category = $"radarr-{id[..8]}",
                    DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
                    ProvisioningState = IntegrationProvisioningState.Configured,
                }
            );
        }
        await context.SaveChangesAsync();
        if (config.AssignUnownedDownloadTasksToRadarrIntegration)
        {
            var integrationId = await context.RadarrIntegrations.Select(x => x.Id).SingleAsync();
            var movieTasks = await context
                .DownloadTaskMovie.AsTracking()
                .Where(x => x.SonarrIntegrationId == null && x.RadarrIntegrationId == null)
                .ToListAsync();
            var tvShowTasks = await context
                .DownloadTaskTvShow.AsTracking()
                .Where(x => x.SonarrIntegrationId == null && x.RadarrIntegrationId == null)
                .ToListAsync();
            foreach (var task in movieTasks.Cast<DownloadTaskBase>().Concat(tvShowTasks))
                task.RadarrIntegrationId = integrationId;
            await context.SaveChangesAsync();

            await context
                .DownloadTaskMovieFile.Where(x => x.RadarrIntegrationId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.RadarrIntegrationId, integrationId));
            await context
                .DownloadTaskTvShowSeason.Where(x => x.RadarrIntegrationId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.RadarrIntegrationId, integrationId));
            await context
                .DownloadTaskTvShowEpisode.Where(x => x.RadarrIntegrationId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.RadarrIntegrationId, integrationId));
            await context
                .DownloadTaskTvShowEpisodeFile.Where(x => x.RadarrIntegrationId == null)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.RadarrIntegrationId, integrationId));
        }
        return context;
    }

    public static async Task<ReaparrDbContext> AddSonarrIntegrations(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        for (var i = 0; i < config.SonarrIntegrationCount; i++)
        {
            var integrationId = new Guid(seed.Next(), 0, 0, new byte[8]);
            var id = integrationId.ToString("N");
            context.SonarrIntegrations.Add(
                new SonarrIntegration
                {
                    Id = integrationId,
                    DisplayName = $"Sonarr {id[..8]}",
                    BaseUrl = $"https://sonarr-{id[..8]}.example.com",
                    SonarrApiKey = id,
                    QBittorrentApiKey = IntegrationApiKeyGenerator.GenerateQBittorrentApiKey(integrationId),
                    TorznabApiKey = IntegrationApiKeyGenerator.GenerateTorznabApiKey(integrationId),
                    Category = $"sonarr-{id[..8]}",
                    DownloadFolderId = FolderTypeDefaults.DefaultDownloadFolderId,
                    ProvisioningState = IntegrationProvisioningState.Configured,
                }
            );
        }
        await context.SaveChangesAsync();
        return context;
    }

    /// <summary>
    /// Creates an in-memory database only to be used for unit and integration testing.
    /// Passing in the same dbName will create a new context for the same database
    /// </summary>
    /// <param name="logger">The ILogger implementation</param>
    /// <param name="pathProvider">The path provider to use for the DbContext, can be shared between contexts that should have the same sandboxed file paths. Use CreatePathProvider(dbName) to create a new one with unique paths based on the dbName.</param>
    /// <param name="appRuntimeInfo">The app runtime info to use for the DbContext, can be shared between contexts that should have the same app runtime info. Use CreateAppRuntimeInfo(dbName) to create a new one with unique values based on the dbName.</param>
    /// <param name="dbName">leave empty to generate a random one</param>
    /// <returns>A <see cref="ReaparrDbContext" /> in memory instance.</returns>
    public static (ReaparrDbContext, AuthDbContext) GetMemoryDbContext(
        Serilog.ILogger logger,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        string dbName = ""
    )
    {
        dbName = string.IsNullOrEmpty(dbName) ? GetMemoryDatabaseName() : dbName;

        return (
            GetMemoryReaparrDbContext(logger, pathProvider, appRuntimeInfo, dbName),
            GetMemoryAuthDbContext(logger, pathProvider, appRuntimeInfo, dbName)
        );
    }

    public static ReaparrDbContext GetMemoryReaparrDbContext(
        Serilog.ILogger logger,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        string dbName = ""
    )
    {
        var optionsBuilder = GetDbContextOptionsBuilder<ReaparrDbContext>(
            appRuntimeInfo.IsIntegrationTestMode ? pathProvider.DatabasePath : dbName,
            appRuntimeInfo.IsIntegrationTestMode ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.Memory
        );

        return new ReaparrDbContext(optionsBuilder.Options, logger, pathProvider, appRuntimeInfo, dbName);
    }

    public static AuthDbContext GetMemoryAuthDbContext(
        Serilog.ILogger logger,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        string dbName = ""
    )
    {
        var optionsBuilder = GetDbContextOptionsBuilder<AuthDbContext>(
            appRuntimeInfo.IsIntegrationTestMode ? pathProvider.DatabasePath : dbName,
            appRuntimeInfo.IsIntegrationTestMode ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.Memory
        );

        return new AuthDbContext(optionsBuilder.Options, logger, pathProvider, appRuntimeInfo, dbName);
    }

    public static async Task Setup(
        this (ReaparrDbContext, AuthDbContext) context,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);
        var (reaparrContext, authContext) = context;

        await _setupLock.WaitAsync();
        try
        {
            await PrepareDatabaseSchemaAsync(reaparrContext, authContext, pathProvider, appRuntimeInfo);
            reaparrContext = await SeedDatabaseAsync(
                reaparrContext,
                seed,
                pathProvider,
                appRuntimeInfo,
                config,
                options
            );
        }
        finally
        {
            _setupLock.Release();
        }

        reaparrContext.ShouldNotBeNull();
    }

    private static async Task PrepareDatabaseSchemaAsync(
        ReaparrDbContext reaparrContext,
        AuthDbContext authContext,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
    {
        if (appRuntimeInfo.IsIntegrationTestMode)
        {
            var migrationResult = Result.Merge(reaparrContext.Migrate(), authContext.Migrate());
            migrationResult.IsSuccess.ShouldBeTrue(migrationResult.ToString());
            return;
        }

        if (_databaseTemplateConnection is null)
            throw new InvalidOperationException("The test database template has not been initialized.");

        await CloneDatabaseTemplateAsync(reaparrContext, pathProvider);
    }

    public static async Task InitializeDatabaseTemplateAsync(IPathProvider pathProvider, IAppRuntimeInfo appRuntimeInfo)
    {
        if (_databaseTemplateConnection is not null)
            return;

        var templateConnection = new SqliteConnection(
            DbContextConnections.GetConnectionString(_databaseTemplateName, SqliteOpenMode.Memory)
        );
        await templateConnection.OpenAsync();

        await using var templateReaparrContext = GetMemoryReaparrDbContext(
            _log,
            pathProvider,
            appRuntimeInfo,
            _databaseTemplateName
        );
        await using var templateAuthContext = GetMemoryAuthDbContext(
            _log,
            pathProvider,
            appRuntimeInfo,
            _databaseTemplateName
        );

        try
        {
            var migrationResult = Result.Merge(templateReaparrContext.Migrate(), templateAuthContext.Migrate());
            migrationResult.IsSuccess.ShouldBeTrue(migrationResult.ToString());
            _databaseTemplateConnection = templateConnection;
        }
        catch
        {
            await templateConnection.DisposeAsync();
            throw;
        }
    }

    public static async Task DisposeDatabaseTemplateAsync()
    {
        if (_databaseTemplateConnection is null)
            return;

        await _databaseTemplateConnection.DisposeAsync();
        _databaseTemplateConnection = null;
        SqliteConnection.ClearAllPools();
    }

    private static async Task CloneDatabaseTemplateAsync(ReaparrDbContext reaparrContext, IPathProvider pathProvider)
    {
        await reaparrContext.Database.OpenConnectionAsync();
        var destinationConnection = (SqliteConnection)reaparrContext.Database.GetDbConnection();
        DbContextConnections.InitializeDatabase(destinationConnection);

        await using var sourceConnection = new SqliteConnection(
            DbContextConnections.GetConnectionString(_databaseTemplateName, SqliteOpenMode.Memory)
        );
        await sourceConnection.OpenAsync();
        sourceConnection.BackupDatabase(destinationConnection);

        await reaparrContext.FolderPaths.ExecuteDeleteAsync();
        ReaparrDBContextSeed.Seed(pathProvider)(reaparrContext, true);
    }

    private static async Task<ReaparrDbContext> SeedDatabaseAsync(
        ReaparrDbContext reaparrContext,
        Seed seed,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        FakeDataConfig config,
        Action<FakeDataConfig>? options
    )
    {
        _log.Here()
            .Debug(
                "Setting up {NameOfReaparrDbContext} for {DatabaseName}",
                nameof(ReaparrDbContext),
                reaparrContext.DatabaseName
            );

        if (config.ShouldHavePlexServer)
            reaparrContext = await reaparrContext.AddPlexServers(seed, options);

        if (config.ShouldHavePlexLibrary)
            reaparrContext = await reaparrContext.AddPlexLibraries(seed, options);

        if (config.PlexAccountCount > 0)
            reaparrContext = await reaparrContext.AddPlexAccount(seed, options);

        if (config.MovieCount > 0)
            reaparrContext = await reaparrContext.AddPlexMovies(seed, options);

        if (config.TvShowCount > 0)
            reaparrContext = await reaparrContext.AddPlexTvShows(seed, options);

        if (config.MovieDownloadTasksCount > 0)
            reaparrContext = await reaparrContext.AddDownloadTaskMovies(seed, pathProvider, appRuntimeInfo, options);

        if (config.TvShowDownloadTasksCount > 0)
            reaparrContext = await reaparrContext.AddDownloadTaskTvShows(seed, pathProvider, appRuntimeInfo, options);

        if (config.RadarrIntegrationCount > 0)
            reaparrContext = await reaparrContext.AddRadarrIntegrations(seed, options);

        if (config.SonarrIntegrationCount > 0)
            reaparrContext = await reaparrContext.AddSonarrIntegrations(seed, options);

        if (config.AccountHasAccessToAllLibraries)
            reaparrContext = await reaparrContext.AddPlexAccountLibraries();

        return reaparrContext;
    }

    #endregion

    #endregion

    private static DbContextOptionsBuilder<TContext> GetDbContextOptionsBuilder<TContext>(
        string dataSource,
        SqliteOpenMode mode
    )
        where TContext : DbContext
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.ConfigureSqlite(dataSource, mode);

        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        optionsBuilder.LogTo(text => LogFactory.DbContextLogger(text), LogLevel.Warning);
        return optionsBuilder;
    }

    #region Add Media

    private static async Task<ReaparrDbContext> AddPlexMovies(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        var plexLibraries = context.PlexLibraries.Where(x => x.Type == PlexMediaType.Movie).ToList();
        plexLibraries.ShouldNotBeNull().ShouldNotBeEmpty();

        // Add movies for each library
        foreach (var plexLibrary in plexLibraries)
        {
            var movies = FakeData.GetPlexMovies(seed, options).Generate(config.MovieCount);
            await context.BulkInsertPlexMoviesAsync(movies, plexLibrary.PlexServerId, plexLibrary.Id);

            var mediaSize = movies.Sum(x => x.MediaSize);
            var mediaDataCount = movies.Sum(x => x.MediaDataList.Count);
            await context.SetMovieMediaMetrics(plexLibrary.Id, movies.Count, mediaDataCount, mediaSize);
        }

        _log.Here()
            .Debug(
                "Added {MovieCount} {NameOfPlexMovie}s to ReaparrDbContext: {DatabaseName}",
                config.MovieCount,
                nameof(PlexMovie),
                context.DatabaseName
            );

        return context;
    }

    private static async Task<ReaparrDbContext> AddPlexTvShows(
        this ReaparrDbContext context,
        Seed seed,
        Action<FakeDataConfig>? options = null
    )
    {
        var config = FakeDataConfig.FromOptions(options);

        var plexLibraries = context.PlexLibraries.Where(x => x.Type == PlexMediaType.TvShow).ToList();
        plexLibraries.ShouldNotBeNull().ShouldNotBeEmpty();

        foreach (var plexLibrary in plexLibraries)
        {
            var tvShows = FakeData.GetPlexTvShows(seed, options).Generate(config.TvShowCount);
            await context.BulkInsertPlexTvShowsAsync(tvShows, plexLibrary.PlexServerId, plexLibrary.Id);

            var seasons = tvShows.SelectMany(x => x.Seasons).ToList();
            var episodes = seasons.SelectMany(x => x.Episodes).ToList();
            await context.SetTvShowMediaMetrics(
                plexLibrary.Id,
                tvShows.Count,
                seasons.Count,
                episodes.Count,
                episodes.Sum(x => x.MediaDataList.Count),
                episodes.Sum(x => x.MediaSize)
            );
        }

        _log.Here()
            .Debug(
                "Added {TvShowCount} {NameOfPlexTvShow}s to ReaparrDbContext: {DatabaseName}",
                config.TvShowCount,
                nameof(PlexTvShow),
                context.DatabaseName
            );

        return context;
    }

    #endregion
}
