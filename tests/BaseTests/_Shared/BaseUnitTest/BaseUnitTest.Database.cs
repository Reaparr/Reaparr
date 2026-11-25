namespace Reaparr.BaseTests;

public partial class BaseUnitTest : IDisposable
{
    private string _databaseName = string.Empty;

    // Held alive for the test duration so setup contexts can be disposed explicitly.
    private ReaparrDbContext? _setupReaparrDbContext;
    private AuthDbContext? _setupAuthDbContext;

    protected bool IsDatabaseSetup;

    /// <summary>
    /// Gets a new instance of <see cref="ReaparrDbContext"/> for every time it is called.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    protected IReaparrDbContext IDbContext
    {
        get
        {
            DataBaseSetupGuard();

            return MockDatabase.GetMemoryReaparrDbContext(
                Mock.Container.Resolve<ILogger>(),
                Mock.Container.Resolve<IPathProvider>(),
                Mock.Container.Resolve<IAppRuntimeInfo>(),
                _databaseName
            );
        }
    }

    private void DataBaseSetupGuard()
    {
        if (!IsDatabaseSetup)
        {
            var logEvent = Log.ErrorMsg(
                "The test database has not been setup yet, run SetupDatabase() in the test first!"
            );
            throw new Exception(logEvent);
        }
    }

    protected IAuthDbContext IAuthDbContext
    {
        get
        {
            DataBaseSetupGuard();

            return MockDatabase.GetMemoryAuthDbContext(
                Mock.Container.Resolve<ILogger>(),
                Mock.Container.Resolve<IPathProvider>(),
                Mock.Container.Resolve<IAppRuntimeInfo>(),
                _databaseName
            );
        }
    }

    protected Mock<IReaparrDbContext> MockIDbContext => new();

    /// <summary>
    /// Creates and maintains a unique in memory database <see cref="ReaparrDbContext"/> for every test.
    /// </summary>
    /// <param name="seed"> The fake data seed to use for the database setup.</param>
    /// <param name="options"> The options to use for the fake data setup.</param>
    protected Task<Seed> SetupDatabase(int seed, Action<FakeDataConfig>? options = null) =>
        SetupDatabase(new Seed(seed), options);

    protected Task<Seed> SetupDatabase(int seed, Action<FakeDataConfig>? options, string? targetMigration) =>
        SetupDatabase(new Seed(seed), options, targetMigration);

    /// <summary>
    /// Creates and maintains a unique in memory database <see cref="ReaparrDbContext"/> for every test.
    /// </summary>
    /// <param name="seed"> The fake data seed to use for the database setup.</param>
    /// <param name="options"> The options to use for the fake data setup.</param>
    protected async Task<Seed> SetupDatabase(
        Seed seed,
        Action<FakeDataConfig>? options = null,
        string? targetMigration = null
    )
    {
        // Database context can be set up once and then retrieved by its DB name.
        _databaseName = MockDatabase.GetMemoryDatabaseName();
        var mockPathProvider = Mock.Container.Resolve<IPathProvider>();
        var mockAppRuntimeInfo = Mock.Container.Resolve<IAppRuntimeInfo>();
        var (reaparrContext, authContext) = MockDatabase.GetMemoryDbContext(
            Mock.Container.Resolve<ILogger>(),
            mockPathProvider,
            mockAppRuntimeInfo,
            _databaseName
        );

        // Hold references so setup contexts can be cleaned up in Dispose().
        _setupReaparrDbContext = reaparrContext;
        _setupAuthDbContext = authContext;
        await (reaparrContext, authContext).Setup(seed, mockPathProvider, mockAppRuntimeInfo, options, targetMigration);
        IsDatabaseSetup = true;
        return seed;
    }
}
