using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Reaparr.Identity;

public sealed class AuthDbContext : IdentityDbContext<AppUser>, IAuthDbContext, IAuthDbContextDatabase
{
    private readonly ILogger _log;
    private readonly IPathProvider _pathProvider;

    private readonly IAppRuntimeInfo _appRuntimeInfo;

    public string DatabaseName { get; }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    [ActivatorUtilitiesConstructor]
    public AuthDbContext(ILogger log, IPathProvider pathProvider, IAppRuntimeInfo appRuntimeInfo)
    {
        _log = log.ForContext<AuthDbContext>();
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;
        DatabaseName = pathProvider.DatabaseName;
    }

    public AuthDbContext(
        DbContextOptions<AuthDbContext> options,
        ILogger log,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo
    )
        : base(options)
    {
        _log = log.ForContext<AuthDbContext>();
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;

        DatabaseName = pathProvider.DatabaseName;
    }

    public AuthDbContext(
        DbContextOptions<AuthDbContext> options,
        ILogger log,
        IPathProvider pathProvider,
        IAppRuntimeInfo appRuntimeInfo,
        string databaseName
    )
        : base(options)
    {
        _log = log.ForContext<AuthDbContext>();
        _pathProvider = pathProvider;
        _appRuntimeInfo = appRuntimeInfo;

        DatabaseName = databaseName;
        Database.OpenConnection();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.DefaultConfiguration(_pathProvider, _appRuntimeInfo, typeof(AuthDbContext));
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

        var result = Result.Try(() => DbContextConnections.EnableWriteAheadLogging(Database.GetDbConnection()));
        result.LogIfFailed();

        return result.IsSuccess;
    }

    /// <inheritdoc/>
    public bool IsInMemory() => Database.IsInMemory();

    /// <inheritdoc/>
    public void CloseConnection() => Database.CloseConnection();

    /// <inheritdoc/>
    public Result<bool> EnsureDeleted() => Result.Try(() => Database.EnsureDeleted());

    /// <inheritdoc/>
    public Result Migrate() =>
        Result
            .Try(() =>
            {
                DbContextConnections.EnableWriteAheadLogging(Database.GetDbConnection());
                Database.Migrate();
            })
            .LogIfFailed();

    /// <inheritdoc/>
    public IEnumerable<string> GetPendingMigrations() => Database.GetPendingMigrations();

    /// <inheritdoc/>
    public Task<Result<int>> Optimize(CancellationToken cancellationToken = default) =>
        Result.Try(async Task<int> () => await Database.ExecuteSqlRawAsync("PRAGMA optimize;", cancellationToken));
}
